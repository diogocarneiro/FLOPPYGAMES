namespace FloppyGames.Core.Media;

/// <summary>Bytes já escritos para o suporte amovível, de um total conhecido à partida (GAME.INI + capa).</summary>
public sealed record MediaWriteProgress(long BytesWritten, long TotalBytes);
