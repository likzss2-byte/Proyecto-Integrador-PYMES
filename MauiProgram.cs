using CommunityToolkit.Maui;
using InventorySystem.Data;
using InventorySystem.AppPages;

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
				fonts.AddFont(
					"OpenSans-Regular.ttf",
					"OpenSansRegular");
			});
		
			builder.Services.AddSingleton<DatabaseService>();
			builder.Services.AddTransient<LoadingScreen>();

		return builder.Build();
	}
}