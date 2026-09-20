namespace FloppyGames.Core.Startup;

/// <summary>
/// Abstrai o acesso à chave de arranque automático do Windows (<c>HKCU\...\Run</c>),
/// para que <see cref="AutostartManager"/> seja testável sem tocar no registo real.
/// </summary>
public interface IAutostartRegistry
{
    public string? GetValue(string valueName);

    public void SetValue(string valueName, string value);

    public void DeleteValue(string valueName);
}
