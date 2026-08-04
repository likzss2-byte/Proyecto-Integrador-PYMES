using SQLite;

namespace InventorySystem.Objects.InventoryData
{
	public class Item
	{
		[PrimaryKey, AutoIncrement]
		public int ItemID { get; set; }
		[Unique, NotNull]
		public string ItemName { get; set; }
		public string? ItemDescription { get; set; }
		[NotNull]
		public decimal SalePrice { get; set; }
		[NotNull]
		public int Stock { get; set; }
		[NotNull]
		public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
	}
	public class ItemTag
	{
		[PrimaryKey, AutoIncrement]
		public int ItemTagID { get; set; }
		[NotNull]
		public int ItemID { get; set; }
		[NotNull]
		public string Tag { get; set; }
	}
	public class ItemPurveyor
	{
		[PrimaryKey, AutoIncrement]
		public int ItemPurveyorID { get; set; }
		public int ItemID { get; set; }
		public int PurveyorID { get; set; }
	}
}
