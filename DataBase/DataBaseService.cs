using InventorySystem.Infrastructure.Data;

namespace InventorySystem.Data;

public sealed class DatabaseService
{
    private readonly InventoryDatabase _database;

    public DatabaseService(InventoryDatabase database)
    {
        _database = database;
    }

    public string DatabasePath => _database.DatabasePath;

    public string? LastBackupPath => _database.LastBackupPath;

    public Task InitializeAsync(CancellationToken cancellationToken = default) =>
        _database.InitializeAsync(cancellationToken);
}
