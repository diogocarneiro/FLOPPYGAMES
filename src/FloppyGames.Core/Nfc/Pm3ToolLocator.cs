namespace FloppyGames.Core.Nfc;

/// <summary>
/// Localiza o cliente Proxmark3 (<c>tools/proxmark3/proxmark3.exe</c>) empacotado junto ao Agent/
/// Label Studio — espelha o padrão já usado para o catálogo partilhado (ficheiro trazido de fora
/// do projeto para a pasta de saída via <c>CopyToOutputDirectory</c>).
/// </summary>
public static class Pm3ToolLocator
{
    private const string RelativePath = @"Tools\proxmark3\proxmark3.exe";

    public static string? FindExecutablePath()
    {
        var candidate = Path.Combine(AppContext.BaseDirectory, RelativePath);
        return File.Exists(candidate) ? candidate : null;
    }
}
