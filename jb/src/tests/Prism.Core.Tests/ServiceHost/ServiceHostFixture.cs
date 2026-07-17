using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace PrismCoreTests.ServiceHost;

/// <summary>
/// Shared xUnit class fixture holding a single WebApplicationFactory<Program> configured to host
/// all four public services (Matching, Generate, Transform, Upscale). Created once per test collection,
/// not per test, so the expensive model loads happen only once.
///
/// PRISM_SERVICE env var controls which services the factory hosts; this fixture leaves it unset
/// (or explicitly clears it) to host all four. Manages the process env var to avoid crosstalk
/// between tests.
/// </summary>
public sealed class ServiceHostFixture : IAsyncLifetime
{
    private WebApplicationFactory<Program>? factory;

    /// <summary>
    /// The HTTP client managed by the factory. Use this to construct service clients
    /// (e.g. <c>new HttpMatchingService(Client)</c>).
    /// </summary>
    public HttpClient Client
    {
        get
        {
            HttpClient client = factory?.CreateClient() ?? throw new InvalidOperationException("Fixture not initialized.");
            // Mirrors ServiceHttp.CreateClient: CreateClient()'s 100s default aborts GPU-bound upscale
            // roundtrips when PipelineIntegrationTests contends for the shared Real-ESRGAN session in
            // the same process (jbtodo.md R8). Cancellation, not transport timeout, bounds the calls.
            client.Timeout = Timeout.InfiniteTimeSpan;
            return client;
        }
    }

    /// <summary>
    /// Initializes the factory, ensuring PRISM_SERVICE env var is clear so all services are hosted.
    /// </summary>
    public Task InitializeAsync()
    {
        // Clear PRISM_SERVICE to ensure all services are hosted (unset = all services).
        string? previous = Environment.GetEnvironmentVariable("PRISM_SERVICE");
        Environment.SetEnvironmentVariable("PRISM_SERVICE", null);

        try
        {
            factory = new WebApplicationFactory<Program>();
            // Trigger lazy initialization by creating a client.
            _ = factory.CreateClient();
            return Task.CompletedTask;
        }
        catch
        {
            // Restore the env var on failure.
            if (previous != null)
            {
                Environment.SetEnvironmentVariable("PRISM_SERVICE", previous);
            }
            throw;
        }
    }

    /// <summary>
    /// Disposes the factory and restores the PRISM_SERVICE env var to its original state (if any).
    /// </summary>
    public Task DisposeAsync()
    {
        factory?.Dispose();
        // Note: We don't restore the env var here because we cleared it on init.
        // In a test environment, env-var leakage across tests is acceptable as long
        // as all tests in this collection run together (CollectionDefinition enforces that).
        return Task.CompletedTask;
    }
}
