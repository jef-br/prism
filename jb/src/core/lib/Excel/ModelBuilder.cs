using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Prism.Lib.Excel;

/// <summary>
/// Builds the Internal Excel Model from one or more workbook files.
/// </summary>
public sealed class ModelBuilder
{
    private static readonly Regex NonAlphaNumericPattern = new("[^a-z0-9]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex HeaderTokenPattern = new("[a-z0-9]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private const string FamilyIdCanonical = "familyid";

    private readonly ExcelConfig config;
    private readonly TranslationConfig translationConfig;
    private readonly ExcelFileHandler excelFileHandler;

    // Header indicator keys (canonical ids or literal terms) the configuration treats as header signals.
    private readonly HashSet<string> activeIndicatorIds;
    // Flat list of every configured header term, ASCII-folded, for edit-distance-1 typo tolerance.
    private readonly IReadOnlyList<string> fuzzyHeaderTerms;
    // Canonical ids that are safe to collapse cross-language duplicate columns onto (C1).
    private static readonly HashSet<string> SafeMergeCanonicals = new(StringComparer.OrdinalIgnoreCase)
    {
        "familyid", "ean", "refco", "color", "material", "description",
        "washinginstructions", "weight", "brand", "season", "gender", "size", "style",
        "producttype", "ngp"
    };

    /// <summary>
    /// Creates a model builder with explicit dependencies.
    /// </summary>
    /// <param name="config">Validated Excel configuration.</param>
    /// <param name="translationConfig">Multilingual header/value dictionary.</param>
    /// <param name="excelFileHandler">Workbook loader.</param>
    public ModelBuilder(ExcelConfig config, TranslationConfig translationConfig, ExcelFileHandler? excelFileHandler = null)
    {
        this.config = config ?? throw new ArgumentNullException(nameof(config));
        this.config.Validate();
        this.translationConfig = translationConfig ?? throw new ArgumentNullException(nameof(translationConfig));
        this.excelFileHandler = excelFileHandler ?? new ExcelFileHandler();

        activeIndicatorIds = new HashSet<string>(
            config.HeaderRowIndicators.Select(NormalizeHeader),
            StringComparer.OrdinalIgnoreCase);

        fuzzyHeaderTerms = translationConfig.HeaderGroups
            .Where(group => activeIndicatorIds.Contains(NormalizeHeader(group.Id)))
            .SelectMany(group => group.Terms)
            .Select(NormalizeHeader)
            .Where(term => term.Length >= 4)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// Loads ExcelConfig.json (and its sibling TranslationDictionary.json) and creates a configured model builder.
    /// </summary>
    /// <param name="configPath">Path to ExcelConfig.json.</param>
    /// <returns>A configured model builder.</returns>
    public static ModelBuilder FromConfigFile(string configPath)
    {
        ExcelConfig excelConfig = ExcelConfig.Load(configPath);

        string configDirectory = System.IO.Path.GetDirectoryName(configPath)
            ?? throw new PrismConfigurationException($"Could not determine config directory from '{configPath}'.");
        string translationConfigPath = System.IO.Path.Combine(configDirectory, "TranslationDictionary.json");

        if (!System.IO.File.Exists(translationConfigPath))
            throw new PrismConfigurationException(
                $"TranslationDictionary.json was not found next to ExcelConfig.json at '{translationConfigPath}'.");

        TranslationConfig translationConfig = TranslationConfig.Load(translationConfigPath);

        return new ModelBuilder(excelConfig, translationConfig);
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
        List<OrphanRow> orphanRows = [];

        foreach (ExcelWorkbook workbook in workbooks)
        {
            ProcessWorkbook(workbook, model, diagnostics, orphanRows);
        }

        // Rows that carried no resolvable FamilyID may still belong to a family built from another
        // sheet or file — join them via unique shared keys (EAN, ref/article digit runs).
        OrphanRowJoiner.Join(model, orphanRows, diagnostics);

        // Model-scope prune: a canonical property can survive the per-worksheet fill-ratio gate yet be
        // blank across every merged family record. Drop those to shrink the matcher search space.
        foreach (string droppedProperty in model.PruneEmptyProperties(config.RecordPrimaryKey))
        {
            diagnostics.Add(ExcelProcessingDiagnostic.ModelWarning(
                "excel.column_dropped_empty_model_wide",
                $"Column '{droppedProperty}' was dropped because it was empty across all family records.",
                droppedProperty));
        }

        return new ExcelModelBuildResult(model, diagnostics);
    }

    private void ProcessWorkbook(
        ExcelWorkbook workbook,
        InternalExcelModel model,
        List<ExcelProcessingDiagnostic> diagnostics,
        List<OrphanRow> orphanRows)
    {
        foreach (ExcelWorksheet worksheet in workbook.Worksheets)
        {
            ProcessWorksheet(worksheet, model, diagnostics, orphanRows);
        }
    }

    private void ProcessWorksheet(
        ExcelWorksheet worksheet,
        InternalExcelModel model,
        List<ExcelProcessingDiagnostic> diagnostics,
        List<OrphanRow> orphanRows)
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

        IReadOnlyList<WorksheetDataRow> dataRows = ReadDataRows(worksheet, headerDetectionResult.HeaderRowIndex);

        // FamilyID column resolved by header-name signal OR by the 8-digit-unique cell pattern.
        int familyIdColumnIndex = FindFamilyIDColumnIndex(headerDetectionResult.Headers, dataRows);

        if (familyIdColumnIndex < 0)
        {
            diagnostics.Add(ExcelProcessingDiagnostic.WorksheetKo(
                "excel.primary_key_column_not_found",
                $"Worksheet header row does not contain configured primary key '{config.RecordPrimaryKey}'.",
                worksheet));

            // The rows are not lost yet: buffer them so OrphanRowJoiner can attach them to
            // families built from other sheets/files via shared keys.
            if (dataRows.Count > 0)
                BufferOrphanRows(worksheet, headerDetectionResult, dataRows, orphanRows, diagnostics);
            return;
        }

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
            familyIdColumnIndex,
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
            AddDataRowToModel(worksheet, dataRow, acceptedColumns, familyIdColumnIndex, columnClassifications, model, diagnostics, orphanRows);
        }
    }

    /// <summary>
    /// Buffers every data row of a worksheet without a FamilyID column as an OrphanRow, using the
    /// same accepted-column plan a keyed worksheet would get (with no column marked as primary key).
    /// </summary>
    private void BufferOrphanRows(
        ExcelWorksheet worksheet,
        HeaderDetectionResult headerDetectionResult,
        IReadOnlyList<WorksheetDataRow> dataRows,
        List<OrphanRow> orphanRows,
        List<ExcelProcessingDiagnostic> diagnostics)
    {
        IReadOnlyList<ColumnPlan> acceptedColumns = BuildAcceptedColumnPlan(
            worksheet,
            headerDetectionResult.Headers,
            dataRows,
            familyIdColumnIndex: -1,
            diagnostics);

        if (acceptedColumns.Count == 0)
            return;

        IReadOnlyDictionary<string, ExcelColumnClassification> columnClassifications = acceptedColumns
            .Where(column => column.CanonicalName.Length > 0)
            .GroupBy(column => column.CanonicalName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First().Classification,
                StringComparer.OrdinalIgnoreCase);

        foreach (WorksheetDataRow dataRow in dataRows)
        {
            List<ExcelPropertyValue> propertyValues = acceptedColumns
                .Select(column => BuildPropertyValue(worksheet, dataRow, column))
                .Where(propertyValue => propertyValue.SourceValues.Any(value => !string.IsNullOrWhiteSpace(value)))
                .ToList();

            if (propertyValues.Count > 0)
                orphanRows.Add(new OrphanRow(worksheet.SourceFile, worksheet.Name, dataRow.RowIndex, propertyValues, columnClassifications));
        }
    }

    //  Header row detection (token-based, multilingual)

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

            // Prefer the row with the most recognized header columns; break ties on average confidence.
            // This rejects sparse single-cell title rows that would otherwise score a perfect ratio.
            if (bestResult is null
                || result.MatchedColumnCount > bestResult.MatchedColumnCount
                || (result.MatchedColumnCount == bestResult.MatchedColumnCount && result.Confidence > bestResult.Confidence))
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
        bool hasFamilyIdHeader = false;

        for (int columnIndex = firstColumn; columnIndex <= lastColumn; columnIndex++)
        {
            string rawHeader = row.Cells[columnIndex].Trim();

            if (string.IsNullOrWhiteSpace(rawHeader))
            {
                continue;
            }

            candidateCellCount++;

            if (TryMatchHeaderCell(rawHeader, out double cellConfidence))
            {
                matchedConfidences.Add(cellConfidence);
            }

            if (!hasFamilyIdHeader && HeaderResolvesToFamilyId(rawHeader))
            {
                hasFamilyIdHeader = true;
            }

            headers[columnIndex] = new HeaderCell(columnIndex, rawHeader, ResolveColumnCanonicalName(rawHeader, columnIndex));
        }

        if (candidateCellCount == 0)
        {
            return null;
        }

        double matchedColumnRatio = matchedConfidences.Count / (double)candidateCellCount;

        // A row with a recognizable FamilyID column plus at least one more known column is a header,
        // even when most sibling columns are language-specific or concatenated (low overall ratio) —
        // the FamilyID column is the single strongest header signal. Best-row ranking by matched count
        // still guards against sparse false positives.
        bool qualifiesByRatio = matchedColumnRatio >= config.HeaderDetection.MinimumMatchedColumnRatio;
        bool qualifiesByFamilyId = hasFamilyIdHeader && matchedConfidences.Count >= 2;

        if (!qualifiesByRatio && !qualifiesByFamilyId)
        {
            return null;
        }

        double averageConfidence = matchedConfidences.Count == 0 ? 0 : matchedConfidences.Average();

        return new HeaderDetectionResult(rowIndex, headers, matchedConfidences.Count, averageConfidence);
    }

    /// <summary>
    /// A header cell matches when any of its significant tokens resolves to an active header
    /// indicator (exact), or is within edit distance 1 of a configured header term (typo tolerance).
    /// </summary>
    private bool TryMatchHeaderCell(string rawHeader, out double confidence)
    {
        confidence = 0;
        bool matched = false;

        // Whole-phrase indicators ("Product Type", "Tipo di prodotto") match before tokenization.
        if (translationConfig.TryResolveHeaderPhrase(rawHeader, out string phraseCanonical)
            && activeIndicatorIds.Contains(NormalizeHeader(phraseCanonical)))
        {
            confidence = 1.0;
            return true;
        }

        foreach (string token in TokenizeFolded(rawHeader))
        {
            if (translationConfig.IsGeneralStopWord(token))
            {
                continue;
            }

            if (TokenMatchesIndicator(token))
            {
                confidence = 1.0;
                return true;
            }

            if (token.Length >= 4 && fuzzyHeaderTerms.Any(term => ComputeLevenshteinDistance(token, term) <= 1))
            {
                matched = true;
                confidence = Math.Max(confidence, config.HeaderDetection.EditDistanceOneConfidence);
            }
        }

        return matched;
    }

    /// <summary>
    /// True when a single header token is an active indicator — either by resolving to a configured
    /// canonical header group id, or by being a literal indicator term in ExcelConfig.
    /// </summary>
    private bool TokenMatchesIndicator(string normalizedToken)
    {
        if (activeIndicatorIds.Contains(normalizedToken))
        {
            return true;
        }

        return translationConfig.TryResolveHeaderCanonical(normalizedToken, out string canonicalId)
            && activeIndicatorIds.Contains(NormalizeHeader(canonicalId));
    }

    //  FamilyID column resolution (header-name OR cell pattern)

    /// <summary>
    /// Resolves the FamilyID column. A column qualifies by header name (a token resolving to the
    /// "familyid" group) or by cell pattern (every non-empty cell is exactly an 8-digit, column-unique
    /// number). Header-name carries sheets that repeat a FamilyID across rows; cell-pattern carries
    /// sheets whose header text is in an unrecognized language.
    /// </summary>
    private int FindFamilyIDColumnIndex(IReadOnlyDictionary<int, HeaderCell> headers, IReadOnlyList<WorksheetDataRow> dataRows)
    {
        List<int> nameCandidates = headers.Values
            .Where(header => HeaderResolvesToFamilyId(header.RawHeader))
            .Select(header => header.ColumnIndex)
            .OrderBy(columnIndex => columnIndex)
            .ToList();

        if (nameCandidates.Count == 1)
        {
            return nameCandidates[0];
        }

        if (nameCandidates.Count > 1)
        {
            List<int> patternConfirmed = nameCandidates
                .Where(columnIndex => ColumnIsFamilyIdByCellPattern(dataRows, columnIndex))
                .ToList();

            if (patternConfirmed.Count > 0)
            {
                return patternConfirmed[0];
            }

            // Multiple header-name candidates, none cell-pattern-clean (e.g. repeated keys) — take leftmost.
            return nameCandidates[0];
        }

        // No header-name signal: identify the FamilyID column purely by the 8-digit-unique cell pattern.
        List<int> patternColumns = headers.Values
            .Select(header => header.ColumnIndex)
            .Where(columnIndex => ColumnIsFamilyIdByCellPattern(dataRows, columnIndex))
            .OrderBy(columnIndex => columnIndex)
            .ToList();

        return patternColumns.Count == 1 ? patternColumns[0] : -1;
    }

    private bool HeaderResolvesToFamilyId(string rawHeader)
    {
        foreach (string token in TokenizeFolded(rawHeader))
        {
            if (translationConfig.TryResolveHeaderCanonical(token, out string canonicalId)
                && string.Equals(NormalizeHeader(canonicalId), FamilyIdCanonical, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// True when every non-empty cell in the column is a valid FamilyID (per FamilyIDProperties) and
    /// all those values are unique within the column.
    /// </summary>
    private bool ColumnIsFamilyIdByCellPattern(IReadOnlyList<WorksheetDataRow> dataRows, int columnIndex)
    {
        List<string> nonEmptyValues = dataRows
            .Select(row => GetCellValue(row.Cells, columnIndex).Trim())
            .Where(value => value.Length > 0)
            .ToList();

        if (nonEmptyValues.Count == 0)
        {
            return false;
        }

        if (!nonEmptyValues.All(IsValidFamilyID))
        {
            return false;
        }

        int uniqueCount = nonEmptyValues.Distinct(StringComparer.OrdinalIgnoreCase).Count();
        return uniqueCount == nonEmptyValues.Count;
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
        int familyIdColumnIndex,
        List<ExcelProcessingDiagnostic> diagnostics)
    {
        List<ColumnPlan> validColumns = [];

        foreach (HeaderCell header in headers.Values.OrderBy(header => header.ColumnIndex))
        {
            IReadOnlyList<string> columnValues = ReadColumnValues(dataRows, header.ColumnIndex);

            if (header.ColumnIndex != familyIdColumnIndex && !ColumnHasEnoughUsefulValues(columnValues, dataRows.Count))
            {
                diagnostics.Add(ExcelProcessingDiagnostic.WorksheetWarning(
                    "excel.column_dropped_low_value_ratio",
                    $"Column '{header.RawHeader}' was dropped because it did not contain enough useful values.",
                    worksheet,
                    header.RawHeader));
                continue;
            }

            ExcelColumnClassification classification = header.ColumnIndex == familyIdColumnIndex
                ? ExcelColumnClassification.FamilyID
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
        if (leftColumn.Classification == ExcelColumnClassification.FamilyID || rightColumn.Classification == ExcelColumnClassification.FamilyID)
        {
            return leftColumn.Classification == ExcelColumnClassification.FamilyID
                && rightColumn.Classification == ExcelColumnClassification.FamilyID
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
        int familyIdColumnIndex,
        IReadOnlyDictionary<string, ExcelColumnClassification> columnClassifications,
        InternalExcelModel model,
        List<ExcelProcessingDiagnostic> diagnostics,
        List<OrphanRow> orphanRows)
    {
        string familyID = GetCellValue(dataRow.Cells, familyIdColumnIndex).Trim();

        if (!IsValidFamilyID(familyID))
        {
            diagnostics.Add(ExcelProcessingDiagnostic.RowKo(
                "excel.invalid_primary_key",
                "Row was skipped because its primary key is missing, malformed, or not compliant with ExcelConfig.FamilyIDProperties.",
                worksheet,
                dataRow.RowIndex,
                familyID));

            // A header like "VeePee Selection" can name-resolve as the FamilyID column while its
            // cells hold something else entirely — buffer the row for the shared-key join instead
            // of discarding it.
            List<ExcelPropertyValue> orphanValues = acceptedColumns
                .Where(column => !column.SourceColumnIndexes.Contains(familyIdColumnIndex))
                .Select(column => BuildPropertyValue(worksheet, dataRow, column))
                .Where(propertyValue => propertyValue.SourceValues.Any(value => !string.IsNullOrWhiteSpace(value)))
                .ToList();

            if (orphanValues.Count > 0)
                orphanRows.Add(new OrphanRow(worksheet.SourceFile, worksheet.Name, dataRow.RowIndex, orphanValues, columnClassifications));
            return;
        }

        List<ExcelPropertyValue> propertyValues = [];

        foreach (ColumnPlan column in acceptedColumns)
        {
            if (column.SourceColumnIndexes.Contains(familyIdColumnIndex))
            {
                continue;
            }

            ExcelPropertyValue propertyValue = BuildPropertyValue(worksheet, dataRow, column);
            propertyValues.Add(propertyValue);
        }

        propertyValues.Add(new ExcelPropertyValue(config.RecordPrimaryKey, [familyID], []));

        var extendedClassifications = new Dictionary<string, ExcelColumnClassification>(
            columnClassifications, StringComparer.OrdinalIgnoreCase)
        {
            [config.RecordPrimaryKey] = ExcelColumnClassification.FamilyID
        };

        model.AddOrMergeFamilyRow(familyID, propertyValues, extendedClassifications);
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

    private bool IsValidFamilyID(string familyId)
    {
        if (string.IsNullOrWhiteSpace(familyId))
        {
            return false;
        }

        string trimmedFamilyId = familyId.Trim();

        if (trimmedFamilyId.Length != config.FamilyIDProperties.Length)
        {
            return false;
        }

        if (config.FamilyIDProperties.IsNumeric == true && !trimmedFamilyId.All(char.IsDigit))
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

    //  Header canonicalization (C1)

    /// <summary>
    /// Resolves a column's canonical name. When every significant header token maps to the same
    /// canonical id and that id is safe to collapse, the canonical id is used so cross-language
    /// duplicates merge (e.g. "Descripción"/"DESCRIPCION" -> "description"). Otherwise the cleaned
    /// raw header is kept so distinct columns are never accidentally merged.
    /// </summary>
    private string ResolveColumnCanonicalName(string rawHeader, int columnIndex)
    {
        // Whole-phrase lookup first: multi-word terms ("Product Type", "Tipo di prodotto") can
        // never resolve token by token because their words are stop words or non-terms alone.
        if (translationConfig.TryResolveHeaderPhrase(rawHeader, out string phraseCanonical))
        {
            string normalizedPhraseCanonical = NormalizeHeader(phraseCanonical);

            if (SafeMergeCanonicals.Contains(normalizedPhraseCanonical))
            {
                return normalizedPhraseCanonical;
            }
        }

        string? singleCanonical = null;
        bool sawSignificantToken = false;

        foreach (string token in TokenizeFolded(rawHeader))
        {
            if (translationConfig.IsGeneralStopWord(token))
            {
                continue;
            }

            sawSignificantToken = true;

            if (!translationConfig.TryResolveHeaderCanonical(token, out string canonicalId))
            {
                return BuildCanonicalHeaderName(rawHeader, columnIndex);
            }

            string normalizedCanonical = NormalizeHeader(canonicalId);

            if (singleCanonical is null)
            {
                singleCanonical = normalizedCanonical;
            }
            else if (!string.Equals(singleCanonical, normalizedCanonical, StringComparison.OrdinalIgnoreCase))
            {
                return BuildCanonicalHeaderName(rawHeader, columnIndex);
            }
        }

        if (sawSignificantToken && singleCanonical is not null && SafeMergeCanonicals.Contains(singleCanonical))
        {
            return singleCanonical;
        }

        return BuildCanonicalHeaderName(rawHeader, columnIndex);
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
        return NonAlphaNumericPattern.Replace(FoldDiacritics(header).Trim().ToLowerInvariant(), string.Empty);
    }

    /// <summary>Splits a header cell into lowercase, diacritics-folded alphanumeric tokens.</summary>
    private static IEnumerable<string> TokenizeFolded(string header)
    {
        return HeaderTokenPattern
            .Matches(FoldDiacritics(header).ToLowerInvariant())
            .Select(match => match.Value);
    }

    /// <summary>Folds accented characters to their ASCII base so "código" tokenizes as "codigo".</summary>
    private static string FoldDiacritics(string input)
    {
        string decomposed = input.Normalize(NormalizationForm.FormD);
        StringBuilder builder = new(decomposed.Length);

        foreach (char ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(ch);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
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

    // Internal (not private): reused by StringMatcher.cs for Bracket 3/4 categorical-column edit-distance
    // tolerance, so the bounded edit-distance calculation exists in exactly one place.
    internal static int ComputeLevenshteinDistance(string left, string right)
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
        int MatchedColumnCount,
        double Confidence);

    private sealed record HeaderCell(int ColumnIndex, string RawHeader, string CanonicalName);

    private sealed record WorksheetDataRow(int RowIndex, IReadOnlyList<string> Cells);

    private sealed record ColumnPlan(
        int ColumnIndex,
        string RawHeader,
        string CanonicalName,
        ExcelColumnClassification Classification,
        IReadOnlyList<int> SourceColumnIndexes);
}
