using PhoneControl.Adb;
using PhoneControl.Domain;

namespace PhoneControl.Tests.Hardware;

[Trait("Category", "Hardware")]
public sealed class AdbHardwareTests
{
    [Fact(Skip = "Requires platform-tools and a physical phone. Run in Phase 1 hardware suite.")]
    public async Task ListDevices_RequiresRealAdb()
    {
        var client = new ProcessAdbClient();
        var devices = await client.ListDevicesAsync(CancellationToken.None);
        Assert.NotNull(devices);
    }
}
