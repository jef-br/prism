namespace Prism.Lib.Ingress;

/// <summary>
/// Holds the result of a completed WeTransfer download.
/// Dispose to release the underlying stream and delete the backing temp file.
/// </summary>
internal sealed class WeTransferDownloadResult : IAsyncDisposable {
    private readonly string _tempFilePath;
    private bool _disposed;

    /// <summary>Open read stream over the downloaded file.</summary>
    public Stream Content { get; }

    /// <summary>File name as reported by the browser (e.g. "archive.zip").</summary>
    public string FileName { get; }

    /// <summary>Total file size in bytes, or null if it could not be determined before download.</summary>
    public long? TotalBytes { get; }

    internal WeTransferDownloadResult(Stream content, string fileName, long? totalBytes, string tempFilePath) {
        this.Content = content;
        this.FileName = fileName;
        this.TotalBytes = totalBytes;
        this._tempFilePath = tempFilePath;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync() {
        if (this._disposed) {
            return;
        }

        this._disposed = true;
        await this.Content.DisposeAsync();
        try {
            File.Delete(this._tempFilePath);
        }
        catch { }
    }
}
