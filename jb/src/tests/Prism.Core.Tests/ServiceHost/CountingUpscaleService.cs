namespace PrismCoreTests.ServiceHost;

/// <summary>
/// IUpscaleService decorator counting how often the remote path is actually invoked — guards the
/// remote-upscale routing test against passing vacuously through the local static session.
/// </summary>
internal sealed class CountingUpscaleService : IUpscaleService
{
    private readonly IUpscaleService inner;
    private int calls;

    public CountingUpscaleService(IUpscaleService inner) => this.inner = inner;

    public int Calls => calls;

    public async Task<byte[]> UpscaleAsync(byte[] imageBytes, double scaleFactor, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref calls);
        return await inner.UpscaleAsync(imageBytes, scaleFactor, cancellationToken);
    }
}
