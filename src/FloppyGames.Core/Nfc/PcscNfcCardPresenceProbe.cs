using PCSC;
using PCSC.Exceptions;

namespace FloppyGames.Core.Nfc;

/// <summary>
/// Deteta se há um cartão pousado num leitor PC/SC, lê o UID (comando pseudo-APDU padrão
/// <c>FF CA 00 00 00</c>, "Get Data") e tenta reconhecer o tipo Mifare Classic a partir do ATR —
/// os leitores contactless compatíveis com PC/SC Parte 3 codificam o nome do cartão nos bytes
/// históricos do ATR (RID do grupo de trabalho PC/SC <c>A0 00 00 03 06</c>, seguido do código do
/// cartão: <c>03 00 01</c> = Mifare 1K, <c>03 00 02</c> = Mifare 4K).
///
/// NÃO VERIFICADO CONTRA HARDWARE REAL nesta sessão — o padrão de ATR está documentado
/// publicamente, mas leitores/firmwares diferentes do assumido (ACR122U ou compatível) podem
/// reportar um ATR ligeiramente diferente. Ver checklist de validação manual no ROADMAP.
/// </summary>
public sealed class PcscNfcCardPresenceProbe : INfcCardPresenceProbe
{
    private static readonly byte[] GetUidApdu = [0xFF, 0xCA, 0x00, 0x00, 0x00];
    private static readonly byte[] Mifare1KAtrMarker = [0x03, 0x00, 0x01];
    private static readonly byte[] Mifare4KAtrMarker = [0x03, 0x00, 0x02];

    public bool TryGetPresentCard(string readerName, out string? uid, out MifareCardType cardType)
    {
        uid = null;
        cardType = MifareCardType.Unknown;

        try
        {
            using var context = ContextFactory.Instance.Establish(SCardScope.System);
            using var reader = context.ConnectReader(readerName, SCardShareMode.Shared, SCardProtocol.Any);

            var receiveBuffer = new byte[258];
            var received = reader.Transmit(GetUidApdu, receiveBuffer);

            if (received < 2 || receiveBuffer[received - 2] != 0x90 || receiveBuffer[received - 1] != 0x00)
            {
                return false;
            }

            var uidBytes = receiveBuffer[..(received - 2)];
            if (uidBytes.Length == 0)
            {
                return false;
            }

            uid = Convert.ToHexString(uidBytes);
            cardType = ClassifyFromAtr(reader.GetAttrib(SCardAttribute.AtrString));
            return true;
        }
        catch (PCSCException)
        {
            // Sem cartão presente, leitor removido a meio da sondagem, ou sem acesso ao leitor.
            return false;
        }
    }

    private static MifareCardType ClassifyFromAtr(byte[]? atr)
    {
        if (atr is null)
        {
            return MifareCardType.Unknown;
        }

        if (Contains(atr, Mifare1KAtrMarker))
        {
            return MifareCardType.Classic1K;
        }

        if (Contains(atr, Mifare4KAtrMarker))
        {
            return MifareCardType.Classic4K;
        }

        return MifareCardType.Unknown;
    }

    private static bool Contains(byte[] haystack, byte[] needle)
    {
        for (var i = 0; i <= haystack.Length - needle.Length; i++)
        {
            var match = true;
            for (var j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j])
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                return true;
            }
        }

        return false;
    }
}
