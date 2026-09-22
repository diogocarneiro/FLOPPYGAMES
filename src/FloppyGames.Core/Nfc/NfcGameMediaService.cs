using FloppyGames.Core.Configuration;
using Serilog;

namespace FloppyGames.Core.Nfc;

/// <summary>
/// Liga o watcher de cartões (chegada/remoção) à leitura do GAME.INI gravado, expondo eventos de
/// alto nível (<see cref="CardInserted"/>, <see cref="CardRemoved"/>) prontos a consumir pelo
/// Agent — espelha <c>RemovableGameMediaService</c> para disquetes/pens, indexado por UID em vez
/// de <c>driveRoot</c>.
/// </summary>
public sealed class NfcGameMediaService : IDisposable
{
    private readonly INfcCardWatcher _watcher;
    private readonly NfcCardConfigReader _reader;
    private readonly ILogger _logger;
    private readonly IReadOnlyList<byte[]> _extraKeys;
    private readonly Dictionary<string, GameConfig> _activeCards = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _stateLock = new();

    /// <param name="extraKeys">
    /// Chaves extra a tentar depois da de fábrica (ex.: derivada de <see cref="FloppyGames.Core.Settings.AgentSettings.NfcCardPassword"/>)
    /// — sem isto, um cartão protegido por password deixaria de ser reconhecido pelo Agent.
    /// </param>
    public NfcGameMediaService(INfcCardWatcher watcher, NfcCardConfigReader reader, ILogger logger, IReadOnlyList<byte[]>? extraKeys = null)
    {
        _watcher = watcher;
        _reader = reader;
        _logger = logger.ForContext<NfcGameMediaService>();
        _extraKeys = extraKeys ?? [];

        _watcher.CardArrived += OnCardArrived;
        _watcher.CardRemoved += OnCardRemoved;
    }

    public event EventHandler<CardInsertedEventArgs>? CardInserted;

    public event EventHandler<CardRemovedEventArgs>? CardRemoved;

    public event EventHandler<InvalidCardEventArgs>? InvalidCardDetected;

    public void Start() => _watcher.Start();

    public void Stop() => _watcher.Stop();

    private void OnCardArrived(object? sender, NfcCardPresence presence)
    {
        lock (_stateLock)
        {
            if (_activeCards.ContainsKey(presence.Uid))
            {
                return;
            }

            var result = _reader.Read(presence.ReaderName, presence.Uid, presence.CardType, _extraKeys);

            switch (result.Status)
            {
                case NfcCardScanStatus.Valid:
                    _activeCards[presence.Uid] = result.Config!;
                    _logger.Information(
                        "Cartão NFC reconhecido: {Title} (UID {Uid}).", result.Config!.Title, presence.Uid);
                    CardInserted?.Invoke(this, new CardInsertedEventArgs(presence.Uid, result.Config));
                    break;

                case NfcCardScanStatus.UnsupportedCardType:
                    _logger.Debug("Cartão NFC de tipo não suportado em UID {Uid} — ignorado.", presence.Uid);
                    break;

                case NfcCardScanStatus.CorruptOrEmptyData:
                    _logger.Debug("Cartão NFC sem dados FloppyGames em UID {Uid} — ignorado.", presence.Uid);
                    break;

                case NfcCardScanStatus.AuthenticationFailed:
                    _logger.Warning("Falha de autenticação a ler o cartão NFC UID {Uid}.", presence.Uid);
                    InvalidCardDetected?.Invoke(this, new InvalidCardEventArgs(presence.Uid, result.Errors));
                    break;

                case NfcCardScanStatus.InvalidGameIni:
                    _logger.Warning(
                        "GAME.INI inválido no cartão NFC UID {Uid}: {Errors}", presence.Uid, string.Join(" | ", result.Errors));
                    InvalidCardDetected?.Invoke(this, new InvalidCardEventArgs(presence.Uid, result.Errors));
                    break;

                case NfcCardScanStatus.ReaderCommunicationFailure:
                    _logger.Warning(
                        "Falha de comunicação com o leitor a ler o cartão NFC UID {Uid}: {Errors}",
                        presence.Uid, string.Join(" | ", result.Errors));
                    break;
            }
        }
    }

    private void OnCardRemoved(object? sender, NfcCardPresence presence)
    {
        lock (_stateLock)
        {
            if (!_activeCards.Remove(presence.Uid, out var config))
            {
                return;
            }

            _logger.Information("Cartão NFC removido: {Title} (UID {Uid}).", config.Title, presence.Uid);
            CardRemoved?.Invoke(this, new CardRemovedEventArgs(presence.Uid, config));
        }
    }

    public void Dispose()
    {
        _watcher.CardArrived -= OnCardArrived;
        _watcher.CardRemoved -= OnCardRemoved;
        _watcher.Dispose();
    }
}
