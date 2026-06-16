/// <summary>
/// PRISM image taxonomy: phenotype ID constants and feature value string constants.
/// All values are lowercase kebab-case or SCREAMING_SNAKE_CASE as defined in
/// <c>jb/docs/ImageNGP/imagePhenotypes.md</c> and <c>jb/docs/ImageNGP/ImageFeatures.md</c>.
/// Rules that map feature values to phenotype IDs live in <c>ImageRoles.json</c>.
/// </summary>
public static class ImageNGP
{
    /// <summary>All 26 phenotype IDs in first-match priority order.</summary>
    public static class Phenotypes
    {
        public const string FrontOnModelFullProduct = "front-on-model-full-product";
        public const string FrontOnModelPartial     = "front-on-model-partial";
        public const string BackOnModelFullProduct  = "back-on-model-full-product";
        public const string SideOnModel             = "side-on-model";
        public const string SittingOnModel          = "sitting-on-model";
        public const string OnModelWithAccessories  = "on-model-with-accessories";
        public const string ModelDetailCloseup      = "model-detail-closeup";
        public const string FrontPackshot           = "front-packshot";
        public const string BackPackshot            = "back-packshot";
        public const string SidePackshot            = "side-packshot";
        public const string DiagonalPackshot        = "diagonal-packshot";
        public const string TopPackshot             = "top-packshot";
        public const string BottomPackshot          = "bottom-packshot";
        public const string GhostFront              = "ghost-front";
        public const string GhostBack               = "ghost-back";
        public const string GhostSide               = "ghost-side";
        public const string FlatlayFront            = "flatlay-front";
        public const string FlatlayStyled           = "flatlay-styled";
        public const string CloseupImage            = "closeup-image";
        public const string PackagingShot           = "packaging-shot";
        public const string SizeChart               = "size-chart";
        public const string LifestyleHero           = "lifestyle-hero";
        public const string ScaleReferenceShot      = "scale-reference-shot";
        public const string InteriorShot            = "interior-shot";
        public const string IllustrationTechnicalDrawing = "illustration-technical-drawing";
        public const string LifestyleContext        = "lifestyle-context";
    }

    /// <summary>
    /// Values for the <c>hero-orientation</c> feature.
    /// Describes the facing direction of the primary subject (product or on-model figure).
    /// </summary>
    public static class Orientation
    {
        public const string Front    = "FRONT";
        public const string Back     = "BACK";
        public const string SideOn   = "SIDEON";
        public const string Diagonal = "DIAGONAL";
        public const string Top      = "TOP";
        public const string Bottom   = "BOTTOM";
        public const string Unknown  = "UNKNOWN";
    }

    /// <summary>
    /// Values for the <c>hero-is-human</c> feature.
    /// True when the primary subject of the image is a human (model, person, mannequin excluded).
    /// </summary>
    public static class HeroIsHuman
    {
        public const string True    = "TRUE";
        public const string False   = "FALSE";
        public const string Unknown = "UNKNOWN";
    }

    /// <summary>
    /// Values for the <c>background-type</c> feature.
    /// </summary>
    public static class BackgroundType
    {
        public const string SolidColor = "SOLIDCOLOR";
        public const string RealLife   = "REALLIFE";
        public const string Studio     = "STUDIO";
        public const string Unknown    = "UNKNOWN";
    }

    /// <summary>
    /// Values for the <c>occlusion-level</c> feature.
    /// Derived from <c>intersection-count</c>.
    /// </summary>
    public static class OcclusionLevel
    {
        public const string FullProduct        = "full-product";
        public const string MostlyVisible      = "mostly-visible";
        public const string PartiallyOccluded  = "partially-occluded";
        public const string Closeup            = "closeup";
        public const string Unknown            = "UNKNOWN";
    }

    /// <summary>Values for the <c>head-visible</c> feature.</summary>
    public static class HeadVisible
    {
        public const string Full    = "FULL";
        public const string Partial = "PARTIAL";
        public const string None    = "NONE";
        public const string Unknown = "UNKNOWN";
    }

    /// <summary>Values for the <c>body-visible</c> feature.</summary>
    public static class BodyVisible
    {
        public const string Full         = "full";
        public const string ThreeQuarter = "three-quarter";
        public const string Half         = "half";
        public const string Bust         = "bust";
        public const string None         = "none";
        public const string Unknown      = "UNKNOWN";
    }
}
