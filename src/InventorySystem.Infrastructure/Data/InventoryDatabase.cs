using SQLite;

namespace InventorySystem.Infrastructure.Data;

public sealed class InventoryDatabase
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile bool _initialized;

    public InventoryDatabase(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("La ruta de la base de datos es obligatoria.", nameof(databasePath));
        }

        DatabasePath = Path.GetFullPath(databasePath);
    }

    public string DatabasePath { get; }

    public string? LastBackupPath { get; private set; }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_initialized)
            {
                return;
            }

            LastBackupPath = await Task.Run(
                () => DatabaseMigrator.Migrate(DatabasePath),
                cancellationToken).ConfigureAwait(false);
            _initialized = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<T> ReadAsync<T>(
        Func<SQLiteConnection, T> action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(() =>
            {
                using var connection = OpenConnection();
                return action(connection);
            }, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<T> WriteAsync<T>(
        Func<SQLiteConnection, T> action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(() =>
            {
                using var connection = OpenConnection();
                connection.BeginTransaction();
                try
                {
                    var result = action(connection);
                    connection.Commit();
                    return result;
                }
                catch
                {
                    connection.Rollback();
                    throw;
                }
            }, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task WriteAsync(
        Action<SQLiteConnection> action,
        CancellationToken cancellationToken = default) =>
        WriteAsync(connection =>
        {
            action(connection);
            return true;
        }, cancellationToken);

    private SQLiteConnection OpenConnection()
    {
        var connection = new SQLiteConnection(
            DatabasePath,
            SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex,
            storeDateTimeAsTicks: false);
        connection.Execute("PRAGMA foreign_keys = ON;");
        _ = connection.ExecuteScalar<int>("PRAGMA busy_timeout = 5000;");
        return connection;
    }
}
