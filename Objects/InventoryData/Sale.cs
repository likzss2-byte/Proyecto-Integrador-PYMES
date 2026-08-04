using SQLite;

namespace InventorySystem.Objects.InventoryData
{
	public class Sale
	{
		[PrimaryKey, AutoIncrement]
		public int SaleID { get; set; }
		public decimal SaleTotal { get; set; }
		public DateTime TransactionDate { get; set; }
	}
	public class SaleIncludes
	{
		[PrimaryKey, AutoIncrement]
		public int SaleIncludesID { get; set; }
		public int SaleID { get; set; }
		public int ItemID { get; set; }
		public int ItemSaleQuantity { get; set; }
	}
}
