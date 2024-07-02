using System;
using System.Globalization;
using System.Text;
using UnityEngine;
using Chisel.Core;

namespace Chisel.Editors
{
    public enum DistanceUnit
    {
        Meters,
        Centimeters,
        Millimeters,
        Inches,
        Feet
    }

    public enum PixelUnit
    {
        Relative,
        Pixels
    }

    public static class Units
    {
        public static DistanceUnit	ActiveDistanceUnit	= DistanceUnit.Meters;
        public static PixelUnit		ActivePixelUnit		= PixelUnit.Pixels;


        public const string DegreeUnitSymbol		= "°";
        public const string PercentageUnitSymbol	= "%";

        public static readonly GUIContent DegreeUnitContent		= new GUIContent(DegreeUnitSymbol);
        public static readonly GUIContent PercentageUnitContent	= new GUIContent(PercentageUnitSymbol);


        static string[] pixelUnitStrings = 
        {
            string.Empty,
            "pixels"
        };

        static GUIContent[] pixelUnitGUIContent = 
        {
            new GUIContent(pixelUnitStrings[0]),
            new GUIContent(pixelUnitStrings[1])
        };
        
        public static string		GetUnitString		(PixelUnit unit) { return pixelUnitStrings[(int)unit]; }
        public static GUIContent	GetUnitGUIContent	(PixelUnit unit) { return pixelUnitGUIContent[(int)unit]; }



        static string[] distanceUnitStrings = 
        {
            "m",
            "cm",
            "mm",
            "ft",
            "\""
        };

        static GUIContent[] distanceUnitGUIContent = 
        {
            new GUIContent(distanceUnitStrings[0]),
            new GUIContent(distanceUnitStrings[1]),
            new GUIContent(distanceUnitStrings[2]),
            new GUIContent(distanceUnitStrings[3]),
            new GUIContent(distanceUnitStrings[4])
        };

        public static string		GetUnitString		(DistanceUnit unit) { return distanceUnitStrings[(int)unit]; }
        public static GUIContent	GetUnitGUIContent	(DistanceUnit unit) { return distanceUnitGUIContent[(int)unit]; }
        


        public static DistanceUnit	CycleToNextUnit		(DistanceUnit unit)
        {
            if (unit < DistanceUnit.Meters)
                return DistanceUnit.Meters;
            return (DistanceUnit)((int)(unit + 1) % (((int)DistanceUnit.Feet) + 1));
        }

        
        public static string		ToPixelsString		(PixelUnit unit, Vector2 value)
        {
            string unitString = GetUnitString(unit);
            if (!String.IsNullOrEmpty(unitString))
                unitString = " " + unitString;
            return string.Format(CultureInfo.InvariantCulture, "x:{0:F}{2}\ny:{1:F}{2}", UnityToPixelsUnit(unit, value.x), UnityToPixelsUnit(unit, value.y), unitString);
        }
        
        public static string		ToRoundedPixelsString(PixelUnit unit, Vector2 value)
        {
            string unitString = GetUnitString(unit);
            if (!String.IsNullOrEmpty(unitString))
                unitString = " " + unitString;
            float x = (long)Math.Round((double)value.x * 16384) / 16384.0f;
            float y = (long)Math.Round((double)value.y * 16384) / 16384.0f;
            return string.Format(CultureInfo.InvariantCulture, "u:{0:F}{2}\nv:{1:F}{2}", UnityToPixelsUnit(unit, x), UnityToPixelsUnit(unit, y), unitString);
        }
        
        public static double		UnityToPixelsUnit	(PixelUnit unit, float value) 
        {
            switch (unit)
            {
                case PixelUnit.Relative:		return (double)value;
                case PixelUnit.Pixels:			return (double)(value * 4096.0);
            }
            Debug.LogWarning("Tried to convert value to unknown pixel unit");
            return (double)value;
        }
        
        public static float			UnityFromPixelsUnit	(PixelUnit unit, double value) 
        {
            switch (unit)
            {
                case PixelUnit.Relative:		return (float)value;
                case PixelUnit.Pixels:			return (float)(value / 4096.0);
            }
            Debug.LogWarning("Tried to convert value to unknown pixel unit");
            return (float)value;
        }

        
        public static string		ToDistanceString	(float value, string name = null)
        {
            return ToDistanceString(ActiveDistanceUnit, value, name);
        }
        
        public static string		ToDistanceString	(DistanceUnit unit, float value, string name = null)
        {
            if (string.IsNullOrEmpty(name))
                return string.Format(CultureInfo.InvariantCulture, "{0} {1}", UnityToDistanceUnit(unit, value), GetUnitString(unit));
            else
                return string.Format(CultureInfo.InvariantCulture, "{0} {1} ({2})", UnityToDistanceUnit(unit, value), GetUnitString(unit), name);
        }		
        
        public static string		ToDistanceString	(DistanceUnit unit, Vector3 value, bool lockX = false, bool lockY = false, bool lockZ = false)
        {
            var builder		= new StringBuilder();
            var unit_string = GetUnitString(unit);
            if (lockX) builder.Append("x: --\n");
            else builder.AppendFormat(CultureInfo.InvariantCulture, "x: {0} {1}\n", UnityToDistanceUnit(unit, value.x), unit_string);
            if (lockY) builder.Append("y: --\n");
            else builder.AppendFormat(CultureInfo.InvariantCulture, "y: {0} {1}\n", UnityToDistanceUnit(unit, value.y), unit_string);
            if (lockZ) builder.Append("z: --");
            else builder.AppendFormat(CultureInfo.InvariantCulture, "z: {0} {1}", UnityToDistanceUnit(unit, value.z), unit_string);
            return builder.ToString();
        }

        
        public static string		ToRoundedDistanceString	(DistanceUnit unit, float value)
        {
            if (float.IsNaN(value))
                return "??";

            return string.Format(CultureInfo.InvariantCulture, "{0:F} {1}", UnityToDistanceUnit(unit, value), GetUnitString(unit));
        }
        
        
        public static string		ToRoundedDistanceString	(DistanceUnit unit, Vector3 value, bool lockX = false, bool lockY = false, bool lockZ = false)
        {
            var builder		= new StringBuilder();
            var unit_string = GetUnitString(unit);
            if (lockX) builder.Append("x: --\n");
            else builder.AppendFormat(CultureInfo.InvariantCulture, "x: {0:F} {1}\n", UnityToDistanceUnit(unit, value.x), unit_string);
            if (lockY) builder.Append("y: --\n");
            else builder.AppendFormat(CultureInfo.InvariantCulture, "y: {0:F} {1}\n", UnityToDistanceUnit(unit, value.y), unit_string);
            if (lockZ) builder.Append("z: --");
            else builder.AppendFormat(CultureInfo.InvariantCulture, "z: {0:F} {1}",   UnityToDistanceUnit(unit, value.z), unit_string);
            return builder.ToString();
        }

        
        public static string		ToRoundedScaleString	(Vector3 value, bool lockX = false, bool lockY = false, bool lockZ = false)
        {
            var builder		= new StringBuilder();
            if (lockX) builder.Append("x: --\n");
            else builder.AppendFormat(CultureInfo.InvariantCulture, "x: {0:F}{1}\n", value.x * 100, PercentageUnitSymbol);
            if (lockY) builder.Append("y: --\n");
            else builder.AppendFormat(CultureInfo.InvariantCulture, "y: {0:F}{1}\n", value.y * 100, PercentageUnitSymbol);
            if (lockZ) builder.Append("z: --");
            else builder.AppendFormat(CultureInfo.InvariantCulture, "z: {0:F}{1}",   value.z * 100, PercentageUnitSymbol);
            return builder.ToString();
        }


        public static string		ToAngleString		(float value)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0,4}{1}", value, DegreeUnitSymbol);//(value % 360));
        }

        public static string		ToRoundedAngleString(float value)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:F}{1}", value, DegreeUnitSymbol);// (value % 360));
        }
        
        const double meter_to_centimeter	= 100.0;
        const double meter_to_millimeter	= 1000.0;
        const double meter_to_inches		= 39.37007874;
        const double meter_to_feet			= 3.28084;
        const double emperial_rounding		= 10000000;

        public static double		UnityToDistanceUnit	(DistanceUnit unit, float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return (double)value;

            double result = (double)(Decimal)value;
            switch (unit)
            {
                // values are in meters by default in unity
                case DistanceUnit.Meters:       return result;
                case DistanceUnit.Centimeters:	result *= meter_to_centimeter; break;
                case DistanceUnit.Millimeters:	result *= meter_to_millimeter; break;
                case DistanceUnit.Inches:		result *= meter_to_inches; result = Math.Round(result * emperial_rounding) / emperial_rounding; break;
                case DistanceUnit.Feet:			result *= meter_to_feet;   result = Math.Round(result * emperial_rounding) / emperial_rounding; break;
                default:
                {
                    Debug.LogWarning("Tried to convert value to unknown distance unit");
                    return value;
                }
            }

            return (double)result;
        }
        


        public static float			DistanceUnitToUnity	(DistanceUnit unit, double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return (float)value;

            double result = (double)value;
            switch (unit)
            {
                // values are in meters by default in unity
                case DistanceUnit.Meters:		break;
                case DistanceUnit.Centimeters:	result /= meter_to_centimeter; break;
                case DistanceUnit.Millimeters:	result /= meter_to_millimeter; break;
                case DistanceUnit.Inches:		result /= meter_to_inches; result = Math.Round(result * emperial_rounding) / emperial_rounding; break;
                case DistanceUnit.Feet:			result /= meter_to_feet;   result = Math.Round(result * emperial_rounding) / emperial_rounding; break;
                default:
                {
                    Debug.LogWarning("Tried to convert value from unknown distance unit");
                    return (float)value;
                }
            }
            
            return (float)result;
        }
    }
}
