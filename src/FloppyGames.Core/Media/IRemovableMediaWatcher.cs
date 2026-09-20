namespace FloppyGames.Core.Media;

/// <summary>
/// Deteta a chegada e a remoção de volumes no sistema operativo, sem recorrer a polling.
/// Não sabe nada sobre GAME.INI nem sobre jogos — é apenas a camada de sistema operativo.
/// </summary>
public interface IRemovableMediaWatcher : IDisposable
{
    /// <summary>Disparado quando um novo volume é montado. O argumento é a raiz da unidade (ex.: "E:\").</summary>
    public event EventHandler<string>? DriveArrived;

    /// <summary>Disparado quando um volume é desmontado. O argumento é a raiz da unidade (ex.: "E:\").</summary>
    public event EventHandler<string>? DriveRemoved;

    public void Start();

    public void Stop();
}
