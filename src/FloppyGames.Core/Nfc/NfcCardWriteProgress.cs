namespace FloppyGames.Core.Nfc;

/// <summary>Um bloco acabado de gravar — para a UI poder mostrar exatamente o que está a ser escrito, não só uma percentagem.</summary>
public sealed record NfcCardWriteProgress(int Current, int Total, int Sector, int AbsoluteBlock, byte[] Data);
