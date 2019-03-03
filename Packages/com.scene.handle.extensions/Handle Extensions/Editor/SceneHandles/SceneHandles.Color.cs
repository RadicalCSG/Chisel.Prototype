using UnityEditor;
using UnityEngine;

namespace UnitySceneExtensions
{
	public sealed partial class SceneHandles
	{
		public static Color		preselectionColor       = new Color(201f / 255, 200f / 255, 144f / 255, 0.89f);
		public static Color		staticColor				= new Color(.5f, .5f, .5f, 0f);
		public static Color		s_DisabledHandleColor	= new Color(.5f, .5f, .5f, .5f);
		public static float		staticBlend				= 0.6f;
		public static float		backfaceAlphaMultiplier = 0.3f;
		
		public static Color		color					{ get { return UnityEditor.Handles.color; } set { UnityEditor.Handles.color = value; } }
		
		public static Color		selectedColor           { get { return UnityEditor.Handles.selectedColor; } }
		public static Color		secondaryColor			{ get { return UnityEditor.Handles.secondaryColor; } }
		public static Color		centerColor				{ get { return UnityEditor.Handles.centerColor; } }

		public static Color		xAxisColor				{ get { return UnityEditor.Handles.xAxisColor; } }
		public static Color		yAxisColor				{ get { return UnityEditor.Handles.yAxisColor; } }
		public static Color		zAxisColor				{ get { return UnityEditor.Handles.zAxisColor; } }


		public static Color ToActiveColorSpace(Color color)
		{
			return (QualitySettings.activeColorSpace == ColorSpace.Linear) ? color.linear : color;
		}

		public static Color StateColor(Color color, bool isDisabled = false, bool isSelected = false, bool isPreSelected = false)
		{
			return	ToActiveColorSpace((isDisabled || disabled) ? Color.Lerp(color, staticColor, staticBlend) : 
									   (isSelected) ? SceneHandles.selectedColor : 
									   (isPreSelected) ? SceneHandles.preselectionColor : color);
		}

		public static Color MultiplyTransparency(Color color, float alpha)
		{
			return new Color(color.r, color.g, color.b, color.a * alpha);
		}
	}
}
