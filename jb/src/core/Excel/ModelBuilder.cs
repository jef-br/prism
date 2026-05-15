/*
The file that builds the Internal Excel Model of all excel files combined. It uses excelfilehandler.cs to load the worksheets of all excel files.
The goal of ModelBuilder.cs is to produce a model of all collated and deduplicated excel data using InternalExcelModel.Cs

Per excel file:
1. Open the file
    1.1 Temporarily load the first worksheet as-is into a "worksheet object"
    1.2 Look for the column header row.
        Do this by checking if a row contains enough values that also appear in `HeaderRowIndicators` found in `jb\src\core\Excel\ExcelConfig.json`
    1.3 If no header row is found, move to the next worksheet (or the next excel file if this is the only/last worksheet in the current excel file)
    1.4 If a header row is found, look for the familyID column in that row.
    1.5 If there is no familyID column, move to the next worksheet or file until a worksheet with familyID column is found.

2. Once a worksheet is found that has a header row that contains a familyID column:
    2.1 Create a new instance of InternalExcelModel.cs
    2.2 For every non-empty cell in the familyID column:
        - Add the row as a new record using the "familyID" value as primary key and consider all other columns as record properties using the column header as property name.
3 Continue until all excel files and worksheets are parsed and every familyID row is added to the InternalExcelModel instance.

* NEVER create duplicate familyID records.
    * If the same familyID is found more than once (in another excel file/worksheet) -> deduplicate the data.
        * Properties can be added, but not changed
        * A property can only appear once per familyID.
        * Multiple appearances of the same property are ignored.
*/