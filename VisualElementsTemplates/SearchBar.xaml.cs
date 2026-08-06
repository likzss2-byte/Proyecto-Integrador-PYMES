namespace InventorySystem.VisualElementsTemplates;

public partial class SearchBar : ContentView
{
	public event EventHandler<TextChangedEventArgs>? SearchTextChanged;

	public SearchBar()
	{
		InitializeComponent();
	}

	public string Text => InventorySearchBar.Text ?? string.Empty;

	public void SetText(string value)
	{
		InventorySearchBar.Text = value;
	}

	private void InventorySearchBar_TextChanged(object? sender, TextChangedEventArgs e)
	{
		SearchTextChanged?.Invoke(this, e);
	}
}
