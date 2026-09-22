namespace FloppyGames.Core.Nfc;

/// <summary>
/// Abstrai a verificação de "há um cartão pousado neste leitor?", para que
/// <see cref="PollingNfcCardWatcher"/> seja testável sem hardware real — mesmo padrão de
/// <c>IDriveReadinessProbe</c> para disquetes.
/// </summary>
public interface INfcCardPresenceProbe
{
    public bool TryGetPresentCard(string readerName, out string? uid, out MifareCardType cardType);
}
