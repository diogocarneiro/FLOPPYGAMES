namespace FloppyGames.Core.Steam;

/// <summary>Obtém a capa (arte vertical) de um jogo pelo seu AppID.</summary>
public interface ICoverArtProvider
{
    public Task<byte[]?> TryDownloadCoverAsync(int appId, CancellationToken cancellationToken);
}
