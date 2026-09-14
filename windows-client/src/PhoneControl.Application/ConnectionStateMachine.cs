using PhoneControl.Domain;

namespace PhoneControl.Application;

public static class ConnectionStateMachine
{
    public static bool CanTransition(ConnectionState from, ConnectionState to)
    {
        return to switch
        {
            ConnectionState.Connecting => from is ConnectionState.Disconnected
                or ConnectionState.Reconnecting
                or ConnectionState.Error
                or ConnectionState.PairingRequired,
            ConnectionState.PairingRequired => from is ConnectionState.Connecting,
            ConnectionState.Connected => from is ConnectionState.Connecting or ConnectionState.PairingRequired,
            ConnectionState.Reconnecting => from is ConnectionState.Connecting
                or ConnectionState.Connected
                or ConnectionState.PairingRequired,
            ConnectionState.Error => from is ConnectionState.Connecting
                or ConnectionState.Reconnecting
                or ConnectionState.PairingRequired
                or ConnectionState.Connected,
            ConnectionState.Disconnected => from != ConnectionState.Disconnected,
            _ => false
        };
    }

    public static void Ensure(ConnectionState from, ConnectionState to)
    {
        if (!CanTransition(from, to))
        {
            throw new InvalidOperationException($"Illegal connection transition {from} -> {to}.");
        }
    }
}
