using Microsoft.Win32;

namespace FloppyGames.Core.Startup;

/// <summary>
/// Implementação real de <see cref="IAutostartRegistry"/> sobre <c>HKCU\Software\Microsoft\Windows\CurrentVersion\Run</c>.
/// Vive em <c>HKEY_CURRENT_USER</c> deliberadamente — não exige privilégios de administrador.
/// </summary>
public sealed class WindowsAutostartRegistry : IAutostartRegistry
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public string? GetValue(string valueName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(valueName) as string;
    }

    public void SetValue(string valueName, string value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        key.SetValue(valueName, value, RegistryValueKind.String);
    }

    public void DeleteValue(string valueName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        key?.DeleteValue(valueName, throwOnMissingValue: false);
    }
}
