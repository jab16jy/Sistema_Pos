using POS.Presentation.Commands;

namespace POS.Presentation.ViewModels;

public sealed class LoginViewModel : ViewModelBase
{
    private string _username = string.Empty;
    private string _password = string.Empty;
    private string _message = "Ingresa tus credenciales para iniciar.";

    public string Username
    {
        get => _username;
        set
        {
            SetProperty(ref _username, value);
            LoginCommand.RaiseCanExecuteChanged();
        }
    }

    public string Password
    {
        get => _password;
        set
        {
            SetProperty(ref _password, value);
            LoginCommand.RaiseCanExecuteChanged();
        }
    }

    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }

    public RelayCommand LoginCommand { get; }

    public LoginViewModel()
    {
        LoginCommand = new RelayCommand(
            execute: _ => { },
            canExecute: _ => !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password));
    }
}
