namespace InventorySystem.VisualElementsTemplates;
public partial class Dock : ContentView
{
	public event EventHandler? AddButtonPressed;

	public Dock()
	{
		InitializeComponent();
	}

	private async void HomeButtonClicked(object sender, EventArgs e)
	{
		await Shell.Current.GoToAsync("//MainPage");
	}

	private async void InventoryButtonClicked(object sender, EventArgs e)
	{
		await Shell.Current.GoToAsync("//InventoryPage");
	}

	private async void PurveyorButtonClicked(object sender, EventArgs e)
	{
		await Shell.Current.GoToAsync("//PurveyorFullPage");
	}

	private void AddButtonClicked(object sender, EventArgs e)
	{
		AddButtonPressed?.Invoke(this, EventArgs.Empty);
	}
	private void DockAddButtonPressed(object sender, EventArgs e)
	{
		AddNewMenuDisplay.ToggleMenu();
	}
}