using POS.Application.DTOs.Auth;
using POS.Application.Interfaces;
using POS.Application.UseCases.Auth;
using POS.Presentation.Commands;

namespace POS.Presentation.ViewModels;

public sealed class LoginViewModel : ViewModelBase
{
    private readonly LoginUseCase _loginUseCase;
    private readonly IWritableOperatorSession _session;
    private string _username = string.Empty;
    private string _password = string.Empty;
    private string _message = "Ingresa tus credenciales para iniciar.";
    private bool _isLoading;

    public event Action? LoginSucceeded;

    public LoginViewModel(LoginUseCase loginUseCase, IWritableOperatorSession session)
    {
        _loginUseCase = loginUseCase;
        _session = session;
        LoginCommand = new RelayCommand(
            execute: _ => ExecuteLogin(),
            canExecute: _ => !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password) && !IsLoading);
    }

    public string Username
    {
        get => _username;
        set { SetProperty(ref _username, value); LoginCommand.RaiseCanExecuteChanged(); }
    }

    public string Password
    {
        get => _password;
        set { SetProperty(ref _password, value); LoginCommand.RaiseCanExecuteChanged(); }
    }

    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set { SetProperty(ref _isLoading, value); LoginCommand.RaiseCanExecuteChanged(); }
    }

    public RelayCommand LoginCommand { get; }

    private void ExecuteLogin()
    {
        IsLoading = true;
        Message = "Verificando credenciales...";
        try
        {
            var result = _loginUseCase.ExecuteAsync(new LoginRequest(Username, Password)).GetAwaiter().GetResult();
            if (result.Success)
            {
                _session.SetUser(result.UserId, result.Username, result.Role);
                LoginSucceeded?.Invoke();
            }
            else
            {
                Message = result.ErrorMessage ?? "Error de acceso.";
            }
        }
    catch (Exception ex)
    {
        Message = $"Error inesperado: {ex.Message}";
    }
        finally
        {
            IsLoading = false;
        }
    }
}
