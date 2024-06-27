using System;

namespace Chisel.Core
{
    [Flags]
    public enum PlacementFlags
    {
        [ToggleFlag("SizeFromBottom",   "Extrude from the bottom",
                    "SizeFromCenter",   "Extrude it from the center")]
        GenerateFromCenterY     = 1,

        
        [ToggleFlag("DragToHeight",     "Drag to extrude distance",
                    "AutoHeight",       "Extrude distance is determined by base size")]
        HeightEqualsXZ          = 2,

        
        [ToggleFlag("DragToHeight",     "Drag to extrude distance",
                    "AutoHeight",       "Extrude distance is determined by base size")]
        HeightEqualsHalfXZ      = 4,

        
        [ToggleFlag("RectangularBase",  "Base width and depth can be sized independently", 
                    "SquareBase",       "Base width and depth are identical in size")]
        SameLengthXZ            = 8,


        [ToggleFlag("SizeBaseFromCorner", "Base is sized from corner",
                    "SizeBaseFromCenter", "Base is sized from center")]
        GenerateFromCenterXZ    = 16,

        [ToggleFlag(ignore: true)] None = 0,
        [ToggleFlag(ignore: true)] AlwaysFaceUp         = 32,
        [ToggleFlag(ignore: true)] AlwaysFaceCameraXZ   = 64,
        [ToggleFlag(ignore: true)] UseLastHeight        = 128,
    }
}
