namespace POS.Presentation.ViewModels;

public sealed class SaleViewModel : ViewModelBase
{
    private string _guideMessage = "Escanea el código o escribe el nombre del producto.";

    public string GuideMessage
    {
        get => _guideMessage;
        set => SetProperty(ref _guideMessage, value);
    }
}
