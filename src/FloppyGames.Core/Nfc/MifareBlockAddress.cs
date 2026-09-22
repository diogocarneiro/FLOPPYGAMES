namespace FloppyGames.Core.Nfc;

/// <summary>
/// Endereço de um bloco de dados utilizável num cartão Mifare Classic. <see cref="AbsoluteBlock"/>
/// é o índice usado nos comandos APDU de leitura/escrita (<see cref="IMifareCardGateway"/>);
/// <see cref="Sector"/> é o que precisa de ser autenticado antes de aceder ao bloco.
/// </summary>
public readonly record struct MifareBlockAddress(int Sector, int BlockInSector, int AbsoluteBlock);
