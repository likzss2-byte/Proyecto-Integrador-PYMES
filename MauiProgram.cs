using CommunityToolkit.Maui;
using InventorySystem.AppPages;
using InventorySystem.Data;
using InventorySystem.DataBase;
using InventorySystem.Domain;
using InventorySystem.Infrastructure.Data;
using InventorySystem.Infrastructure.Repositories;
using InventorySystem.Infrastructure.Services;

namespace InventorySystem;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddSingleton(_ => new InventoryDatabase(DataBaseInitialize.DatabasePath));
        builder.Services.AddSingleton<DatabaseService>();
        builder.Services.AddSingleton<ProductRepository>();
        builder.Services.AddSingleton<SupplierRepository>();
        builder.Services.AddSingleton<BusinessService>();
        builder.Services.AddSingleton<InventoryTransactionService>();
        builder.Services.AddSingleton<InventoryAdjustmentService>();
        builder.Services.AddSingleton<InventoryCatalogService>();
        builder.Services.AddSingleton<InventoryCountSessionService>();
        builder.Services.AddSingleton<InventoryLotService>();
        builder.Services.AddSingleton<PurchaseOrderService>();
        builder.Services.AddSingleton<DashboardService>();
        builder.Services.AddSingleton<BarcodeReadGuard>();
        builder.Services.AddSingleton<BarcodeScannerService>();
        builder.Services.AddSingleton(new HttpClient { Timeout = Timeout.InfiniteTimeSpan });
        builder.Services.AddSingleton<IExternalProductCatalog>(services =>
            new ExternalProductService(services.GetRequiredService<HttpClient>()));
        builder.Services.AddSingleton<ProductLookupService>();

        builder.Services.AddTransient<LoadingScreen>();
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<InventoryPage>();
        builder.Services.AddTransient<SupplierInventoryPage>();
        builder.Services.AddTransient<BrandInventoryPage>();
        builder.Services.AddTransient<OperationalInventoryPage>();
        builder.Services.AddTransient<NewItemPage>();
        builder.Services.AddTransient<NewPurveyorPage>();
        builder.Services.AddTransient<NewOrderPage>();
        builder.Services.AddTransient<PurchaseOrdersPage>();
        builder.Services.AddTransient<NewSalePage>();
        builder.Services.AddTransient<ItemFullPage>();
        builder.Services.AddTransient<PurveyorFullPage>();
        builder.Services.AddTransient<PurveyorContactPage>();

        return builder.Build();
    }
}
