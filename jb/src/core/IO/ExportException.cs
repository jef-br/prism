/*
Represents a structured reason why an export image became KO.
Fields still need to be defined in jbtodo.md.
*/

public class ExportException : PrismIOException
{
    public ExportException(string message, Exception? inner = null)
        : base(message, inner) { }
}