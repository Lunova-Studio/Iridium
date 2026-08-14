using System.Diagnostics;
using Iridium.Models.Launch;
using Iridium.Models.Minecraft;

namespace Iridium.Launch;

public sealed class MinecraftProcess : IDisposable {
    public Process? Process { get; private set; }
    public IEnumerable<string> ArgumentList { get; init; }
    public IReadOnlyList<MinecraftLibrary>? Natives { get; private set; }
    public nint MainWindowHandle => Process!.MainWindowHandle;

    public event EventHandler? Started;
    public event EventHandler<EventArgs>? Exited;
    public event EventHandler<LogReceivedEventArgs>? OutputLogReceived;

    public MinecraftProcess(Process process, IEnumerable<string> launchArgs) {
        ArgumentList = launchArgs;
        if (!ArgumentList.Any())
            return;

        Process = process;
        Process.Exited += OnMinecraftProcessExited;
        Process.ErrorDataReceived += OnOutputDataReceived;
        Process.OutputDataReceived += OnOutputDataReceived;

        Start();
    }

    public void Start() {
        Process!.BeginOutputReadLine();
        Process.BeginErrorReadLine();
        Started?.Invoke(this, EventArgs.Empty);
    }

    public void Close() {
        Process!.Kill();
    }

    public void Dispose() => Process?.Dispose();

    private void OnMinecraftProcessExited(object? sender, EventArgs e) {
        Exited?.Invoke(this, EventArgs.Empty);
    }

    private void OnOutputDataReceived(object? sender, DataReceivedEventArgs e) {
        if (!string.IsNullOrEmpty(e.Data)) 
            OutputLogReceived?.Invoke(this, new LogReceivedEventArgs(e.Data));
    }
}

public record LogReceivedEventArgs(string Data);

// public sealed class MinecraftProcess : IDisposable {
//     private readonly Process _process;
//     private readonly CancellationTokenSource _cts = new();
//     private readonly Lock _lock = new();
//     private readonly List<string> _lines = [];
//     private readonly Task _monitorTask;
//
//     public event EventHandler<MinecraftLogEventArgs>? Log;
//     public event EventHandler<MinecraftExitedEventArgs>? Exited;
//
//     public IReadOnlyList<string> CommandLine { get; }
//     public bool IsRunning { get; private set; } = true;
//     public int ExitCode => _process.HasExited ? _process.ExitCode : 0;
//
//     public IReadOnlyList<string> Lines {
//         get {
//             lock (_lock)
//                 return [.. _lines];
//         }
//     }
//
//     private MinecraftProcess(Process process, IReadOnlyList<string> commandLine) {
//         _process = process;
//         CommandLine = commandLine;
//         _monitorTask = MonitorAsync();
//     }
//
//     internal static MinecraftProcess Start(Process process, IReadOnlyList<string> commandLine)
//         => new(process, commandLine);
//
//     public Task WaitForExitAsync(CancellationToken cancellationToken = default)
//         => _monitorTask.WaitAsync(cancellationToken);
//
//     public void Close() {
//         _cts.Cancel();
//         if (!_process.HasExited)
//             _process.Kill();
//     }
//
//     public void Dispose() {
//         Close();
//         _process.Dispose();
//     }
//
//     private async Task MonitorAsync() {
//         try {
//             var stdout = PumpAsync(_process.StandardOutput, isError: false);
//             var stderr = PumpAsync(_process.StandardError, isError: true);
//
//             await Task.WhenAll(stdout, stderr);
//             await _process.WaitForExitAsync(_cts.Token);
//         } catch (OperationCanceledException) {
//         } finally {
//             IsRunning = false;
//             var exitCode = _process.HasExited ? _process.ExitCode : -1;
//             Exited?.Invoke(this, new MinecraftExitedEventArgs(exitCode));
//         }
//     }
//
//     private async Task PumpAsync(StreamReader reader, bool isError) {
//         try {
//             while (true) {
//                 var line = await reader.ReadLineAsync(_cts.Token).ConfigureAwait(false);
//                 if (line is null)
//                     break;
//
//                 lock (_lock)
//                     _lines.Add(line);
//
//                 Log?.Invoke(this, new MinecraftLogEventArgs(line, isError));
//             }
//         } catch (OperationCanceledException) {
//         } catch (IOException) {
//         }
//     }
// }

public sealed class MinecraftLogEventArgs(string line, bool isError) : EventArgs {
    public string Line { get; } = line;
    public bool IsError { get; } = isError;
}

public sealed class MinecraftExitedEventArgs(int exitCode) : EventArgs {
    public int ExitCode { get; } = exitCode;
}
