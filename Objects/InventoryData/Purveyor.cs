using SQLite;

namespace InventorySystem.Objects.InventoryData
{
	public class Purveyor
	{
		[PrimaryKey, AutoIncrement]
		public int PurveyorID { get; set; }
		[NotNull]
		public string CompanyRegisteredName { get; set; } = string.Empty;

	}
	public class PurveyorPhoneNumber
	{
		[PrimaryKey, AutoIncrement]
		public int PurveyorPhoneNumberID { get; set; }
		public int PurveyorID { get; set; }
		public int? PhoneNumber { get; set; }
	}
	public class PurveyorEmail
	{
		[PrimaryKey, AutoIncrement]
		public int PurveyorPhoneNumberID { get; set; }
		public int PurveyorID { get; set; }
		public string? Email { get; set; }
	}
	public class PurveyorAddress
	{
		[PrimaryKey, AutoIncrement]
		public int PurveyorAddressID { get; set; }
		public int PurveyorID { get; set; }
		public string Country { get; set; } = string.Empty;
		public string State { get; set; } = string.Empty;
		public int? PostalCode { get; set; }
		public string? Neighborhood { get; set; }
		public string Street { get; set; } = string.Empty;
		public string? AditionalReferences { get; set; }
	}
}
