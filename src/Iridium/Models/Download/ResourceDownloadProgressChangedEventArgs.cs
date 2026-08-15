namespace Iridium.Models.Download;

public sealed class ResourceDownloadProgressChangedEventArgs : EventArgs {
    public string? CurrentFileName { get; init; }
    
    public int CompletedCount { get; init; }
    public int TotalCount { get; init; }
    
    public double Progress => TotalCount > 0 ? (double)CompletedCount / TotalCount : 0;
}