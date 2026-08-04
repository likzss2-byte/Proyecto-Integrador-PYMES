using global::InventorySystem.Data;
using InventorySystem.DataBase;

namespace InventorySystem.AppPages;

public partial class LoadingScreen : ContentPage
{
	private readonly DatabaseService _databaseService;

	private bool _initializationStarted;

	public LoadingScreen(DatabaseService databaseService)
	{
		InitializeComponent();

		_databaseService = databaseService;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();

		if (_initializationStarted)
			return;

		_initializationStarted = true;

		await InitializeApplicationAsync();
	}

	private async Task InitializeApplicationAsync()
	{
		try
		{
			await _databaseService.InitializeAsync();

			await Shell.Current.GoToAsync("//MainPage");
		}
		catch (Exception exception)
		{
			await DisplayAlertAsync(
				"Error de inicialización",
				$"No se pudo preparar la base de datos.\n\n{exception.Message}",
				"Aceptar"
			);
		}
	}
}