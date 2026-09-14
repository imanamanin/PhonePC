namespace PhoneControl.Application;

public sealed class ClipboardLoopGuard
{
    private string? _lastSentHash;
    private string? _lastAppliedHash;

    public bool ShouldApplyIncoming(string origin, string hash, string localOrigin)
    {
        if (string.IsNullOrWhiteSpace(hash))
        {
            return false;
        }

        if (string.Equals(origin, localOrigin, StringComparison.Ordinal))
        {
            return false;
        }

        if (string.Equals(hash, _lastSentHash, StringComparison.Ordinal))
        {
            return false;
        }

        if (string.Equals(hash, _lastAppliedHash, StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    public void MarkApplied(string hash) => _lastAppliedHash = hash;

    public void MarkSent(string hash) => _lastSentHash = hash;
}
