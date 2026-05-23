/*
Represents a structured reason why an input or image result became KO.
Fields still need to be defined in jbtodo.md.
*/

public class ImportException : PrismIOException
{
    public ImportException(string message, Exception? inner = null)
        : base(message, inner) { }
}