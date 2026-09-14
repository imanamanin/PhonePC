using PhoneControl.Application;
using PhoneControl.Domain;
using PhoneControl.Infrastructure;
using PhoneControl.Tests.Fakes;

namespace PhoneControl.Tests.Unit;

internal static class ConnectionHarness
{
    public static ConnectionManager Create(
        IControlTransportFactory? transport = null,
        INetworkDiscoveryService? network = null,
        IDeviceDiscovery? discovery = null,
        ConnectionOptions? options = null,
        IDelay? delay = null,
        ISecureStore? secureStore = null)
    {
        return new ConnectionManager(new ConnectionDependencies
        {
            Discovery = discovery ?? new MockDeviceDiscovery(),
            Network = network ?? new FakeNetworkDiscovery(),
            TransportFactory = transport ?? new AutoReplyingTransportFactory(),
            Clock = new SystemClock(),
            Delay = delay ?? new ImmediateDelay(),
            Logger = new NullAppLogger(),
            SecureStore = secureStore ?? new InMemorySecureStore(),
            Options = options ?? new ConnectionOptions
            {
                HandshakeTimeout = TimeSpan.FromMilliseconds(200),
                HeartbeatInterval = TimeSpan.FromMilliseconds(50),
                HeartbeatTimeout = TimeSpan.FromMilliseconds(200),
                MaxReconnectAttempts = 2,
                ReconnectInitialDelayMs = 1,
                ReconnectMaxDelayMs = 1
            }
        });
    }
}
