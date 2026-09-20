using FloppyGames.Core.Media;

namespace FloppyGames.Core.Tests.Media;

/// <summary>Dublê de teste de <see cref="IRemovableDriveInspector"/> — simula unidades sem tocar no disco real.</summary>
internal sealed class FakeDriveInspector : IRemovableDriveInspector
{
    private readonly Dictionary<string, bool> _removable = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _gameIniContent = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, long> _freeBytes = new(StringComparer.OrdinalIgnoreCase);

    public FakeDriveInspector WithRemovableDrive(string driveRoot)
    {
        _removable[driveRoot] = true;
        return this;
    }

    public FakeDriveInspector WithFixedDrive(string driveRoot)
    {
        _removable[driveRoot] = false;
        return this;
    }

    public FakeDriveInspector WithGameIni(string driveRoot, string content)
    {
        _gameIniContent[driveRoot] = content;
        return this;
    }

    public FakeDriveInspector WithFreeBytes(string driveRoot, long freeBytes)
    {
        _freeBytes[driveRoot] = freeBytes;
        return this;
    }

    public bool IsRemovableDrive(string driveRoot) => _removable.GetValueOrDefault(driveRoot);

    public bool TryReadGameIni(string driveRoot, out string? content) =>
        _gameIniContent.TryGetValue(driveRoot, out content);

    public long GetAvailableFreeBytes(string driveRoot) =>
        _freeBytes.GetValueOrDefault(driveRoot, long.MaxValue);
}
