namespace PhoneControl.Application;

public sealed class ReconnectPolicy
{
    private readonly int _initialDelayMs;
    private readonly int _maxDelayMs;
    private int _attempt;

    public ReconnectPolicy(int initialDelayMs = 500, int maxDelayMs = 8000)
    {
        if (initialDelayMs <= 0 || maxDelayMs < initialDelayMs)
        {
            throw new ArgumentOutOfRangeException(nameof(initialDelayMs));
        }

        _initialDelayMs = initialDelayMs;
        _maxDelayMs = maxDelayMs;
    }

    public int Attempt => _attempt;

    public TimeSpan NextDelay()
    {
        var factor = Math.Min(_attempt, 16);
        var delay = Math.Min(_maxDelayMs, _initialDelayMs * (1 << factor));
        _attempt++;
        return TimeSpan.FromMilliseconds(delay);
    }

    public void Reset() => _attempt = 0;
}
