using System.Security.Cryptography;
using System.Text;

namespace FloppyGames.Core.Nfc;

/// <summary>
/// Deriva a chave de 6 bytes (Key A/B Mifare) usada para proteger um cartão a partir da palavra-passe
/// configurada nas Definições — o cartão nunca guarda a palavra-passe em si, só os 6 bytes de uma
/// chave. SHA-256 truncado: determinístico, sem dependências extra, suficiente para este uso
/// (impedir leitura/escrita acidental ou por outro programa — não um cofre criptográfico; quem
/// tiver acesso físico ao cartão e a um leitor pode sempre tentar as 2^48 chaves possíveis
/// offline, como em qualquer Mifare Classic).
/// </summary>
public static class NfcCardPasswordKey
{
    public static byte[] Derive(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return hash[..6];
    }
}
