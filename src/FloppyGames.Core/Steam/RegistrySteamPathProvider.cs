using Microsoft.Win32;

namespace FloppyGames.Core.Steam;

/// <summary>Lê <c>HKCU\Software\Valve\Steam\SteamPath</c>, escrito pelo cliente Steam na instalação.</summary>
public sealed class RegistrySteamPathProvider : ISteamPathProvider
{
    public string? GetSteamInstallPath()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
        var path = key?.GetValue("SteamPath") as string;

        return string.IsNullOrWhiteSpace(path) ? null : path.Replace('/', '\\');
    }
}
