using System;

namespace Chisel.Core
{
    static partial class CSGConstants
    {
        // TODO: do this properly
        public const float  kPlaneDistanceEpsilon	= 0.0006f;
        public const float  kDistanceEpsilon	    = 0.0001f;//0.01f;
        public const float  kSqrDistanceEpsilon	    = kDistanceEpsilon * kDistanceEpsilon;
        public const float  kMergeEpsilon	        = 0.0005f;
        public const float  kSqrMergeEpsilon	    = kMergeEpsilon * kMergeEpsilon;
        public const float  kNormalEpsilon			= 0.9999f;
        public const double kVertexEqualEpsilon     = 0.00001;
        public const double kVertexEqualEpsilonSqr  = kVertexEqualEpsilon * kVertexEqualEpsilon;
        public const double kEpsilon                = 0.00001;
    }
}
