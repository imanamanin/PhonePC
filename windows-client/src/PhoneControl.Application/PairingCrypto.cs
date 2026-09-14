using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PhoneControl.Domain;

namespace PhoneControl.Application;

public sealed record TrustedDevice(
    string PairingId,
    byte[] SessionToken,
    DateTimeOffset ExpiresAt);

public static class PairingCrypto
{
    public const int TokenBytes = 32;
    public const int PinLength = 6;

    public static bool ConstantTimeEquals(string left, string right)
    {
        var a = Encoding.UTF8.GetBytes(left);
        var b = Encoding.UTF8.GetBytes(right);
        var diff = (uint)a.Length ^ (uint)b.Length;
        var n = Math.Max(a.Length, b.Length);
        for (var i = 0; i < n; i++)
        {
            var av = i < a.Length ? a[i] : (byte)0;
            var bv = i < b.Length ? b[i] : (byte)0;
            diff |= (uint)(av ^ bv);
        }

        CryptographicOperations.ZeroMemory(a);
        CryptographicOperations.ZeroMemory(b);
        return diff == 0;
    }

    public static bool ConstantTimeEquals(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right) =>
        CryptographicOperations.FixedTimeEquals(left, right);

    public static byte[] CreateToken() => RandomNumberGenerator.GetBytes(TokenBytes);

    /// <summary>Six-digit SAS shared by both peers from the session token. Not a PIN.</summary>
    public static string ComputeSas(ReadOnlySpan<byte> token)
    {
        var hash = SHA256.HashData(token);
        var n = (hash[0] << 16) | (hash[1] << 8) | hash[2];
        return (n % 1_000_000).ToString("D6");
    }

    public static bool IsPinShape(string pin) =>
        pin.Length == PinLength && pin.All(char.IsDigit);
}

public sealed class TrustedDeviceStore
{
    public const string StoreKey = "trusted-device";

    private readonly ISecureStore _store;

    public TrustedDeviceStore(ISecureStore store)
    {
        _store = store;
    }

    public async Task SaveAsync(TrustedDevice device, CancellationToken cancellationToken)
    {
        var dto = new TrustedDeviceDto
        {
            PairingId = device.PairingId,
            SessionToken = Convert.ToBase64String(device.SessionToken),
            ExpiresAt = device.ExpiresAt.ToUnixTimeMilliseconds()
        };
        var json = JsonSerializer.SerializeToUtf8Bytes(dto);
        await _store.SaveAsync(StoreKey, json, cancellationToken).ConfigureAwait(false);
        CryptographicOperations.ZeroMemory(json);
    }

    public async Task<TrustedDevice?> LoadAsync(IClock clock, CancellationToken cancellationToken)
    {
        var json = await _store.LoadAsync(StoreKey, cancellationToken).ConfigureAwait(false);
        if (json is null || json.Length == 0)
        {
            return null;
        }

        try
        {
            var dto = JsonSerializer.Deserialize<TrustedDeviceDto>(json);
            if (dto is null || string.IsNullOrWhiteSpace(dto.PairingId) || string.IsNullOrWhiteSpace(dto.SessionToken))
            {
                return null;
            }

            var token = Convert.FromBase64String(dto.SessionToken);
            var expires = DateTimeOffset.FromUnixTimeMilliseconds(dto.ExpiresAt);
            if (expires <= clock.UtcNow)
            {
                return null;
            }

            return new TrustedDevice(dto.PairingId, token, expires);
        }
        catch (FormatException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(json);
        }
    }

    public Task ClearAsync(CancellationToken cancellationToken) =>
        _store.DeleteAsync(StoreKey, cancellationToken);

    private sealed class TrustedDeviceDto
    {
        public string PairingId { get; set; } = string.Empty;
        public string SessionToken { get; set; } = string.Empty;
        public long ExpiresAt { get; set; }
    }
}
