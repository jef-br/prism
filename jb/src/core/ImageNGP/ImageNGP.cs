public class ImageNGP
{
    public enum Lighting
    {
        EASY,
        HARD,
        UNKNOWN
    }

    public enum Background
    {
        SOLIDCOLOR,
        STUDIO,
        REALLIFE,
        UNKNOWN
    }
    public enum TypeOfShot
    {
        PACKSHOT,           // Hero product image on a non-flat background
        ONMODEL,            // Hero PaP image
        DETAIL,             // Partial product shown
        LIFESTYLE,          // Additional image, normally in non-studio setting, has image frame intersections
        GHOST,              // Product image cut out on a 100% single-color (=flat) background
        FLAT,               // Product in full view, lies on a table. Camera is installed above the product pointing down towards the product. Usually for kids clothing. Can have heavy shadows or can be cut-out with a clippingpath.
        STILLIFE,
        UNKNOWN
    }
    public enum HERO_ORIENTATION
    {
        FRONT,
        RIGHT,
        BACK,
        LEFT,
        TOP,
        BOTTOM,
        UNKNOWN
    }
    public enum HERO_HASHEAD
    {
        FULL,
        NONE,
        PARTIAL,
        UNKNOWN
    }
    public enum HERO_IS_HUMAN
    {
        TRUE,
        FALSE,
        UNKNOWN
    }
}