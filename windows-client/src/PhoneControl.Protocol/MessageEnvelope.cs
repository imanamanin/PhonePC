using System.Text.Json;

namespace PhoneControl.Protocol;

public sealed class ProtocolError
{
    public string Code { get; init; } = "INTERNAL";
    public string Message { get; init; } = "error";
    public bool Retryable { get; init; }
}

public sealed class MessageEnvelope
{
    public int Version { get; init; } = 1;
    public string Type { get; init; } = string.Empty;
    public string RequestId { get; init; } = string.Empty;
    public long Timestamp { get; init; }
    public JsonElement? Payload { get; init; }
    public ProtocolError? Error { get; init; }
}
