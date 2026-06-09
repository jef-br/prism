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
        PACKSHOT=100,           // Hero product image on a non-flat background
        ONMODEL=85,            // Hero PaP image
        GHOST=70,              // Product image cut out on a 100% single-color (=flat) background
        FLAT=55,               // Product in full view, lies on a table. Camera is installed above the product pointing down towards the product. Usually for kids clothing. Can have heavy shadows or can be cut-out with a clippingpath.
        DETAIL=20,             // Partial product shown
        LIFESTYLE=12,          // Additional image, normally in non-studio setting, has image frame intersections
        STILLIFE=6,
        UNKNOWN=0
    }
    public enum HERO_ORIENTATION
    {
        FRONT=30,
        DIAGONAL=24,
        SIDEON=18,
        TOP=12,
        BOTTOM=4,
        BACK=2,
        UNKNOWN=8
    }
    public enum HERO_HASHEAD
    {
        FULL=5,
        PARTIAL=3,
        UNKNOWN=2.5,
        NONE=0
    }
    public enum HERO_IS_HUMAN
    {
        TRUE=5,
        FALSE=0,
        UNKNOWN=2.5
    }
}