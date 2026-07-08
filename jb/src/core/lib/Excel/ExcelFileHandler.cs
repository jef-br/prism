using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

namespace Prism.Lib.Excel;

/// <summary>
/// Loads worksheet data from supported Excel-like files into simple row and cell objects.
/// </summary>
public sealed class ExcelFileHandler
{
    private static readonly XNamespace SpreadsheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace RelationshipNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PackageRelationshipNamespace = "http://schemas.openxmlformats.org/package/2006/relationships";

    /// <summary>
    /// Loads one workbook from disk.
    /// </summary>
    /// <param name="filePath">Path to an .xlsx, .csv, .tsv, or .txt file.</param>
    /// <returns>The workbook representation used by ModelBuilder.</returns>
    public ExcelWorkbook LoadWorkbook(string filePath)
    {
        ValidateWorkbookPath(filePath);

        string extension = Path.GetExtension(filePath);

        if (extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return LoadOpenXmlWorkbook(filePath);
        }

        if (extension.Equals(".csv", StringComparison.OrdinalIgnoreCase))
        {
            return LoadDelimitedWorkbook(filePath, ',');
        }

        if (extension.Equals(".tsv", StringComparison.OrdinalIgnoreCase) || extension.Equals(".txt", StringComparison.OrdinalIgnoreCase))
        {
            return LoadDelimitedWorkbook(filePath, '\t');
        }

        throw new NotSupportedException($"Unsupported Excel input type '{extension}'. Supported types are .xlsx, .csv, .tsv, and .txt.");
    }

    private static void ValidateWorkbookPath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("Excel file path is required.", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Excel file was not found.", filePath);
        }
    }

    private static ExcelWorkbook LoadOpenXmlWorkbook(string filePath)
    {
        // FileShare.ReadWrite so we can read a workbook the user still has open in Excel
        // (ZipFile.OpenRead uses FileShare.Read, which Excel's lock denies).
        using FileStream workbookStream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using ZipArchive archive = new(workbookStream, ZipArchiveMode.Read);
        ZipArchiveEntry workbookEntry = archive.GetEntry("xl/workbook.xml")
            ?? throw new InvalidOperationException($"Workbook '{filePath}' does not contain xl/workbook.xml.");
        ZipArchiveEntry relationshipsEntry = archive.GetEntry("xl/_rels/workbook.xml.rels")
            ?? throw new InvalidOperationException($"Workbook '{filePath}' does not contain xl/_rels/workbook.xml.rels.");

        XDocument workbookDocument = LoadXml(workbookEntry);
        XDocument relationshipsDocument = LoadXml(relationshipsEntry);
        IReadOnlyList<string> sharedStrings = LoadSharedStrings(archive);
        Dictionary<string, string> relationshipTargets = LoadRelationshipTargets(relationshipsDocument);
        List<ExcelWorksheet> worksheets = [];

        IEnumerable<XElement> sheetElements = workbookDocument
            .Root?
            .Element(SpreadsheetNamespace + "sheets")?
            .Elements(SpreadsheetNamespace + "sheet")
            ?? [];

        foreach (XElement sheetElement in sheetElements)
        {
            string worksheetName = sheetElement.Attribute("name")?.Value ?? "Worksheet";
            string relationshipId = sheetElement.Attribute(RelationshipNamespace + "id")?.Value ?? string.Empty;

            if (!relationshipTargets.TryGetValue(relationshipId, out string? worksheetTarget))
            {
                throw new InvalidOperationException($"Worksheet '{worksheetName}' in '{filePath}' has no workbook relationship target.");
            }

            string worksheetPath = NormalizeWorkbookTargetPath(worksheetTarget);
            ZipArchiveEntry worksheetEntry = archive.GetEntry(worksheetPath)
                ?? throw new InvalidOperationException($"Worksheet '{worksheetName}' in '{filePath}' points to missing part '{worksheetPath}'.");

            worksheets.Add(LoadOpenXmlWorksheet(filePath, worksheetName, worksheetEntry, sharedStrings));
        }

        return new ExcelWorkbook(filePath, worksheets);
    }

    private static XDocument LoadXml(ZipArchiveEntry entry)
    {
        using Stream stream = entry.Open();
        return XDocument.Load(stream);
    }

    private static IReadOnlyList<string> LoadSharedStrings(ZipArchive archive)
    {
        ZipArchiveEntry? sharedStringsEntry = archive.GetEntry("xl/sharedStrings.xml");

        if (sharedStringsEntry is null)
        {
            return [];
        }

        XDocument sharedStringsDocument = LoadXml(sharedStringsEntry);

        return sharedStringsDocument
            .Descendants(SpreadsheetNamespace + "si")
            .Select(ReadSharedString)
            .ToArray();
    }

    private static string ReadSharedString(XElement sharedStringElement)
    {
        XElement? directText = sharedStringElement.Element(SpreadsheetNamespace + "t");

        if (directText is not null)
        {
            return directText.Value;
        }

        return string.Concat(sharedStringElement
            .Descendants(SpreadsheetNamespace + "t")
            .Select(textElement => textElement.Value));
    }

    private static Dictionary<string, string> LoadRelationshipTargets(XDocument relationshipsDocument)
    {
        return relationshipsDocument
            .Root?
            .Elements(PackageRelationshipNamespace + "Relationship")
            .Where(element => element.Attribute("Id") is not null && element.Attribute("Target") is not null)
            .ToDictionary(
                element => element.Attribute("Id")!.Value,
                element => element.Attribute("Target")!.Value,
                StringComparer.OrdinalIgnoreCase)
            ?? [];
    }

    private static string NormalizeWorkbookTargetPath(string relationshipTarget)
    {
        string normalizedTarget = relationshipTarget.Replace('\\', '/').TrimStart('/');

        if (normalizedTarget.StartsWith("xl/", StringComparison.OrdinalIgnoreCase))
        {
            return normalizedTarget;
        }

        return $"xl/{normalizedTarget}";
    }

    private static ExcelWorksheet LoadOpenXmlWorksheet(
        string sourceFile,
        string worksheetName,
        ZipArchiveEntry worksheetEntry,
        IReadOnlyList<string> sharedStrings)
    {
        XDocument worksheetDocument = LoadXml(worksheetEntry);
        Dictionary<int, Dictionary<int, string>> rowValues = [];

        IEnumerable<XElement> rowElements = worksheetDocument
            .Descendants(SpreadsheetNamespace + "sheetData")
            .Elements(SpreadsheetNamespace + "row");

        foreach (XElement rowElement in rowElements)
        {
            int rowIndex = ReadOneBasedIndex(rowElement.Attribute("r")?.Value) - 1;

            if (rowIndex < 0)
            {
                continue;
            }

            Dictionary<int, string> cells = [];

            foreach (XElement cellElement in rowElement.Elements(SpreadsheetNamespace + "c"))
            {
                string cellReference = cellElement.Attribute("r")?.Value ?? string.Empty;
                int columnIndex = ReadColumnIndex(cellReference);

                if (columnIndex < 0)
                {
                    continue;
                }

                cells[columnIndex] = ReadOpenXmlCellValue(cellElement, sharedStrings);
            }

            rowValues[rowIndex] = cells;
        }

        ApplyVerticalMergedCells(worksheetDocument, rowValues);

        return BuildWorksheet(sourceFile, worksheetName, rowValues);
    }

    private static string ReadOpenXmlCellValue(XElement cellElement, IReadOnlyList<string> sharedStrings)
    {
        string cellType = cellElement.Attribute("t")?.Value ?? string.Empty;

        if (cellType.Equals("inlineStr", StringComparison.OrdinalIgnoreCase))
        {
            return cellElement
                .Descendants(SpreadsheetNamespace + "t")
                .FirstOrDefault()?
                .Value
                .Trim()
                ?? string.Empty;
        }

        string rawValue = cellElement.Element(SpreadsheetNamespace + "v")?.Value ?? string.Empty;

        if (cellType.Equals("s", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int sharedStringIndex)
            && sharedStringIndex >= 0
            && sharedStringIndex < sharedStrings.Count)
        {
            return sharedStrings[sharedStringIndex].Trim();
        }

        return rawValue.Trim();
    }

    private static void ApplyVerticalMergedCells(XDocument worksheetDocument, Dictionary<int, Dictionary<int, string>> rowValues)
    {
        IEnumerable<XElement> mergeElements = worksheetDocument
            .Descendants(SpreadsheetNamespace + "mergeCells")
            .Elements(SpreadsheetNamespace + "mergeCell");

        foreach (XElement mergeElement in mergeElements)
        {
            string reference = mergeElement.Attribute("ref")?.Value ?? string.Empty;
            ExcelRange? range = ExcelRange.TryParse(reference);

            if (range is null || range.StartColumn != range.EndColumn)
            {
                continue;
            }

            if (!TryGetCellValue(rowValues, range.StartRow, range.StartColumn, out string? mergedValue)
                || string.IsNullOrWhiteSpace(mergedValue))
            {
                continue;
            }

            for (int rowIndex = range.StartRow; rowIndex <= range.EndRow; rowIndex++)
            {
                if (!rowValues.TryGetValue(rowIndex, out Dictionary<int, string>? row))
                {
                    row = [];
                    rowValues[rowIndex] = row;
                }

                row[range.StartColumn] = mergedValue;
            }
        }
    }

    private static bool TryGetCellValue(
        Dictionary<int, Dictionary<int, string>> rowValues,
        int rowIndex,
        int columnIndex,
        out string? cellValue)
    {
        cellValue = null;

        if (!rowValues.TryGetValue(rowIndex, out Dictionary<int, string>? row))
        {
            return false;
        }

        return row.TryGetValue(columnIndex, out cellValue);
    }

    private static ExcelWorkbook LoadDelimitedWorkbook(string filePath, char delimiter)
    {
        string[] lines = File.ReadAllLines(filePath);
        Dictionary<int, Dictionary<int, string>> rowValues = [];

        for (int rowIndex = 0; rowIndex < lines.Length; rowIndex++)
        {
            string[] cells = ParseDelimitedLine(lines[rowIndex], delimiter);
            Dictionary<int, string> row = [];

            for (int columnIndex = 0; columnIndex < cells.Length; columnIndex++)
            {
                row[columnIndex] = cells[columnIndex].Trim();
            }

            rowValues[rowIndex] = row;
        }

        string worksheetName = Path.GetFileNameWithoutExtension(filePath);
        ExcelWorksheet worksheet = BuildWorksheet(filePath, worksheetName, rowValues);

        return new ExcelWorkbook(filePath, [worksheet]);
    }

    private static string[] ParseDelimitedLine(string line, char delimiter)
    {
        List<string> cells = [];
        bool isInsideQuote = false;
        string currentCell = string.Empty;

        for (int index = 0; index < line.Length; index++)
        {
            char currentCharacter = line[index];

            if (currentCharacter == '"')
            {
                bool isEscapedQuote = isInsideQuote && index + 1 < line.Length && line[index + 1] == '"';

                if (isEscapedQuote)
                {
                    currentCell += '"';
                    index++;
                    continue;
                }

                isInsideQuote = !isInsideQuote;
                continue;
            }

            if (currentCharacter == delimiter && !isInsideQuote)
            {
                cells.Add(currentCell);
                currentCell = string.Empty;
                continue;
            }

            currentCell += currentCharacter;
        }

        cells.Add(currentCell);

        return cells.ToArray();
    }

    private static ExcelWorksheet BuildWorksheet(string sourceFile, string worksheetName, Dictionary<int, Dictionary<int, string>> rowValues)
    {
        int lastRowIndex = rowValues.Count == 0 ? -1 : rowValues.Keys.Max();
        int lastColumnIndex = rowValues.Values.SelectMany(row => row.Keys).DefaultIfEmpty(-1).Max();
        List<ExcelWorksheetRow> rows = [];

        for (int rowIndex = 0; rowIndex <= lastRowIndex; rowIndex++)
        {
            Dictionary<int, string> row = rowValues.TryGetValue(rowIndex, out Dictionary<int, string>? existingRow)
                ? existingRow
                : [];

            List<string> cells = [];

            for (int columnIndex = 0; columnIndex <= lastColumnIndex; columnIndex++)
            {
                cells.Add(row.TryGetValue(columnIndex, out string? cellValue) ? cellValue : string.Empty);
            }

            rows.Add(new ExcelWorksheetRow(rowIndex, cells));
        }

        return new ExcelWorksheet(sourceFile, worksheetName, rows);
    }

    private static int ReadOneBasedIndex(string? value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedValue)
            ? parsedValue
            : 0;
    }

    private static int ReadColumnIndex(string cellReference)
    {
        string columnLetters = new(cellReference.TakeWhile(char.IsLetter).ToArray());

        if (string.IsNullOrWhiteSpace(columnLetters))
        {
            return -1;
        }

        int columnNumber = 0;

        foreach (char letter in columnLetters.ToUpperInvariant())
        {
            columnNumber *= 26;
            columnNumber += letter - 'A' + 1;
        }

        return columnNumber - 1;
    }

    private sealed record ExcelRange(int StartRow, int EndRow, int StartColumn, int EndColumn)
    {
        public static ExcelRange? TryParse(string reference)
        {
            string[] parts = reference.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (parts.Length != 2)
            {
                return null;
            }

            int startColumn = ReadColumnIndex(parts[0]);
            int endColumn = ReadColumnIndex(parts[1]);
            int startRow = ReadOneBasedIndex(new string(parts[0].Where(char.IsDigit).ToArray())) - 1;
            int endRow = ReadOneBasedIndex(new string(parts[1].Where(char.IsDigit).ToArray())) - 1;

            if (startColumn < 0 || endColumn < 0 || startRow < 0 || endRow < startRow)
            {
                return null;
            }

            return new ExcelRange(startRow, endRow, startColumn, endColumn);
        }
    }
}

/// <summary>
/// Workbook data loaded from one Excel-like source file.
/// </summary>
public sealed record ExcelWorkbook(string SourceFile, IReadOnlyList<ExcelWorksheet> Worksheets);

/// <summary>
/// Worksheet data represented as ordered rows and cells.
/// </summary>
public sealed record ExcelWorksheet(string SourceFile, string Name, IReadOnlyList<ExcelWorksheetRow> Rows);

/// <summary>
/// One zero-based worksheet row.
/// </summary>
public sealed record ExcelWorksheetRow(int RowIndex, IReadOnlyList<string> Cells);
