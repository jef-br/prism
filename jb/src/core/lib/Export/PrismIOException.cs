namespace Prism.Lib.Export;

public abstract class PrismIOException : Exception {
    protected PrismIOException(
        string message,
        Exception? inner = null)
        : base(message, inner) { }
}
