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

    /// <summary>
    /// Equivalente em lote a <see cref="Authenticate"/> para vários setores com a MESMA chave —
    /// ver <see cref="ReadBlocks"/> para a razão de existir (custo fixo por invocação do backend
    /// Proxmark3). Devolve o subconjunto de <paramref name="sectors"/> autenticado com sucesso;
    /// um setor cuja chave atual seja outra simplesmente não aparece no resultado, tal como
    /// <see cref="Authenticate"/> devolveria <c>false</c> para esse setor individualmente. Por
    /// omissão cai para <see cref="Authenticate"/> setor a setor.
    /// </summary>
    public IReadOnlyCollection<int> AuthenticateSectors(string readerName, IReadOnlyList<int> sectors, byte[] keyA) =>
        [.. sectors.Where(sector => Authenticate(readerName, sector, keyA))];

    /// <summary>Lê um bloco de 16 bytes já autenticado. Lança se a transmissão falhar (leitor desligado a meio, etc.).</summary>
    public byte[] ReadBlock(string readerName, int absoluteBlock);

    /// <summary>Escreve um bloco de 16 bytes já autenticado. Nunca deve ser chamado num bloco trailer ou no bloco de fabrico.</summary>
    public void WriteBlock(string readerName, int absoluteBlock, byte[] data);

    /// <summary>
    /// Tenta escrever um bloco sem qualquer autenticação, usando o comando de "acordar em modo
    /// mágico" que os cartões clone Gen1a/Gen2 respondem — nunca funciona num cartão Mifare
    /// Classic genuíno da NXP, só nestes clones "formatáveis". Não precisa (nem usa) a chave
    /// atual do setor, seja ela qual for; é por isso a única forma de recuperar um cartão com
    /// chaves desconhecidas. Devolve <c>false</c> se o cartão não responder ao modo mágico — não
    /// lança, é um resultado normal para qualquer cartão que não seja um clone destes.
    /// </summary>
    public bool TryMagicWriteBlock(string readerName, int absoluteBlock, byte[] data);

    /// <summary>
    /// Equivalente em lote a <see cref="ReadBlock"/> — todos os blocos têm de já estar
    /// autenticados (mesmo setor ou setores). Existe só por desempenho: o backend Proxmark3 invoca
    /// um processo externo por comando (~2s fixos de arranque/ligação USB, medido ao vivo, contra
    /// menos de 1.2s para 3 blocos combinados numa só invocação) — agrupar blocos numa só chamada
    /// evita pagar esse custo fixo por bloco. Por omissão cai para <see cref="ReadBlock"/> bloco a
    /// bloco; só vale a pena substituir num backend com esse custo fixo por invocação (ver
    /// <c>Pm3MifareCardGateway</c>). O resultado é sempre idêntico a chamar <see cref="ReadBlock"/>
    /// em cada bloco individualmente, na mesma ordem.
    /// </summary>
    public byte[][] ReadBlocks(string readerName, IReadOnlyList<int> absoluteBlocks) =>
        [.. absoluteBlocks.Select(block => ReadBlock(readerName, block))];

    /// <summary>Equivalente em lote a <see cref="WriteBlock"/> — ver <see cref="ReadBlocks"/>.</summary>
    public void WriteBlocks(string readerName, IReadOnlyList<(int AbsoluteBlock, byte[] Data)> blocks)
    {
        foreach (var (absoluteBlock, data) in blocks)
        {
            WriteBlock(readerName, absoluteBlock, data);
        }
    }

    /// <summary>Equivalente em lote a <see cref="TryMagicWriteBlock"/> — ver <see cref="ReadBlocks"/>.</summary>
    public bool TryMagicWriteBlocks(string readerName, IReadOnlyList<(int AbsoluteBlock, byte[] Data)> blocks) =>
        blocks.All(block => TryMagicWriteBlock(readerName, block.AbsoluteBlock, block.Data));
}
