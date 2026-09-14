using FluentAssertions;
using PhoneControl.Adb;
using PhoneControl.Domain;

namespace PhoneControl.Tests.Unit;

public sealed class AdbClientTests
{
    [Fact]
    public void Parser_ReadsDevicesDashL()
    {
        const string output = """
            List of devices attached
            RFCN123 device usb:1-2 product:oriole model:Pixel_6 device:oriole transport_id:1
            emulator-5554 offline
            """;
        var devices = AdbDeviceParser.Parse(output);
        devices.Should().HaveCount(2);
        devices[0].Serial.Should().Be("RFCN123");
        devices[0].State.Should().Be("device");
        devices[0].Model.Should().Be("Pixel 6");
        devices[1].State.Should().Be("offline");
    }

    [Fact]
    public async Task MissingBinary_ReturnsEmpty_DoesNotThrow()
    {
        var client = new ProcessAdbClient(new UnavailableAdbRunner());
        var devices = await client.ListDevicesAsync(CancellationToken.None);
        devices.Should().BeEmpty();
    }

    [Fact]
    public async Task RunnerIsOnlyDevicesList()
    {
        var runner = new RecordingAdbRunner();
        var client = new ProcessAdbClient(runner);
        await client.ListDevicesAsync(CancellationToken.None);
        runner.Calls.Should().Be(1);
    }

    [Fact]
    public async Task ProcessRunner_MissingAdb_IsUnavailable()
    {
        var runner = new SystemAdbProcessRunner(adbPath: "adb-that-does-not-exist-phonecontrol.exe");
        var result = await runner.RunDevicesListAsync(CancellationToken.None);
        result.Available.Should().BeFalse();
    }

    private sealed class UnavailableAdbRunner : IAdbProcessRunner
    {
        public Task<AdbProcessResult> RunDevicesListAsync(CancellationToken cancellationToken) =>
            Task.FromResult(AdbProcessResult.Unavailable);
    }

    private sealed class RecordingAdbRunner : IAdbProcessRunner
    {
        public int Calls { get; private set; }

        public Task<AdbProcessResult> RunDevicesListAsync(CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new AdbProcessResult(true, 0, "List of devices attached\n"));
        }
    }
}
