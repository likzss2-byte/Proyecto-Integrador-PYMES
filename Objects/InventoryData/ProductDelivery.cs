using InventorySystem.Objects.Enums;
using SQLite;

namespace InventorySystem.Objects.InventoryData
{
	internal class ProductDelivery
	{
		[PrimaryKey, AutoIncrement]
		public int ProductDeliveryID { get; set; }
		public int Purveyor_ID { get; set; }
		[Unique]
		public DateTime DeliveryRequestDate { get; set; }
		public DateTime? DeliveryReceivedDate { get; set; }
		public DateTime? DeliveryCancelDate { get; set; }
		[NotNull]
		public DeliveryState State { get; set; }
	}
}
