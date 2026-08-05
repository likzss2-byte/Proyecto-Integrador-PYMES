using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using InventorySystem.Domain;

namespace InventorySystem.AppPages;

internal sealed class InventoryCountRowViewModel : INotifyPropertyChanged
{
    private string _physicalText;

    public InventoryCountRowViewModel(InventoryCountLine line, bool canRemove)
    {
        Line = line;
        CanRemove = canRemove;
        _physicalText = line.PhysicalStock?.ToString("0.###", CultureInfo.CurrentCulture) ?? string.Empty;
    }

    public InventoryCountLine Line { get; }
    public long ProductId => Line.ProductId;
    public string Code => Line.Code;
    public string ProductName => Line.ProductName;
    public string ProductDetail => string.IsNullOrWhiteSpace(Line.Brand)
        ? UnitName
        : $"{Line.Brand} · {UnitName}";
    public string UnitName => Line.UnitOfMeasure switch
    {
        UnitOfMeasure.Kilogram => "Kilogramo",
        UnitOfMeasure.Liter => "Litro",
        _ => "Unidad"
    };
    public string TheoreticalDisplay => $"{Line.TheoreticalStock:0.###} {Line.UnitSymbol}";
    public bool CanRemove { get; }
    public bool CanCountLots => Line.ExpirationMode == ExpirationMode.Tracked;
    public string LotActionText => Line.CountByLot ? "Editar lotes" : "Contar lotes";

    public string PhysicalText
    {
        get => _physicalText;
        set
        {
            if (_physicalText == value)
            {
                return;
            }

            _physicalText = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(MissingDisplay));
            OnPropertyChanged(nameof(SurplusDisplay));
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(StatusColor));
            OnPropertyChanged(nameof(RowBackground));
        }
    }

    public string MissingDisplay => TryGetPhysical(out var physical, out _)
        ? Math.Max(Line.TheoreticalStock - physical, 0m).ToString("0.###", CultureInfo.CurrentCulture)
        : "—";
    public string SurplusDisplay => TryGetPhysical(out var physical, out _)
        ? Math.Max(physical - Line.TheoreticalStock, 0m).ToString("0.###", CultureInfo.CurrentCulture)
        : "—";
    public string StatusText
    {
        get
        {
            if (!TryGetPhysical(out var physical, out _))
            {
                return "Pendiente";
            }

            return physical < Line.TheoreticalStock
                ? "Faltante"
                : physical > Line.TheoreticalStock
                    ? "Sobrante"
                    : "Sin diferencia";
        }
    }
    public Color StatusColor => StatusText switch
    {
        "Faltante" => Color.FromArgb("#9B2C21"),
        "Sobrante" => Color.FromArgb("#28723D"),
        "Sin diferencia" => Color.FromArgb("#486154"),
        _ => Color.FromArgb("#6B746E")
    };
    public Color RowBackground => StatusText switch
    {
        "Faltante" => Color.FromArgb("#FFF6F4"),
        "Sobrante" => Color.FromArgb("#F4FAF5"),
        "Sin diferencia" => Color.FromArgb("#F8FAF8"),
        _ => Colors.White
    };

    public bool TryGetPhysical(out decimal value, out string? error)
    {
        value = 0m;
        error = null;
        if (string.IsNullOrWhiteSpace(PhysicalText))
        {
            error = $"Captura el inventario físico de {ProductName}.";
            return false;
        }

        if (!decimal.TryParse(PhysicalText, NumberStyles.Number, CultureInfo.CurrentCulture, out value)
            && !decimal.TryParse(PhysicalText, NumberStyles.Number, CultureInfo.InvariantCulture, out value))
        {
            error = $"La cantidad de {ProductName} no es válida.";
            return false;
        }

        value = InventoryRules.NormalizeQuantity(value);
        if (value < 0)
        {
            error = $"La cantidad de {ProductName} no puede ser negativa.";
            return false;
        }

        if (Line.UnitOfMeasure == UnitOfMeasure.Unit && value != decimal.Truncate(value))
        {
            error = $"La cantidad de {ProductName} debe ser entera porque se maneja por unidad.";
            return false;
        }

        return true;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

internal sealed class InventoryLotCountRowViewModel : INotifyPropertyChanged
{
    private string _physicalText;

    public InventoryLotCountRowViewModel(InventoryCountLotLine line, UnitOfMeasure unit)
    {
        Line = line;
        Unit = unit;
        _physicalText = line.PhysicalQuantity?.ToString("0.###", CultureInfo.CurrentCulture) ?? string.Empty;
    }

    public InventoryCountLotLine Line { get; }
    public UnitOfMeasure Unit { get; }
    public string LotCode => Line.LotCode;
    public string ExpirationDisplay => Line.ExpirationDate?.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture) ?? "Sin fecha";
    public string ExpirationStatus => Line.ExpirationStatus;
    public string TheoreticalDisplay => Line.TheoreticalQuantity.ToString("0.###", CultureInfo.CurrentCulture);
    public string DifferenceDisplay => TryGetPhysical(out var physical, out _)
        ? (physical - Line.TheoreticalQuantity).ToString("+0.###;-0.###;0", CultureInfo.CurrentCulture)
        : "—";

    public string PhysicalText
    {
        get => _physicalText;
        set
        {
            if (_physicalText == value)
            {
                return;
            }

            _physicalText = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PhysicalText)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DifferenceDisplay)));
        }
    }

    public bool TryGetPhysical(out decimal value, out string? error)
    {
        value = 0m;
        error = null;
        if (!decimal.TryParse(PhysicalText, NumberStyles.Number, CultureInfo.CurrentCulture, out value)
            && !decimal.TryParse(PhysicalText, NumberStyles.Number, CultureInfo.InvariantCulture, out value))
        {
            error = $"Captura una cantidad válida para el lote {LotCode}.";
            return false;
        }

        value = InventoryRules.NormalizeQuantity(value);
        if (value < 0 || (Unit == UnitOfMeasure.Unit && value != decimal.Truncate(value)))
        {
            error = Unit == UnitOfMeasure.Unit
                ? $"El lote {LotCode} requiere una cantidad entera no negativa."
                : $"La cantidad del lote {LotCode} no puede ser negativa.";
            return false;
        }

        return true;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

internal sealed record InventorySessionOption(long Id, string Label)
{
    public override string ToString() => Label;
}
