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
