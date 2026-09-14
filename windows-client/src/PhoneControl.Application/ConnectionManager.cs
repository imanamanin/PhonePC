using System.Collections.Concurrent;
using System.Net.Sockets;
using PhoneControl.Domain;
using PhoneControl.Protocol;

namespace PhoneControl.Application;

public sealed class ConnectionManager : IAsyncDisposable
{
    private readonly ConnectionDependencies _deps;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ConcurrentDictionary<string, TaskCompletionSource<MessageEnvelope>> _pending = new();
    private readonly ReconnectPolicy _reconnect;
    private readonly TrustedDeviceStore _trustedStore;
    private ConnectionSnapshot _snapshot = ConnectionSnapshot.Idle();
    private CancellationTokenSource? _sessionCts;
    private IControlTransport? _transport;
    private Task? _sessionTask;
    private Task? _readLoopTask;
    private int _reconnectAttempts;
    private bool _userDisconnect;

    public ConnectionManager(ConnectionDependencies deps)
    {
        _deps = deps;
        _reconnect = new ReconnectPolicy(deps.Options.ReconnectInitialDelayMs, deps.Options.ReconnectMaxDelayMs);
        _trustedStore = new TrustedDeviceStore(deps.SecureStore);
    }

    public ConnectionSnapshot Snapshot => _snapshot;

    public event EventHandler<ConnectionSnapshot>? StateChanged;

    public event EventHandler<MessageEnvelope>? MessageReceived;

    public bool CanSend => _transport?.IsConnected == true;

    public async Task SubmitPinAsync(string pin, CancellationToken cancellationToken)
    {
        if (!PairingCrypto.IsPinShape(pin))
        {
            Apply(_snapshot with { ErrorCode = "PIN_INVALID" });
            return;
        }

        var transport = _transport ?? throw new InvalidOperationException("Not connected.");
        var request = EnvelopeFactory.Create(
            MessageTypes.PairingSubmit,
            _deps.Clock.UtcNow.ToUnixTimeMilliseconds(),
            new PairingSubmitPayload { Pin = pin });
        var reply = await SendAndWaitAsync(transport, request, _deps.Options.HandshakeTimeout, cancellationToken)
            .ConfigureAwait(false);

        if (string.Equals(reply.Type, MessageTypes.PairingAccepted, StringComparison.Ordinal) && reply.Error is null)
        {
            await AcceptPairingAsync(reply, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (string.Equals(reply.Type, MessageTypes.PairingExpired, StringComparison.Ordinal))
        {
            Apply(_snapshot with { ErrorCode = "PIN_EXPIRED", State = ConnectionState.PairingRequired });
            return;
        }

        var reason = EnvelopeFactory.ReadPayload<PairingRejectedPayload>(reply)?.Reason ?? "pin_mismatch";
        Apply(_snapshot with
        {
            ErrorCode = reason == "locked" ? "PIN_LOCKED" : "PIN_MISMATCH",
            State = ConnectionState.PairingRequired
        });
    }

    public async Task UnpairAsync(CancellationToken cancellationToken)
    {
        await _trustedStore.ClearAsync(cancellationToken).ConfigureAwait(false);
        if (CanSend)
        {
            try
            {
                await SendAsync(
                    EnvelopeFactory.Create(MessageTypes.PairingCleared, _deps.Clock.UtcNow.ToUnixTimeMilliseconds()),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Best-effort unpair on the wire.
            }
        }

        Apply(_snapshot with { TrustedDevice = false, Sas = null });
    }

    public async Task RefreshDiscoveryAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var probe = await ProbeAsync(cancellationToken).ConfigureAwait(false);
            var state = _snapshot.State is ConnectionState.Connected or ConnectionState.PairingRequired
                ? _snapshot.State
                : ConnectionState.Disconnected;
            Apply(new ConnectionSnapshot(
                state,
                probe.Device,
                probe.Usb,
                probe.Adb,
                probe.Tether,
                probe.Endpoints.FirstOrDefault(),
                _snapshot.ErrorCode,
                probe.Endpoints,
                _snapshot.Status,
                _snapshot.Sas,
                _snapshot.TrustedDevice));
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        if (_sessionTask is { IsCompleted: false })
        {
            await WaitForStableAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        _userDisconnect = false;
        _reconnect.Reset();
        _reconnectAttempts = 0;
        _sessionTask = RunSessionAsync(cancellationToken);
        try
        {
            await WaitForStableAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _userDisconnect = true;
            CancelSession();
            MoveToIfAllowed(ConnectionState.Disconnected, null);
        }

        if (_sessionTask.IsFaulted)
        {
            await _sessionTask.ConfigureAwait(false);
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _userDisconnect = true;
        CancelSession();
        if (_sessionTask is not null)
        {
            try
            {
                await _sessionTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected when the user disconnects.
            }
        }

        await DisposeTransportAsync().ConfigureAwait(false);
        MoveTo(ConnectionState.Disconnected, null);
    }

    public async Task SendAsync(MessageEnvelope message, CancellationToken cancellationToken)
    {
        var transport = _transport ?? throw new InvalidOperationException("Not connected.");
        await transport.SendAsync(MessageSerializer.Serialize(message), cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync(CancellationToken.None).ConfigureAwait(false);
        _gate.Dispose();
        _sessionCts?.Dispose();
    }

    private async Task RunSessionAsync(CancellationToken external)
    {
        while (!_userDisconnect && !external.IsCancellationRequested)
        {
            using var sessionCts = CancellationTokenSource.CreateLinkedTokenSource(external);
            _sessionCts = sessionCts;
            var ct = sessionCts.Token;
            try
            {
                await EnterConnectingAsync(ct).ConfigureAwait(false);
                if (_snapshot.DiscoveredEndpoints.Count == 0)
                {
                    MoveTo(ConnectionState.Error, "NO_ENDPOINT");
                    return;
                }

                await ConnectFirstEndpointAsync(_snapshot.DiscoveredEndpoints, ct).ConfigureAwait(false);
                _reconnect.Reset();
                _reconnectAttempts = 0;
                await HeartbeatLoopAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_userDisconnect || external.IsCancellationRequested)
            {
                MoveTo(ConnectionState.Disconnected, null);
                return;
            }
            catch (Exception ex) when (ex is TimeoutException or IOException or SocketException or InvalidOperationException)
            {
                await DisposeTransportAsync().ConfigureAwait(false);
                _reconnectAttempts++;
                if (_userDisconnect)
                {
                    MoveTo(ConnectionState.Disconnected, null);
                    return;
                }

                if (_reconnectAttempts > _deps.Options.MaxReconnectAttempts)
                {
                    MoveTo(ConnectionState.Error, ErrorCode(ex));
                    return;
                }

                MoveTo(ConnectionState.Reconnecting, ErrorCode(ex));
                try
                {
                    await _deps.Delay.Delay(_reconnect.NextDelay(), ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (_userDisconnect || external.IsCancellationRequested)
                {
                    MoveTo(ConnectionState.Disconnected, null);
                    return;
                }
            }
        }
    }

    private async Task ConnectFirstEndpointAsync(IReadOnlyList<string> endpoints, CancellationToken cancellationToken)
    {
        Exception? lastError = null;
        foreach (var endpoint in endpoints)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await HandshakeAsync(endpoint, cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is TimeoutException or IOException or SocketException or InvalidOperationException)
            {
                lastError = ex;
                _deps.Logger.Warn("Endpoint {Endpoint} failed: {Code}", endpoint, ex.GetType().Name);
                await DisposeTransportAsync().ConfigureAwait(false);
            }
        }

        throw lastError ?? new InvalidOperationException("NO_ENDPOINT");
    }

    private async Task EnterConnectingAsync(CancellationToken cancellationToken)
    {
        var probe = await ProbeAsync(cancellationToken).ConfigureAwait(false);
        var from = _snapshot.State;
        if (!ConnectionStateMachine.CanTransition(from, ConnectionState.Connecting))
        {
            from = ConnectionState.Disconnected;
            Apply(_snapshot with { State = ConnectionState.Disconnected });
        }

        ConnectionStateMachine.Ensure(from, ConnectionState.Connecting);
        Apply(new ConnectionSnapshot(
            ConnectionState.Connecting,
            probe.Device,
            probe.Usb,
            probe.Adb,
            probe.Tether,
            probe.Endpoints.FirstOrDefault(),
            null,
            probe.Endpoints,
            _snapshot.Status,
            _snapshot.Sas,
            _snapshot.TrustedDevice));
    }

    private async Task HandshakeAsync(string endpoint, CancellationToken cancellationToken)
    {
        var (host, port) = SplitEndpoint(endpoint);
        Apply(_snapshot with { Endpoint = endpoint });

        var transport = _deps.TransportFactory.Create();
        _transport = transport;
        using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        connectCts.CancelAfter(_deps.Options.HandshakeTimeout);
        try
        {
            await transport.ConnectAsync(host, port, connectCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("TIMEOUT");
        }

        _pending.Clear();
        _readLoopTask = ReadLoopAsync(transport, cancellationToken);
        _ = Observe(_readLoopTask);

        var trusted = await _trustedStore.LoadAsync(_deps.Clock, cancellationToken).ConfigureAwait(false);
        var hello = EnvelopeFactory.Create(
            MessageTypes.SessionHello,
            _deps.Clock.UtcNow.ToUnixTimeMilliseconds(),
            new SessionHelloPayload
            {
                ClientName = _deps.Options.ClientName,
                ClientVersion = _deps.Options.ClientVersion,
                PairingId = trusted?.PairingId,
                SessionToken = trusted is null ? null : Convert.ToBase64String(trusted.SessionToken)
            });
        _deps.Logger.Info("Handshake hello sent to {Endpoint}", endpoint);
        var ack = await SendAndWaitAsync(transport, hello, _deps.Options.HandshakeTimeout, cancellationToken)
            .ConfigureAwait(false);
        if (!string.Equals(ack.Type, MessageTypes.SessionHelloAck, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("HANDSHAKE_FAILED");
        }

        var payload = EnvelopeFactory.ReadPayload<SessionHelloAckPayload>(ack);
        var device = payload?.Device is null
            ? _snapshot.Device
            : new DeviceIdentity(
                payload.Device.Model,
                payload.Device.Manufacturer,
                payload.Device.AndroidVersion,
                payload.Device.SdkInt,
                payload.Device.SerialHash);

        if (payload?.PairingRequired == false)
        {
            MoveTo(ConnectionState.Connected, null, device);
            Apply(_snapshot with { TrustedDevice = trusted is not null, Device = device });
            await RequestDeviceStatusAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        MoveTo(ConnectionState.PairingRequired, null, device);
    }

    private async Task HeartbeatLoopAsync(CancellationToken cancellationToken)
    {
        var transport = _transport ?? throw new InvalidOperationException("Transport missing.");
        while (!cancellationToken.IsCancellationRequested)
        {
            await _deps.Delay.Delay(_deps.Options.HeartbeatInterval, cancellationToken).ConfigureAwait(false);
            var ping = EnvelopeFactory.Create(
                MessageTypes.SessionPing,
                _deps.Clock.UtcNow.ToUnixTimeMilliseconds(),
                new SessionPingPayload { T = _deps.Clock.UtcNow.ToUnixTimeMilliseconds() });
            var pong = await SendAndWaitAsync(transport, ping, _deps.Options.HeartbeatTimeout, cancellationToken)
                .ConfigureAwait(false);
            if (!string.Equals(pong.Type, MessageTypes.SessionPong, StringComparison.Ordinal))
            {
                throw new IOException("Heartbeat mismatch.");
            }
        }
    }

    private async Task ReadLoopAsync(IControlTransport transport, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var frame in transport.ReadFramesAsync(cancellationToken).ConfigureAwait(false))
            {
                MessageEnvelope envelope;
                try
                {
                    envelope = MessageSerializer.Deserialize(frame.Span);
                }
                catch (ProtocolValidationException ex)
                {
                    _deps.Logger.Warn("Dropped invalid frame: {Reason}", ex.Message);
                    continue;
                }

                if (_pending.TryRemove(envelope.RequestId, out var waiter))
                {
                    waiter.TrySetResult(envelope);
                }
                else
                {
                    ApplyIncoming(envelope);
                    MessageReceived?.Invoke(this, envelope);
                }
            }

            FailPending(new IOException("Control channel closed."));
        }
        catch (OperationCanceledException)
        {
            FailPending(new OperationCanceledException(cancellationToken));
        }
        catch (Exception ex)
        {
            FailPending(ex);
        }
    }

    private async Task<MessageEnvelope> SendAndWaitAsync(
        IControlTransport transport,
        MessageEnvelope request,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<MessageEnvelope>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[request.RequestId] = tcs;
        await transport.SendAsync(MessageSerializer.Serialize(request), cancellationToken).ConfigureAwait(false);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);
        using (timeoutCts.Token.Register(() => tcs.TrySetCanceled(timeoutCts.Token)))
        {
            try
            {
                return await tcs.Task.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _pending.TryRemove(request.RequestId, out _);
                throw new TimeoutException("TIMEOUT");
            }
        }
    }

    private async Task WaitForStableAsync(CancellationToken cancellationToken)
    {
        if (IsStable(_snapshot.State))
        {
            return;
        }

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Handler(object? _, ConnectionSnapshot snap)
        {
            if (IsStable(snap.State))
            {
                tcs.TrySetResult();
            }
        }

        StateChanged += Handler;
        try
        {
            if (IsStable(_snapshot.State))
            {
                return;
            }

            using var linked = cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
            await tcs.Task.ConfigureAwait(false);
        }
        finally
        {
            StateChanged -= Handler;
        }
    }

    private async Task<ProbeResult> ProbeAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<DiscoveredDevice> devices;
        try
        {
            devices = await _deps.Discovery.DiscoverAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _deps.Logger.Warn("Device discovery failed: {Type}", ex.GetType().Name);
            devices = Array.Empty<DiscoveredDevice>();
        }

        IReadOnlyList<TetherCandidate> network;
        try
        {
            network = await _deps.Network.DiscoverAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _deps.Logger.Warn("Network discovery failed: {Type}", ex.GetType().Name);
            network = TetherEndpointSelector.FromAdapters(Array.Empty<NetworkAdapterSnapshot>());
        }

        var endpoints = network
            .Select(c => $"{c.Host}:{c.Port}")
            .Concat(devices.Select(d => d.TetherEndpoint)
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Select(e => e!.Contains(':', StringComparison.Ordinal) ? e! : $"{e}:{ProtocolPorts.Control}"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var first = devices.FirstOrDefault();
        var device = first is null
            ? null
            : new DeviceIdentity(first.DisplayName, "unknown", "unknown", 0, first.SerialHash ?? "none");
        return new ProbeResult(
            device,
            endpoints,
            network.Any(c => !c.IsFallback) || devices.Any(d => d.TetheringLikely),
            devices.Any(d => d.AdbAvailable),
            devices.Any(d => d.UsbPresent) || network.Any(c => !c.IsFallback));
    }

    private void MoveToIfAllowed(ConnectionState state, string? errorCode)
    {
        if (_snapshot.State == state || ConnectionStateMachine.CanTransition(_snapshot.State, state))
        {
            MoveTo(state, errorCode);
        }
    }

    private void MoveTo(ConnectionState state, string? errorCode, DeviceIdentity? device = null)
    {
        var from = _snapshot.State;
        if (from == state)
        {
            Apply(_snapshot with { ErrorCode = errorCode, Device = device ?? _snapshot.Device });
            return;
        }

        ConnectionStateMachine.Ensure(from, state);
        Apply(_snapshot with
        {
            State = state,
            ErrorCode = errorCode,
            Device = device ?? _snapshot.Device
        });
    }

    private async Task AcceptPairingAsync(MessageEnvelope reply, CancellationToken cancellationToken)
    {
        var accepted = EnvelopeFactory.ReadPayload<PairingAcceptedPayload>(reply);
        if (accepted is null || string.IsNullOrWhiteSpace(accepted.SessionToken))
        {
            Apply(_snapshot with { ErrorCode = "PAIRING_FAILED" });
            return;
        }

        var token = Convert.FromBase64String(accepted.SessionToken);
        var sas = PairingCrypto.ComputeSas(token);
        if (!string.IsNullOrWhiteSpace(accepted.Sas) && !PairingCrypto.ConstantTimeEquals(sas, accepted.Sas))
        {
            Apply(_snapshot with { ErrorCode = "SAS_MISMATCH" });
            return;
        }

        var expires = accepted.ExpiresAt > 0
            ? DateTimeOffset.FromUnixTimeMilliseconds(accepted.ExpiresAt)
            : _deps.Clock.UtcNow.AddDays(30);
        await _trustedStore.SaveAsync(
            new TrustedDevice(accepted.PairingId, token, expires),
            cancellationToken).ConfigureAwait(false);

        MoveTo(ConnectionState.Connected, null);
        Apply(_snapshot with { Sas = sas, TrustedDevice = true, ErrorCode = null });
        await RequestDeviceStatusAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task RequestDeviceStatusAsync(CancellationToken cancellationToken)
    {
        if (!CanSend)
        {
            return;
        }

        try
        {
            var reply = await SendAndWaitAsync(
                _transport!,
                EnvelopeFactory.Create(MessageTypes.DeviceStatus, _deps.Clock.UtcNow.ToUnixTimeMilliseconds()),
                _deps.Options.HandshakeTimeout,
                cancellationToken).ConfigureAwait(false);
            ApplyIncoming(reply);
        }
        catch (Exception)
        {
            // Status is best-effort.
        }
    }

    private void ApplyIncoming(MessageEnvelope envelope)
    {
        if (envelope.Type is not (MessageTypes.DeviceStatus or MessageTypes.StateUpdate))
        {
            return;
        }

        var payload = EnvelopeFactory.ReadPayload<DeviceStatusPayload>(envelope);
        if (payload is null)
        {
            return;
        }

        var network = payload.Network switch
        {
            "usb_tether" => NetworkKind.UsbTether,
            "wifi" => NetworkKind.Wifi,
            "cellular" => NetworkKind.Cellular,
            "none" => NetworkKind.None,
            _ => NetworkKind.Unknown
        };
        Apply(_snapshot with
        {
            Status = new DeviceStatus(
                payload.BatteryPercent,
                payload.Charging,
                network,
                payload.DeviceTimeUtc > 0
                    ? DateTimeOffset.FromUnixTimeMilliseconds(payload.DeviceTimeUtc)
                    : null,
                payload.ScreenWidth,
                payload.ScreenHeight,
                payload.Rotation,
                null)
        });
    }

    private void Apply(ConnectionSnapshot next)
    {
        _snapshot = next;
        StateChanged?.Invoke(this, next);
    }

    private void CancelSession()
    {
        try
        {
            _sessionCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Session already torn down.
        }
    }

    private async Task DisposeTransportAsync()
    {
        FailPending(new IOException("Transport closed."));
        if (_transport is not null)
        {
            await _transport.DisposeAsync().ConfigureAwait(false);
            _transport = null;
        }

        if (_readLoopTask is not null)
        {
            try
            {
                await _readLoopTask.ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Read loop has already recorded the failure.
            }

            _readLoopTask = null;
        }
    }

    private void FailPending(Exception exception)
    {
        foreach (var key in _pending.Keys)
        {
            if (_pending.TryRemove(key, out var waiter))
            {
                waiter.TrySetException(exception);
            }
        }
    }

    private static Task Observe(Task task)
    {
        return task.ContinueWith(
            t => _ = t.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private static bool IsStable(ConnectionState state) =>
        state is ConnectionState.PairingRequired
            or ConnectionState.Connected
            or ConnectionState.Error
            or ConnectionState.Disconnected;

    private static (string Host, int Port) SplitEndpoint(string endpoint)
    {
        var parts = endpoint.Split(':');
        if (parts.Length == 2 && int.TryParse(parts[1], out var port))
        {
            return (parts[0], port);
        }

        return (endpoint, ProtocolPorts.Control);
    }

    private static string ErrorCode(Exception exception)
    {
        return exception switch
        {
            TimeoutException => "TIMEOUT",
            InvalidOperationException when exception.Message == "NO_ENDPOINT" => "NO_ENDPOINT",
            OperationCanceledException => "CANCELLED",
            _ => "NETWORK_DROP"
        };
    }

    private sealed record ProbeResult(
        DeviceIdentity? Device,
        IReadOnlyList<string> Endpoints,
        bool Tether,
        bool Adb,
        bool Usb);
}
