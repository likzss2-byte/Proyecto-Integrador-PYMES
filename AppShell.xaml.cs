namespace InventorySystem;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute(nameof(AppPages.ItemFullPage), typeof(AppPages.ItemFullPage));
        Routing.RegisterRoute(nameof(AppPages.PurveyorContactPage), typeof(AppPages.PurveyorContactPage));
        Routing.RegisterRoute(nameof(AppPages.SupplierInventoryPage), typeof(AppPages.SupplierInventoryPage));
        Routing.RegisterRoute(nameof(AppPages.BrandInventoryPage), typeof(AppPages.BrandInventoryPage));
        Routing.RegisterRoute(nameof(AppPages.OperationalInventoryPage), typeof(AppPages.OperationalInventoryPage));
        Routing.RegisterRoute(nameof(AppPages.BarcodeScannerPage), typeof(AppPages.BarcodeScannerPage));
    }
}
