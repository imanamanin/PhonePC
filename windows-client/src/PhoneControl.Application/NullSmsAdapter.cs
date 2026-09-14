using PhoneControl.Domain;

namespace PhoneControl.Application;

public sealed class NullSmsAdapter : ISmsAdapter
{
    public PermissionState ReadState { get; init; } = PermissionState.Denied;
    public PermissionState SendState { get; init; } = PermissionState.Denied;

    public Task<IReadOnlyList<SmsPreview>> ListAsync(int max, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (ReadState != PermissionState.Granted)
        {
            throw new InvalidOperationException("SMS_PERMISSION_DENIED");
        }

        if (max <= 0)
        {
            return Task.FromResult<IReadOnlyList<SmsPreview>>(Array.Empty<SmsPreview>());
        }

        return Task.FromResult<IReadOnlyList<SmsPreview>>(Array.Empty<SmsPreview>());
    }

    public Task SendAsync(string to, string body, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (SendState != PermissionState.Granted)
        {
            throw new InvalidOperationException("SMS_PERMISSION_DENIED");
        }

        if (string.IsNullOrWhiteSpace(to) || string.IsNullOrWhiteSpace(body))
        {
            throw new ArgumentException("SMS requires destination and body.");
        }

        return Task.CompletedTask;
    }
}
