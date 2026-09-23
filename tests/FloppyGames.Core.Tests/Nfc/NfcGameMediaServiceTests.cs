using System.Threading;
using FloppyGames.Core.Nfc;
using Serilog.Core;

namespace FloppyGames.Core.Tests.Nfc;

public class NfcGameMediaServiceTests
{
    private const string Reader = "ACME PC/SC Reader 0";
    private const string Uid = "04A1B2C3";

    private const string ValidIni = """
        [Game]
        TITLE=Portal
        APPID=400
        PROCESS=portal.exe

        """;

    private static (FakeNfcCardWatcher Watcher, FakeMifareCardGateway Gateway, NfcGameMediaService Service) CreateService()
    {
        var watcher = new FakeNfcCardWatcher();
        var gateway = new FakeMifareCardGateway();
        var reader = new NfcCardConfigReader(gateway);
        var service = new NfcGameMediaService(watcher, reader, Logger.None);
        return (watcher, gateway, service);
    }

    [Fact]
    public void Start_DelegatesToUnderlyingWatcher()
    {
        var (watcher, _, service) = CreateService();

        service.Start();

        Assert.Equal(1, watcher.StartCalls);
    }

    [Fact]
    public void CardArrived_WithValidContent_RaisesCardInserted()
    {
        var (watcher, gateway, service) = CreateService();
        gateway.SeedCardContent(ValidIni, MifareCardType.Classic1K);

        CardInsertedEventArgs? received = null;
        service.CardInserted += (_, e) => received = e;

        watcher.RaiseArrived(new NfcCardPresence(Reader, Uid, MifareCardType.Classic1K));

        Assert.NotNull(received);
        Assert.Equal(Uid, received!.Uid);
        Assert.Equal("Portal", received.Config.Title);
    }

    [Fact]
    public void CardArrived_RaisedTwiceForSameUid_RaisesCardInsertedOnlyOnce()
    {
        var (watcher, gateway, service) = CreateService();
        gateway.SeedCardContent(ValidIni, MifareCardType.Classic1K);

        var insertedCount = 0;
        service.CardInserted += (_, _) => insertedCount++;

        watcher.RaiseArrived(new NfcCardPresence(Reader, Uid, MifareCardType.Classic1K));
        watcher.RaiseArrived(new NfcCardPresence(Reader, Uid, MifareCardType.Classic1K));

        Assert.Equal(1, insertedCount);
    }

    [Fact]
    public void CardArrived_BlankCard_IsIgnoredNotReportedAsInvalid()
    {
        // Cartão nunca gravado (ou formatado no Label Studio): não tem jogo, mas também não tem
        // erros — o Agent ignora-o em silêncio, como uma pen sem GAME.INI, em vez de o anunciar
        // como um cartão inválido.
        var (watcher, _, service) = CreateService();

        var insertedRaised = false;
        var invalidRaised = false;
        service.CardInserted += (_, _) => insertedRaised = true;
        service.InvalidCardDetected += (_, _) => invalidRaised = true;

        watcher.RaiseArrived(new NfcCardPresence(Reader, Uid, MifareCardType.Classic1K));

        Assert.False(insertedRaised);
        Assert.False(invalidRaised);
    }

    [Fact]
    public void CardArrived_AuthenticationFails_RaisesInvalidCardDetectedNotCardInserted()
    {
        var (watcher, gateway, service) = CreateService();
        gateway.FailAuthenticationForSector(0);

        var insertedRaised = false;
        InvalidCardEventArgs? invalid = null;
        service.CardInserted += (_, _) => insertedRaised = true;
        service.InvalidCardDetected += (_, e) => invalid = e;

        watcher.RaiseArrived(new NfcCardPresence(Reader, Uid, MifareCardType.Classic1K));

        Assert.False(insertedRaised);
        Assert.NotNull(invalid);
    }

    [Fact]
    public void CardArrived_ThenRemoved_RaisesCardRemovedWithSameConfig()
    {
        var (watcher, gateway, service) = CreateService();
        gateway.SeedCardContent(ValidIni, MifareCardType.Classic1K);

        CardRemovedEventArgs? removed = null;
        service.CardRemoved += (_, e) => removed = e;

        watcher.RaiseArrived(new NfcCardPresence(Reader, Uid, MifareCardType.Classic1K));
        watcher.RaiseRemoved(new NfcCardPresence(Reader, Uid, MifareCardType.Classic1K));

        Assert.NotNull(removed);
        Assert.Equal(Uid, removed!.Uid);
        Assert.Equal(400, removed.Config.AppId);
    }

    [Fact]
    public void CardRemoved_ForUntrackedUid_DoesNotRaiseCardRemoved()
    {
        var (watcher, _, service) = CreateService();

        var removedRaised = false;
        service.CardRemoved += (_, _) => removedRaised = true;

        watcher.RaiseRemoved(new NfcCardPresence(Reader, Uid, MifareCardType.Classic1K));

        Assert.False(removedRaised);
    }

    [Fact]
    public async Task CardArrived_RaisedConcurrentlyForSameUid_RaisesCardInsertedExactlyOnce()
    {
        var (watcher, gateway, service) = CreateService();
        gateway.SeedCardContent(ValidIni, MifareCardType.Classic1K);

        var insertedCount = 0;
        service.CardInserted += (_, _) => Interlocked.Increment(ref insertedCount);

        var tasks = Enumerable.Range(0, 20)
            .Select(_ => Task.Run(() => watcher.RaiseArrived(new NfcCardPresence(Reader, Uid, MifareCardType.Classic1K))));
        await Task.WhenAll(tasks);

        Assert.Equal(1, insertedCount);
    }
}
