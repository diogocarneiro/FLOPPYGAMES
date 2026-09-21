using System.Diagnostics;
using FloppyGames.Core.Configuration;
using FloppyGames.Core.Platforms;

namespace FloppyGames.Core.Launch;

/// <summary>
/// Lança jogos GOG diretamente pelo executável instalado — sem protocolo oficial fiável para
/// validar (ao contrário da Steam/Epic), resolve o caminho a partir do <c>GOG_ID</c> via
/// <see cref="GogGameLibraryScanner"/> no momento do lançamento, em vez de gravar um caminho
/// absoluto no GAME.INI, para o floppy continuar portátil entre reinstalações.
/// </summary>
public sealed class GogExeLauncher : IGameLauncher
{
    private readonly GogGameLibraryScanner _scanner;

    public GogExeLauncher(GogGameLibraryScanner scanner)
    {
        _scanner = scanner;
    }

    public void Launch(GameConfig config)
    {
        var gogGameId = config.GogGameId
            ?? throw new InvalidOperationException("GAME.INI sem GOG_ID — não é possível lançar.");

        var exePath = _scanner.TryResolveExePath(gogGameId)
            ?? throw new InvalidOperationException($"Jogo GOG '{gogGameId}' não encontrado no Registo — está instalado?");

        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = exePath,
            WorkingDirectory = Path.GetDirectoryName(exePath),
            UseShellExecute = true,
        });
    }
}
