
namespace InventorySystem
{
	public partial class AppShell : Shell
	{
		public AppShell()
		{
			InitializeComponent();
			Routing.RegisterRoute(nameof(AppPages.ItemFullPage), typeof(AppPages.ItemFullPage));
			Routing.RegisterRoute(nameof(AppPages.PurveyorContactPage), typeof(AppPages.PurveyorContactPage));
		}
	}
}
