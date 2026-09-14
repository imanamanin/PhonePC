using FluentAssertions;
using PhoneControl.Application;
using PhoneControl.Domain;

namespace PhoneControl.Tests.Unit;

public sealed class ConnectionStateMachineTests
{
    [Theory]
    [InlineData(ConnectionState.Disconnected, ConnectionState.Connecting, true)]
    [InlineData(ConnectionState.Connecting, ConnectionState.PairingRequired, true)]
    [InlineData(ConnectionState.Connecting, ConnectionState.Connected, true)]
    [InlineData(ConnectionState.Connecting, ConnectionState.Reconnecting, true)]
    [InlineData(ConnectionState.Connecting, ConnectionState.Error, true)]
    [InlineData(ConnectionState.Connecting, ConnectionState.Disconnected, true)]
    [InlineData(ConnectionState.PairingRequired, ConnectionState.Connected, true)]
    [InlineData(ConnectionState.PairingRequired, ConnectionState.Disconnected, true)]
    [InlineData(ConnectionState.PairingRequired, ConnectionState.Reconnecting, true)]
    [InlineData(ConnectionState.Connected, ConnectionState.Reconnecting, true)]
    [InlineData(ConnectionState.Connected, ConnectionState.Disconnected, true)]
    [InlineData(ConnectionState.Reconnecting, ConnectionState.Connecting, true)]
    [InlineData(ConnectionState.Reconnecting, ConnectionState.Error, true)]
    [InlineData(ConnectionState.Error, ConnectionState.Connecting, true)]
    [InlineData(ConnectionState.Error, ConnectionState.Disconnected, true)]
    [InlineData(ConnectionState.Disconnected, ConnectionState.Connected, false)]
    [InlineData(ConnectionState.Connected, ConnectionState.PairingRequired, false)]
    [InlineData(ConnectionState.Reconnecting, ConnectionState.Connected, false)]
    public void TransitionTable(ConnectionState from, ConnectionState to, bool allowed)
    {
        ConnectionStateMachine.CanTransition(from, to).Should().Be(allowed);
    }

    [Fact]
    public void IllegalTransition_Throws()
    {
        var act = () => ConnectionStateMachine.Ensure(ConnectionState.Disconnected, ConnectionState.Connected);
        act.Should().Throw<InvalidOperationException>();
    }
}
