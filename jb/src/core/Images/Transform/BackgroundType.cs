/// <summary>
/// Describes the background type of an image, as determined by the Classified stage preprocessor.
/// Used by transform strategies to select the appropriate fill technique.
/// </summary>
public enum BackgroundType
{
    /// <summary>Uniform, studio-grade white or grey background with no texture.</summary>
    FLAT_PERFECT,

    /// <summary>Mostly flat background with slight colour variation or minor imperfections.</summary>
    FLAT_NATURAL,

    /// <summary>Background has visible pattern, grain, or texture.</summary>
    TEXTURED,

    /// <summary>Background contains lifestyle, environmental, or contextual scene content.</summary>
    AMBIANCE,

    /// <summary>Background type has not been determined.</summary>
    UNKNOWN
}
