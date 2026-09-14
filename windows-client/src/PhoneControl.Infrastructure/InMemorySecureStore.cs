using PhoneControl.Domain;

namespace PhoneControl.Infrastructure;

public sealed class InMemorySecureStore : ISecureStore
{
    private readonly Dictionary<string, byte[]> _values = new(StringComparer.Ordinal);

    public Task SaveAsync(string key, byte[] secret, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _values[key] = secret.ToArray();
        return Task.CompletedTask;
    }

    public Task<byte[]?> LoadAsync(string key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_values.TryGetValue(key, out var secret))
        {
            return Task.FromResult<byte[]?>(secret.ToArray());
        }

        return Task.FromResult<byte[]?>(null);
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _values.Remove(key);
        return Task.CompletedTask;
    }
}
