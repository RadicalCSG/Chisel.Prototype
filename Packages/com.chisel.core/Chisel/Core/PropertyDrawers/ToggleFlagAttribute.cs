using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Chisel.Core
{
    public sealed class ToggleFlagAttribute : PropertyAttribute
    {
        // TODO: turn this in separate attribute
        public ToggleFlagAttribute(bool ignore)
        {
            this.Ignore = ignore;
            Debug.Assert(this.Ignore); // needs to be true to make sense
            this.ActiveIcon             = string.Empty;
            this.ActiveDescription      = string.Empty;
            this.InactiveIcon           = string.Empty;
            this.InactiveDescription    = string.Empty;
        }

        public ToggleFlagAttribute(string inactiveIcon, string inactiveDescription, 
                                   string activeIcon,   string activeDescription, bool ignore = false) 
        {
            this.Ignore                 = ignore;
            this.ActiveIcon             = activeIcon;
            this.ActiveDescription      = activeDescription;
            this.InactiveIcon           = inactiveIcon;
            this.InactiveDescription    = inactiveDescription;
        }

        public readonly bool    Ignore;
        public readonly string  ActiveIcon;
        public readonly string  ActiveDescription;
        public readonly string  InactiveIcon;
        public readonly string  InactiveDescription;
    }
}
