using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace PhoneControl.Protocol;

public static class MessageSerializer
{
    private static readonly Regex TypePattern = new(
        "^[a-z0-9]+(\\.[a-z0-9_]+)+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static byte[] Serialize(MessageEnvelope envelope)
    {
        Validate(envelope);
        return JsonSerializer.SerializeToUtf8Bytes(envelope, Options);
    }

    public static string SerializeToString(MessageEnvelope envelope)
    {
        return Encoding.UTF8.GetString(Serialize(envelope));
    }

    public static MessageEnvelope Deserialize(ReadOnlySpan<byte> utf8Json)
    {
        var envelope = JsonSerializer.Deserialize<MessageEnvelope>(utf8Json, Options)
            ?? throw new ProtocolValidationException("Envelope was null.");
        Validate(envelope);
        return envelope;
    }

    public static MessageEnvelope Deserialize(string json)
    {
        return Deserialize(Encoding.UTF8.GetBytes(json));
    }

    public static void Validate(MessageEnvelope envelope)
    {
        if (envelope.Version < 1)
        {
            throw new ProtocolValidationException("version must be >= 1.");
        }

        if (string.IsNullOrWhiteSpace(envelope.Type) || !TypePattern.IsMatch(envelope.Type))
        {
            throw new ProtocolValidationException("type is invalid.");
        }

        if (!Guid.TryParse(envelope.RequestId, out _))
        {
            throw new ProtocolValidationException("requestId must be a UUID.");
        }

        if (envelope.Timestamp < 0)
        {
            throw new ProtocolValidationException("timestamp must be >= 0.");
        }
    }
}

public sealed class ProtocolValidationException : Exception
{
    public ProtocolValidationException(string message)
        : base(message)
    {
    }
}
