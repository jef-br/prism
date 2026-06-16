using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>
/// Builds the Internal Excel Model from one or more workbook files.
/// </summary>
public sealed class ModelBuilder
{
    private static readonly Regex NonAlphaNumericPattern = new("[^a-z0-9]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex HeaderTokenPattern = new("[a-z0-9]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly ExcelConfig config;
    private readonly ExcelFileHandler excelFileHandler;

    /// <summary>
    /// Creates a model builder with explicit dependencies.
    /// </summary>
    /// <param name="config">Validated Excel configuration.</param>
    /// <param name="excelFileHandler">Workbook loader.</param>
    public ModelBuilder(ExcelConfig config, ExcelFileHandler? excelFileHandler = null)
    {
        this.config = config ?? throw new ArgumentNullException(nameof(config));
        this.config.Validate();
        this.excelFileHandler = excelFileHandler ?? new ExcelFileHandler();
    }

    /// <summary>
    /// Loads ExcelConfig.json and creates a configured model builder.
    /// </summary>
    /// <param name="configPath">Path to ExcelConfig.json.</param>
    /// <returns>A configured model builder.</returns>
    public static ModelBuilder FromConfigFile(string configPath)
    {
        return new ModelBuilder(ExcelConfig.Load(configPath));
    }

    /// <summary>
    /// Builds an Internal Excel Model from workbook file paths.
    /// </summary>
    /// <param name="excelFilePaths">Excel-like file paths to process.</param>
    /// <returns>The model and worksheet/row diagnostics produced during parsing.</returns>
    public ExcelModelBuildResult BuildFromExcelFiles(IEnumerable<string> excelFilePaths)
    {
        if (excelFilePaths is null)
        {
            throw new ArgumentNullException(nameof(excelFilePaths));
        }

        IReadOnlyList<ExcelWorkbook> workbooks = excelFilePaths
            .Select(excelFileHandler.LoadWorkbook)
            .ToArray();

        return BuildFromWorkbooks(workbooks);
    }

    /// <summary>
    /// Builds an Internal Excel Model from already-loaded workbook objects.
    /// </summary>
    /// <param name="workbooks">Workbook data to process.</param>
    /// <returns>The model and worksheet/row diagnostics produced during parsing.</returns>
    public ExcelModelBuildResult BuildFromWorkbooks(IEnumerable<ExcelWorkbook> workbooks)
    {
        if (workbooks is null)
        {
            throw new ArgumentNullException(nameof(workbooks));
        }

        InternalExcelModel model = new();
        List<ExcelProcessingDiagnostic> diagnostics = [];

        foreach (ExcelWorkbook workbook in workbooks)
        {
            ProcessWorkbook(workbook, model, diagnostics);
        }

        return new ExcelModelBuildResult(model, diagnostics);
    }

    private void ProcessWorkbook(
        ExcelWorkbook workbook,
        InternalExcelModel model,
        List<ExcelProcessingDiagnostic> diagnostics)
    {
        foreach (ExcelWorksheet worksheet in workbook.Worksheets)
        {
            ProcessWorksheet(worksheet, model, diagnostics);
        }
    }

    private void ProcessWorksheet(
        ExcelWorksheet worksheet,
        InternalExcelModel model,
        List<ExcelProcessingDiagnostic> diagnostics)
    {
        HeaderDetectionResult? headerDetectionResult = DetectHeaderRow(worksheet);

        if (headerDetectionResult is null)
        {
            diagnostics.Add(ExcelProcessingDiagnostic.WorksheetKo(
                "excel.header_not_found",
                "No worksheet header row matched the configured Excel header indicators.",
                worksheet));
            return;
        }

        int primaryKeyColumnIndex = FindPrimaryKeyColumnIndex(headerDetectionResult.Headers);

        if (primaryKeyColumnIndex < 0)
        {
            diagnostics.Add(ExcelProcessingDiagnostic.WorksheetKo(
                "excel.primary_key_column_not_found",
                $"Worksheet header row does not contain configured primary key '{config.RecordPrimaryKey}'.",
                worksheet));
            return;
        }

        IReadOnlyList<WorksheetDataRow> dataRows = ReadDataRows(worksheet, headerDetectionResult.HeaderRowIndex);

        if (dataRows.Count == 0)
        {
            diagnostics.Add(ExcelProcessingDiagnostic.WorksheetKo(
                "excel.no_data_rows",
                "Worksheet contains a detected header row but no data rows.",
                worksheet));
            return;
        }

        IReadOnlyList<ColumnPlan> acceptedColumns = BuildAcceptedColumnPlan(
            worksheet,
            headerDetectionResult.Headers,
            dataRows,
            primaryKeyColumnIndex,
            diagnostics);

        IReadOnlyDictionary<string, ExcelColumnClassification> columnClassifications = acceptedColumns
            .Where(column => column.CanonicalName.Length > 0)
            .GroupBy(column => column.CanonicalName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First().Classification,
                StringComparer.OrdinalIgnoreCase);

        foreach (WorksheetDataRow dataRow in dataRows)
        {
            AddDataRowToModel(worksheet, dataRow, acceptedColumns, primaryKeyColumnIndex, columnClassifications, model, diagnostics);
        }
    }

    private HeaderDetectionResult? DetectHeaderRow(ExcelWorksheet worksheet)
    {
        HeaderDetectionResult? bestResult = null;
        int firstRow = Math.Max(config.HeaderRowSearchSpace.FirstRow, 0);
        int lastRow = Math.Min(config.HeaderRowSearchSpace.LastRow, worksheet.Rows.Count - 1);

        for (int rowIndex = firstRow; rowIndex <= lastRow; rowIndex++)
        {
            ExcelWorksheetRow row = worksheet.Rows[rowIndex];
            HeaderDetectionResult? result = EvaluateHeaderCandidateRow(row, rowIndex);

            if (result is null)
            {
                continue;
            }

            if (bestResult is null || result.Confidence > bestResult.Confidence)
            {
                bestResult = result;
            }
        }

        return bestResult;
    }

    private HeaderDetectionResult? EvaluateHeaderCandidateRow(ExcelWorksheetRow row, int rowIndex)
    {
        int firstColumn = Math.Max(config.HeaderRowSearchSpace.FirstColumn, 0);
        int lastColumn = Math.Min(config.HeaderRowSearchSpace.LastColumn, row.Cells.Count - 1);
        Dictionary<int, HeaderCell> headers = [];
        List<double> matchedConfidences = [];
        int candidateCellCount = 0;

        for (int columnIndex = firstColumn; columnIndex <= lastColumn; columnIndex++)
        {
            string rawHeader = row.Cells[columnIndex].Trim();

            if (string.IsNullOrWhiteSpace(rawHeader))
            {
                continue;
            }

            candidateCellCount++;
            HeaderIndicatorMatch? indicatorMatch = FindBestHeaderIndicatorMatch(rawHeader);

            if (indicatorMatch is not null)
            {
                matchedConfidences.Add(indicatorMatch.Confidence);
            }

            headers[columnIndex] = new HeaderCell(columnIndex, rawHeader, BuildCanonicalHeaderName(rawHeader, columnIndex));
        }

        if (candidateCellCount == 0)
        {
            return null;
        }

        double matchedColumnRatio = matchedConfidences.Count / (double)candidateCellCount;

        if (matchedColumnRatio < config.HeaderDetection.MinimumMatchedColumnRatio)
        {
            return null;
        }

        double averageConfidence = matchedConfidences.Count == 0 ? 0 : matchedConfidences.Average();

        return new HeaderDetectionResult(rowIndex, headers, averageConfidence);
    }

    private HeaderIndicatorMatch? FindBestHeaderIndicatorMatch(string rawHeader)
    {
        string normalizedHeader = NormalizeHeader(rawHeader);
        HeaderIndicatorMatch? bestMatch = null;

        foreach (string indicator in config.HeaderRowIndicators)
        {
            string normalizedIndicator = NormalizeHeader(indicator);

            if (normalizedIndicator.Length == 0)
            {
                continue;
            }

            int editDistance = ComputeLevenshteinDistance(normalizedHeader, normalizedIndicator);
            double distanceRatio = editDistance / (double)Math.Max(normalizedHeader.Length, normalizedIndicator.Length);

            if (distanceRatio > config.HeaderDetection.MaximumEditDistanceRatio)
            {
                continue;
            }

            double confidence = CalculateHeaderMatchConfidence(rawHeader, normalizedIndicator, editDistance, distanceRatio);
            HeaderIndicatorMatch match = new(indicator, confidence);

            if (bestMatch is null || match.Confidence > bestMatch.Confidence)
            {
                bestMatch = match;
            }
        }

        return bestMatch;
    }

    private double CalculateHeaderMatchConfidence(string rawHeader, string normalizedIndicator, int editDistance, double distanceRatio)
    {
        if (editDistance == 0)
        {
            string[] tokens = TokenizeHeader(rawHeader);
            double tcd = TokenizedConcatenationDistance.Compute(tokens, normalizedIndicator);

            if (double.IsPositiveInfinity(tcd))
            {
                return 1.0;
            }

            return Math.Max(0.01, TokenizedConcatenationDistance.ConvertDistanceToConfidence(tcd) / 100.0);
        }

        if (editDistance == 1)
        {
            return config.HeaderDetection.EditDistanceOneConfidence;
        }

        if (editDistance == 2)
        {
            return config.HeaderDetection.EditDistanceTwoConfidence;
        }

        return Math.Max(0.01, 1.0 - distanceRatio);
    }

    private int FindPrimaryKeyColumnIndex(IReadOnlyDictionary<int, HeaderCell> headers)
    {
        string normalizedPrimaryKey = NormalizeHeader(config.RecordPrimaryKey);

        foreach (HeaderCell header in headers.Values)
        {
            if (NormalizeHeader(header.RawHeader) == normalizedPrimaryKey)
            {
                return header.ColumnIndex;
            }
        }

        return -1;
    }

    private IReadOnlyList<WorksheetDataRow> ReadDataRows(ExcelWorksheet worksheet, int headerRowIndex)
    {
        return worksheet.Rows
            .Where(row => row.RowIndex > headerRowIndex)
            .Select(row => new WorksheetDataRow(row.RowIndex, row.Cells))
            .Where(row => row.Cells.Any(cell => !string.IsNullOrWhiteSpace(cell)))
            .ToArray();
    }

    private IReadOnlyList<ColumnPlan> BuildAcceptedColumnPlan(
        ExcelWorksheet worksheet,
        IReadOnlyDictionary<int, HeaderCell> headers,
        IReadOnlyList<WorksheetDataRow> dataRows,
        int primaryKeyColumnIndex,
        List<ExcelProcessingDiagnostic> diagnostics)
    {
        List<ColumnPlan> validColumns = [];

        foreach (HeaderCell header in headers.Values.OrderBy(header => header.ColumnIndex))
        {
            IReadOnlyList<string> columnValues = ReadColumnValues(dataRows, header.ColumnIndex);

            if (header.ColumnIndex != primaryKeyColumnIndex && !ColumnHasEnoughUsefulValues(columnValues, dataRows.Count))
            {
                diagnostics.Add(ExcelProcessingDiagnostic.WorksheetWarning(
                    "excel.column_dropped_low_value_ratio",
                    $"Column '{header.RawHeader}' was dropped because it did not contain enough useful values.",
                    worksheet,
                    header.RawHeader));
                continue;
            }

            ExcelColumnClassification classification = header.ColumnIndex == primaryKeyColumnIndex
                ? ExcelColumnClassification.PrimaryKey
                : ClassifyColumn(columnValues);

            validColumns.Add(new ColumnPlan(
                header.ColumnIndex,
                header.RawHeader,
                header.CanonicalName,
                classification,
                [header.ColumnIndex]));
        }

        return MergeDuplicateColumns(worksheet, validColumns, dataRows, diagnostics);
    }

    private IReadOnlyList<string> ReadColumnValues(IReadOnlyList<WorksheetDataRow> dataRows, int columnIndex)
    {
        return dataRows
            .Select(row => GetCellValue(row.Cells, columnIndex))
            .ToArray();
    }

    private bool ColumnHasEnoughUsefulValues(IReadOnlyList<string> columnValues, int rowCount)
    {
        if (rowCount <= 0)
        {
            return false;
        }

        int nonEmptyValueCount = columnValues.Count(value => !string.IsNullOrWhiteSpace(value));
        double usefulValueRatio = nonEmptyValueCount / (double)rowCount;

        return usefulValueRatio >= config.ColumnValidity.MinimumUsefulValueRatio;
    }

    private ExcelColumnClassification ClassifyColumn(IReadOnlyList<string> columnValues)
    {
        string[] nonEmptyValues = columnValues
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .ToArray();

        if (nonEmptyValues.Length == 0)
        {
            return ExcelColumnClassification.Descriptive;
        }

        if (nonEmptyValues.All(IsNumericValue))
        {
            return ExcelColumnClassification.Numerical;
        }

        if (nonEmptyValues.Any(ContainsLetterAndDigit))
        {
            return ExcelColumnClassification.Mixed;
        }

        int uniqueValueCount = nonEmptyValues.Distinct(StringComparer.OrdinalIgnoreCase).Count();
        int maximumValueLength = nonEmptyValues.Max(value => value.Length);

        if (uniqueValueCount <= config.ColumnClassification.CategoricalMaximumUniqueValues
            && maximumValueLength <= config.ColumnClassification.CategoricalMaximumValueLength)
        {
            return ExcelColumnClassification.Categorical;
        }

        return ExcelColumnClassification.Descriptive;
    }

    private IReadOnlyList<ColumnPlan> MergeDuplicateColumns(
        ExcelWorksheet worksheet,
        IReadOnlyList<ColumnPlan> validColumns,
        IReadOnlyList<WorksheetDataRow> dataRows,
        List<ExcelProcessingDiagnostic> diagnostics)
    {
        List<ColumnPlan> mergedColumns = [];
        HashSet<int> consumedColumnIndexes = [];

        foreach (ColumnPlan column in validColumns)
        {
            if (consumedColumnIndexes.Contains(column.ColumnIndex))
            {
                continue;
            }

            List<ColumnPlan> duplicateGroup = [column];

            foreach (ColumnPlan candidate in validColumns)
            {
                if (candidate.ColumnIndex == column.ColumnIndex || consumedColumnIndexes.Contains(candidate.ColumnIndex))
                {
                    continue;
                }

                if (ColumnsShouldMerge(column, candidate, dataRows))
                {
                    duplicateGroup.Add(candidate);
                    consumedColumnIndexes.Add(candidate.ColumnIndex);
                }
            }

            consumedColumnIndexes.Add(column.ColumnIndex);
            ColumnPlan mergedColumn = BuildMergedColumnPlan(duplicateGroup);
            mergedColumns.Add(mergedColumn);

            if (duplicateGroup.Count > 1)
            {
                diagnostics.Add(ExcelProcessingDiagnostic.WorksheetWarning(
                    "excel.duplicate_columns_merged",
                    $"Merged duplicate-like columns into '{mergedColumn.CanonicalName}'.",
                    worksheet,
                    mergedColumn.RawHeader));
            }
        }

        return mergedColumns;
    }

    private bool ColumnsShouldMerge(ColumnPlan leftColumn, ColumnPlan rightColumn, IReadOnlyList<WorksheetDataRow> dataRows)
    {
        if (leftColumn.Classification == ExcelColumnClassification.PrimaryKey || rightColumn.Classification == ExcelColumnClassification.PrimaryKey)
        {
            return leftColumn.Classification == ExcelColumnClassification.PrimaryKey
                && rightColumn.Classification == ExcelColumnClassification.PrimaryKey
                && string.Equals(leftColumn.CanonicalName, rightColumn.CanonicalName, StringComparison.OrdinalIgnoreCase);
        }

        bool headersAreIdentical = string.Equals(leftColumn.CanonicalName, rightColumn.CanonicalName, StringComparison.OrdinalIgnoreCase);

        if (headersAreIdentical)
        {
            return true;
        }

        return CalculateColumnOverlapRatio(leftColumn.ColumnIndex, rightColumn.ColumnIndex, dataRows)
            > config.DuplicateColumnHandling.OverlapRatioForMerge;
    }

    private double CalculateColumnOverlapRatio(int leftColumnIndex, int rightColumnIndex, IReadOnlyList<WorksheetDataRow> dataRows)
    {
        string[] leftValues = dataRows
            .Select(row => NormalizeCellValue(GetCellValue(row.Cells, leftColumnIndex)))
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        string[] rightValues = dataRows
            .Select(row => NormalizeCellValue(GetCellValue(row.Cells, rightColumnIndex)))
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (leftValues.Length == 0 || rightValues.Length == 0)
        {
            return 0;
        }

        int sharedValueCount = leftValues.Intersect(rightValues, StringComparer.OrdinalIgnoreCase).Count();
        int comparisonBase = Math.Min(leftValues.Length, rightValues.Length);

        return sharedValueCount / (double)comparisonBase;
    }

    private static ColumnPlan BuildMergedColumnPlan(IReadOnlyList<ColumnPlan> duplicateGroup)
    {
        ColumnPlan firstColumn = duplicateGroup[0];
        int[] sourceColumnIndexes = duplicateGroup
            .SelectMany(column => column.SourceColumnIndexes)
            .Distinct()
            .OrderBy(columnIndex => columnIndex)
            .ToArray();

        return firstColumn with { SourceColumnIndexes = sourceColumnIndexes };
    }

    private void AddDataRowToModel(
        ExcelWorksheet worksheet,
        WorksheetDataRow dataRow,
        IReadOnlyList<ColumnPlan> acceptedColumns,
        int primaryKeyColumnIndex,
        IReadOnlyDictionary<string, ExcelColumnClassification> columnClassifications,
        InternalExcelModel model,
        List<ExcelProcessingDiagnostic> diagnostics)
    {
        string familyID = GetCellValue(dataRow.Cells, primaryKeyColumnIndex).Trim();

        if (!IsValidPrimaryKey(familyID))
        {
            diagnostics.Add(ExcelProcessingDiagnostic.RowKo(
                "excel.invalid_primary_key",
                "Row was skipped because its primary key is missing, malformed, or not compliant with ExcelConfig.FamilyIDProperties.",
                worksheet,
                dataRow.RowIndex,
                familyID));
            return;
        }

        List<ExcelPropertyValue> propertyValues = [];

        foreach (ColumnPlan column in acceptedColumns)
        {
            if (column.SourceColumnIndexes.Contains(primaryKeyColumnIndex))
            {
                continue;
            }

            ExcelPropertyValue propertyValue = BuildPropertyValue(worksheet, dataRow, column);
            propertyValues.Add(propertyValue);
        }

        model.AddOrMergeFamilyRow(familyID, propertyValues, columnClassifications);
    }

    private ExcelPropertyValue BuildPropertyValue(ExcelWorksheet worksheet, WorksheetDataRow dataRow, ColumnPlan column)
    {
        List<string> sourceValues = [];
        List<ExcelCellAddress> sourceLocations = [];

        foreach (int sourceColumnIndex in column.SourceColumnIndexes)
        {
            string sourceValue = GetCellValue(dataRow.Cells, sourceColumnIndex);
            string filledValue = sourceValue ?? string.Empty;
            sourceValues.Add(filledValue);
            sourceLocations.Add(new ExcelCellAddress(
                worksheet.SourceFile,
                worksheet.Name,
                dataRow.RowIndex + 1,
                sourceColumnIndex + 1,
                column.RawHeader));
        }

        string[] uniqueSourceValues = sourceValues
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new ExcelPropertyValue(column.CanonicalName, uniqueSourceValues, sourceLocations);
    }

    private bool IsValidPrimaryKey(string primaryKey)
    {
        if (string.IsNullOrWhiteSpace(primaryKey))
        {
            return false;
        }

        string trimmedPrimaryKey = primaryKey.Trim();

        if (trimmedPrimaryKey.Length != config.FamilyIDProperties.Length)
        {
            return false;
        }

        if (config.FamilyIDProperties.IsNumeric == true && !trimmedPrimaryKey.All(char.IsDigit))
        {
            return false;
        }

        return true;
    }

    private static bool IsNumericValue(string value)
    {
        return decimal.TryParse(value.Trim(), out _);
    }

    private static bool ContainsLetterAndDigit(string value)
    {
        return value.Any(char.IsLetter) && value.Any(char.IsDigit);
    }

    private static string BuildCanonicalHeaderName(string rawHeader, int columnIndex)
    {
        string collapsedHeader = Regex.Replace(rawHeader.Trim(), "\\s+", " ");

        if (string.IsNullOrWhiteSpace(collapsedHeader))
        {
            return $"Column{columnIndex + 1}";
        }

        return collapsedHeader;
    }

    private static string NormalizeHeader(string header)
    {
        return NonAlphaNumericPattern.Replace(header.Trim().ToLowerInvariant(), string.Empty);
    }

    private static string[] TokenizeHeader(string header)
    {
        return HeaderTokenPattern
            .Matches(header.ToLowerInvariant())
            .Select(match => match.Value)
            .ToArray();
    }

    private static string NormalizeCellValue(string value)
    {
        return value.Trim().ToLowerInvariant();
    }

    private static string GetCellValue(IReadOnlyList<string> cells, int columnIndex)
    {
        if (columnIndex < 0 || columnIndex >= cells.Count)
        {
            return string.Empty;
        }

        return cells[columnIndex] ?? string.Empty;
    }

    private static int ComputeLevenshteinDistance(string left, string right)
    {
        if (left.Length == 0)
        {
            return right.Length;
        }

        if (right.Length == 0)
        {
            return left.Length;
        }

        int[,] distances = new int[left.Length + 1, right.Length + 1];

        for (int leftIndex = 0; leftIndex <= left.Length; leftIndex++)
        {
            distances[leftIndex, 0] = leftIndex;
        }

        for (int rightIndex = 0; rightIndex <= right.Length; rightIndex++)
        {
            distances[0, rightIndex] = rightIndex;
        }

        for (int leftIndex = 1; leftIndex <= left.Length; leftIndex++)
        {
            for (int rightIndex = 1; rightIndex <= right.Length; rightIndex++)
            {
                int substitutionCost = left[leftIndex - 1] == right[rightIndex - 1] ? 0 : 1;

                distances[leftIndex, rightIndex] = Math.Min(
                    Math.Min(distances[leftIndex - 1, rightIndex] + 1, distances[leftIndex, rightIndex - 1] + 1),
                    distances[leftIndex - 1, rightIndex - 1] + substitutionCost);
            }
        }

        return distances[left.Length, right.Length];
    }

    private sealed record HeaderDetectionResult(
        int HeaderRowIndex,
        IReadOnlyDictionary<int, HeaderCell> Headers,
        double Confidence);

    private sealed record HeaderIndicatorMatch(string Indicator, double Confidence);

    private sealed record HeaderCell(int ColumnIndex, string RawHeader, string CanonicalName);

    private sealed record WorksheetDataRow(int RowIndex, IReadOnlyList<string> Cells);

    private sealed record ColumnPlan(
        int ColumnIndex,
        string RawHeader,
        string CanonicalName,
        ExcelColumnClassification Classification,
        IReadOnlyList<int> SourceColumnIndexes);
}

/// <summary>
/// Result returned after building the Internal Excel Model.
/// </summary>
public sealed record ExcelModelBuildResult(
    InternalExcelModel Model,
    IReadOnlyList<ExcelProcessingDiagnostic> Diagnostics)
{
    /// <summary>
    /// FamilyRecord projection consumed by downstream matching.
    /// </summary>
    public IReadOnlyList<FamilyRecord> FamilyRecords => Model.ToFamilyRecords();
}

/// <summary>
/// Safe diagnostic emitted for worksheet and row issues during Excel parsing.
/// </summary>
public sealed record ExcelProcessingDiagnostic(
    ExcelDiagnosticSeverity Severity,
    string ReasonCode,
    string Message,
    string SourceFile,
    string WorksheetName,
    int? RowNumber,
    string? ColumnName,
    string? ItemID)
{
    /// <summary>
    /// Creates a worksheet-level KO diagnostic.
    /// </summary>
    /// <param name="reasonCode">Stable reason code.</param>
    /// <param name="message">Safe diagnostic message.</param>
    /// <param name="worksheet">Source worksheet.</param>
    /// <returns>A worksheet-level diagnostic.</returns>
    public static ExcelProcessingDiagnostic WorksheetKo(string reasonCode, string message, ExcelWorksheet worksheet)
    {
        return new ExcelProcessingDiagnostic(
            ExcelDiagnosticSeverity.Error,
            reasonCode,
            message,
            worksheet.SourceFile,
            worksheet.Name,
            null,
            null,
            null);
    }

    /// <summary>
    /// Creates a worksheet-level warning diagnostic.
    /// </summary>
    /// <param name="reasonCode">Stable reason code.</param>
    /// <param name="message">Safe diagnostic message.</param>
    /// <param name="worksheet">Source worksheet.</param>
    /// <param name="columnName">Optional source column name.</param>
    /// <returns>A worksheet-level diagnostic.</returns>
    public static ExcelProcessingDiagnostic WorksheetWarning(
        string reasonCode,
        string message,
        ExcelWorksheet worksheet,
        string? columnName = null)
    {
        return new ExcelProcessingDiagnostic(
            ExcelDiagnosticSeverity.Warning,
            reasonCode,
            message,
            worksheet.SourceFile,
            worksheet.Name,
            null,
            columnName,
            null);
    }

    /// <summary>
    /// Creates a row-level KO diagnostic.
    /// </summary>
    /// <param name="reasonCode">Stable reason code.</param>
    /// <param name="message">Safe diagnostic message.</param>
    /// <param name="worksheet">Source worksheet.</param>
    /// <param name="zeroBasedRowIndex">Zero-based row index.</param>
    /// <param name="itemID">Problematic row primary-key value when available.</param>
    /// <returns>A row-level diagnostic.</returns>
    public static ExcelProcessingDiagnostic RowKo(
        string reasonCode,
        string message,
        ExcelWorksheet worksheet,
        int zeroBasedRowIndex,
        string? itemID)
    {
        return new ExcelProcessingDiagnostic(
            ExcelDiagnosticSeverity.Error,
            reasonCode,
            message,
            worksheet.SourceFile,
            worksheet.Name,
            zeroBasedRowIndex + 1,
            null,
            itemID);
    }
}

/// <summary>
/// Severity of a safe Excel processing diagnostic.
/// </summary>
public enum ExcelDiagnosticSeverity
{
    /// <summary>
    /// Informational note.
    /// </summary>
    Info,

    /// <summary>
    /// Non-fatal warning.
    /// </summary>
    Warning,

    /// <summary>
    /// KO item or worksheet diagnostic.
    /// </summary>
    Error
}
