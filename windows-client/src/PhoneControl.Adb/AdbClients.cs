using System.Diagnostics;
using System.Text.RegularExpressions;
using PhoneControl.Domain;

namespace PhoneControl.Adb;

public sealed record AdbProcessResult(bool Available, int ExitCode, string StandardOutput)
{
    public static AdbProcessResult Unavailable { get; } = new(false, -1, string.Empty);
}

public interface IAdbProcessRunner
{
    /// <summary>Runs only <c>adb devices -l</c>. Implementations must not issue USB-mode commands.</summary>
    Task<AdbProcessResult> RunDevicesListAsync(CancellationToken cancellationToken);
}

public static class AdbDeviceParser
{
    private static readonly Regex LinePattern = new(
        @"^(?<serial>\S+)\s+(?<state>device|offline|unauthorized|no permissions)(?:\s+(?<rest>.*))?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static IReadOnlyList<AdbDevice> Parse(string output)
    {
        var devices = new List<AdbDevice>();
        if (string.IsNullOrWhiteSpace(output))
        {
            return devices;
        }

        foreach (var raw in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.Trim();
            if (line.StartsWith("List of devices", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var match = LinePattern.Match(line);
            if (!match.Success)
            {
                continue;
            }

            var rest = match.Groups["rest"].Value;
            string? model = null;
            var modelToken = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(p => p.StartsWith("model:", StringComparison.OrdinalIgnoreCase));
            if (modelToken is not null)
            {
                model = modelToken["model:".Length..].Replace('_', ' ');
            }

            devices.Add(new AdbDevice(match.Groups["serial"].Value, match.Groups["state"].Value, model));
        }

        return devices;
    }
}

public sealed class SystemAdbProcessRunner : IAdbProcessRunner
{
    private readonly string _adbPath;

    public SystemAdbProcessRunner(string adbPath = "adb")
    {
        _adbPath = adbPath;
    }

    public async Task<AdbProcessResult> RunDevicesListAsync(CancellationToken cancellationToken)
    {
        try
        {
            var start = new ProcessStartInfo
            {
                FileName = _adbPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            start.ArgumentList.Add("devices");
            start.ArgumentList.Add("-l");

            using var process = Process.Start(start);
            if (process is null)
            {
                return AdbProcessResult.Unavailable;
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));
            try
            {
                await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                    // Already exited.
                }

                return AdbProcessResult.Unavailable;
            }

            var stdout = await stdoutTask.ConfigureAwait(false);
            _ = await stderrTask.ConfigureAwait(false);
            return new AdbProcessResult(true, process.ExitCode, stdout);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return AdbProcessResult.Unavailable;
        }
        catch (FileNotFoundException)
        {
            return AdbProcessResult.Unavailable;
        }
    }
}

/// <summary>
/// ADB is optional bootstrap. The only process invocation is <c>adb devices -l</c>.
/// </summary>
public sealed class ProcessAdbClient : IAdbClient
{
    private readonly IAdbProcessRunner _runner;

    public ProcessAdbClient(IAdbProcessRunner runner)
    {
        _runner = runner;
    }

    public ProcessAdbClient(string adbPath = "adb")
        : this(new SystemAdbProcessRunner(adbPath))
    {
    }

    public async Task<IReadOnlyList<AdbDevice>> ListDevicesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = await _runner.RunDevicesListAsync(cancellationToken).ConfigureAwait(false);
        if (!result.Available || result.ExitCode != 0)
        {
            return Array.Empty<AdbDevice>();
        }

        return AdbDeviceParser.Parse(result.StandardOutput);
    }
}

public sealed class FakeAdbClient : IAdbClient
{
    public IReadOnlyList<AdbDevice> Devices { get; init; } = Array.Empty<AdbDevice>();

    public Task<IReadOnlyList<AdbDevice>> ListDevicesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Devices);
    }
}
