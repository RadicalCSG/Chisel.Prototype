using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Chisel.Core
{
    public sealed class ToggleFlagAttribute : PropertyAttribute
    {
        public ToggleFlagAttribute(string inactiveIcon, string inactiveDescription, 
                                   string activeIcon,   string activeDescription) 
        {
            this.ActiveIcon             = activeIcon;
            this.ActiveDescription      = activeDescription;
            this.InactiveIcon           = inactiveIcon;
            this.InactiveDescription    = inactiveDescription;
        }

        public readonly string ActiveIcon;
        public readonly string ActiveDescription;
        public readonly string InactiveIcon;
        public readonly string InactiveDescription;
    }
}
