using System.Net.Http.Json;
using System.Text.Json;

namespace Prism.Core;

/// <summary>
/// Shared JSON settings and POST helper for the HTTP service clients. PascalCase property names match the
/// host's <c>ConfigureHttpJsonOptions</c> and the API, so the wire format is identical in both directions.
/// </summary>
internal static class ServiceHttp
{
    /// <summary>Serializer options shared by every service client. Null naming policy = PascalCase.</summary>
    internal static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = null };

    /// <summary>
    /// Creates the HttpClient for a service client. Transport timeout is infinite: a stage POST legitimately
    /// runs for many minutes (CLIP classify, Real-ESRGAN warmup), and HttpClient's 100s default killed
    /// distributed runs mid-stage. Stage lifetime is governed by the job's CancellationToken instead.
    /// </summary>
    internal static HttpClient CreateClient(Uri baseAddress) =>
        new() { BaseAddress = baseAddress, Timeout = Timeout.InfiniteTimeSpan };

    /// <summary>
    /// POSTs <paramref name="body"/> as JSON to <paramref name="route"/> and deserializes the response.
    /// Throws when the host returns a non-success status or an empty body.
    /// </summary>
    internal static async Task<TResult> PostJson<TBody, TResult>(
        HttpClient client,
        string route,
        TBody body,
        CancellationToken cancellationToken)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(route, body, Json, cancellationToken);
        response.EnsureSuccessStatusCode();

        TResult? result = await response.Content.ReadFromJsonAsync<TResult>(Json, cancellationToken);
        return result ?? throw new InvalidOperationException($"PRISM service at '{route}' returned an empty response.");
    }
}
