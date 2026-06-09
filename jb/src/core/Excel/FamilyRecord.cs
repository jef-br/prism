/**
 * Represents one deduplicated product record built from InternalExcelModel. (aka, the mapping of one excel row to an object so that it can be used as anchorpoint for all images that are part of one family.)
 * 
 * Apart from the column header row, all columns found on a single row represent a familyID.
 * The amount and type of columns changes per Job.
 * Every column in a row should be stored in a seperate entity (be it a dictionary item, a TKey/TValue list or anything else)
 *  in the internal excel model
 */
class FamilyRecord
{
    public string FamilyID { get; set; }


}