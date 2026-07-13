namespace POS.Infrastructure.Services;

public sealed class PosRuntimeOptions
{
    public SmtpRuntimeOptions Smtp { get; } = new();
    public WhatsAppScriptRuntimeOptions WhatsAppScript { get; } = new();
    public PrinterRuntimeOptions Printer { get; } = new();
}

public sealed class SmtpRuntimeOptions
{
    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public string? User { get; set; }
    public string? Password { get; set; }
    public bool UseSsl { get; set; }
    public string? FromAddress { get; set; }
}

public sealed class WhatsAppScriptRuntimeOptions
{
    public string? FileName { get; set; }
    public string? WorkingDirectory { get; set; }
}

public sealed class PrinterRuntimeOptions
{
    public string? Command { get; set; }
    public string? WorkingDirectory { get; set; }
    public string? Port { get; set; }
}
