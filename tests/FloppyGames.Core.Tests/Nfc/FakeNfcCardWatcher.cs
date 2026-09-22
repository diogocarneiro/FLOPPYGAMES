using FloppyGames.Core.Nfc;

namespace FloppyGames.Core.Tests.Nfc;

/// <summary>Dublê de teste de <see cref="INfcCardWatcher"/> — dispara eventos sob controlo do teste, sem PC/SC.</summary>
internal sealed class FakeNfcCardWatcher : INfcCardWatcher
{
    public int StartCalls { get; private set; }

    public int StopCalls { get; private set; }

    public event EventHandler<NfcCardPresence>? CardArrived;

    public event EventHandler<NfcCardPresence>? CardRemoved;

    public void Start() => StartCalls++;

    public void Stop() => StopCalls++;

    public void RaiseArrived(NfcCardPresence presence) => CardArrived?.Invoke(this, presence);

    public void RaiseRemoved(NfcCardPresence presence) => CardRemoved?.Invoke(this, presence);

    public void Dispose()
    {
    }
}
