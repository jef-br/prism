/*
A data class used by `ModelBuilder.cs` that holds a model of all loaded excel files. A Plain Old data class.
The model is built runtime using Reflection and maps excel column headers as record property names and adds the rows below the column-header-row as data objects.

* All records contain at least one non-empty property: A numerical primary key recovered from the first Excel file where a column header was found.
* The column header of the column that contains the primary keys is stored here: `jb\src\core\Excel\ExcelConfig.json` under "RecordPrimaryKey"
    * Typically, the column header contains a variation on the string "familyID" (famID, "famille ID", ...)
    * The famID column contains only cells that hold an 8 digit number. ("98765432", "97654321", ...)

* All properties except for the primary key are labelled as either numerical, categorical, descriptive, or mixed.
    * Numerical: 100% of non-null values are purely numeric → Used later by NumericalMatcher.cs
    * Categorical: Values are short, low-cardinality strings (< ~100 unique values, < 20 chars each) → Used by StringMatcher.cs
    * Descriptive: Long or high-cardinality (product names, notes, ... Lower priority, use with caution) → Used by StringMatcher.cs and ImageLabelingMatcher.cs
    * Mixed: digits + letters in a pattern → treat as string for StringMatcher.cs, but also extract the numerical-only subsequence for NumericalMatcher.cs if the numbers are not immediately followed by a common unit of measure (%,kg,g,m,cm,mm,meter,centi,milli,kilo)

* Numerical properties have the highest value
* Categorical matches have a very high value if they are confirmed using image classification/labelling
* Descriptive properties are often less valuable to make matches, but strings with a cardinality between 4 and 8 that appear in the initial image filename are very high value.
* 


*/

public sealed class ExcelTokenStore
{

    /* This comment block contains a sketch indicating the direction for the model.
    This direction is the least important source of information for this model but is here because it is guaranteed to work with the first version of MatchType.cs and MatchEvidence.cs
    public List<ExcelToken> Tokens { get; init; } = new();

    // optional indexes for fast lookup
    public Dictionary<string, List<ExcelToken>> ByNormalizedValue { get; init; }
        = new();
    */
}