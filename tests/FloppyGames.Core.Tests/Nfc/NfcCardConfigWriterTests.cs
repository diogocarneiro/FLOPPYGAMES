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
        var reports = new List<NfcCardWriteProgress>();

        // Progress<T> marshals via the captured SynchronizationContext (async, not synchronous) —
        // a plain synchronous IProgress<T> avoids that indirection entirely for this assertion.
        writer.Write(Reader, MifareCardType.Classic1K, Config, new SynchronousProgress<NfcCardWriteProgress>(reports.Add));

        Assert.NotEmpty(reports);
        Assert.Equal(gateway.WriteCalls.Count, reports.Count);
        Assert.All(reports, r => Assert.Equal(reports[^1].Total, r.Total));
        Assert.Equal(Enumerable.Range(1, reports.Count), reports.Select(r => r.Current));
        Assert.Equal(gateway.WriteCalls.Select(c => c.AbsoluteBlock), reports.Select(r => r.AbsoluteBlock));
        Assert.Equal(gateway.WriteCalls.Select(c => c.Data), reports.Select(r => r.Data));
    }

    [Fact]
    public void WriteMagic_ReportsProgressForEveryBlock()
    {
        var gateway = new FakeMifareCardGateway { SupportsMagicWrite = true };
        var writer = new NfcCardConfigWriter(gateway);
        var reports = new List<NfcCardWriteProgress>();

        writer.WriteMagic(Reader, MifareCardType.Classic1K, Config, new SynchronousProgress<NfcCardWriteProgress>(reports.Add));

        Assert.NotEmpty(reports);
        Assert.Equal(gateway.MagicWriteCalls.Count, reports.Count);
    }

    private sealed class SynchronousProgress<T>(Action<T> onReport) : IProgress<T>
    {
        public void Report(T value) => onReport(value);
    }

    [Fact]
    public void Write_AlwaysTouchesEveryUsableBlock_RegardlessOfContentSize()
    {
        var gateway = new FakeMifareCardGateway();
        var writer = new NfcCardConfigWriter(gateway);

        writer.Write(Reader, MifareCardType.Classic1K, Config);

        Assert.Equal(MifareCardLayout.UsableDataBlocks(MifareCardType.Classic1K).Count, gateway.WriteCalls.Count);
    }

    [Fact]
    public void Write_BlocksBeyondContent_AreZeroed()
    {
        var gateway = new FakeMifareCardGateway();
        var writer = new NfcCardConfigWriter(gateway);

        writer.Write(Reader, MifareCardType.Classic1K, Config);

        var contentBlockCount = MifareConfigCodec.Encode(GameIniWriter.Write(Config), MifareCardType.Classic1K).Length;
        var leftoverWrites = gateway.WriteCalls.Skip(contentBlockCount);
        Assert.NotEmpty(leftoverWrites);
        Assert.All(leftoverWrites, call => Assert.Equal(new byte[16], call.Data));
    }

    [Fact]
    public void Write_ShorterConfigAfterLongerOne_LeavesNoLeftoverBytesOnCard()
    {
        var gateway = new FakeMifareCardGateway();
        var writer = new NfcCardConfigWriter(gateway);
        var longConfig = Config with { Description = new string('X', 500) };
        writer.Write(Reader, MifareCardType.Classic1K, longConfig);

        writer.Write(Reader, MifareCardType.Classic1K, Config);

        var layout = MifareCardLayout.UsableDataBlocks(MifareCardType.Classic1K);
        var contentBlockCount = MifareConfigCodec.Encode(GameIniWriter.Write(Config), MifareCardType.Classic1K).Length;
        for (var i = contentBlockCount; i < layout.Count; i++)
        {
            Assert.Equal(new byte[16], gateway.ReadBlock(Reader, layout[i].AbsoluteBlock));
        }
    }

    [Fact]
    public void WriteMagic_AlwaysTouchesEveryUsableBlock_RegardlessOfContentSize()
    {
        var gateway = new FakeMifareCardGateway { SupportsMagicWrite = true };
        var writer = new NfcCardConfigWriter(gateway);

        writer.WriteMagic(Reader, MifareCardType.Classic1K, Config);

        Assert.Equal(MifareCardLayout.UsableDataBlocks(MifareCardType.Classic1K).Count, gateway.MagicWriteCalls.Count);
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

    [Fact]
    public void Check_SectorProtectedByPasswordKey_ExtraKeyProvided_ReturnsReady()
    {
        var gateway = new FakeMifareCardGateway();
        var passwordKey = NfcCardPasswordKey.Derive("hunter2");
        gateway.RequireKeyForSector(0, passwordKey);
        var writer = new NfcCardConfigWriter(gateway);

        var check = writer.Check(Reader, MifareCardType.Classic1K, Config, [passwordKey]);

        Assert.Equal(NfcCardWriteCheckStatus.Ready, check.Status);
    }

    [Fact]
    public void Write_SectorProtectedByPasswordKey_ExtraKeyProvided_Succeeds()
    {
        var gateway = new FakeMifareCardGateway();
        var passwordKey = NfcCardPasswordKey.Derive("hunter2");
        gateway.RequireKeyForSector(0, passwordKey);
        var writer = new NfcCardConfigWriter(gateway);

        writer.Write(Reader, MifareCardType.Classic1K, Config, extraKeys: [passwordKey]);

        Assert.NotEmpty(gateway.WriteCalls);
    }

    /// <summary>
    /// Regressão: a autenticação em lote (ver <see cref="IMifareCardGateway.AuthenticateSectors"/>)
    /// tenta cada chave candidata sobre TODOS os setores ainda por autenticar, não só o primeiro
    /// que falhar — vários setores com chaves diferentes (alguns de fábrica, outros protegidos por
    /// password) têm de acabar todos autenticados antes da escrita prosseguir.
    /// </summary>
    [Fact]
    public void Write_MultipleSectorsProtectedByPasswordKey_ExtraKeyProvided_Succeeds()
    {
        var gateway = new FakeMifareCardGateway();
        var passwordKey = NfcCardPasswordKey.Derive("hunter2");
        gateway.RequireKeyForSector(0, passwordKey);
        gateway.RequireKeyForSector(2, passwordKey);
        gateway.RequireKeyForSector(5, passwordKey);
        var writer = new NfcCardConfigWriter(gateway);

        writer.Write(Reader, MifareCardType.Classic1K, Config, extraKeys: [passwordKey]);

        var layout = MifareCardLayout.UsableDataBlocks(MifareCardType.Classic1K);
        Assert.Equal(layout.Count, gateway.WriteCalls.Count);
    }

    [Fact]
    public void Write_SectorAuthenticationFailsWithAllCandidateKeys_Throws()
    {
        var gateway = new FakeMifareCardGateway();
        gateway.FailAuthenticationForSector(3);
        var writer = new NfcCardConfigWriter(gateway);
        var passwordKey = NfcCardPasswordKey.Derive("hunter2");

        Assert.Throws<InvalidOperationException>(
            () => writer.Write(Reader, MifareCardType.Classic1K, Config, extraKeys: [passwordKey]));
    }

    [Fact]
    public void ProtectWithPassword_WritesTrailerForEverySector()
    {
        var gateway = new FakeMifareCardGateway();
        var writer = new NfcCardConfigWriter(gateway);
        writer.Write(Reader, MifareCardType.Classic1K, Config);
        gateway.WriteCalls.Clear();
        var allSectors = MifareCardLayout.UsableDataBlocks(MifareCardType.Classic1K).Select(b => b.Sector).Distinct().ToArray();

        var protectedCard = writer.ProtectWithPassword(Reader, MifareCardType.Classic1K, "hunter2");

        Assert.True(protectedCard);
        var expectedTrailerBlocks = allSectors.Select(MifareCardLayout.TrailerAbsoluteBlock).OrderBy(b => b);
        var actualTrailerBlocks = gateway.WriteCalls.Select(c => c.AbsoluteBlock).OrderBy(b => b);
        Assert.Equal(expectedTrailerBlocks, actualTrailerBlocks);
    }

    [Fact]
    public void ProtectWithPassword_TrailerContent_KeepsFactoryAccessBitsAndUserByte()
    {
        var gateway = new FakeMifareCardGateway();
        var writer = new NfcCardConfigWriter(gateway);
        writer.Write(Reader, MifareCardType.Classic1K, Config);

        writer.ProtectWithPassword(Reader, MifareCardType.Classic1K, "hunter2");

        var key = NfcCardPasswordKey.Derive("hunter2");
        var trailerBlock = MifareCardLayout.TrailerAbsoluteBlock(0);
        var trailerData = gateway.WriteCalls.Last(c => c.AbsoluteBlock == trailerBlock).Data;
        Assert.Equal(key, trailerData[..6]);
        Assert.Equal(new byte[] { 0xFF, 0x07, 0x80, 0x69 }, trailerData[6..10]);
        Assert.Equal(key, trailerData[10..]);
    }

    [Fact]
    public void ProtectWithPassword_AuthenticationFails_ReturnsFalse()
    {
        var gateway = new FakeMifareCardGateway();
        gateway.FailAuthenticationForSector(0);
        var writer = new NfcCardConfigWriter(gateway);

        var result = writer.ProtectWithPassword(Reader, MifareCardType.Classic1K, "hunter2");

        Assert.False(result);
    }

    [Fact]
    public void ProtectWithPassword_ReportsProgressForEverySectorTrailer()
    {
        var gateway = new FakeMifareCardGateway();
        var writer = new NfcCardConfigWriter(gateway);
        writer.Write(Reader, MifareCardType.Classic1K, Config);
        var reports = new List<NfcCardWriteProgress>();

        writer.ProtectWithPassword(Reader, MifareCardType.Classic1K, "hunter2", new SynchronousProgress<NfcCardWriteProgress>(reports.Add));

        Assert.NotEmpty(reports);
        Assert.All(reports, r => Assert.Equal(MifareCardLayout.TrailerAbsoluteBlock(r.Sector), r.AbsoluteBlock));
    }

    [Fact]
    public void ProtectWithPassword_ThenReadWithFactoryKeyOnly_Fails()
    {
        var gateway = new FakeMifareCardGateway();
        var writer = new NfcCardConfigWriter(gateway);
        writer.Write(Reader, MifareCardType.Classic1K, Config);
        var key = NfcCardPasswordKey.Derive("hunter2");
        writer.ProtectWithPassword(Reader, MifareCardType.Classic1K, "hunter2");

        // A escrita do trailer, por si só, não faz o Fake passar a exigir a nova chave (isso é
        // comportamento de hardware real) — este teste simula explicitamente esse efeito para
        // confirmar que o fluxo de leitura com a chave extra continua a funcionar depois.
        gateway.RequireKeyForSector(0, key);
        var readWithoutKey = new NfcCardConfigReader(gateway).Read(Reader, "04A1B2C3", MifareCardType.Classic1K);
        var readWithKey = new NfcCardConfigReader(gateway).Read(Reader, "04A1B2C3", MifareCardType.Classic1K, [key]);

        Assert.Equal(NfcCardScanStatus.AuthenticationFailed, readWithoutKey.Status);
        Assert.Equal(NfcCardScanStatus.Valid, readWithKey.Status);
    }

    [Fact]
    public void Format_CardWithGame_LeavesCardEmpty()
    {
        var gateway = new FakeMifareCardGateway();
        var writer = new NfcCardConfigWriter(gateway);
        writer.Write(Reader, MifareCardType.Classic1K, Config with { Description = new string('X', 300) });

        var formatted = writer.Format(Reader, MifareCardType.Classic1K);

        Assert.True(formatted);
        Assert.Equal(NfcCardScanStatus.Empty, new NfcCardConfigReader(gateway).Read(Reader, "04A1B2C3", MifareCardType.Classic1K).Status);
        Assert.All(
            MifareCardLayout.UsableDataBlocks(MifareCardType.Classic1K),
            b => Assert.Equal(new byte[16], gateway.ReadBlock(Reader, b.AbsoluteBlock)));
    }

    [Fact]
    public void Format_FactoryKeyedCard_NeverRewritesTrailers()
    {
        var gateway = new FakeMifareCardGateway();
        var writer = new NfcCardConfigWriter(gateway);

        writer.Format(Reader, MifareCardType.Classic1K);

        Assert.DoesNotContain(gateway.WriteCalls, call => (call.AbsoluteBlock + 1) % 4 == 0);
        Assert.DoesNotContain(gateway.WriteCalls, call => call.AbsoluteBlock == 0);
    }

    [Fact]
    public void Format_PasswordProtectedSectors_RestoresFactoryTrailerOnlyThere()
    {
        var gateway = new FakeMifareCardGateway();
        var passwordKey = NfcCardPasswordKey.Derive("hunter2");
        gateway.RequireKeyForSector(0, passwordKey);
        gateway.RequireKeyForSector(4, passwordKey);
        var writer = new NfcCardConfigWriter(gateway);

        var formatted = writer.Format(Reader, MifareCardType.Classic1K, [passwordKey]);

        Assert.True(formatted);
        var trailerWrites = gateway.WriteCalls.Where(c => (c.AbsoluteBlock + 1) % 4 == 0).ToList();
        Assert.Equal([MifareCardLayout.TrailerAbsoluteBlock(0), MifareCardLayout.TrailerAbsoluteBlock(4)], trailerWrites.Select(c => c.AbsoluteBlock));
        byte[] factoryTrailer = [0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x07, 0x80, 0x69, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF];
        Assert.All(trailerWrites, c => Assert.Equal(factoryTrailer, c.Data));
    }

    [Fact]
    public void Format_UnknownKeyOnSomeSector_ReturnsFalseWithoutWriting()
    {
        var gateway = new FakeMifareCardGateway();
        gateway.RequireKeyForSector(2, [1, 2, 3, 4, 5, 6]);
        var writer = new NfcCardConfigWriter(gateway);

        var formatted = writer.Format(Reader, MifareCardType.Classic1K, [NfcCardPasswordKey.Derive("hunter2")]);

        Assert.False(formatted);
        Assert.Empty(gateway.WriteCalls);
    }

    [Fact]
    public void FormatMagic_WritesZeroDataAndFactoryTrailersEverywhere_NeverBlock0()
    {
        var gateway = new FakeMifareCardGateway { SupportsMagicWrite = true };
        var writer = new NfcCardConfigWriter(gateway);

        var formatted = writer.FormatMagic(Reader, MifareCardType.Classic1K);

        Assert.True(formatted);
        Assert.DoesNotContain(gateway.MagicWriteCalls, c => c.AbsoluteBlock == 0);
        Assert.Equal(16, gateway.MagicWriteCalls.Count(c => (c.AbsoluteBlock + 1) % 4 == 0));
        Assert.Equal(MifareCardLayout.UsableDataBlocks(MifareCardType.Classic1K).Count + 16, gateway.MagicWriteCalls.Count);
    }
}
