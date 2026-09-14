using System.Threading.Channels;
using PhoneControl.Application;
using PhoneControl.Domain;
using PhoneControl.Protocol;

namespace PhoneControl.Tests.Fakes;

public sealed class AutoReplyingTransport : IControlTransport
{
    private readonly Channel<byte[]> _incoming = Channel.CreateUnbounded<byte[]>();

    public AutoReplyingTransport(
        bool pairingRequired = true,
        bool replyToHello = true,
        bool autoPong = true,
        string? expectedPin = "123456",
        string? issuedToken = null,
        string? issuedPairingId = null)
    {
        PairingRequired = pairingRequired;
        ReplyToHello = replyToHello;
        AutoPong = autoPong;
        ExpectedPin = expectedPin;
        IssuedPairingId = issuedPairingId ?? Guid.NewGuid().ToString();
        IssuedToken = issuedToken ?? Convert.ToBase64String(PairingCrypto.CreateToken());
    }

    public bool PairingRequired { get; }
    public bool ReplyToHello { get; }
    public bool AutoPong { get; }
    public string? ExpectedPin { get; }
    public string IssuedPairingId { get; }
    public string IssuedToken { get; }
    public bool IsConnected { get; private set; }
    public List<MessageEnvelope> Sent { get; } = new();

    public Task ConnectAsync(string host, int port, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = host;
        _ = port;
        IsConnected = true;
        return Task.CompletedTask;
    }

    public async Task SendAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken)
    {
        var envelope = MessageSerializer.Deserialize(frame.Span);
        Sent.Add(envelope);
        if (ReplyToHello && envelope.Type == MessageTypes.SessionHello)
        {
            var hello = EnvelopeFactory.ReadPayload<SessionHelloPayload>(envelope);
            var trusted = hello?.SessionToken == IssuedToken;
            var ack = EnvelopeFactory.Create(
                MessageTypes.SessionHelloAck,
                envelope.Timestamp,
                new SessionHelloAckPayload
                {
                    PairingRequired = PairingRequired && !trusted,
                    Device = new SessionDevicePayload
                    {
                        Model = "Loopback Phone",
                        Manufacturer = "Test",
                        AndroidVersion = "14",
                        SdkInt = 34,
                        SerialHash = "loopback"
                    }
                },
                envelope.RequestId);
            await EnqueueAsync(ack, cancellationToken).ConfigureAwait(false);
        }

        if (envelope.Type == MessageTypes.PairingSubmit)
        {
            var pin = EnvelopeFactory.ReadPayload<PairingSubmitPayload>(envelope)?.Pin;
            if (ExpectedPin is not null && pin == ExpectedPin)
            {
                var token = Convert.FromBase64String(IssuedToken);
                var accepted = EnvelopeFactory.Create(
                    MessageTypes.PairingAccepted,
                    envelope.Timestamp,
                    new PairingAcceptedPayload
                    {
                        PairingId = IssuedPairingId,
                        SessionToken = IssuedToken,
                        ExpiresAt = DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeMilliseconds(),
                        Sas = PairingCrypto.ComputeSas(token)
                    },
                    envelope.RequestId);
                await EnqueueAsync(accepted, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                var rejected = EnvelopeFactory.Create(
                    MessageTypes.PairingRejected,
                    envelope.Timestamp,
                    new PairingRejectedPayload { Reason = "pin_mismatch" },
                    envelope.RequestId);
                await EnqueueAsync(rejected, cancellationToken).ConfigureAwait(false);
            }
        }

        if (envelope.Type == MessageTypes.DeviceStatus)
        {
            var status = EnvelopeFactory.Create(
                MessageTypes.DeviceStatus,
                envelope.Timestamp,
                new DeviceStatusPayload
                {
                    BatteryPercent = 80,
                    Charging = true,
                    Network = "usb_tether",
                    DeviceTimeUtc = envelope.Timestamp
                },
                envelope.RequestId);
            await EnqueueAsync(status, cancellationToken).ConfigureAwait(false);
        }

        if (AutoPong && envelope.Type == MessageTypes.SessionPing)
        {
            var pong = EnvelopeFactory.Create(
                MessageTypes.SessionPong,
                envelope.Timestamp,
                EnvelopeFactory.ReadPayload<SessionPingPayload>(envelope),
                envelope.RequestId);
            await EnqueueAsync(pong, cancellationToken).ConfigureAwait(false);
        }
    }

    public async IAsyncEnumerable<ReadOnlyMemory<byte>> ReadFramesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var frame in _incoming.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return frame;
        }
    }

    public ValueTask DisposeAsync()
    {
        _incoming.Writer.TryComplete();
        IsConnected = false;
        return ValueTask.CompletedTask;
    }

    private ValueTask EnqueueAsync(MessageEnvelope envelope, CancellationToken cancellationToken) =>
        _incoming.Writer.WriteAsync(MessageSerializer.Serialize(envelope), cancellationToken);
}

public sealed class AutoReplyingTransportFactory : IControlTransportFactory
{
    private readonly Func<IControlTransport> _create;

    public AutoReplyingTransportFactory(Func<IControlTransport>? create = null)
    {
        _create = create ?? (() => new AutoReplyingTransport());
    }

    public IControlTransport Create() => _create();
}

public sealed class HangingTransport : IControlTransport
{
    public bool IsConnected => false;

    public Task ConnectAsync(string host, int port, CancellationToken cancellationToken) =>
        Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);

    public Task SendAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public async IAsyncEnumerable<ReadOnlyMemory<byte>> ReadFramesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
        yield break;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
