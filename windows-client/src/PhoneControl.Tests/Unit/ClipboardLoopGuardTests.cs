using FluentAssertions;
using PhoneControl.Application;

namespace PhoneControl.Tests.Unit;

public sealed class ClipboardLoopGuardTests
{
    [Fact]
    public void DropsEcho_OfLastSentHash()
    {
        var guard = new ClipboardLoopGuard();
        guard.MarkSent("hash-a");
        guard.ShouldApplyIncoming("android", "hash-a", "windows").Should().BeFalse();
    }

    [Fact]
    public void Drops_SameOrigin()
    {
        var guard = new ClipboardLoopGuard();
        guard.ShouldApplyIncoming("windows", "hash-b", "windows").Should().BeFalse();
    }

    [Fact]
    public void Applies_NewRemoteHash()
    {
        var guard = new ClipboardLoopGuard();
        guard.MarkSent("hash-a");
        guard.ShouldApplyIncoming("android", "hash-b", "windows").Should().BeTrue();
        guard.MarkApplied("hash-b");
        guard.ShouldApplyIncoming("android", "hash-b", "windows").Should().BeFalse();
    }
}
