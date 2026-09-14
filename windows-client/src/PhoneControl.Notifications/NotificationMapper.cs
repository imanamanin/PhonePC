namespace PhoneControl.Notifications;

public sealed record NotificationItem(
    string Key,
    string PackageName,
    string? Title,
    string? Text,
    DateTimeOffset PostedAt,
    IReadOnlyList<NotificationAction> Actions);

public sealed record NotificationAction(string Id, string Label, bool HasRemoteInput);

public sealed class NotificationMapper
{
    public NotificationItem Map(
        string key,
        string packageName,
        string? title,
        string? text,
        long postedAtUnixMs,
        IReadOnlyList<NotificationAction>? actions)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Notification key is required.", nameof(key));
        }

        if (string.IsNullOrWhiteSpace(packageName))
        {
            throw new ArgumentException("Package name is required.", nameof(packageName));
        }

        var posted = postedAtUnixMs <= 0
            ? DateTimeOffset.UnixEpoch
            : DateTimeOffset.FromUnixTimeMilliseconds(postedAtUnixMs);

        return new NotificationItem(
            key,
            packageName,
            title,
            text,
            posted,
            actions ?? Array.Empty<NotificationAction>());
    }
}
