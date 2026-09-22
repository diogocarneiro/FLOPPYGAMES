using FloppyGames.Core.Nfc;

namespace FloppyGames.Core.Tests.Nfc;

internal sealed class FakeNfcCardPresenceProbe : INfcCardPresenceProbe
{
    private readonly Dictionary<string, (string Uid, MifareCardType CardType)> _present =
        new(StringComparer.OrdinalIgnoreCase);

    public void SetPresent(string readerName, string uid, MifareCardType cardType) =>
        _present[readerName] = (uid, cardType);

    public void SetAbsent(string readerName) => _present.Remove(readerName);

    public bool TryGetPresentCard(string readerName, out string? uid, out MifareCardType cardType)
    {
        if (_present.TryGetValue(readerName, out var card))
        {
            uid = card.Uid;
            cardType = card.CardType;
            return true;
        }

        uid = null;
        cardType = MifareCardType.Unknown;
        return false;
    }
}
