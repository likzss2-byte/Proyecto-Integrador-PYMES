namespace InventorySystem;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute(nameof(AppPages.ItemFullPage), typeof(AppPages.ItemFullPage));
        Routing.RegisterRoute(nameof(AppPages.LotDetailPage), typeof(AppPages.LotDetailPage));
        Routing.RegisterRoute(nameof(AppPages.InventoryDocumentDetailPage), typeof(AppPages.InventoryDocumentDetailPage));
        Routing.RegisterRoute(nameof(AppPages.PurveyorContactPage), typeof(AppPages.PurveyorContactPage));
        Routing.RegisterRoute(nameof(AppPages.BarcodeScannerPage), typeof(AppPages.BarcodeScannerPage));
    }
}
