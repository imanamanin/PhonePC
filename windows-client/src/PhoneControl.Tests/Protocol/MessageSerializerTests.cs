using FluentAssertions;
using PhoneControl.Protocol;

namespace PhoneControl.Tests.Protocol;

public sealed class MessageSerializerTests
{
    [Fact]
    public void RoundTrips_TouchExample()
    {
        var json = File.ReadAllText(ExamplePath("input-touch.json"));
        var envelope = MessageSerializer.Deserialize(json);

        envelope.Type.Should().Be(MessageTypes.InputTouch);
        envelope.Version.Should().Be(1);
        envelope.Payload.Should().NotBeNull();
        envelope.Payload!.Value.GetProperty("action").GetString().Should().Be("tap");

        var again = MessageSerializer.Deserialize(MessageSerializer.Serialize(envelope));
        again.RequestId.Should().Be(envelope.RequestId);
    }

    [Fact]
    public void RoundTrips_NotificationExample()
    {
        var json = File.ReadAllText(ExamplePath("notification-received.json"));
        var envelope = MessageSerializer.Deserialize(json);
        envelope.Type.Should().Be(MessageTypes.NotificationReceived);
        envelope.Payload!.Value.GetProperty("packageName").GetString().Should().Be("example.package");
    }

    [Fact]
    public void Rejects_InvalidType()
    {
        var act = () => MessageSerializer.Deserialize("""
            {"version":1,"type":"NOT VALID","requestId":"11111111-1111-1111-1111-111111111111","timestamp":0}
            """);
        act.Should().Throw<ProtocolValidationException>();
    }

    [Fact]
    public void CommandCatalog_RejectsUnknown()
    {
        CommandCatalog.CanExecute("input.touch").Should().BeTrue();
        CommandCatalog.CanExecute("shell.root").Should().BeFalse();
    }

    private static string ExamplePath(string name)
    {
        var root = FindRepoRoot();
        return Path.Combine(root, "shared", "protocol", "examples", name);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "README.md")) &&
                Directory.Exists(Path.Combine(dir.FullName, "shared")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
