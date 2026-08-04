using SQLite;
namespace InventorySystem.DataBase;

public static class DataBaseInitialize
{
	public const string DatabaseFilename = "InventorySystem.db";

	public static string DatabasePath =>
		Path.Combine(
			FileSystem.AppDataDirectory,
			DatabaseFilename
		);

	public const SQLiteOpenFlags Flags =
		SQLiteOpenFlags.ReadWrite |
		SQLiteOpenFlags.Create |
		SQLiteOpenFlags.SharedCache;
}