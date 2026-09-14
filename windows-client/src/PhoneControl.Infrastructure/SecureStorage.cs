using PhoneControl.Domain;

namespace PhoneControl.Infrastructure;

/// <summary>Test double. Production uses <see cref="DpapiDataProtector"/>.</summary>
public sealed class PassthroughDataProtector : IDataProtector
{
    public byte[] Protect(byte[] plaintext) => plaintext.ToArray();

    public byte[] Unprotect(byte[] ciphertext) => ciphertext.ToArray();
}

/// <summary>Windows DPAPI, CurrentUser scope. Optional entropy is a purpose string, not a secret.</summary>
public sealed class DpapiDataProtector : IDataProtector
{
    private static readonly byte[] Purpose = "PhoneControl.Suite.v1"u8.ToArray();

    public byte[] Protect(byte[] plaintext)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("DPAPI requires Windows.");
        }

        return System.Security.Cryptography.ProtectedData.Protect(
            plaintext,
            Purpose,
            System.Security.Cryptography.DataProtectionScope.CurrentUser);
    }

    public byte[] Unprotect(byte[] ciphertext)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("DPAPI requires Windows.");
        }

        return System.Security.Cryptography.ProtectedData.Unprotect(
            ciphertext,
            Purpose,
            System.Security.Cryptography.DataProtectionScope.CurrentUser);
    }
}

public sealed class ProtectedSecureStore : ISecureStore
{
    private readonly ISecureStore _inner;
    private readonly IDataProtector _protector;

    public ProtectedSecureStore(ISecureStore inner, IDataProtector protector)
    {
        _inner = inner;
        _protector = protector;
    }

    public Task SaveAsync(string key, byte[] secret, CancellationToken cancellationToken)
    {
        var protectedBytes = _protector.Protect(secret);
        return _inner.SaveAsync(key, protectedBytes, cancellationToken);
    }

    public async Task<byte[]?> LoadAsync(string key, CancellationToken cancellationToken)
    {
        var stored = await _inner.LoadAsync(key, cancellationToken).ConfigureAwait(false);
        return stored is null ? null : _protector.Unprotect(stored);
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken) =>
        _inner.DeleteAsync(key, cancellationToken);
}

public sealed class FileSecureStore : ISecureStore
{
    private readonly string _directory;

    public FileSecureStore(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(directory);
    }

    public async Task SaveAsync(string key, byte[] secret, CancellationToken cancellationToken)
    {
        var path = PathFor(key);
        await File.WriteAllBytesAsync(path, secret, cancellationToken).ConfigureAwait(false);
    }

    public async Task<byte[]?> LoadAsync(string key, CancellationToken cancellationToken)
    {
        var path = PathFor(key);
        if (!File.Exists(path))
        {
            return null;
        }

        return await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = PathFor(key);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private string PathFor(string key)
    {
        var safe = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key)));
        return Path.Combine(_directory, safe + ".bin");
    }
}

public sealed class RedactingAppLogger : IAppLogger
{
    private readonly IAppLogger _inner;

    public RedactingAppLogger(IAppLogger inner)
    {
        _inner = inner;
    }

    public void Info(string messageTemplate, params object[] args) => Write(_inner.Info, messageTemplate, args);

    public void Warn(string messageTemplate, params object[] args) => Write(_inner.Warn, messageTemplate, args);

    public void Error(Exception exception, string messageTemplate, params object[] args)
    {
        EnsureSafe(messageTemplate);
        EnsureArgs(args);
        _inner.Error(exception, messageTemplate, args);
    }

    private void Write(Action<string, object[]> emit, string messageTemplate, object[] args)
    {
        EnsureSafe(messageTemplate);
        EnsureArgs(args);
        emit(messageTemplate, args);
    }

    private static void EnsureArgs(object[] args)
    {
        foreach (var arg in args)
        {
            if (arg is string text)
            {
                EnsureSafe(text);
            }
        }
    }

    private static void EnsureSafe(string value)
    {
        if (PhoneControl.Domain.LogRedaction.ContainsSecretHint(value))
        {
            throw new InvalidOperationException("Refusing to log a secret-shaped value.");
        }
    }
}

public sealed class SerilogAppLogger : IAppLogger
{
    public void Info(string messageTemplate, params object[] args) =>
        Serilog.Log.Information(messageTemplate, args);

    public void Warn(string messageTemplate, params object[] args) =>
        Serilog.Log.Warning(messageTemplate, args);

    public void Error(Exception exception, string messageTemplate, params object[] args) =>
        Serilog.Log.Error(exception, messageTemplate, args);
}
