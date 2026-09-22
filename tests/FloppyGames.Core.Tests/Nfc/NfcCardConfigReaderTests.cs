using FloppyGames.Core.Nfc;

namespace FloppyGames.Core.Tests.Nfc;

public class NfcCardConfigReaderTests
{
    private const string Reader = "ACME PC/SC Reader 0";
    private const string Uid = "04A1B2C3";

    private const string ValidIni = """
        [Game]
        TITLE=Portal
        APPID=400
        PROCESS=portal.exe

        """;

    [Fact]
    public void Read_ValidCardContent_ReturnsValidWithParsedConfig()
    {
        var gateway = new FakeMifareCardGateway();
        gateway.SeedCardContent(ValidIni, MifareCardType.Classic1K);
        var reader = new NfcCardConfigReader(gateway);

        var result = reader.Read(Reader, Uid, MifareCardType.Classic1K);

        Assert.Equal(NfcCardScanStatus.Valid, result.Status);
        Assert.Equal("Portal", result.Config!.Title);
        Assert.Equal(400, result.Config.AppId);
    }

    [Fact]
    public void Read_UnknownCardType_ReturnsUnsupportedCardType()
    {
        var reader = new NfcCardConfigReader(new FakeMifareCardGateway());

        var result = reader.Read(Reader, Uid, MifareCardType.Unknown);

        Assert.Equal(NfcCardScanStatus.UnsupportedCardType, result.Status);
    }

    [Fact]
    public void Read_AuthenticationFailsOnFirstSector_ReturnsAuthenticationFailed()
    {
        var gateway = new FakeMifareCardGateway();
        gateway.FailAuthenticationForSector(0);
        var reader = new NfcCardConfigReader(gateway);

        var result = reader.Read(Reader, Uid, MifareCardType.Classic1K);

        Assert.Equal(NfcCardScanStatus.AuthenticationFailed, result.Status);
    }

    [Fact]
    public void Read_BlankCard_ReturnsInvalidGameIni()
    {
        // Cartão nunca gravado: todos os blocos lêem como zero — comprimento declarado 0, que
        // descodifica para texto vazio. Isso não é "corrupto" o suficiente para ser distinguido
        // à parte — cai naturalmente no GameIniParser existente, que rejeita por faltarem TITLE/PROCESS.
        var reader = new NfcCardConfigReader(new FakeMifareCardGateway());

        var result = reader.Read(Reader, Uid, MifareCardType.Classic1K);

        Assert.Equal(NfcCardScanStatus.InvalidGameIni, result.Status);
    }

    [Fact]
    public void Read_DeclaredLengthExceedsCardCapacity_ReturnsCorruptOrEmptyData()
    {
        var gateway = new FakeMifareCardGateway();
        // Comprimento declarado (65000) muito maior do que o cartão pode conter.
        gateway.SetBlock(1, [0xFD, 0xE8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]);
        var reader = new NfcCardConfigReader(gateway);

        var result = reader.Read(Reader, Uid, MifareCardType.Classic1K);

        Assert.Equal(NfcCardScanStatus.CorruptOrEmptyData, result.Status);
    }

    [Fact]
    public void Read_InvalidGameIniOnCard_ReturnsInvalidGameIni()
    {
        var gateway = new FakeMifareCardGateway();
        gateway.SeedCardContent("[Game]\nTITLE=Sem AppID\n", MifareCardType.Classic1K);
        var reader = new NfcCardConfigReader(gateway);

        var result = reader.Read(Reader, Uid, MifareCardType.Classic1K);

        Assert.Equal(NfcCardScanStatus.InvalidGameIni, result.Status);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void Read_GatewayThrowsWhileReading_ReturnsReaderCommunicationFailure()
    {
        var gateway = new FakeMifareCardGateway { ThrowOnRead = true };
        var reader = new NfcCardConfigReader(gateway);

        var result = reader.Read(Reader, Uid, MifareCardType.Classic1K);

        Assert.Equal(NfcCardScanStatus.ReaderCommunicationFailure, result.Status);
    }

    [Fact]
    public void Read_MultiBlockContent_AuthenticatesEachSectorOnlyOnce()
    {
        var gateway = new FakeMifareCardGateway();
        gateway.SeedCardContent(new string('A', 100), MifareCardType.Classic1K);
        var reader = new NfcCardConfigReader(gateway);

        // 102 bytes (100 + prefixo) cabem em 7 blocos: setor 0 (2 blocos) + setor 1 (3 blocos) + setor 2 (2 de 3 blocos).
        // O conteúdo em si é lixo para o GameIniParser, mas o que interessa aqui é a contagem de autenticações.
        reader.Read(Reader, Uid, MifareCardType.Classic1K);

        Assert.Equal(gateway.AuthenticatedSectors.Count, gateway.AuthenticatedSectors.Distinct().Count());
    }
}
