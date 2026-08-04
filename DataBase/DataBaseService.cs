using SQLite;
using InventorySystem.Objects.InventoryData;
using InventorySystem.Objects.UserData;
using InventorySystem.DataBase;

namespace InventorySystem.Data;

public class DatabaseService
{
	private SQLiteAsyncConnection? _connection;

	private readonly SemaphoreSlim _initializationLock = new(1, 1);

	private bool _isInitialized;

	public async Task InitializeAsync()
	{
		if (_isInitialized)
			return;

		await _initializationLock.WaitAsync();

		try
		{
			if (_isInitialized)
				return;

			_connection = new SQLiteAsyncConnection(
				DataBaseInitialize.DatabasePath,
				DataBaseInitialize.Flags
			);

			await _connection.ExecuteAsync(
				"PRAGMA foreign_keys = ON;"
			);

			await _connection.CreateTableAsync<Item>();
			await _connection.CreateTableAsync<ItemTag>();
			await _connection.CreateTableAsync<ItemPurveyor>();
			await _connection.CreateTableAsync<ProductDelivery>();

			await _connection.CreateTableAsync<Purveyor>();
			await _connection.CreateTableAsync<PurveyorPhoneNumber>();
			await _connection.CreateTableAsync<PurveyorEmail>();
			await _connection.CreateTableAsync<PurveyorAddress>();

			await _connection.CreateTableAsync<Sale>();
			await _connection.CreateTableAsync<SaleIncludes>();

			await _connection.CreateTableAsync<LocalBusiness>();
			await _connection.CreateTableAsync<User>();

			_isInitialized = true;
		}
		finally
		{
			_initializationLock.Release();
		}
	}
}