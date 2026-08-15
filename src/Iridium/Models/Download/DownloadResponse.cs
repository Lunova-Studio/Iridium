namespace Iridium.Models.Download;

public sealed record DownloadResponse {
    public int SuccessCount { get; init; }
    public int FailCount { get; init; }
    public IReadOnlyList<Exception> Exceptions { get; init; } = [];
}

public sealed record DownloadRequest {
    public string Url { get; init; } = string.Empty;
    public string LocalPath { get; init; } = string.Empty;
    
    public long Size { get; init; }
    
    public FileInfo FileInfo => new(LocalPath);
    
    public Action<EventArgs>? Completed { get; init; }
    public Action<ResourceDownloadProgressChangedEventArgs>? ProgressChanged { get; init; }
}