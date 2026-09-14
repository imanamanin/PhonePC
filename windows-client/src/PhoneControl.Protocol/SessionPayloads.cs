using System.Text.Json;
using System.Text.Json.Serialization;

namespace PhoneControl.Protocol;

public sealed class SessionHelloPayload
{
    public string ClientName { get; init; } = "PhoneControl.Desktop";
    public string ClientVersion { get; init; } = "0.1.0";
    public int ProtocolMin { get; init; } = 1;
    public int ProtocolMax { get; init; } = 1;
    public string? PairingId { get; init; }
    public IReadOnlyList<string> SupportedEncodings { get; init; } = new[] { "json" };
    public IReadOnlyList<string> SupportedScreenCodecs { get; init; } = new[] { "h264", "jpeg" };
    public string? SessionToken { get; init; }
}

public sealed class SessionDevicePayload
{
    public string Model { get; init; } = "unknown";
    public string Manufacturer { get; init; } = "unknown";
    public string AndroidVersion { get; init; } = "unknown";
    public int SdkInt { get; init; }
    public string SerialHash { get; init; } = "none";
}

public sealed class SessionHelloAckPayload
{
    public string AgentName { get; init; } = "PhoneControl.Agent";
    public string AgentVersion { get; init; } = "0.1.0";
    public int Protocol { get; init; } = 1;
    public SessionDevicePayload? Device { get; init; }
    public bool PairingRequired { get; init; }
    public IReadOnlyList<string> Capabilities { get; init; } = Array.Empty<string>();
}

public sealed class SessionPingPayload
{
    [JsonPropertyName("t")]
    public long T { get; init; }
}

public static class EnvelopeFactory
{
    public static MessageEnvelope Create(string type, long timestamp, object? payload = null, string? requestId = null)
    {
        return new MessageEnvelope
        {
            Version = 1,
            Type = type,
            RequestId = requestId ?? Guid.NewGuid().ToString(),
            Timestamp = timestamp,
            Payload = payload is null ? null : JsonSerializer.SerializeToElement(payload, MessageSerializer.Options)
        };
    }

    public static T? ReadPayload<T>(MessageEnvelope envelope)
    {
        if (envelope.Payload is null)
        {
            return default;
        }

        return envelope.Payload.Value.Deserialize<T>(MessageSerializer.Options);
    }
}
