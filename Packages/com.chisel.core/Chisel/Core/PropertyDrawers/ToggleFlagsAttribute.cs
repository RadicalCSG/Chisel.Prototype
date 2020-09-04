﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Chisel.Core
{
    public sealed class ToggleFlagsAttribute : PropertyAttribute
    {
        public ToggleFlagsAttribute(bool showPrefix = true, int includeFlags = ~0, int excludeFlags = 0)
        {
            this.ShowPrefix = showPrefix;
            this.IncludeFlags = includeFlags;
            this.ExcludeFlags = excludeFlags;
        }    
        public readonly bool ShowPrefix;
        public readonly int IncludeFlags;
        public readonly int ExcludeFlags;
    }
}
