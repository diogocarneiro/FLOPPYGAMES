using System.Text.RegularExpressions;

namespace FloppyGames.Core.Nfc;

/// <summary>
/// Deteta um cartão pousado na antena de um Proxmark3, correndo <c>hf 14a info</c> e lendo o UID/
/// SAK do texto devolvido. Verificado contra hardware real nesta sessão (um Proxmark3 RDV4 com
/// firmware Iceman, cartão Mifare Classic 1K Gen1a) — o formato de saída pode variar ligeiramente
/// entre versões do cliente, por isso a leitura assenta só na presença da linha "UID:"/"SAK:",
/// não numa correspondência exata de todo o bloco de texto.
/// </summary>
public sealed partial class Pm3NfcCardPresenceProbe : INfcCardPresenceProbe
{
    private readonly string _pm3ExecutablePath;

    public Pm3NfcCardPresenceProbe(string pm3ExecutablePath) => _pm3ExecutablePath = pm3ExecutablePath;

    public bool TryGetPresentCard(string readerName, out string? uid, out MifareCardType cardType)
    {
        uid = null;
        cardType = MifareCardType.Unknown;

        string output;
        try
        {
            output = Pm3CommandRunner.Run(_pm3ExecutablePath, readerName, "hf 14a info");
        }
        catch (Exception ex) when (ex is InvalidOperationException or TimeoutException)
        {
            return false;
        }

        var uidMatch = UidPattern().Match(output);
        if (!uidMatch.Success)
        {
            return false;
        }

        uid = uidMatch.Groups[1].Value.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();

        var sakMatch = SakPattern().Match(output);
        if (sakMatch.Success)
        {
            cardType = sakMatch.Groups[1].Value.ToUpperInvariant() switch
            {
                "08" => MifareCardType.Classic1K,
                "18" => MifareCardType.Classic4K,
                _ => MifareCardType.Unknown,
            };
        }

        return true;
    }

    [GeneratedRegex(@"UID:\s*((?:[0-9A-Fa-f]{2}\s*)+)")]
    private static partial Regex UidPattern();

    [GeneratedRegex(@"SAK:\s*([0-9A-Fa-f]{2})")]
    private static partial Regex SakPattern();
}
