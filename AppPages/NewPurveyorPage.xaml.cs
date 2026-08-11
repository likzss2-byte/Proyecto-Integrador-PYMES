using InventorySystem.Domain;
using InventorySystem.Infrastructure.Repositories;
using InventorySystem.Infrastructure.Services;

namespace InventorySystem.AppPages;

public partial class NewPurveyorPage : ContentPage
{
    private readonly SupplierRepository _suppliers;
    private readonly BusinessService _businesses;
    private readonly List<Entry> _phoneEntries = [];
    private readonly List<Entry> _emailEntries = [];
    private long _businessId;

    public NewPurveyorPage(SupplierRepository suppliers, BusinessService businesses)
    {
        InitializeComponent();
        _suppliers = suppliers;
        _businesses = businesses;
        _phoneEntries.Add(PhoneEntry);
        _emailEntries.Add(EmailEntry);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try { _businessId = (await _businesses.GetDefaultAsync()).Id; }
        catch (Exception error) { await DisplayAlertAsync("Proveedor", error.Message, "Aceptar"); }
    }

    private void ContactField_TextChanged(object? sender, TextChangedEventArgs e) => UpdateAddContactButtons();
    private void AddPhone_Clicked(object? sender, EventArgs e) => AddContactEntry(PhoneEntriesContainer, _phoneEntries, Keyboard.Telephone);
    private void AddEmail_Clicked(object? sender, EventArgs e) => AddContactEntry(EmailEntriesContainer, _emailEntries, Keyboard.Email);

    private void AddContactEntry(VerticalStackLayout container, List<Entry> entries, Keyboard keyboard)
    {
        var entry = new Entry { Style = (Style)Application.Current!.Resources["FormEntry"], Keyboard = keyboard };
        entry.TextChanged += ContactField_TextChanged;
        entries.Add(entry);
        container.Children.Add(entry);
        entry.Focus();
        UpdateAddContactButtons();
    }

    private void UpdateAddContactButtons()
    {
        AddPhoneButton.IsVisible = _phoneEntries.Any(entry => !string.IsNullOrWhiteSpace(entry.Text));
        AddEmailButton.IsVisible = _emailEntries.Any(entry => !string.IsNullOrWhiteSpace(entry.Text));
    }

    private async void SaveSupplier_Clicked(object? sender, EventArgs e)
    {
        try
        {
            SupplierErrorLabel.Text = string.Empty;
            var phones = Values(_phoneEntries);
            var emails = Values(_emailEntries);
            var supplier = await _suppliers.SaveAsync(
                _businessId,
                new SupplierInput(
                    CompanyEntry.Text ?? string.Empty,
                    ContactEntry.Text,
                    phones.FirstOrDefault(),
                    emails.FirstOrDefault(),
                    CountryEntry.Text,
                    StateEntry.Text,
                    AddressEntry.Text,
                    NotesEntry.Text,
                    ActiveSwitch.IsToggled,
                    phones,
                    emails));
            await DisplayAlertAsync("Proveedor", $"{supplier.CompanyName} se guardó correctamente.", "Aceptar");
            ClearForm();
        }
        catch (Exception error) { SupplierErrorLabel.Text = error.Message; }
    }

    private static IReadOnlyList<string> Values(IEnumerable<Entry> entries) => entries
        .Select(entry => entry.Text?.Trim())
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(value => value!)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    private void ClearForm()
    {
        CompanyEntry.Text = ContactEntry.Text = CountryEntry.Text = StateEntry.Text = string.Empty;
        AddressEntry.Text = NotesEntry.Text = string.Empty;
        ResetContactEntries(PhoneEntriesContainer, _phoneEntries, PhoneEntry);
        ResetContactEntries(EmailEntriesContainer, _emailEntries, EmailEntry);
        ActiveSwitch.IsToggled = true;
        SupplierErrorLabel.Text = string.Empty;
        UpdateAddContactButtons();
    }

    private static void ResetContactEntries(VerticalStackLayout container, List<Entry> entries, Entry primary)
    {
        foreach (var entry in entries.Skip(1).ToArray()) container.Children.Remove(entry);
        entries.Clear();
        entries.Add(primary);
        primary.Text = string.Empty;
    }
}
