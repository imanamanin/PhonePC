using FluentAssertions;
using PhoneControl.Notifications;

namespace PhoneControl.Tests.Unit;

public sealed class NotificationMapperTests
{
    [Fact]
    public void Maps_RequiredFields()
    {
        var mapper = new NotificationMapper();
        var item = mapper.Map("pkg|1|tag", "example.package", "Title", "Preview", 1_700_000_000_000, null);
        item.PackageName.Should().Be("example.package");
        item.Title.Should().Be("Title");
        item.Actions.Should().BeEmpty();
        item.PostedAt.ToUnixTimeMilliseconds().Should().Be(1_700_000_000_000);
    }

    [Fact]
    public void Requires_KeyAndPackage()
    {
        var mapper = new NotificationMapper();
        var act = () => mapper.Map(" ", "pkg", null, null, 0, null);
        act.Should().Throw<ArgumentException>();
    }
}
