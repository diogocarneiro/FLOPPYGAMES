using System.Reflection;

namespace FloppyGames.Core;

/// <summary>
/// Nome, versão e copyright da aplicação em execução, lidos dos atributos do assembly de entrada
/// (definidos uma só vez em <c>Directory.Build.props</c> e no <c>AssemblyTitle</c> de cada app) —
/// para o rodapé do Agent e do Label Studio nunca divergirem da versão compilada.
/// </summary>
public static class AppInfo
{
    private static readonly Assembly Entry = Assembly.GetEntryAssembly() ?? typeof(AppInfo).Assembly;

    public static string Title => Entry.GetCustomAttribute<AssemblyTitleAttribute>()?.Title ?? "FloppyGames";

    /// <summary>A versão sem o sufixo "+commit" que o SDK .NET acrescenta à versão informativa.</summary>
    public static string Version =>
        Entry.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? Entry.GetName().Version?.ToString(3)
        ?? string.Empty;

    public static string Copyright => Entry.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? string.Empty;

    /// <summary>Ex.: "FloppyGames Agent 0.1.0 · © 2026 Diogo Carneiro. Tous droits réservés."</summary>
    public static string FooterText => $"{Title} {Version} · {Copyright}";
}
