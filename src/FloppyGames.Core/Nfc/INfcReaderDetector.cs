namespace FloppyGames.Core.Nfc;

/// <summary>
/// Abstrai a lista de leitores PC/SC ligados ao sistema, para que o resto do código NFC seja
/// testável sem hardware real. Devolve uma lista vazia — nunca lança — quando não há nenhum
/// leitor ou o subsistema PC/SC não está disponível.
/// </summary>
public interface INfcReaderDetector
{
    public IReadOnlyList<string> ListConnectedReaders();
}
