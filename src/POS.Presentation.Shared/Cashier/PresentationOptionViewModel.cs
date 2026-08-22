using System.Globalization;
using POS.Presentation.Shared.ViewModels;

namespace POS.Presentation.Shared.Cashier;

public sealed class PresentationOptionViewModel : ViewModelBase
{
    private static readonly CultureInfo ColombianCulture = CultureInfo.GetCultureInfo("es-CO");
    private bool _isSelected;

    public PresentationOptionViewModel(string? name, int units, decimal price)
    {
        Name = name;
        Units = units;
        Price = price;
    }

    public string? Name { get; }
    public int Units { get; }
    public decimal Price { get; }
    public CashierProductSearchResultViewModel? Product { get; set; }

    public bool IsUnit => string.IsNullOrWhiteSpace(Name);

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public string DisplayText => IsUnit
        ? $"Unidad (${Price.ToString("N0", ColombianCulture)})"
        : Units == 1
            ? $"{Name} (${Price.ToString("N0", ColombianCulture)})"
            : $"{Name} x{Units} (${Price.ToString("N0", ColombianCulture)})";

    public string Label => IsUnit
        ? $"Unidad"
        : Units == 1
            ? $"${Price.ToString("N0", ColombianCulture)}"
            : $"x{Units} · ${Price.ToString("N0", ColombianCulture)}";
}
