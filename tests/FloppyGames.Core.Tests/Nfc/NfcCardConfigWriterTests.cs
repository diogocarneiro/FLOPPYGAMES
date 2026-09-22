using FloppyGames.Core.Configuration;
using FloppyGames.Core.Nfc;

namespace FloppyGames.Core.Tests.Nfc;

public class NfcCardConfigWriterTests
{
    private const string Reader = "ACME PC/SC Reader 0";

    private static readonly GameConfig Config = new()
    {
        Title = "Portal", Platform = GamePlatform.Steam, AppId = 400, Process = "portal.exe",
    };

    [Fact]
    public void Check_UnknownCardType_ReturnsBlocked()
    {
        var writer = new NfcCardConfigWriter(new FakeMifareCardGateway());

        var check = writer.Check(Reader, MifareCardType.Unknown, Config);

        Assert.Equal(NfcCardWriteCheckStatus.Blocked, check.Status);
    }

    [Fact]
    public void Check_ConfigTooLargeForCard_ReturnsBlockedWithByteCounts()
    {
        var writer = new NfcCardConfigWriter(new FakeMifareCardGateway());
        var oversizedConfig = Config with { Description = new string('A', 2000) };

        var check = writer.Check(Reader, MifareCardType.Classic1K, oversizedConfig);

        Assert.Equal(NfcCardWriteCheckStatus.Blocked, check.Status);
        Assert.Contains("752", check.Message);
    }

    [Fact]
    public void Check_AuthenticationFailsOnFirstSector_ReturnsAuthenticationFailed()
    {
        // Distinto de Blocked: um cartão clone "magic" ainda pode ser escrito via WriteMagic
        // mesmo que a chave de fábrica não autentique.
        var gateway = new FakeMifareCardGateway();
        gateway.FailAuthenticationForSector(0);
        var writer = new NfcCardConfigWriter(gateway);

        var check = writer.Check(Reader, MifareCardType.Classic1K, Config);

        Assert.Equal(NfcCardWriteCheckStatus.AuthenticationFailed, check.Status);
    }

    [Fact]
    public void Check_BlankCard_ReturnsReady()
    {
        var writer = new NfcCardConfigWriter(new FakeMifareCardGateway());

        var check = writer.Check(Reader, MifareCardType.Classic1K, Config);

        Assert.Equal(NfcCardWriteCheckStatus.Ready, check.Status);
    }

    [Fact]
    public void Check_CardAlreadyHasData_ReturnsNeedsConfirmation()
    {
        var gateway = new FakeMifareCardGateway();
        gateway.SeedCardContent("[Game]\nTITLE=Old\nAPPID=1\nPROCESS=old.exe\n", MifareCardType.Classic1K);
        var writer = new NfcCardConfigWriter(gateway);

        var check = writer.Check(Reader, MifareCardType.Classic1K, Config);

        Assert.Equal(NfcCardWriteCheckStatus.NeedsConfirmation, check.Status);
    }

    [Fact]
    public void Check_GatewayThrows_ReturnsBlocked()
    {
        var gateway = new FakeMifareCardGateway { ThrowOnRead = true };
        var writer = new NfcCardConfigWriter(gateway);

        var check = writer.Check(Reader, MifareCardType.Classic1K, Config);

        Assert.Equal(NfcCardWriteCheckStatus.Blocked, check.Status);
    }

    [Fact]
    public void Write_ThenRead_RoundTripsConfig()
    {
        var gateway = new FakeMifareCardGateway();
        var writer = new NfcCardConfigWriter(gateway);

        writer.Write(Reader, MifareCardType.Classic1K, Config);
        var result = new NfcCardConfigReader(gateway).Read(Reader, "04A1B2C3", MifareCardType.Classic1K);

        Assert.Equal(NfcCardScanStatus.Valid, result.Status);
        Assert.Equal("Portal", result.Config!.Title);
        Assert.Equal(400, result.Config.AppId);
    }

    [Fact]
    public void Write_ReportsProgressForEveryBlock()
    {
        var gateway = new FakeMifareCardGateway();
        var writer = new NfcCardConfigWriter(gateway);
        var reports = new List<(int Current, int Total)>();

        // Progress<T> marshals via the captured SynchronizationContext (async, not synchronous) —
        // a plain synchronous IProgress<T> avoids that indirection entirely for this assertion.
        writer.Write(Reader, MifareCardType.Classic1K, Config, new SynchronousProgress<(int, int)>(reports.Add));

        Assert.NotEmpty(reports);
        Assert.Equal(gateway.WriteCalls.Count, reports.Count);
        Assert.All(reports, r => Assert.Equal(reports[^1].Total, r.Total));
        Assert.Equal(Enumerable.Range(1, reports.Count), reports.Select(r => r.Current));
    }

    [Fact]
    public void WriteMagic_ReportsProgressForEveryBlock()
    {
        var gateway = new FakeMifareCardGateway { SupportsMagicWrite = true };
        var writer = new NfcCardConfigWriter(gateway);
        var reports = new List<(int Current, int Total)>();

        writer.WriteMagic(Reader, MifareCardType.Classic1K, Config, new SynchronousProgress<(int, int)>(reports.Add));

        Assert.NotEmpty(reports);
        Assert.Equal(gateway.MagicWriteCalls.Count, reports.Count);
    }

    private sealed class SynchronousProgress<T>(Action<T> onReport) : IProgress<T>
    {
        public void Report(T value) => onReport(value);
    }

    [Fact]
    public void Write_NeverTargetsTrailerOrManufacturerBlocks()
    {
        var gateway = new FakeMifareCardGateway();
        var writer = new NfcCardConfigWriter(gateway);

        writer.Write(Reader, MifareCardType.Classic1K, Config);

        Assert.DoesNotContain(gateway.WriteCalls, call => call.AbsoluteBlock == 0);
        Assert.DoesNotContain(gateway.WriteCalls, call => (call.AbsoluteBlock + 1) % 4 == 0);
    }

    [Fact]
    public void Write_AuthenticationFailsMidWrite_Throws()
    {
        var gateway = new FakeMifareCardGateway();
        gateway.FailAuthenticationForSector(0);
        var writer = new NfcCardConfigWriter(gateway);

        Assert.Throws<InvalidOperationException>(() => writer.Write(Reader, MifareCardType.Classic1K, Config));
    }

    [Fact]
    public void WriteMagic_CardSupportsBackdoor_WritesWithoutAuthenticating()
    {
        var gateway = new FakeMifareCardGateway { SupportsMagicWrite = true };
        gateway.FailAuthenticationForSector(0);
        var writer = new NfcCardConfigWriter(gateway);

        var success = writer.WriteMagic(Reader, MifareCardType.Classic1K, Config);

        Assert.True(success);
        Assert.Empty(gateway.AuthenticatedSectors);
        Assert.NotEmpty(gateway.MagicWriteCalls);
    }

    [Fact]
    public void WriteMagic_ThenRead_RoundTripsConfig()
    {
        var gateway = new FakeMifareCardGateway { SupportsMagicWrite = true };
        var writer = new NfcCardConfigWriter(gateway);

        writer.WriteMagic(Reader, MifareCardType.Classic1K, Config);
        var result = new NfcCardConfigReader(gateway).Read(Reader, "04A1B2C3", MifareCardType.Classic1K);

        Assert.Equal(NfcCardScanStatus.Valid, result.Status);
        Assert.Equal("Portal", result.Config!.Title);
    }

    [Fact]
    public void WriteMagic_CardDoesNotSupportBackdoor_ReturnsFalse()
    {
        var gateway = new FakeMifareCardGateway { SupportsMagicWrite = false };
        var writer = new NfcCardConfigWriter(gateway);

        var success = writer.WriteMagic(Reader, MifareCardType.Classic1K, Config);

        Assert.False(success);
    }

    [Fact]
    public void WriteMagic_NeverTargetsTrailerOrManufacturerBlocks()
    {
        var gateway = new FakeMifareCardGateway { SupportsMagicWrite = true };
        var writer = new NfcCardConfigWriter(gateway);

        writer.WriteMagic(Reader, MifareCardType.Classic1K, Config);

        Assert.DoesNotContain(gateway.MagicWriteCalls, call => call.AbsoluteBlock == 0);
        Assert.DoesNotContain(gateway.MagicWriteCalls, call => (call.AbsoluteBlock + 1) % 4 == 0);
    }
}
