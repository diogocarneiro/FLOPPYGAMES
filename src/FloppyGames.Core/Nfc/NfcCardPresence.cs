namespace FloppyGames.Core.Nfc;

/// <summary>Um cartão presente num leitor específico, com o UID e o tipo detetados.</summary>
public sealed record NfcCardPresence(string ReaderName, string Uid, MifareCardType CardType);
