using FluentAssertions;
using PhoneControl.Application;
using PhoneControl.Domain;
using PhoneControl.Infrastructure;
using PhoneControl.Protocol;
using PhoneControl.Tests.Fakes;
using PhoneControl.Tests.Unit;

namespace PhoneControl.Tests.Unit;

public sealed class PairingAndStatusTests
{
    [Fact]
    public void Sas_IsSixDigits_AndStable()
    {
        var token = Enumerable.Repeat((byte)7, 32).ToArray();
        var sas = PairingCrypto.ComputeSas(token);
        sas.Should().HaveLength(6);
        sas.Should().MatchRegex("^[0-9]{6}$");
        PairingCrypto.ComputeSas(token).Should().Be(sas);
    }

    [Fact]
    public void PinShape_RejectsShortCodes()
    {
        PairingCrypto.IsPinShape("123456").Should().BeTrue();
        PairingCrypto.IsPinShape("12345").Should().BeFalse();
        PairingCrypto.IsPinShape("abcdef").Should().BeFalse();
    }

    [Fact]
    public async Task TrustedStore_DropsExpiredToken()
    {
        var inner = new InMemorySecureStore();
        var store = new TrustedDeviceStore(inner);
        var clock = new FixedClock { UtcNow = DateTimeOffset.UnixEpoch.AddDays(2) };
        await store.SaveAsync(
            new TrustedDevice("id", PairingCrypto.CreateToken(), DateTimeOffset.UnixEpoch.AddDays(1)),
            CancellationToken.None);
        (await store.LoadAsync(clock, CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task ProtectedStore_WithPassthrough_RoundTrips()
    {
        var store = new ProtectedSecureStore(new InMemorySecureStore(), new PassthroughDataProtector());
        await store.SaveAsync("k", new byte[] { 1, 2, 9 }, CancellationToken.None);
        (await store.LoadAsync("k", CancellationToken.None)).Should().Equal(1, 2, 9);
    }

    [Fact]
    public void RedactingLogger_RefusesSecretShapedTemplates()
    {
        var logger = new RedactingAppLogger(new NullAppLogger());
        var act = () => logger.Info("pairing-code displayed");
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task SubmitPin_WrongCode_StaysPairingRequired()
    {
        await using var manager = ConnectionHarness.Create();
        await manager.ConnectAsync(CancellationToken.None);
        manager.Snapshot.State.Should().Be(ConnectionState.PairingRequired);
        await manager.SubmitPinAsync("000000", CancellationToken.None);
        manager.Snapshot.State.Should().Be(ConnectionState.PairingRequired);
        manager.Snapshot.ErrorCode.Should().Be("PIN_MISMATCH");
    }

    [Fact]
    public async Task SubmitPin_Match_StoresTrustedDeviceAndStatus()
    {
        var store = new InMemorySecureStore();
        await using var manager = ConnectionHarness.Create(secureStore: store);
        await manager.ConnectAsync(CancellationToken.None);
        await manager.SubmitPinAsync("123456", CancellationToken.None);
        manager.Snapshot.State.Should().Be(ConnectionState.Connected);
        manager.Snapshot.TrustedDevice.Should().BeTrue();
        manager.Snapshot.Sas.Should().MatchRegex("^[0-9]{6}$");
        manager.Snapshot.Status!.BatteryPercent.Should().Be(80);
        manager.Snapshot.Status.Charging.Should().BeTrue();
        (await store.LoadAsync(TrustedDeviceStore.StoreKey, CancellationToken.None)).Should().NotBeNull();
    }

    [Fact]
    public async Task HelloWithStoredToken_SkipsPin()
    {
        var token = PairingCrypto.CreateToken();
        var id = Guid.NewGuid().ToString();
        var store = new InMemorySecureStore();
        await new TrustedDeviceStore(store).SaveAsync(
            new TrustedDevice(id, token, DateTimeOffset.UtcNow.AddDays(7)),
            CancellationToken.None);
        var b64 = Convert.ToBase64String(token);
        await using var manager = ConnectionHarness.Create(
            transport: new AutoReplyingTransportFactory(() => new AutoReplyingTransport(
                pairingRequired: true,
                issuedToken: b64,
                issuedPairingId: id)),
            secureStore: store);
        await manager.ConnectAsync(CancellationToken.None);
        manager.Snapshot.State.Should().Be(ConnectionState.Connected);
        manager.Snapshot.TrustedDevice.Should().BeTrue();
    }

    [Fact]
    public void CommandCatalog_KnowsPairingConfirmAndStateUpdate()
    {
        CommandCatalog.CanExecute(MessageTypes.PairingConfirm).Should().BeTrue();
        CommandCatalog.CanExecute(MessageTypes.StateUpdate).Should().BeTrue();
    }
}

public sealed class FixedClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow;
}
