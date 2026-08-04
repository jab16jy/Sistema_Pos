namespace POS.Presentation.Shared.ViewModels;

public sealed class PresentationRowViewModel : ViewModelBase
{
    private string _name = string.Empty;
    private int? _units;
    private decimal? _price;

    public PresentationRowViewModel()
    {
    }

    public PresentationRowViewModel(string name, int units, decimal price)
    {
        _name = name;
        _units = units;
        _price = price;
    }

    /// <summary>
    /// A blank row the form seeded itself, not one the shop entered. Only these may be dropped on
    /// save without a word: judging that by name instead would silently discard a real presentation
    /// that happens to be called "Caja" and had its figures cleared during an edit.
    /// </summary>
    public bool IsSeededPlaceholder { get; private init; }

    public static PresentationRowViewModel Seeded(string name) =>
        new(name, 0, 0m) { IsSeededPlaceholder = true };

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public int? Units
    {
        get => _units;
        set => SetProperty(ref _units, value);
    }

    public decimal? Price
    {
        get => _price;
        set => SetProperty(ref _price, value);
    }
}
