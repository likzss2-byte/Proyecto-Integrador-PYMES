using InventorySystem.VisualElementsTemplates;

namespace InventorySystem.AppPages;

public partial class InventoryPage : ContentPage
{
	private InventorySortOption _selectedSortOption =
		InventorySortOption.Recent;

	private bool _sortDescending = true;

	public InventoryPage()
	{
		InitializeComponent();
	}

	private async void InventorySortButton_SortChanged(
		object? sender,
		SortChangedEventArgs e)
	{
		_selectedSortOption = e.SortOption;
		_sortDescending = e.IsDescending;

		await RefreshInventoryAsync();
	}

	private Task RefreshInventoryAsync()
	{
		return Task.CompletedTask;
	}
}