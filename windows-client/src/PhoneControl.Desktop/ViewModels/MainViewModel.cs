using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhoneControl.Application;
using PhoneControl.Domain;
using PhoneControl.Input;
using PhoneControl.Protocol;
using PhoneControl.Screen;

namespace PhoneControl.Desktop.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly ConnectionManager _connection;
    private readonly IRemoteInputClient _input;
    private readonly IClock _clock;
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private string _phaseLabel = "Phase 3 — Pairing and state sync";
    private string _connectionStateText = string.Empty;
    private string _deviceText = "No device";
    private string _tetheringText = "Tethering: unknown";
    private string _adbText = "ADB: unknown";
    private string _endpointText = "Endpoint: none";
    private string _discoveredText = "Discovered: none";
    private string _notice =
        "Enter the 6-digit PIN shown on the phone. Tokens are stored with DPAPI. Secrets are never logged.";
    private string _permissionsText =
        "Accessibility: unknown | Notifications: unknown | MediaProjection: unknown | SMS: unknown";
    private string _statusBarText = "Battery —  ·  Latency — ms";
    private string _batteryText = "Battery: unknown";
    private string _sasText = "SAS: —";
    private string _pinText = string.Empty;
    private StreamHud _hud = new(0, 0, 0, 0, 0, 0, "none");

    public MainViewModel(
        ConnectionManager connection,
        VideoSession video,
        IRemoteInputClient input,
        IClock clock)
    {
        _connection = connection;
        _input = input;
        _clock = clock;
        _connection.StateChanged += (_, snapshot) => Marshal(() => Apply(snapshot));
        video.HudChanged += (_, hud) => Marshal(() => ApplyHud(hud));
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        ConnectCommand = new AsyncRelayCommand(ConnectAsync);
        DisconnectCommand = new AsyncRelayCommand(DisconnectAsync);
        PairCommand = new AsyncRelayCommand(PairAsync, () => PinText.Length == 6);
        UnpairCommand = new AsyncRelayCommand(UnpairAsync);
        BackCommand = new AsyncRelayCommand(() => SendKey("back"));
        HomeCommand = new AsyncRelayCommand(() => SendKey("home"));
        RecentsCommand = new AsyncRelayCommand(() => SendKey("recents"));
        Apply(_connection.Snapshot);
        ApplyHud(video.Hud);
    }

    public string PhaseLabel
    {
        get => _phaseLabel;
        private set => SetProperty(ref _phaseLabel, value);
    }

    public string ConnectionStateText
    {
        get => _connectionStateText;
        private set => SetProperty(ref _connectionStateText, value);
    }

    public string DeviceText
    {
        get => _deviceText;
        private set => SetProperty(ref _deviceText, value);
    }

    public string TetheringText
    {
        get => _tetheringText;
        private set => SetProperty(ref _tetheringText, value);
    }

    public string AdbText
    {
        get => _adbText;
        private set => SetProperty(ref _adbText, value);
    }

    public string EndpointText
    {
        get => _endpointText;
        private set => SetProperty(ref _endpointText, value);
    }

    public string DiscoveredText
    {
        get => _discoveredText;
        private set => SetProperty(ref _discoveredText, value);
    }

    public string Notice
    {
        get => _notice;
        private set => SetProperty(ref _notice, value);
    }

    public string PermissionsText
    {
        get => _permissionsText;
        private set => SetProperty(ref _permissionsText, value);
    }

    public string StatusBarText
    {
        get => _statusBarText;
        private set => SetProperty(ref _statusBarText, value);
    }

    public string BatteryText
    {
        get => _batteryText;
        private set => SetProperty(ref _batteryText, value);
    }

    public string SasText
    {
        get => _sasText;
        private set => SetProperty(ref _sasText, value);
    }

    public string PinText
    {
        get => _pinText;
        set
        {
            if (SetProperty(ref _pinText, value))
            {
                PairCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand ConnectCommand { get; }
    public IAsyncRelayCommand DisconnectCommand { get; }
    public IAsyncRelayCommand PairCommand { get; }
    public IAsyncRelayCommand UnpairCommand { get; }
    public IAsyncRelayCommand BackCommand { get; }
    public IAsyncRelayCommand HomeCommand { get; }
    public IAsyncRelayCommand RecentsCommand { get; }

    private Task RefreshAsync() => _connection.RefreshDiscoveryAsync(CancellationToken.None);

    private Task ConnectAsync() => _connection.ConnectAsync(CancellationToken.None);

    private Task DisconnectAsync() => _connection.DisconnectAsync(CancellationToken.None);

    private async Task PairAsync()
    {
        var pin = PinText;
        PinText = string.Empty;
        await _connection.SubmitPinAsync(pin, CancellationToken.None).ConfigureAwait(true);
    }

    private Task UnpairAsync() => _connection.UnpairAsync(CancellationToken.None);

    private Task SendKey(string key)
    {
        var envelope = EnvelopeFactory.Create(
            MessageTypes.InputKey,
            _clock.UtcNow.ToUnixTimeMilliseconds(),
            new InputKeyPayload { Action = "click", Key = key });
        return _input.SendAsync(envelope, CancellationToken.None);
    }

    private void Marshal(Action action)
    {
        if (_dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            _dispatcher.Invoke(action);
        }
    }

    private void ApplyHud(StreamHud hud)
    {
        _hud = hud;
        RefreshStatusBar(_connection.Snapshot);
    }

    private void Apply(ConnectionSnapshot snapshot)
    {
        ConnectionStateText = snapshot.State.ToString();
        DeviceText = snapshot.Device is null
            ? "No device"
            : $"{snapshot.Device.Model} ({snapshot.Device.SerialHash})";
        TetheringText = snapshot.TetheringLikely ? "Tethering: adapter/subnet detected" : "Tethering: fallback only";
        AdbText = snapshot.AdbAvailable ? "ADB: available (devices -l only)" : "ADB: not required for control";
        EndpointText = $"Active: {snapshot.Endpoint ?? "none"}";
        DiscoveredText = snapshot.DiscoveredEndpoints.Count == 0
            ? "Discovered: none"
            : "Discovered: " + string.Join(", ", snapshot.DiscoveredEndpoints);
        BatteryText = FormatBattery(snapshot.Status);
        SasText = string.IsNullOrEmpty(snapshot.Sas)
            ? (snapshot.TrustedDevice ? "Trusted device (token stored with DPAPI)" : "SAS: —")
            : $"Numeric comparison SAS: {snapshot.Sas} (must match the phone)";
        if (snapshot.ErrorCode is not null)
        {
            Notice = snapshot.ErrorCode switch
            {
                "PIN_MISMATCH" => "PIN did not match. Check the code on the phone. The code itself is not logged.",
                "PIN_EXPIRED" => "PIN expired. Ask the phone to show a new code.",
                "PIN_LOCKED" => "Too many PIN attempts. Start pairing again on the phone.",
                "PIN_INVALID" => "PIN must be 6 digits.",
                "SAS_MISMATCH" => "Numeric comparison failed. Unpair and start over.",
                _ => $"Error code: {snapshot.ErrorCode}"
            };
        }
        else if (snapshot.State == ConnectionState.PairingRequired)
        {
            Notice = "Pairing required. Type the 6-digit PIN shown only on the phone, then Pair.";
        }

        RefreshStatusBar(snapshot);
    }

    private void RefreshStatusBar(ConnectionSnapshot snapshot)
    {
        var battery = FormatBattery(snapshot.Status);
        StatusBarText =
            $"{battery}  ·  Latency {_hud.SmoothedLatencyMs} ms (last {_hud.LatencyMs} ms)  ·  {_hud.Fps} fps  ·  {_hud.Width}x{_hud.Height}  ·  {_hud.BitrateKbps} kbps  ·  {_hud.Codec}";
    }

    private static string FormatBattery(DeviceStatus? status)
    {
        if (status?.BatteryPercent is null)
        {
            return "Battery: unknown";
        }

        var charge = status.Charging == true ? "charging" : "on battery";
        return $"Battery {status.BatteryPercent}% {charge}";
    }
}
