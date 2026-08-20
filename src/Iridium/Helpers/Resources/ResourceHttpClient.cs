using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Flurl.Http;
using Flurl.Http.Configuration;
using Iridium.Enums.Resources;
using Iridium.Models.Resources;

namespace Iridium.Helpers.Resources;


public sealed class ResourceHttpClient : IDisposable {
    private readonly IFlurlClient _client;
    private readonly ResourceApiOptions _options;
    private readonly ConcurrentDictionary<string, SourceHealth> _health = new(StringComparer.OrdinalIgnoreCase);
    private long _sequence;

    public ResourceHttpClient(ResourceApiOptions? options = null) {
        _options = options ?? new ResourceApiOptions();
        var handler = new SocketsHttpHandler();
        _client = new FlurlClient(new HttpClient(handler)) {
            Settings = {
                Timeout = _options.Timeout,
                JsonSerializer = new DefaultJsonSerializer(),
                Redirects = { Enabled = true, MaxAutoRedirects = 3 }
            }
        };
    }


    public Task<string> GetStringAsync(string url, CancellationToken cancellationToken = default) =>
        SendAsync(url, HttpMethod.Get, null, cancellationToken);


    public async Task<string?> GetStringOrNullAsync(string url, CancellationToken cancellationToken = default) {
        try {
            return await GetStringAsync(url, cancellationToken);
        } catch (HttpRequestException exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound) {
            return null;
        }
    }


    public async Task<Stream> GetStreamAsync(string url, CancellationToken cancellationToken = default) {
        var (response, _) = await SendCoreAsync(url, HttpMethod.Get, null, cancellationToken);
        return await response.Content.ReadAsStreamAsync(cancellationToken);
    }


    public Task<string> PostJsonAsync(string url, object? body, bool allowMirror = false,
        CancellationToken cancellationToken = default) =>
        SendAsync(url, HttpMethod.Post, body, cancellationToken, allowMirror);

    public void Dispose() => _client.Dispose();

    private async Task<string> SendAsync(string url, HttpMethod method, object? body,
        CancellationToken cancellationToken, bool allowMirror = false) {
        var (response, disposeResponse) = await SendCoreAsync(url, method, body, cancellationToken, allowMirror);
        try {
            return await response.Content.ReadAsStringAsync(cancellationToken);
        } finally {
            if (disposeResponse) response.Dispose();
        }
    }

    private async Task<(HttpResponseMessage Response, bool DisposeResponse)> SendCoreAsync(
        string url, HttpMethod method, object? body, CancellationToken cancellationToken, bool allowMirror = false) {
        var candidates = ResolveCandidates(url, method, allowMirror);
        var rawBody = body is null ? null : JsonSerializer.SerializeToUtf8Bytes(body);
        Exception? lastError = null;
        HttpResponseMessage? previous = null;

        for (var attempt = 0; attempt < candidates.Count; attempt++) {
            var candidate = candidates[attempt];
            var started = Stopwatch.GetTimestamp();
            HttpResponseMessage? current = null;
            try {
                using var request = BuildRequest(candidate, method, rawBody);
                current = await _client.HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                if (current.IsSuccessStatusCode) {
                    RecordSuccess(candidate, Stopwatch.GetElapsedTime(started));
                    previous?.Dispose();
                    return (current, DisposeResponse: true);
                }

                RecordFailure(candidate);
                lastError = new HttpRequestException(
                    $"HTTP {(int)current.StatusCode} from {new Uri(candidate).Host}", null, current.StatusCode);
            } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                throw;
            } catch (Exception exception) {
                lastError = exception;
                RecordFailure(candidate);
            } finally {
                previous?.Dispose();
                previous = current;
            }

            if (attempt + 1 < candidates.Count)
                await Task.Delay(TimeSpan.FromMilliseconds(250 * (attempt + 1)), cancellationToken).ConfigureAwait(false);
        }

        previous?.Dispose();
        throw lastError ?? new HttpRequestException("All download sources failed.");
    }

    private IReadOnlyList<string> ResolveCandidates(string url, HttpMethod method, bool allowMirror) {
        if (method == HttpMethod.Get || allowMirror)
            return OrderByMode(url);
        return [url];
    }

    private IReadOnlyList<string> OrderByMode(string url) {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            return [url];

        var mirrors = _options.Mirrors
            .Select(mirror => mirror.TryRewrite(url))
            .Where(mirror => mirror is not null &&
                             !mirror.Equals(url, StringComparison.OrdinalIgnoreCase))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (mirrors.Length == 0)
            return [url];

        return _options.Mode switch {
            ResourceDownloadMode.OfficialOnly => [url],
            ResourceDownloadMode.MirrorPreferred => [..mirrors, url],
            ResourceDownloadMode.OfficialPreferred => [url, ..mirrors],
            _ => OrderByHealth(url, mirrors)
        };
    }

    private IReadOnlyList<string> OrderByHealth(string official, IReadOnlyList<string> mirrors) {
        var officialHealth = GetHealth(official);
        if (officialHealth.HasSamples || mirrors.Any(mirror => GetHealth(mirror).HasSamples)) {
            var bestMirror = mirrors
                .Select(mirror => (Url: mirror, Health: GetHealth(mirror)))
                .OrderBy(item => item.Health.Failures)
                .ThenBy(item => item.Health.AverageDuration)
                .First();
            if (officialHealth.Failures != bestMirror.Health.Failures)
                return officialHealth.Failures < bestMirror.Health.Failures ? [official, bestMirror.Url] : [bestMirror.Url, official];
            if (officialHealth.AverageDuration != bestMirror.Health.AverageDuration)
                return officialHealth.AverageDuration <= bestMirror.Health.AverageDuration ? [official, bestMirror.Url] : [bestMirror.Url, official];
        }

        return Interlocked.Increment(ref _sequence) % 2 == 0 ? [official, ..mirrors] : [..mirrors, official];
    }

    private HttpRequestMessage BuildRequest(string url, HttpMethod method, byte[]? rawBody) {
        var request = new HttpRequestMessage(method, url);
        if (!string.IsNullOrWhiteSpace(_options.UserAgent))
            request.Headers.TryAddWithoutValidation("User-Agent", _options.UserAgent);
        if (IsCurseForgeHost(url) && !string.IsNullOrWhiteSpace(_options.CurseForgeApiKey))
            request.Headers.TryAddWithoutValidation("x-api-key", _options.CurseForgeApiKey);
        if (rawBody is not null) {
            request.Content = new ByteArrayContent(rawBody);
            request.Content.Headers.ContentType = new("application/json");
        }

        return request;
    }

    private static bool IsCurseForgeHost(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        uri.Host.Equals("api.curseforge.com", StringComparison.OrdinalIgnoreCase);

    private SourceHealth GetHealth(string url) =>
        _health.TryGetValue(GetHealthKey(url), out var health) ? health : SourceHealth.Empty;

    private void RecordSuccess(string url, TimeSpan duration) {
        var health = _health.GetOrAdd(GetHealthKey(url), static _ => new SourceHealth());
        lock (health) {
            health.Successes++;
            health.Failures = 0;
            health.AverageDuration = health.Successes == 1
                ? duration
                : TimeSpan.FromTicks((long)(health.AverageDuration.Ticks * 0.75 + duration.Ticks * 0.25));
        }
    }

    private void RecordFailure(string url) {
        var health = _health.GetOrAdd(GetHealthKey(url), static _ => new SourceHealth());
        lock (health) health.Failures++;
    }

    private static string GetHealthKey(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Authority.ToLowerInvariant() : url;

    private sealed class SourceHealth {
        public static readonly SourceHealth Empty = new();
        public int Successes;
        public int Failures;
        public TimeSpan AverageDuration = TimeSpan.MaxValue;
        public bool HasSamples => Successes > 0 || Failures > 0;
    }
}
