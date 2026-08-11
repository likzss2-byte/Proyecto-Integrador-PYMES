using CommunityToolkit.Maui;
using InventorySystem.AppPages;
using InventorySystem.Data;
using InventorySystem.DataBase;
using InventorySystem.Domain;
using InventorySystem.Infrastructure.Data;
using InventorySystem.Infrastructure.Repositories;
using InventorySystem.Infrastructure.Services;
using InventorySystem.Services;
#if ANDROID
using InventorySystem.Platforms.Android;
#elif WINDOWS
using InventorySystem.Platforms.Windows;
#endif

namespace InventorySystem;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureMauiHandlers(handlers =>
            {
#if ANDROID || WINDOWS
                handlers.AddHandler<Controls.BarcodeCameraPreview, BarcodeCameraPreviewHandler>();
#endif
            })
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
        builder.Services.AddSingleton<InventoryLotService>();
        builder.Services.AddSingleton<DashboardService>();
        builder.Services.AddSingleton<BarcodeReadGuard>();
        builder.Services.AddSingleton<BarcodeScannerService>();
#if ANDROID
        builder.Services.AddSingleton<IBarcodeCameraScannerService, AndroidBarcodeCameraScannerService>();
#elif WINDOWS
        builder.Services.AddSingleton<IBarcodeCameraScannerService, WindowsBarcodeCameraScannerService>();
#else
        builder.Services.AddSingleton<IBarcodeCameraScannerService, UnsupportedBarcodeCameraScannerService>();
#endif
        builder.Services.AddSingleton<BarcodeScannerCoordinator>();
        builder.Services.AddSingleton(new HttpClient { Timeout = Timeout.InfiniteTimeSpan });
        builder.Services.AddSingleton<IExternalProductCatalog>(services =>
            new ExternalProductService(services.GetRequiredService<HttpClient>()));
        builder.Services.AddSingleton<ProductLookupService>();

        builder.Services.AddTransient<LoadingScreen>();
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<InventoryPage>();
        builder.Services.AddTransient<NewItemPage>();
        builder.Services.AddTransient<NewPurveyorPage>();
        builder.Services.AddTransient<NewEntryPage>();
        builder.Services.AddTransient<NewSalePage>();
        builder.Services.AddTransient<EntriesHistoryPage>();
        builder.Services.AddTransient<SalesHistoryPage>();
        builder.Services.AddTransient<LotsPage>();
        builder.Services.AddTransient<InventoryDocumentDetailPage>();
        builder.Services.AddTransient<LotDetailPage>();
        builder.Services.AddTransient<ItemFullPage>();
        builder.Services.AddTransient<PurveyorFullPage>();
        builder.Services.AddTransient<PurveyorContactPage>();
        builder.Services.AddTransient<BarcodeScannerPage>();

        return builder.Build();
    }
}
