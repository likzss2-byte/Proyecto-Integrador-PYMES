namespace InventorySystem.VisualElementsTemplates;

public enum InventorySortOption
{
	Recent,
	Alphabetical,
	Price
}

public class SortChangedEventArgs : EventArgs
{
	public InventorySortOption SortOption { get; }

	public bool IsDescending { get; }

	public SortChangedEventArgs(
		InventorySortOption sortOption,
		bool isDescending)
	{
		SortOption = sortOption;
		IsDescending = isDescending;
	}
}

public partial class SortButton : ContentView
{
	private InventorySortOption _selectedSortOption =
		InventorySortOption.Recent;

	private bool _sortDescending = true;

	public event EventHandler<SortChangedEventArgs>? SortChanged;

	public SortButton()
	{
		InitializeComponent();

		UpdateSortButtonTexts();
	}

	private void SortButton_Clicked(
		object sender,
		EventArgs e)
	{
		SortMenu.IsVisible = !SortMenu.IsVisible;
	}

	private void RecentSortButton_Clicked(
		object sender,
		EventArgs e)
	{
		SelectSortOption(
			InventorySortOption.Recent,
			defaultDescending: true
		);
	}

	private void AlphabeticalSortButton_Clicked(
		object sender,
		EventArgs e)
	{
		SelectSortOption(
			InventorySortOption.Alphabetical,
			defaultDescending: false
		);
	}

	private void PriceSortButton_Clicked(
		object sender,
		EventArgs e)
	{
		SelectSortOption(
			InventorySortOption.Price,
			defaultDescending: false
		);
	}

	private void SelectSortOption(
		InventorySortOption selectedOption,
		bool defaultDescending)
	{
		if (_selectedSortOption == selectedOption)
		{
			_sortDescending = !_sortDescending;
		}
		else
		{
			_selectedSortOption = selectedOption;
			_sortDescending = defaultDescending;
		}

		UpdateSortButtonTexts();

		SortMenu.IsVisible = false;

		SortChanged?.Invoke(
			this,
			new SortChangedEventArgs(
				_selectedSortOption,
				_sortDescending
			)
		);
	}

	private void UpdateSortButtonTexts()
	{
		RecentSortButton.Text =
			_selectedSortOption == InventorySortOption.Recent
				? $"Recientes {GetDirectionArrow()}"
				: "Recientes";

		AlphabeticalSortButton.Text =
			_selectedSortOption == InventorySortOption.Alphabetical
				? $"Alfabético {GetDirectionArrow()}"
				: "Alfabético";

		PriceSortButton.Text =
			_selectedSortOption == InventorySortOption.Price
				? $"Precio {GetDirectionArrow()}"
				: "Precio";
	}

	private string GetDirectionArrow()
	{
		return _sortDescending ? "▼" : "▲";
	}
}