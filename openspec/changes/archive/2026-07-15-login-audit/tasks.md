# Tasks: Login Module Mitigations

## Phase 1: Security and Logical Improvements

- [x] 1.1 Update `LoginUseCase.cs` to return generic "Usuario o contraseña incorrectos." messages to prevent user enumeration, while maintaining detailed error reasons inside the audit log.
- [x] 1.2 Handle missing roles gracefully in `LoginUseCase.cs` with an audited failure instead of throwing `InvalidOperationException`.
- [x] 1.3 Add a protected `OnPropertyChanged` helper to `ViewModelBase.cs` to enable derived property change notifications.
- [x] 1.4 Refactor `LoginViewModel.cs` to receive the `PasswordBox` parameter in `LoginCommand` and read the password on-demand, removing the mutable `Password` field.
- [x] 1.5 Add `IsNotLoading` property to `LoginViewModel.cs` and trigger its notification.
- [x] 1.6 Update `LoginView.xaml` to bind control enabling state to `IsNotLoading`, add `IsDefault="True"` to the login button, and configure the CommandParameter for password transmission.
- [x] 1.7 Simplify `LoginView.xaml.cs` by removing the `OnPasswordChanged` event handler.
