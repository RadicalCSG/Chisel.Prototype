using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Chisel.Editors
{
    public static class ColorManager
    {
        // TODO: create proper color management
        //static Color kUnselectedOutlineColor	= new Color(255.0f / 255.0f, 102.0f / 255.0f, 55.0f / 255.0f, 128.0f / 255.0f);
        public static Color kUnselectedOutlineColor		= new Color( 94.0f / 255.0f, 119.0f / 255.0f, 155.0f / 255.0f, 255.0f / 255.0f);
        public static Color kPreSelectedOutlineColor	= new Color(201.0f / 255.0f, 200.0f / 255.0f, 144.0f / 255.0f, 255.0f / 255.0f);
        public static Color kSelectedHoverOutlineColor	= new Color(246.0f / 255.0f, 242.0f / 255.0f,  50.0f / 255.0f, 255.0f / 255.0f);
        public static Color kSelectedOutlineColor		= new Color(255.0f / 255.0f, 102.0f / 255.0f,   0.0f / 255.0f, 255.0f / 255.0f);

    }
}
