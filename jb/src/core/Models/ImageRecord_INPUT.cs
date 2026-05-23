/*
Represents one source image after import and pre-pipeline normalization.
Fields still need to be defined in jbtodo.md.
*/


class ImageRecord_INPUT : ImageRecord_Base
{
    public string[] StringTokens { get; set; }
    public string[] NumericTokens { get; set; }
    public ClassificationToken[] ClassificationTokens {get;set;}
};
