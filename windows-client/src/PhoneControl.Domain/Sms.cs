namespace PhoneControl.Domain;

public sealed record SmsPreview(string Id, string Address, int BodyLength, DateTimeOffset ReceivedAt, bool Outgoing);

public interface ISmsAdapter
{
    Task<IReadOnlyList<SmsPreview>> ListAsync(int max, CancellationToken cancellationToken);
    Task SendAsync(string to, string body, CancellationToken cancellationToken);
}
