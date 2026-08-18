using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Threading.Channels;
using Flurl.Http;
using Iridium.Models.Download;
using Microsoft.Win32.SafeHandles;

namespace Iridium.Download;

// internal static class DownloadManager {
//     public static int MaxThread { get; set; } = 128;
//     public static int MaxRetryCount { get; set; } = 3;
//     public static bool IsEnableFragment { get; set; } = true;
// }

public sealed class DefaultDownloader : IDisposable {
    private const long MultipartThreshold = 16L * 1024 * 1024;
    private const long SegmentSize = 8L * 1024 * 1024;

    private const int MultipartConcurrency = 16;
    private const int QueueMultiplier = 8;
    private const int BufferSize = 64 * 1024;
    
    private readonly SemaphoreSlim _globalSemaphore;
    private readonly CancellationTokenSource _disposeCts;

    private readonly bool _isEnableFragment;
    
    private readonly int _maxConcurrency;
    private readonly int _maxRetryCount;
    
    private int _disposed;
    
    public DefaultDownloader(int maxConcurrency = 32, int maxRetryCount = 3, bool isEnableFragment = true) {
        if (maxConcurrency <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxConcurrency), maxConcurrency, 
                "Max concurrency must be greater than zero.");

        _maxConcurrency = maxConcurrency;
        _maxRetryCount = Math.Max(1, maxRetryCount);
        _isEnableFragment =  isEnableFragment;
        
        _globalSemaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        _disposeCts = new CancellationTokenSource();
    }
    
    public async Task<DownloadResponse> DownloadAsync(DownloadRequest request, CancellationToken cancellationToken = default) {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(request);
    
            using var linkedCts = CancellationTokenSource
                .CreateLinkedTokenSource(cancellationToken, _disposeCts.Token);
    
            for (var attempt = 0; attempt < _maxRetryCount; attempt++)
                try {
                     await DownloadFileAsync(request, null, linkedCts.Token)
                         .ConfigureAwait(false);
    
                    request.Completed?.Invoke(EventArgs.Empty);
    
                    return new DownloadResponse {
                        SuccessCount = 1,
                        FailCount = 0
                    };
                } catch (OperationCanceledException) when (linkedCts.IsCancellationRequested) {
                    return new DownloadResponse {
                        SuccessCount = 0,
                        FailCount = 0
                    };
                } catch (Exception ex) {
                    if (attempt >= _maxRetryCount - 1)
                        return new DownloadResponse {
                            SuccessCount = 0,
                            FailCount = 1,
                            Exceptions = [ex]
                        };
    
                    await DelayBeforeRetryAsync(attempt, linkedCts.Token)
                        .ConfigureAwait(false);
                }
    
            return new DownloadResponse {
                SuccessCount = 0,
                FailCount = 1
            };
        }
    
    public async Task<DownloadResponse> DownloadManyAsync(
        IReadOnlyList<DownloadRequest> requests,
        Action<ResourceDownloadProgressChangedEventArgs>? onProgress = null,
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(requests);

        if (requests.Count == 0) {
            onProgress?.Invoke(new ResourceDownloadProgressChangedEventArgs {
                CompletedCount = 0,
                TotalCount = 0,
                CurrentFileName = null
            });

            return new DownloadResponse {
                SuccessCount = 0,
                FailCount = 0
            };
        }

        using var linkedCts = CancellationTokenSource
            .CreateLinkedTokenSource(cancellationToken, _disposeCts.Token);

        var progress = new DownloadProgress(requests.Count, onProgress);
        var queueCapacity = Math.Clamp(checked(_maxConcurrency * QueueMultiplier), 32, 1024);

        Channel<DownloadRequest> channel =
            Channel.CreateBounded<DownloadRequest>(
                new BoundedChannelOptions(queueCapacity) {
                    SingleWriter = true,
                    SingleReader = false,
                    FullMode = BoundedChannelFullMode.Wait,
                    AllowSynchronousContinuations = false
                });

        var workerCount = Math.Min(_maxConcurrency, requests.Count);
        Task[] workers = new Task[workerCount];

        for (var i = 0; i < workerCount; i++)
            workers[i] = RunWorkerAsync(channel.Reader, progress, linkedCts.Token);

        try {
            foreach (var request in requests)
                await channel.Writer.WriteAsync(request, linkedCts.Token)
                    .ConfigureAwait(false);

            channel.Writer.TryComplete();

            await Task.WhenAll(workers)
                .ConfigureAwait(false);
        } catch {
            await linkedCts.CancelAsync();
            channel.Writer.TryComplete();

            try {
                await Task.WhenAll(workers).ConfigureAwait(false);
            } catch {
                // ignored
            }

            throw;
        } finally {
            channel.Writer.TryComplete();
        }
        
        onProgress?.Invoke(new ResourceDownloadProgressChangedEventArgs {
            TotalCount = progress.TotalCount,
            CompletedCount = progress.CompletedCount,
            CurrentFileName = null
        });

        return new DownloadResponse {
            SuccessCount = progress.CompletedCount,
            FailCount = progress.TotalCount - progress.CompletedCount,
            Exceptions = progress.GetExceptions()
        };
    }

    private async Task RunWorkerAsync(
        ChannelReader<DownloadRequest> reader,
        DownloadProgress progress,
        CancellationToken cancellationToken) {
        var singlePartBuffer = ArrayPool<byte>.Shared.Rent(BufferSize);

        try {
            await foreach (var request in reader.ReadAllAsync(cancellationToken).ConfigureAwait(false)) {
                try {
                    await DownloadInBatchAsync(request, progress, singlePartBuffer, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                    return;
                }
                catch (Exception ex) {
                    progress.AddException(ex);
                }
            }
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
        } finally {
            ArrayPool<byte>.Shared.Return(singlePartBuffer);
        }
    }

    private async Task DownloadInBatchAsync(
        DownloadRequest request,
        DownloadProgress progress,
        byte[] singlePartBuffer,
        CancellationToken cancellationToken) {
        var directory = Path.GetDirectoryName(request.FileInfo.FullName);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        Exception? lastException = null;

        for (var attempt = 0; attempt < _maxRetryCount; attempt++) {
            try {
                await DownloadFileAsync(request, singlePartBuffer, cancellationToken)
                    .ConfigureAwait(false);
                
                progress.CompleteFile(request.FileInfo.Name);
                request.Completed?.Invoke(EventArgs.Empty);
                return;
            } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                return;
            } catch (Exception ex) {
                lastException = ex;
                if (attempt >= _maxRetryCount - 1)
                    break;

                await DelayBeforeRetryAsync(attempt, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        if (lastException is not null)
            progress.AddException(lastException);
    }

    private async Task DownloadFileAsync(
        DownloadRequest request,
        byte[]? singlePartBuffer,
        CancellationToken cancellationToken) {
        var directory = Path.GetDirectoryName(request.FileInfo.FullName);

        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var preparation = await PrepareForDownloadAsync(request.Url, cancellationToken)
            .ConfigureAwait(false);

        var totalBytes = preparation.ContentLength ?? request.Size;
        if (totalBytes < 0)
            totalBytes = 0;

        var context = new DownloadContext(preparation.FinalUrl, request.FileInfo.FullName, totalBytes, SegmentSize);
        var shouldUseMultipart = 
            _isEnableFragment && totalBytes >= MultipartThreshold;

        if (shouldUseMultipart) {
            var supportsRange = preparation.SupportsRanges;

            if (!supportsRange)
                supportsRange = await ValidateRangeSupportAsync(preparation.FinalUrl, cancellationToken)
                    .ConfigureAwait(false);

            if (supportsRange) {
                await DownloadMultiPartAsync(context, cancellationToken)
                    .ConfigureAwait(false);

                return;
            }
        }

        await DownloadSinglePartAsync(context, singlePartBuffer, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task DownloadSinglePartAsync(
        DownloadContext context,
        byte[]? sharedBuffer,
        CancellationToken cancellationToken) {
        await _globalSemaphore.WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        try {
            using var response = await context.Url
                .WithTimeout(TimeSpan.FromSeconds(60))
                .GetAsync(HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            response.ResponseMessage.EnsureSuccessStatusCode();

            await using var contentStream = await response
                .GetStreamAsync()
                .ConfigureAwait(false);

            await using var fileStream = new FileStream(
                context.LocalPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.ReadWrite,
                BufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            if (context.TotalBytes > 0)
                fileStream.SetLength(context.TotalBytes);

            byte[] buffer;
            var returnBuffer = false;

            if (sharedBuffer is not null) {
                buffer = sharedBuffer;
            } else {
                buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
                returnBuffer = true;
            }

            try {
                long receivedBytes = 0;
                while (true) {
                    var bytesRead = await contentStream
                        .ReadAsync(buffer.AsMemory(0, BufferSize), cancellationToken)
                        .ConfigureAwait(false);

                    if (bytesRead <= 0)
                        break;

                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken)
                        .ConfigureAwait(false);

                    receivedBytes += bytesRead;
                }

                if (context.TotalBytes > 0 && receivedBytes != context.TotalBytes)
                    throw new IOException(
                        $"The server returned an incomplete response. Expected {context.TotalBytes} bytes, but received {receivedBytes} bytes.");
            } finally {
                if (returnBuffer)
                    ArrayPool<byte>.Shared.Return(buffer);
            }
        } finally {
            _globalSemaphore.Release();
        }
    }

    private async Task MultipartWorkerAsync(
        DownloadContext context,
        SafeFileHandle fileHandle,
        CancellationToken cancellationToken) {
        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        try {
            while (context.NextSegment() is var (start, end)) {
                await _globalSemaphore
                    .WaitAsync(cancellationToken)
                    .ConfigureAwait(false);

                try {
                    using var response = await context.Url
                        .WithHeader("Range", $"bytes={start}-{end}")
                        .WithTimeout(TimeSpan.FromSeconds(60))
                        .GetAsync(
                            HttpCompletionOption.ResponseHeadersRead,
                            cancellationToken: cancellationToken)
                        .ConfigureAwait(false);

                    var message = response.ResponseMessage;

                    if (message.StatusCode != HttpStatusCode.PartialContent)
                        throw new InvalidOperationException(
                            $"The server did not honor the Range request. Expected 206 Partial Content, but received {(int)message.StatusCode}.");

                    await using var contentStream = await response
                        .GetStreamAsync()
                        .ConfigureAwait(false);

                    var offset = start;
                    var expectedBytes = end - start + 1;
                    var segmentBytes = 0L;

                    while (true) {
                        var bytesRead = await contentStream
                            .ReadAsync(buffer.AsMemory(0, BufferSize), cancellationToken)
                            .ConfigureAwait(false);

                        if (bytesRead <= 0)
                            break;

                        await RandomAccess.WriteAsync(
                                fileHandle,
                                buffer.AsMemory(0, bytesRead),
                                offset,
                                cancellationToken)
                            .ConfigureAwait(false);

                        offset += bytesRead;
                        segmentBytes += bytesRead;
                    }

                    if (segmentBytes != expectedBytes)
                        throw new IOException(
                            $"The server returned an incomplete Range response. Expected {expectedBytes} bytes, but received {segmentBytes} bytes.");
                } finally {
                    _globalSemaphore.Release();
                }
            }
        } finally {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
    
    private async Task DownloadMultiPartAsync(DownloadContext context, CancellationToken cancellationToken) {
        if (context.TotalBytes <= 0)
            return;

        await using var fileStream = new FileStream(
            context.LocalPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.ReadWrite,
            BufferSize,
            FileOptions.Asynchronous | FileOptions.RandomAccess);

        fileStream.SetLength(context.TotalBytes);

        var fileHandle = fileStream.SafeFileHandle;
        var workerCount = Math.Min(MultipartConcurrency, checked((int)Math.Min(context.TotalFragments, int.MaxValue)));

        if (workerCount <= 0)
            return;

        Task[] workers = new Task[workerCount];

        for (var i = 0; i < workerCount; i++)
            workers[i] = MultipartWorkerAsync(context, fileHandle, cancellationToken);

        await Task.WhenAll(workers)
            .ConfigureAwait(false);
    }

    private async Task<DownloadPreparation> PrepareForDownloadAsync(string url, CancellationToken cancellationToken) {
        await _globalSemaphore.WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        try {
            using var response = await url
                .AllowAnyHttpStatus()
                .WithTimeout(TimeSpan.FromSeconds(30))
                .HeadAsync(HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            var message = response.ResponseMessage;
            message.EnsureSuccessStatusCode();

            var finalUrl = message.RequestMessage?.RequestUri?.AbsoluteUri ?? url;
            var contentLength = message.Content.Headers.ContentLength;

            var supportsRanges = message.Headers.AcceptRanges.Any(static value =>
                value.Equals("bytes", StringComparison.OrdinalIgnoreCase));

            return new DownloadPreparation(finalUrl, contentLength, supportsRanges);
        } finally {
            _globalSemaphore.Release();
        }
    }

    private async Task<bool> ValidateRangeSupportAsync(string url, CancellationToken cancellationToken) {
        await _globalSemaphore.WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        try {
            using var response = await url
                .AllowAnyHttpStatus()
                .WithHeader("Range", "bytes=0-0")
                .WithTimeout(TimeSpan.FromSeconds(10))
                .GetAsync(HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            return response.ResponseMessage.StatusCode == HttpStatusCode.PartialContent;
        } catch (OperationCanceledException) {
            throw;
        } catch {
            return false;
        } finally {
            _globalSemaphore.Release();
        }
    }
    
    private static Task DelayBeforeRetryAsync(int attempt, CancellationToken cancellationToken) {
        var milliseconds = checked(1000 * (attempt + 1));
        return Task.Delay(milliseconds, cancellationToken);
    }
    
    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
    
    public void Dispose() {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _disposeCts.Cancel();

        _globalSemaphore.Dispose();
        _disposeCts.Dispose();
    }
    
    private sealed class DownloadContext {
        private long _nextSegment = -1;

        public string Url { get; }
        public string LocalPath { get; }
        public long TotalBytes { get; }
        public long FragmentSize { get; }
        public long TotalFragments { get; }

        public DownloadContext(
            string url,
            string localPath,
            long totalBytes,
            long fragmentSize) {
            Url = url;
            LocalPath = localPath;
            TotalBytes = totalBytes;
            FragmentSize = fragmentSize;

            TotalFragments = totalBytes > 0
                ? (totalBytes + fragmentSize - 1) / fragmentSize
                : 0;
        }

        public (long Start, long End)? NextSegment() {
            var index = Interlocked.Increment(ref _nextSegment);

            if (index >= TotalFragments)
                return null;

            var start = index * FragmentSize;
            var end = Math.Min(start + FragmentSize, TotalBytes) - 1;

            return (start, end);
        }
    }
    
    private sealed class DownloadProgress(
        int totalCount,
        Action<ResourceDownloadProgressChangedEventArgs>? onProgress) {
        private readonly ConcurrentQueue<Exception> _exceptions = new();
        private int _completedCount;

        public int TotalCount { get; } = totalCount;

        public int CompletedCount => Volatile.Read(ref _completedCount);

        public void AddException(Exception exception) {
            ArgumentNullException.ThrowIfNull(exception);
            _exceptions.Enqueue(exception);
        }

        public Exception[] GetExceptions() {
            return [.. _exceptions];
        }

        public void CompleteFile(string fileName) {
            var completedCount = Interlocked.Increment(ref _completedCount);

            onProgress?.Invoke(new ResourceDownloadProgressChangedEventArgs {
                CompletedCount = completedCount,
                TotalCount = TotalCount,
                CurrentFileName = fileName
            });
        }
    }
    
    private readonly struct DownloadPreparation {
        public string FinalUrl { get; }
        public bool SupportsRanges { get; }
        public long? ContentLength { get; }

        public DownloadPreparation(string finalUrl, long? contentLength, bool supportsRanges) {
            FinalUrl = finalUrl;
            ContentLength = contentLength;
            SupportsRanges = supportsRanges;
        }
    }
}