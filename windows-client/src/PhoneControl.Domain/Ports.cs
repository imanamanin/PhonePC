namespace PhoneControl.Domain;

/// <summary>Discover phones without assuming ADB survives USB tethering.</summary>
public interface IDeviceDiscovery
{
    Task<IReadOnlyList<DiscoveredDevice>> DiscoverAsync(CancellationToken cancellationToken);
}

public sealed record DiscoveredDevice(
    string DisplayName,
    string? SerialHash,
    string? TetherEndpoint,
    bool AdbAvailable,
    bool UsbPresent,
    bool TetheringLikely);

public interface IAdbClient
{
    Task<IReadOnlyList<AdbDevice>> ListDevicesAsync(CancellationToken cancellationToken);
}

public sealed record AdbDevice(string Serial, string State, string? Model);

public interface IControlTransport : IAsyncDisposable
{
    Task ConnectAsync(string host, int port, CancellationToken cancellationToken);
    Task SendAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken);
    IAsyncEnumerable<ReadOnlyMemory<byte>> ReadFramesAsync(CancellationToken cancellationToken);
    bool IsConnected { get; }
}

public interface ISecureStore
{
    Task SaveAsync(string key, byte[] secret, CancellationToken cancellationToken);
    Task<byte[]?> LoadAsync(string key, CancellationToken cancellationToken);
    Task DeleteAsync(string key, CancellationToken cancellationToken);
}

/// <summary>DPAPI (or a test double). Never log plaintext passed to these methods.</summary>
public interface IDataProtector
{
    byte[] Protect(byte[] plaintext);
    byte[] Unprotect(byte[] ciphertext);
}

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public static class LogRedaction
{
    private static readonly string[] Hints =
    {
        "pairingcode", "pairing-code", "pairing_code", "sessiontoken", "session-token",
        "password", "secret", "clipboard", "sms-body", "smsbody"
    };

    public static bool ContainsSecretHint(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        var compact = value.Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
        if (compact.Contains("pin", StringComparison.Ordinal) &&
            (compact.Contains("code", StringComparison.Ordinal) || compact.Contains("pairing", StringComparison.Ordinal)))
        {
            return true;
        }

        return Hints.Any(h => compact.Contains(h, StringComparison.Ordinal));
    }
}

public interface IAppLogger
{
    void Info(string messageTemplate, params object[] args);
    void Warn(string messageTemplate, params object[] args);
    void Error(Exception exception, string messageTemplate, params object[] args);
}
