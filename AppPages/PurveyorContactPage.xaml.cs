using InventorySystem.Domain;

namespace InventorySystem.AppPages;

public partial class PurveyorContactPage : ContentPage, IQueryAttributable
{
    public PurveyorContactPage()
    {
        InitializeComponent();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (!query.TryGetValue("Supplier", out var value) || value is not Supplier supplier)
        {
            return;
        }

        CompanyLabel.Text = supplier.CompanyName;
        ContactLabel.Text = $"Contacto: {supplier.ContactName ?? "Sin contacto"}";
        PhoneLabel.Text = $"Teléfono: {supplier.Phone ?? "Sin teléfono"}";
        EmailLabel.Text = $"Correo: {supplier.Email ?? "Sin correo"}";
        LocationLabel.Text = $"{supplier.Country ?? ""} {supplier.State ?? ""}".Trim();
        AddressLabel.Text = supplier.Address ?? "Sin dirección";
        NotesLabel.Text = supplier.Notes ?? "Sin notas";
        StatusLabel.Text = supplier.Active ? "Activo" : "Inactivo";
    }
}
