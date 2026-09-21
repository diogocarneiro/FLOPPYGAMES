using FloppyGames.Core.Configuration;
using Microsoft.Win32;

namespace FloppyGames.Core.Platforms;

/// <summary>
/// Lê os jogos GOG instalados a partir do Registo
/// (<c>HKLM\SOFTWARE\WOW6432Node\GOG.com\Games\&lt;gameID&gt;</c>, com valores <c>name</c>,
/// <c>path</c> e <c>exe</c>).
/// <para>
/// <b>Não verificado em hardware real</b> — o GOG Galaxy não estava instalado em nenhuma máquina
/// disponível ao escrever isto, por isso esta estrutura segue apenas o que é documentado pela
/// comunidade (usada por ferramentas como o Playnite), ao contrário da Steam e da Epic, cujo
/// formato foi confirmado diretamente contra ficheiros/registo reais. Falha graciosamente (lista
/// vazia / <c>null</c>) se a chave não existir ou os valores não baterem certo — nunca lança.
/// </para>
/// </summary>
public sealed class GogGameLibraryScanner
{
    private const string GamesRegistryPath = @"SOFTWARE\WOW6432Node\GOG.com\Games";

    public IReadOnlyList<DiscoveredGame> ScanInstalledGames()
    {
        try
        {
            using var gamesKey = Registry.LocalMachine.OpenSubKey(GamesRegistryPath);
            if (gamesKey is null)
            {
                return [];
            }

            var games = new List<DiscoveredGame>();

            foreach (var gameId in gamesKey.GetSubKeyNames())
            {
                using var gameKey = gamesKey.OpenSubKey(gameId);
                var game = TryReadGameKey(gameId, gameKey);
                if (game is not null)
                {
                    games.Add(game);
                }
            }

            return games.OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>Resolve o executável instalado de um jogo pelo seu GOG_ID — usado no momento do lançamento.</summary>
    public string? TryResolveExePath(string gogGameId)
    {
        try
        {
            using var gameKey = Registry.LocalMachine.OpenSubKey($@"{GamesRegistryPath}\{gogGameId}");
            if (gameKey is null)
            {
                return null;
            }

            var path = gameKey.GetValue("path") as string;
            var exe = gameKey.GetValue("exe") as string;

            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(exe))
            {
                return null;
            }

            return Path.IsPathRooted(exe) ? exe : Path.Combine(path, exe);
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static DiscoveredGame? TryReadGameKey(string gameId, RegistryKey? gameKey)
    {
        if (gameKey is null)
        {
            return null;
        }

        var name = gameKey.GetValue("name") as string;
        var path = gameKey.GetValue("path") as string;

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        return new DiscoveredGame(name, GamePlatform.Gog, path, GogGameId: gameId);
    }
}
