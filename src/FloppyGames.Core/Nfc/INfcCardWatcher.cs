namespace FloppyGames.Core.Nfc;

/// <summary>
/// Deteta a chegada e a remoção de cartões num leitor PC/SC. Não sabe nada sobre GAME.INI nem
/// sobre jogos — é apenas a camada de deteção física, tal como <c>IRemovableMediaWatcher</c> para
/// disquetes/pens.
/// </summary>
public interface INfcCardWatcher : IDisposable
{
    public event EventHandler<NfcCardPresence>? CardArrived;

    public event EventHandler<NfcCardPresence>? CardRemoved;

    public void Start();

    public void Stop();
}
