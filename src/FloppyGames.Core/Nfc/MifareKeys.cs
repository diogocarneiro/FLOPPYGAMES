namespace FloppyGames.Core.Nfc;

/// <summary>Chaves Mifare Classic conhecidas. FloppyGames só re-chaveia um setor quando o utilizador pede explicitamente proteção por password (ver <see cref="NfcCardPasswordKey"/>).</summary>
public static class MifareKeys
{
    /// <summary>A chave Key A de fábrica de um cartão Mifare Classic em branco.</summary>
    public static readonly byte[] FactoryDefaultKeyA = [0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF];

    /// <summary>
    /// A chave de fábrica, seguida de qualquer chave extra (ex.: derivada de uma password de
    /// proteção configurada) a tentar depois — usado para que um cartão que já tenha sido
    /// protegido continue a poder ser lido/reescrito, sem deixar de reconhecer cartões ainda em
    /// configuração de fábrica.
    /// </summary>
    public static IReadOnlyList<byte[]> CandidatesWith(IReadOnlyList<byte[]>? extraKeys) =>
        extraKeys is null || extraKeys.Count == 0 ? [FactoryDefaultKeyA] : [FactoryDefaultKeyA, .. extraKeys];
}
