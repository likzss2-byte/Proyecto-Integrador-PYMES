using System.Collections.ObjectModel;
using InventorySystem.Domain;
using InventorySystem.Infrastructure.Repositories;
using InventorySystem.Infrastructure.Services;

namespace InventorySystem.AppPages;

public partial class PurveyorFullPage : ContentPage
{
    private readonly SupplierRepository _suppliers;
    private readonly BusinessService _businesses;
    private readonly ObservableCollection<Supplier> _items = [];
    private long _businessId;

    public PurveyorFullPage(SupplierRepository suppliers, BusinessService businesses)
    {
        InitializeComponent();
        _suppliers = suppliers;
        _businesses = businesses;
        SupplierList.ItemsSource = _items;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            _businessId = (await _businesses.GetDefaultAsync()).Id;
            await RefreshAsync();
        }
        catch (Exception error)
        {
            await DisplayAlertAsync("Proveedores", error.Message, "Aceptar");
        }
    }

    private async void SupplierSearch_TextChanged(object? sender, TextChangedEventArgs e) => await RefreshAsync();

    private async Task RefreshAsync()
    {
        if (_businessId == 0)
        {
            return;
        }

        var suppliers = await _suppliers.SearchAsync(_businessId, SupplierSearch.Text);
        _items.Clear();
        foreach (var supplier in suppliers)
        {
            _items.Add(supplier);
        }
    }

    private async void SupplierList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not Supplier supplier)
        {
            return;
        }

        SupplierList.SelectedItem = null;
        await Shell.Current.GoToAsync(
            nameof(PurveyorContactPage),
            new Dictionary<string, object> { ["SupplierId"] = supplier.Id });
    }
}
