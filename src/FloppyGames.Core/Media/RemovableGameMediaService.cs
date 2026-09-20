using FloppyGames.Core.Configuration;
using Serilog;

namespace FloppyGames.Core.Media;

/// <summary>
/// Liga o watcher de baixo nível (chegada/remoção de volumes) à validação do GAME.INI,
/// expondo eventos de alto nível (<see cref="MediaInserted"/>, <see cref="MediaRemoved"/>)
/// prontos a consumir pelo Agent. Mantém internamente qual o jogo associado a cada unidade,
/// para que a remoção devolva sempre a configuração correta a terminar.
/// </summary>
public sealed class RemovableGameMediaService : IDisposable
{
    private readonly IRemovableMediaWatcher _watcher;
    private readonly GameMediaScanner _scanner;
    private readonly ILogger _logger;
    private readonly Dictionary<string, GameConfig> _activeMedia = new(StringComparer.OrdinalIgnoreCase);

    public RemovableGameMediaService(IRemovableMediaWatcher watcher, GameMediaScanner scanner, ILogger logger)
    {
        _watcher = watcher;
        _scanner = scanner;
        _logger = logger.ForContext<RemovableGameMediaService>();

        _watcher.DriveArrived += OnDriveArrived;
        _watcher.DriveRemoved += OnDriveRemoved;
    }

    public event EventHandler<MediaInsertedEventArgs>? MediaInserted;

    public event EventHandler<MediaRemovedEventArgs>? MediaRemoved;

    public event EventHandler<InvalidMediaEventArgs>? InvalidMediaDetected;

    public void Start() => _watcher.Start();

    public void Stop() => _watcher.Stop();

    private void OnDriveArrived(object? sender, string driveRoot)
    {
        var result = _scanner.Scan(driveRoot);

        switch (result.Status)
        {
            case GameMediaScanStatus.Valid:
                _activeMedia[driveRoot] = result.Config!;
                _logger.Information(
                    "Disquete reconhecida em {Drive}: {Title} (AppID {AppId}).",
                    driveRoot, result.Config!.Title, result.Config.AppId);
                MediaInserted?.Invoke(this, new MediaInsertedEventArgs(driveRoot, result.Config));
                break;

            case GameMediaScanStatus.NotRemovable:
                // Unidade fixa/ótica: não é um suporte FloppyGames, ignorar sem registar ruído nos logs.
                break;

            case GameMediaScanStatus.NoGameIni:
                _logger.Debug("Volume amovível em {Drive} sem GAME.INI — ignorado.", driveRoot);
                break;

            case GameMediaScanStatus.InvalidGameIni:
                _logger.Warning(
                    "GAME.INI inválido em {Drive}: {Errors}", driveRoot, string.Join(" | ", result.Errors));
                InvalidMediaDetected?.Invoke(this, new InvalidMediaEventArgs(driveRoot, result.Errors));
                break;
        }
    }

    private void OnDriveRemoved(object? sender, string driveRoot)
    {
        if (!_activeMedia.Remove(driveRoot, out var config))
        {
            return;
        }

        _logger.Information("Disquete removida de {Drive}: {Title}.", driveRoot, config.Title);
        MediaRemoved?.Invoke(this, new MediaRemovedEventArgs(driveRoot, config));
    }

    public void Dispose()
    {
        _watcher.DriveArrived -= OnDriveArrived;
        _watcher.DriveRemoved -= OnDriveRemoved;
        _watcher.Dispose();
    }
}
