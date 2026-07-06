using System.Security.Cryptography;
using System.Text;

namespace POS.Infrastructure.Persistence.Configurations;

internal static class SmtpPasswordProtector
{
    private const string ProtectedPrefix = "dpapi:v1:";

    public static string? Protect(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.StartsWith(ProtectedPrefix, StringComparison.Ordinal))
        {
            return password;
        }

        if (!OperatingSystem.IsWindows())
        {
            return password;
        }

        byte[] clearBytes = Encoding.UTF8.GetBytes(password);
        byte[] protectedBytes = ProtectedData.Protect(clearBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);

        return ProtectedPrefix + Convert.ToBase64String(protectedBytes);
    }

    public static string? Unprotect(string? storedPassword)
    {
        if (string.IsNullOrEmpty(storedPassword) || !storedPassword.StartsWith(ProtectedPrefix, StringComparison.Ordinal))
        {
            return storedPassword;
        }

        if (!OperatingSystem.IsWindows())
        {
            return storedPassword;
        }

        try
        {
            string payload = storedPassword[ProtectedPrefix.Length..];
            byte[] protectedBytes = Convert.FromBase64String(payload);
            byte[] clearBytes = ProtectedData.Unprotect(protectedBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);

            return Encoding.UTF8.GetString(clearBytes);
        }
        catch (CryptographicException)
        {
            return string.Empty;
        }
        catch (FormatException)
        {
            return string.Empty;
        }
    }
}
