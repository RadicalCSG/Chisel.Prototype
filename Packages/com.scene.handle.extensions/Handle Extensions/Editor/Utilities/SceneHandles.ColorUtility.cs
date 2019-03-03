using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace UnitySceneExtensions
{
	public static class ColorUtility
	{
		public static Color GetPreferenceColor(string name, Color defaultColor)
		{
			string prevname = string.Empty;
			Color resultColor = defaultColor;
			if (GetPreferenceColor(name, ref prevname, defaultColor, ref resultColor))
				return resultColor;
			return defaultColor;
		}

		public static bool GetPreferenceColor(string name, ref string prevname, Color defaultColor, ref Color resultColor)
		{
			var prefString	= EditorPrefs.GetString(name);
			if (prevname == prefString)
				return false; 

			prevname = prefString;
			var split		= prefString.Split(';');
			if (split.Length != 5)
			{
				resultColor = defaultColor;
				return false;
			}
		
			split[1] = split[1].Replace(',', '.');
			split[2] = split[2].Replace(',', '.');
			split[3] = split[3].Replace(',', '.');
			split[4] = split[4].Replace(',', '.');

			bool success;
			float r, g, b, a;
			success =  float.TryParse(split[1], NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture.NumberFormat, out r);
			success &= float.TryParse(split[2], NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture.NumberFormat, out g);
			success &= float.TryParse(split[3], NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture.NumberFormat, out b);
			success &= float.TryParse(split[4], NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture.NumberFormat, out a);

			if (success)
			{
				var newColor = new Color(r, g, b, a);
				if (newColor == resultColor)
					return false;
				resultColor = newColor;
				return true;
			}
			
			resultColor = defaultColor;
			return false;
		}

		public static Color CopyHueSaturation(Color src1, Color src2)
		{
			float h1, s1, v1;
			Color.RGBToHSV(src1, out h1, out s1, out v1);
			
			float h2, s2, v2;
			Color.RGBToHSV(src2, out h2, out s2, out v2);

			var dstColor = Color.HSVToRGB(h2, (s1 + s2) * 0.5f, v2);
			dstColor.a = src1.a;// (src1.a + src2.a) * 0.5f;

			return dstColor;
		}
		
		public static Color CopyValue(Color src1, Color src2)
		{
			float h1, s1, v1;
			Color.RGBToHSV(src1, out h1, out s1, out v1);
			
			float h2, s2, v2;
			Color.RGBToHSV(src2, out h2, out s2, out v2);

			var dstColor = Color.HSVToRGB(h2, s2, v1);
			dstColor.a = src2.a;// (src1.a + src2.a) * 0.5f;

			return dstColor;
		}
		
		public static Color InvertHue(Color src)
		{
			float h1, s1, v1;
			Color.RGBToHSV(src, out h1, out s1, out v1);
			
			var dstColor = Color.HSVToRGB(1-h1, s1, v1);
			dstColor.a = src.a;

			return dstColor;
		}

		public static Color Brighten(Color color, float brightScale)
		{
			float h, s, v;
			Color.RGBToHSV(color, out h, out s, out v);
			
			v *= brightScale;

			var outColor = Color.HSVToRGB(h, s, v);
			outColor.a = color.a;
			return outColor;
		}

	}
}
