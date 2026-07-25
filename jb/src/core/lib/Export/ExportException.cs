namespace Prism.Lib.Export;

/*
Represents a structured reason why an export image became KO.

*/

public class ExportException : PrismIOException {
    public ExportException(string message, Exception? inner = null)
        : base(message, inner) { }
}
