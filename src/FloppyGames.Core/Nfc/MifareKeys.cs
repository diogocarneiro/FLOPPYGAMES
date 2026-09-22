namespace FloppyGames.Core.Nfc;

/// <summary>Chaves Mifare Classic conhecidas. FloppyGames só usa a chave de fábrica — nunca re-chaveia setores.</summary>
public static class MifareKeys
{
    /// <summary>A chave Key A de fábrica de um cartão Mifare Classic em branco.</summary>
    public static readonly byte[] FactoryDefaultKeyA = [0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF];
}
