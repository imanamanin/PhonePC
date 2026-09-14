using FluentAssertions;
using PhoneControl.Input;

namespace PhoneControl.Tests.Unit;

public sealed class CoordinateMapperTests
{
    private readonly CoordinateMapper _mapper = new();

    [Fact]
    public void MapsCenter_Portrait_NoLetterbox()
    {
        var point = _mapper.TryMap(new WindowRect(1080, 2400), new PhoneScreen(1080, 2400, 0), 540, 1200);
        point.Should().NotBeNull();
        point!.Value.X.Should().BeApproximately(540, 0.5);
        point.Value.Y.Should().BeApproximately(1200, 0.5);
    }

    [Fact]
    public void IgnoresClicks_InLetterbox()
    {
        var point = _mapper.TryMap(new WindowRect(2000, 1000), new PhoneScreen(1080, 2400, 0), 10, 10);
        point.Should().BeNull();
    }

    [Fact]
    public void Maps_LandscapeRotation90()
    {
        var window = new WindowRect(2400, 1080);
        var phone = new PhoneScreen(1080, 2400, 90);
        var point = _mapper.TryMap(window, phone, 0, 0);
        point.Should().NotBeNull();
        point!.Value.X.Should().BeApproximately(0, 1);
        point.Value.Y.Should().BeApproximately(2400, 2);
    }
}
