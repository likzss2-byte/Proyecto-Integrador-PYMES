using InventorySystem.Domain;
using InventorySystem.Infrastructure.Repositories;
using InventorySystem.Infrastructure.Services;

namespace InventorySystem.AppPages;

public partial class NewPurveyorPage : ContentPage
{
    private readonly SupplierRepository _suppliers;
    private readonly BusinessService _businesses;
    private long _businessId;

    public NewPurveyorPage(SupplierRepository suppliers, BusinessService businesses)
    {
        InitializeComponent();
        _suppliers = suppliers;
        _businesses = businesses;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            _businessId = (await _businesses.GetDefaultAsync()).Id;
        }
        catch (Exception error)
        {
            await DisplayAlertAsync("Proveedor", error.Message, "Aceptar");
        }
    }

    private async void SaveSupplier_Clicked(object? sender, EventArgs e)
    {
        try
        {
            SupplierErrorLabel.Text = string.Empty;
            var supplier = await _suppliers.SaveAsync(
                _businessId,
                new SupplierInput(
                    CompanyEntry.Text ?? string.Empty,
                    ContactEntry.Text,
                    PhoneEntry.Text,
                    EmailEntry.Text,
                    CountryEntry.Text,
                    StateEntry.Text,
                    AddressEntry.Text,
                    NotesEntry.Text,
                    ActiveSwitch.IsToggled));
            await DisplayAlertAsync("Proveedor", $"{supplier.CompanyName} se guardó correctamente.", "Aceptar");
            ClearForm();
        }
        catch (Exception error)
        {
            SupplierErrorLabel.Text = error.Message;
        }
    }

    private void ClearForm()
    {
        CompanyEntry.Text = string.Empty;
        ContactEntry.Text = string.Empty;
        PhoneEntry.Text = string.Empty;
        EmailEntry.Text = string.Empty;
        CountryEntry.Text = string.Empty;
        StateEntry.Text = string.Empty;
        AddressEntry.Text = string.Empty;
        NotesEntry.Text = string.Empty;
        ActiveSwitch.IsToggled = true;
        SupplierErrorLabel.Text = string.Empty;
    }
}
