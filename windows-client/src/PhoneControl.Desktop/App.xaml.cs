using System.IO;
using Microsoft.Extensions.DependencyInjection;
using PhoneControl.Adb;
using PhoneControl.Application;
using PhoneControl.Desktop.Decoding;
using PhoneControl.Desktop.ViewModels;
using PhoneControl.Domain;
using PhoneControl.Infrastructure;
using PhoneControl.Screen;
using Serilog;
using Serilog.Events;
using WpfApplication = System.Windows.Application;
using StartupEventArgs = System.Windows.StartupEventArgs;
using ExitEventArgs = System.Windows.ExitEventArgs;

namespace PhoneControl.Desktop;

public partial class App : WpfApplication
{
    private ServiceProvider? _services;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PhoneControl",
            "logs");
        Directory.CreateDirectory(logDir);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .WriteTo.File(
                Path.Combine(logDir, "desktop-.log"),
                rollingInterval: RollingInterval.Day)
            .CreateLogger();

        var services = new ServiceCollection();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IAppLogger>(_ => new RedactingAppLogger(new SerilogAppLogger()));
        services.AddSingleton<IDelay, TaskDelay>();
        services.AddSingleton<INetworkInterfaceProbe, WindowsNetworkInterfaceProbe>();
        services.AddSingleton<INetworkDiscoveryService, NetworkDiscoveryService>();
        services.AddSingleton<IAdbProcessRunner, SystemAdbProcessRunner>();
        services.AddSingleton<IAdbClient>(sp => new ProcessAdbClient(sp.GetRequiredService<IAdbProcessRunner>()));
        services.AddSingleton<IDeviceDiscovery, CompositeDeviceDiscovery>();
        services.AddSingleton<IControlTransportFactory, TcpControlTransportFactory>();
        services.AddSingleton<IVideoTransportFactory, TcpVideoTransportFactory>();
        services.AddSingleton<IVideoDecoder>(_ =>
        {
            var mf = MediaFoundationH264Decoder.TryCreate();
            return new CompositeVideoDecoder(
                mf ?? new RawBgraCodec(),
                new JpegWicDecoder(),
                new RawBgraCodec());
        });
        services.AddSingleton(new ConnectionOptions());
        services.AddSingleton(sp => new ConnectionDependencies
        {
            Discovery = sp.GetRequiredService<IDeviceDiscovery>(),
            Network = sp.GetRequiredService<INetworkDiscoveryService>(),
            TransportFactory = sp.GetRequiredService<IControlTransportFactory>(),
            Clock = sp.GetRequiredService<IClock>(),
            Delay = sp.GetRequiredService<IDelay>(),
            Logger = sp.GetRequiredService<IAppLogger>(),
            SecureStore = sp.GetRequiredService<ISecureStore>(),
            Options = sp.GetRequiredService<ConnectionOptions>()
        });
        services.AddSingleton<IDataProtector, DpapiDataProtector>();
        services.AddSingleton<ISecureStore>(sp =>
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PhoneControl",
                "secure");
            return new ProtectedSecureStore(new FileSecureStore(dir), sp.GetRequiredService<IDataProtector>());
        });
        services.AddSingleton<ISmsAdapter, NullSmsAdapter>();
        services.AddSingleton<ConnectionManager>();
        services.AddSingleton<IRemoteInputClient, RemoteInputClient>();
        services.AddSingleton<PointerInputController>();
        services.AddSingleton<VideoSession>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
        _services = services.BuildServiceProvider();

        var window = _services.GetRequiredService<MainWindow>();
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_services is not null)
        {
            _services.GetRequiredService<VideoSession>().DisposeAsync().AsTask().GetAwaiter().GetResult();
            _services.GetRequiredService<ConnectionManager>().DisposeAsync().AsTask().GetAwaiter().GetResult();
            _services.Dispose();
        }

        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
