namespace POS.Presentation.Shared.Shell;

public sealed record ModuleNavigationItem(
    string Key,
    string Title,
    string Description,
    string Shortcut,
    bool IsImplemented);
