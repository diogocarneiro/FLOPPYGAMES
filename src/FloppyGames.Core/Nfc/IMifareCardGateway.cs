namespace FloppyGames.Core.Nfc;

/// <summary>
/// Abstrai a autenticação e o acesso a blocos de um cartão Mifare Classic, para que a lógica de
/// leitura/escrita (<see cref="NfcCardConfigReader"/>, <see cref="NfcCardConfigWriter"/>) seja
/// testável sem hardware real.
/// </summary>
public interface IMifareCardGateway
{
    /// <summary>
    /// Autentica o setor com a chave A indicada. Uma falha de autenticação (cartão previamente
    /// re-chaveado, clone não-standard) é um resultado normal — devolve <c>false</c>, não lança.
    /// </summary>
    public bool Authenticate(string readerName, int sector, byte[] keyA);

    /// <summary>Lê um bloco de 16 bytes já autenticado. Lança se a transmissão falhar (leitor desligado a meio, etc.).</summary>
    public byte[] ReadBlock(string readerName, int absoluteBlock);

    /// <summary>Escreve um bloco de 16 bytes já autenticado. Nunca deve ser chamado num bloco trailer ou no bloco de fabrico.</summary>
    public void WriteBlock(string readerName, int absoluteBlock, byte[] data);
}
