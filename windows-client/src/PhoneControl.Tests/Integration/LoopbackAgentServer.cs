using System.Net;
using System.Net.Sockets;
using PhoneControl.Application;
using PhoneControl.Infrastructure;
using PhoneControl.Protocol;

namespace PhoneControl.Tests.Integration;

public sealed class LoopbackAgentServer : IAsyncDisposable
{
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _acceptLoop;

    private LoopbackAgentServer(TcpListener listener, bool pairingRequired, TimeSpan pairingReplyDelay)
    {
        _listener = listener;
        PairingRequired = pairingRequired;
        PairingReplyDelay = pairingReplyDelay;
        var endpoint = (IPEndPoint)_listener.LocalEndpoint;
        Port = endpoint.Port;
        ExpectedPin = "123456";
        IssuedPairingId = Guid.NewGuid().ToString();
        IssuedToken = Convert.ToBase64String(PairingCrypto.CreateToken());
        _acceptLoop = AcceptLoopAsync(_cts.Token);
    }

    public int Port { get; }
    public bool PairingRequired { get; }
    public TimeSpan PairingReplyDelay { get; }
    public string ExpectedPin { get; }
    public string IssuedPairingId { get; }
    public string IssuedToken { get; }
    public int HelloCount { get; private set; }
    public int PongCount { get; private set; }
    public int PairingAcceptedCount { get; private set; }

    public static async Task<LoopbackAgentServer> StartAsync(
        bool pairingRequired = false,
        TimeSpan? pairingReplyDelay = null)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var server = new LoopbackAgentServer(listener, pairingRequired, pairingReplyDelay ?? TimeSpan.Zero);
        await Task.Yield();
        return server;
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _listener.Stop();
        try
        {
            await _acceptLoop.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Listener stopped.
        }
        catch (SocketException)
        {
            // Listener stopped.
        }

        _cts.Dispose();
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            _ = HandleClientAsync(client, cancellationToken).ContinueWith(
                t => _ = t.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using (client)
        await using (var stream = client.GetStream())
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                byte[] frame;
                try
                {
                    frame = await LengthPrefixedFrame.ReadAsync(stream, cancellationToken).ConfigureAwait(false);
                }
                catch (EndOfStreamException)
                {
                    return;
                }
                catch (IOException)
                {
                    return;
                }

                var envelope = MessageSerializer.Deserialize(frame);
                MessageEnvelope? reply = null;
                if (envelope.Type == MessageTypes.SessionHello)
                {
                    HelloCount++;
                    var hello = EnvelopeFactory.ReadPayload<SessionHelloPayload>(envelope);
                    var trusted = hello?.SessionToken == IssuedToken;
                    reply = EnvelopeFactory.Create(
                        MessageTypes.SessionHelloAck,
                        envelope.Timestamp,
                        new SessionHelloAckPayload
                        {
                            PairingRequired = PairingRequired && !trusted,
                            Protocol = hello?.ProtocolMax ?? 1,
                            Device = new SessionDevicePayload
                            {
                                Model = "Loopback Agent",
                                Manufacturer = "PhoneControl",
                                AndroidVersion = "14",
                                SdkInt = 34,
                                SerialHash = "loopback-hash"
                            },
                            Capabilities = new[] { "session.ping", "device.status" }
                        },
                        envelope.RequestId);
                }
                else if (envelope.Type is MessageTypes.SessionPing or MessageTypes.SessionHeartbeat)
                {
                    PongCount++;
                    reply = EnvelopeFactory.Create(
                        MessageTypes.SessionPong,
                        envelope.Timestamp,
                        EnvelopeFactory.ReadPayload<SessionPingPayload>(envelope),
                        envelope.RequestId);
                }
                else if (envelope.Type == MessageTypes.PairingSubmit)
                {
                    if (PairingReplyDelay > TimeSpan.Zero)
                    {
                        await Task.Delay(PairingReplyDelay, cancellationToken).ConfigureAwait(false);
                    }

                    var pin = EnvelopeFactory.ReadPayload<PairingSubmitPayload>(envelope)?.Pin;
                    if (pin == ExpectedPin)
                    {
                        PairingAcceptedCount++;
                        var token = Convert.FromBase64String(IssuedToken);
                        reply = EnvelopeFactory.Create(
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
                    }
                    else
                    {
                        reply = EnvelopeFactory.Create(
                            MessageTypes.PairingRejected,
                            envelope.Timestamp,
                            new PairingRejectedPayload { Reason = "pin_mismatch" },
                            envelope.RequestId);
                    }
                }
                else if (envelope.Type == MessageTypes.DeviceStatus)
                {
                    reply = EnvelopeFactory.Create(
                        MessageTypes.DeviceStatus,
                        envelope.Timestamp,
                        new DeviceStatusPayload
                        {
                            BatteryPercent = 64,
                            Charging = true,
                            Network = "usb_tether"
                        },
                        envelope.RequestId);
                }

                if (reply is not null)
                {
                    await LengthPrefixedFrame.WriteAsync(stream, MessageSerializer.Serialize(reply), cancellationToken)
                        .ConfigureAwait(false);
                }
            }
        }
    }
}
