using FluentAssertions;
using PhoneControl.Application;
using PhoneControl.Domain;

namespace PhoneControl.Tests.Unit;

public sealed class SmsAdapterTests
{
    [Fact]
    public async Task List_WithoutPermission_FailsWithStableCode()
    {
        var adapter = new NullSmsAdapter();
        var act = async () => await adapter.ListAsync(10, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("SMS_PERMISSION_DENIED");
    }

    [Fact]
    public async Task Send_WithPermission_RequiresAddress()
    {
        var adapter = new NullSmsAdapter
        {
            SendState = PermissionState.Granted
        };
        var act = async () => await adapter.SendAsync(" ", "hello", CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task List_WithPermission_ReturnsEmptyWithoutLoggingBody()
    {
        var adapter = new NullSmsAdapter { ReadState = PermissionState.Granted };
        var list = await adapter.ListAsync(10, CancellationToken.None);
        list.Should().BeEmpty();
    }
}
