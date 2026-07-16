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
    private string _message = "Ingresa tus credenciales para iniciar.";
    private bool _isLoading;
    private bool _isError;

    public event Action? LoginSucceeded;

    public LoginViewModel(LoginUseCase loginUseCase, IWritableOperatorSession session)
    {
        _loginUseCase = loginUseCase;
        _session = session;
        LoginCommand = new RelayCommand(
            execute: parameter => RunSafe(ExecuteLoginAsync(parameter), ex => { Message = $"Error inesperado: {ex.Message}"; IsError = true; }),
            canExecute: _ => !IsLoading);
    }

    public string Username
    {
        get => _username;
        set { SetProperty(ref _username, value); LoginCommand.RaiseCanExecuteChanged(); }
    }

    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (SetProperty(ref _isLoading, value))
            {
                OnPropertyChanged(nameof(IsNotLoading));
                LoginCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsNotLoading => !IsLoading;

    public bool IsError
    {
        get => _isError;
        set => SetProperty(ref _isError, value);
    }

    public RelayCommand LoginCommand { get; }

    internal async Task ExecuteLoginAsync(object? parameter)
    {
        if (string.IsNullOrWhiteSpace(Username))
        {
            Message = "El usuario es requerido.";
            IsError = true;
            return;
        }

        string password = string.Empty;
        if (parameter is System.Windows.Controls.PasswordBox passwordBox)
        {
            password = passwordBox.Password;
        }
        else if (parameter is string passwordStr)
        {
            password = passwordStr;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            Message = "La contraseña es requerida.";
            IsError = true;
            return;
        }

        IsLoading = true;
        IsError = false;
        Message = "Verificando credenciales...";
        try
        {
            var result = await _loginUseCase.ExecuteAsync(new LoginRequest(Username, password)).ConfigureAwait(true);
            if (result.Success)
            {
                _session.SetUser(result.UserId, result.Username, result.RoleId, result.RoleName, result.Permissions ?? new HashSet<string>());
                LoginSucceeded?.Invoke();
            }
            else
            {
                Message = result.ErrorMessage ?? "Error de acceso.";
                IsError = true;
            }
        }
        catch (Exception ex)
        {
            Message = $"Error inesperado: {ex.Message}";
            IsError = true;
        }
        finally
        {
            IsLoading = false;
        }
    }
}
