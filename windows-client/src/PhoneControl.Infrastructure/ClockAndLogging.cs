using PhoneControl.Domain;

namespace PhoneControl.Infrastructure;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

public sealed class NullAppLogger : IAppLogger
{
    public void Info(string messageTemplate, params object[] args)
    {
        _ = messageTemplate;
        _ = args;
    }

    public void Warn(string messageTemplate, params object[] args)
    {
        _ = messageTemplate;
        _ = args;
    }

    public void Error(Exception exception, string messageTemplate, params object[] args)
    {
        _ = exception;
        _ = messageTemplate;
        _ = args;
    }
}
