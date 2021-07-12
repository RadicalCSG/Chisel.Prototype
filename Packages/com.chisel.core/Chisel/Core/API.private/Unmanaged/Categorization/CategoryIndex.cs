using System;

namespace Chisel.Core
{
    [Serializable]
    enum CategoryIndex : sbyte
    {
        None                = -1,
#if HAVE_SELF_CATEGORIES
        Inside = 0,
        Aligned             = 1,
        SelfAligned         = 2,
        SelfReverseAligned  = 3,
        ReverseAligned      = 4,
        Outside             = 5,
        
        ValidAligned        = SelfAligned,
        ValidReverseAligned = SelfReverseAligned,
#else
        Inside              = 0,
        Aligned             = 1,
        ReverseAligned      = 2,
        Outside             = 3,

        ValidAligned        = Aligned,
        ValidReverseAligned = ReverseAligned,
#endif
        LastCategory        = Outside
    };


    enum EdgeCategory : sbyte
    {
        None                = -1,

        LastCategory        = 3,

        Inside              = 0,
        Aligned             = 1,
        ReverseAligned      = 2,
        Outside             = 3
    };
}
