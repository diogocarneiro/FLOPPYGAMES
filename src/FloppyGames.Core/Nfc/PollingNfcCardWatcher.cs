using System.Threading;
using Serilog;

namespace FloppyGames.Core.Nfc;

/// <summary>
/// Sonda ativamente os leitores PC/SC ligados à procura de cartões — o PC/SC não tem uma forma
/// simples e portátil de notificar por eventos entre bibliotecas .NET, por isso segue o mesmo
/// padrão de <c>PollingFloppyDriveWatcher</c>: um <see cref="Timer"/> a um intervalo modesto,
/// serializado por um <see cref="Lock"/> para não duplicar eventos entre ciclos sobrepostos.
/// Nunca lança: a ausência de leitor, ou uma falha pontual do PC/SC, é registada uma vez e
/// tratada como "nada presente" em vez de derrubar o Agent ou o Label Studio.
/// </summary>
public sealed class PollingNfcCardWatcher : INfcCardWatcher
{
    private static readonly TimeSpan DefaultPollInterval = TimeSpan.FromSeconds(1);

    private readonly INfcReaderDetector _readerDetector;
    private readonly INfcCardPresenceProbe _presenceProbe;
    private readonly TimeSpan _pollInterval;
    private readonly ILogger _logger;
    private readonly Dictionary<string, NfcCardPresence> _presentByReader = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _pollLock = new();
    private Timer? _timer;
    private bool _loggedNoReader;

    public PollingNfcCardWatcher(
        INfcReaderDetector readerDetector,
        INfcCardPresenceProbe presenceProbe,
        ILogger logger,
        TimeSpan? pollInterval = null)
    {
        _readerDetector = readerDetector;
        _presenceProbe = presenceProbe;
        _logger = logger.ForContext<PollingNfcCardWatcher>();
        _pollInterval = pollInterval ?? DefaultPollInterval;
    }

    public event EventHandler<NfcCardPresence>? CardArrived;

    public event EventHandler<NfcCardPresence>? CardRemoved;

    public void Start()
    {
        if (_timer is not null)
        {
            return;
        }

        _timer = new Timer(_ => PollNow(), null, TimeSpan.Zero, _pollInterval);
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }

    /// <summary>
    /// Executa um ciclo de sondagem imediatamente. Público para testes determinísticos, tal como o
    /// watcher de disquetes. Usa <see cref="Lock.TryEnter()"/> em vez de <c>lock</c> simples: um
    /// ciclo de sondagem pode ficar bloqueado vários segundos à espera do lock exclusivo por porta
    /// COM (partilhado com <c>Pm3CommandRunner</c>) enquanto uma gravação deliberada está em curso
    /// — mas o <see cref="Timer"/> continua a disparar um novo tick a cada segundo de qualquer
    /// forma. Com <c>lock</c> simples, cada tick bloqueado consome uma nova thread do ThreadPool à
    /// espera, e estas empilham-se (centenas de threads em poucos minutos) até esgotar o
    /// ThreadPool e derrubar o processo inteiro sem exceção alguma passar por um try/catch da
    /// aplicação — verificado ao vivo nesta sessão. Saltar um ciclo quando o anterior ainda não
    /// acabou é seguro: o próximo tick, um segundo depois, tenta outra vez.
    /// </summary>
    public void PollNow()
    {
        if (!_pollLock.TryEnter())
        {
            return;
        }

        try
        {
            IReadOnlyList<string> readers;
            try
            {
                readers = _readerDetector.ListConnectedReaders();
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Falha ao listar leitores NFC — a ignorar este ciclo.");
                return;
            }

            if (readers.Count == 0)
            {
                ForgetAllPresentCards();

                if (!_loggedNoReader)
                {
                    _logger.Debug("Nenhum leitor NFC ligado.");
                    _loggedNoReader = true;
                }

                return;
            }

            _loggedNoReader = false;
            PollReaders(readers);
        }
        finally
        {
            _pollLock.Exit();
        }
    }

    private void PollReaders(IReadOnlyList<string> readers)
    {
        foreach (var reader in readers)
        {
            bool present;
            string? uid;
            MifareCardType cardType;

            try
            {
                present = _presenceProbe.TryGetPresentCard(reader, out uid, out cardType);
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Falha ao sondar o leitor NFC {Reader} — a ignorar este ciclo.", reader);
                continue;
            }

            _presentByReader.TryGetValue(reader, out var previous);

            if (present && uid is not null)
            {
                if (previous is not null && string.Equals(previous.Uid, uid, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (previous is not null)
                {
                    _presentByReader.Remove(reader);
                    CardRemoved?.Invoke(this, previous);
                }

                var current = new NfcCardPresence(reader, uid, cardType);
                _presentByReader[reader] = current;
                _logger.Debug("Cartão NFC detetado em {Reader}: UID {Uid}.", reader, uid);
                CardArrived?.Invoke(this, current);
            }
            else if (!present && previous is not null)
            {
                _presentByReader.Remove(reader);
                _logger.Debug("Cartão NFC removido de {Reader}: UID {Uid}.", reader, previous.Uid);
                CardRemoved?.Invoke(this, previous);
            }
        }
    }

    private void ForgetAllPresentCards()
    {
        if (_presentByReader.Count == 0)
        {
            return;
        }

        foreach (var presence in _presentByReader.Values.ToList())
        {
            CardRemoved?.Invoke(this, presence);
        }

        _presentByReader.Clear();
    }

    public void Dispose() => Stop();
}
