
namespace InventorySystem
{
	public partial class AppShell : Shell
	{
		public AppShell()
		{
			InitializeComponent();
			ConfigureFlyoutForCurrentDevice();
			Routing.RegisterRoute(nameof(AppPages.ItemFullPage), typeof(AppPages.ItemFullPage));
			Routing.RegisterRoute(nameof(AppPages.PurveyorContactPage), typeof(AppPages.PurveyorContactPage));
			Routing.RegisterRoute(nameof(AppPages.SupplierInventoryPage), typeof(AppPages.SupplierInventoryPage));
			Routing.RegisterRoute(nameof(AppPages.BrandInventoryPage), typeof(AppPages.BrandInventoryPage));
			Routing.RegisterRoute(nameof(AppPages.OperationalInventoryPage), typeof(AppPages.OperationalInventoryPage));
		}

		private void ConfigureFlyoutForCurrentDevice()
		{
			if (!IsDesktopDevice())
			{
				FlyoutBehavior = FlyoutBehavior.Flyout;
				return;
			}

			FlyoutBehavior = FlyoutBehavior.Locked;
		}

		private static bool IsDesktopDevice()
		{
			return DeviceInfo.Current.Idiom == DeviceIdiom.Desktop
				|| DeviceInfo.Current.Platform == DevicePlatform.WinUI
				|| DeviceInfo.Current.Platform == DevicePlatform.MacCatalyst;
		}

		private void ToggleDesktopFlyout_Clicked(object? sender, EventArgs e)
		{
			if (FlyoutBehavior == FlyoutBehavior.Locked)
			{
				FlyoutBehavior = FlyoutBehavior.Flyout;
				FlyoutIsPresented = false;
				ToggleDesktopFlyoutButton.Text = "Fijar";
				return;
			}

			FlyoutBehavior = FlyoutBehavior.Locked;
			FlyoutIsPresented = true;
			ToggleDesktopFlyoutButton.Text = "Contraer";
		}
	}
}
