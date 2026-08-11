using System.Globalization;
using InventorySystem.Domain;
using InventorySystem.Infrastructure.Repositories;
using InventorySystem.Infrastructure.Services;

namespace InventorySystem.AppPages;

public partial class PurveyorContactPage : ContentPage, IQueryAttributable
{
    private readonly SupplierRepository _suppliers;
    private readonly BusinessService _businesses;
    private readonly List<Entry> _phoneEntries = [];
    private readonly List<Entry> _emailEntries = [];
    private long _businessId;
    private long _supplierId;
    private Supplier? _supplier;

    public PurveyorContactPage(SupplierRepository suppliers, BusinessService businesses)
    {
        InitializeComponent();
        _suppliers = suppliers;
        _businesses = businesses;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("SupplierId", out var idValue))
            _supplierId = Convert.ToInt64(idValue, CultureInfo.InvariantCulture);
        else if (query.TryGetValue("Supplier", out var value) && value is Supplier supplier)
            _supplierId = supplier.Id;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            _businessId = (await _businesses.GetDefaultAsync()).Id;
            await RefreshAsync();
        }
        catch (Exception error) { ErrorLabel.Text = error.Message; }
    }

    private async Task RefreshAsync()
    {
        _supplier = await _suppliers.GetAsync(_businessId, _supplierId)
            ?? throw new InventoryRuleException("El proveedor no existe.");
        TitleLabel.Text = _supplier.CompanyName;
        PopulateDetails(_supplier);
        PopulateEditor(_supplier);
        DetailsCard.IsVisible = true;
        EditCard.IsVisible = false;
    }

    private void PopulateDetails(Supplier supplier)
    {
        CompanyValue.Text = supplier.CompanyName;
        SetOptional(ContactDetail, ContactValue, supplier.ContactName);
        SetOptional(CountryDetail, CountryValue, supplier.Country);
        SetOptional(StateDetail, StateValue, supplier.State);
        SetOptional(AddressDetail, AddressValue, supplier.Address);
        SetOptional(NotesDetail, NotesValue, supplier.Notes);
        PopulateLabels(PhonesDetail, PhonesDetailList, supplier.Phones);
        PopulateLabels(EmailsDetail, EmailsDetailList, supplier.Emails);
        ActiveValue.Text = supplier.Active ? "Proveedor activo" : "Proveedor archivado";
    }

    private static void SetOptional(VisualElement container, Label label, string? value)
    {
        container.IsVisible = !string.IsNullOrWhiteSpace(value);
        label.Text = value ?? string.Empty;
    }

    private static void PopulateLabels(VisualElement container, VerticalStackLayout list, IReadOnlyList<string> values)
    {
        list.Children.Clear();
        container.IsVisible = values.Count > 0;
        foreach (var value in values) list.Children.Add(new Label { Text = value });
    }

    private void PopulateEditor(Supplier supplier)
    {
        CompanyEntry.Text = supplier.CompanyName;
        ContactEntry.Text = supplier.ContactName;
        CountryEntry.Text = supplier.Country;
        StateEntry.Text = supplier.State;
        AddressEntry.Text = supplier.Address;
        NotesEntry.Text = supplier.Notes;
        ActiveSwitch.IsToggled = supplier.Active;
        RebuildContactEntries(PhoneEntriesContainer, _phoneEntries, supplier.Phones, Keyboard.Telephone);
        RebuildContactEntries(EmailEntriesContainer, _emailEntries, supplier.Emails, Keyboard.Email);
    }

    private static void RebuildContactEntries(VerticalStackLayout container, List<Entry> entries, IReadOnlyList<string> values, Keyboard keyboard)
    {
        container.Children.Clear();
        entries.Clear();
        foreach (var value in values.DefaultIfEmpty(string.Empty))
        {
            var entry = new Entry { Text = value, Style = (Style)Application.Current!.Resources["FormEntry"], Keyboard = keyboard };
            entries.Add(entry);
            container.Children.Add(entry);
        }
    }

    private void AddPhone_Clicked(object? sender, EventArgs e) => AddContactEntry(PhoneEntriesContainer, _phoneEntries, Keyboard.Telephone);
    private void AddEmail_Clicked(object? sender, EventArgs e) => AddContactEntry(EmailEntriesContainer, _emailEntries, Keyboard.Email);

    private static void AddContactEntry(VerticalStackLayout container, List<Entry> entries, Keyboard keyboard)
    {
        var entry = new Entry { Style = (Style)Application.Current!.Resources["FormEntry"], Keyboard = keyboard };
        entries.Add(entry);
        container.Children.Add(entry);
        entry.Focus();
    }

    private void Edit_Clicked(object? sender, EventArgs e)
    {
        DetailsCard.IsVisible = false;
        EditCard.IsVisible = true;
    }

    private void CancelEdit_Clicked(object? sender, EventArgs e)
    {
        if (_supplier is not null) PopulateEditor(_supplier);
        DetailsCard.IsVisible = true;
        EditCard.IsVisible = false;
        ErrorLabel.Text = string.Empty;
    }

    private async void Save_Clicked(object? sender, EventArgs e)
    {
        try
        {
            ErrorLabel.Text = string.Empty;
            SaveButton.IsEnabled = false;
            var phones = Values(_phoneEntries);
            var emails = Values(_emailEntries);
            await _suppliers.SaveAsync(
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
                    emails),
                _supplierId);
            await RefreshAsync();
            ErrorLabel.Style = (Style)Application.Current!.Resources["InfoText"];
            ErrorLabel.Text = "Proveedor actualizado.";
        }
        catch (Exception error)
        {
            ErrorLabel.Style = (Style)Application.Current!.Resources["ErrorText"];
            ErrorLabel.Text = error.Message;
        }
        finally { SaveButton.IsEnabled = true; }
    }

    private async void Delete_Clicked(object? sender, EventArgs e)
    {
        var confirm = await DisplayAlertAsync(
            "Eliminar proveedor",
            "Si el proveedor ya participa en entradas, lotes u otros registros se archivará para conservar el historial. Si nunca se ha utilizado, se eliminará definitivamente. ¿Deseas continuar?",
            "Eliminar",
            "Cancelar");
        if (!confirm) return;

        try
        {
            DeleteButton.IsEnabled = false;
            var deleted = await _suppliers.DeleteOrArchiveAsync(_businessId, _supplierId);
            await DisplayAlertAsync("Proveedor", deleted ? "El proveedor se eliminó definitivamente." : "El proveedor tiene historial y fue archivado.", "Aceptar");
            await Shell.Current.GoToAsync("//PurveyorFullPage");
        }
        catch (Exception error) { ErrorLabel.Text = error.Message; }
        finally { DeleteButton.IsEnabled = true; }
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (width > 0)
        {
            PageContent.WidthRequest = Math.Max(280, Math.Min(980, width - 56));
        }
    }

    private static IReadOnlyList<string> Values(IEnumerable<Entry> entries) => entries
        .Select(entry => entry.Text?.Trim())
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(value => value!)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
}
