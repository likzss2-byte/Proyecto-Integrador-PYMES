namespace InventorySystem.VisualElementsTemplates;

public partial class AddNewMenu : ContentView
{
	public AddNewMenu()
	{
		InitializeComponent();
	}

	public void ToggleMenu()
	{
		AddNewMenuDisplay.IsVisible = !AddNewMenuDisplay.IsVisible;
	}

	private async void NewItem_Clicked(object sender, EventArgs e)
	{
		await Shell.Current.GoToAsync("//NewItemPage");
	}

	private async void NewPurveyor_Clicked(object sender, EventArgs e)
	{
		await Shell.Current.GoToAsync("//NewPurveyorPage");
	}

	private async void NewOrder_Clicked(object sender, EventArgs e)
	{
		await Shell.Current.GoToAsync("//NewEntryPage");
	}

	private async void NewSale_Clicked(object sender, EventArgs e)
	{
		await Shell.Current.GoToAsync("//NewSalePage");
	}
}