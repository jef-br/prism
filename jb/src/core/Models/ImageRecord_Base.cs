class ImageRecord_Base
{
    //Core
    public string InitialFullName {get;set;}
    public int Width { get; set; }
    public int Height { get; set; }

    //Renaming
    public string Family { get; set; }
    public int DetOrder { get; set; }
    public string NewName => $"{Family}_det{DetOrder}.jpg";
}