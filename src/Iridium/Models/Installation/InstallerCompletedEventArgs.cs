namespace Iridium.Models.Installation;

public sealed class InstallerCompletedEventArgs : EventArgs {
    public bool IsSuccess { get; init; }
    public Exception? Exception { get; init; }
}