using System;

namespace Chisel.Core
{
    static partial class CSGConstants
    {
        // TODO: do this properly
        
        const double        kPlaneDAlignEpsilonDouble       = 0.0006;
        const double        kNormalDotAlignEpsilonDouble    = 0.9999;

        const double        kBoundsDistanceEpsilonDouble    = 0.0006;
        const double        kEdgeDistanceEpsilonDouble	    = 0.0006;
        const double        kVertexEqualEpsilonDouble	    = 0.005;
        const double        kFatPlaneWidthEpsilonDouble	    = 0.0006;

        public const float  kBoundsDistanceEpsilon	    = (float)kBoundsDistanceEpsilonDouble;

        public const float  kFatPlaneWidthEpsilon	    = (float)kFatPlaneWidthEpsilonDouble;
        public const float  kEdgeIntersectionEpsilon    = (float)kEdgeDistanceEpsilonDouble;
        public const float  kSqrEdgeDistanceEpsilon	    = (float)(kEdgeDistanceEpsilonDouble * kEdgeDistanceEpsilonDouble);
        
        public const float  kVertexEqualEpsilon	        = (float)(kVertexEqualEpsilonDouble * 2.5f);
        public const float  kSqrVertexEqualEpsilon	    = kVertexEqualEpsilon * kVertexEqualEpsilon;

        public const float  kNormalDotAlignEpsilon		= (float)kNormalDotAlignEpsilonDouble;
        public const float  kPlaneDAlignEpsilon	        = (float)kPlaneDAlignEpsilonDouble;

        public const double kDivideMinimumEpsilon       = 0.000001;
    }
}
