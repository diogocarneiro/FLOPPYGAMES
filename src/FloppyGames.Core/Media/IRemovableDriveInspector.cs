namespace FloppyGames.Core.Media;

/// <summary>
/// Abstrai o acesso ao sistema de ficheiros de uma unidade, para que a lógica de
/// validação de mídia (<see cref="GameMediaScanner"/>) possa ser testada sem
/// depender de unidades físicas reais.
/// </summary>
public interface IRemovableDriveInspector
{
    /// <summary>Indica se a unidade indicada é reportada pelo Windows como amovível.</summary>
    public bool IsRemovableDrive(string driveRoot);

    /// <summary>Tenta ler o conteúdo do GAME.INI na raiz da unidade.</summary>
    public bool TryReadGameIni(string driveRoot, out string? content);

    /// <summary>Espaço livre, em bytes, disponível na unidade.</summary>
    public long GetAvailableFreeBytes(string driveRoot);
}
