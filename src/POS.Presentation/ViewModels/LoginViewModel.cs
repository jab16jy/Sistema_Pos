namespace POS.Presentation.ViewModels;

public sealed class LoginViewModel : ViewModelBase
{
    private string _username = string.Empty;
    private string _message = "Ingresa tus credenciales para iniciar.";

    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }
}
