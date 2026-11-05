using UnityEngine;

namespace Maily
{
    /// <summary>
    /// Art bible — the single colour source for the whole game.
    /// Style: sunny low-poly kibbutz afternoon; flat-shaded shapes, cream plaster houses with
    /// terracotta roofs, lush lawns under big ficus and eucalyptus trees.
    /// Mood: late Mediterranean sun from the west, cool blue sky bounce, warm sandy kurkar ground.
    /// </summary>
    public static class Palette
    {
        static Color H(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }

        // Ground & surfaces
        public static readonly Color Kurkar      = H("#D8C297"); // sandy hill ground / dry field
        public static readonly Color Field       = H("#C7B276"); // stubble fields outside the fence
        public static readonly Color Lawn        = H("#78AE4A"); // kibbutz lawns
        public static readonly Color LawnDeep    = H("#5E9440"); // parks / wood floor
        public static readonly Color Asphalt     = H("#5E6066"); // roads
        public static readonly Color PathStone   = H("#CDBB9C"); // footways
        public static readonly Color Parking     = H("#8A8780");
        public static readonly Color PoolWater   = H("#3FB6D9");
        public static readonly Color Playground  = H("#E2A15A"); // rubber play surface
        public static readonly Color Pitch       = H("#4F9A54");

        // Buildings
        public static readonly Color WallCream   = H("#EFE6D2");
        public static readonly Color WallWhite   = H("#F6F2EA");
        public static readonly Color WallOchre   = H("#E6CFA4");
        public static readonly Color RoofTile    = H("#C2573A"); // terracotta
        public static readonly Color RoofFlat    = H("#B9B3A8");
        public static readonly Color Industrial  = H("#B7BEC4");
        public static readonly Color Greenhouse  = H("#DDEBE6");

        // Vegetation
        public static readonly Color Foliage     = H("#4F8238"); // ficus
        public static readonly Color FoliageGray = H("#7F9C6B"); // eucalyptus
        public static readonly Color FoliageLime = H("#8CB848"); // citrus
        public static readonly Color Trunk       = H("#7A5A3C");
        public static readonly Color Citrus      = H("#F29A2E");

        // Characters
        public static readonly Color DogCream    = H("#F1DDB4");
        public static readonly Color DogBrown    = H("#8B5A35");
        public static readonly Color DogBlack    = H("#2E2A28");
        public static readonly Color DogGinger   = H("#D88A3D");
        public static readonly Color DogWhite    = H("#F4F1EA");
        public static readonly Color DogGray     = H("#8E8C88");
        public static readonly Color ParrotGreen = H("#46C34A"); // drara
        public static readonly Color ParrotDeep  = H("#2E8F3A");
        public static readonly Color ParrotBeak  = H("#D8343A");

        // Shadow & accents
        public static readonly Color Shadow      = H("#5E6F8C"); // cool slate
        public static readonly Color CoinGold    = H("#F5C542"); // accent — everything that scores glows
        public static readonly Color Bone        = H("#F3EEDF");
        public static readonly Color Heart       = H("#FF6B8A");

        // Light & sky
        public static readonly Color SunKey      = H("#FFE6C4");
        public static readonly Color SkyTop      = H("#8FBCE6");
        public static readonly Color SkyEquator  = H("#D3CBB5");
        public static readonly Color SkyGround   = H("#6E5A40");
        public static readonly Color Haze        = H("#CFDDE6");

        // UI
        public static readonly Color UiInk       = H("#2B2A33");
        public static readonly Color UiPaper     = H("#FFF8EC");
        public static readonly Color UiPanel     = H("#2B2A33E0");
    }
}
