//UNITY_SHADER_NO_UPGRADE
Shader "Hidden/UnitySceneExtensions/internal/Grid"
{
	Properties
	{
		_GridSpacing			("Grid Spacing", Float) = (1.0, 1.0, 1.0)
//		_GridSubdivisions		("Grid Subdivisions", Float) = (4, 4, 0, 0)
		_GridCenter				("Grid Center", Float) = (0, 0, 0, 1.0)

		_GridSize				("Grid Size", Float) = 1
		_GridColor				("Grid Color", Color) = (1.0, 1.0, 1.0, 0.5)
		_CenterColor			("Center Color", Color) = (1.0, 1.0, 1.0, 1.0)
//		_StartLevel				("Start Zoom Level", Float) = 4
		
		_GridLineThickness		("Grid Line Thickness", Float) = 0.125
		_CenterLineThickness	("Center Line Thickness", Float) = 1.5
//		_SubdivisionTransparency("Subdivision Transparency", Float) = 0.8
	}

	SubShader
	{
		Tags
		{ 
			"ForceSupported" = "True" 
			"Queue" = "Overlay+5105" 
			"IgnoreProjector" = "True" 
			"DisableBatching" = "True"
			"ForceNoShadowCasting" = "True" 
			"RenderType" = "Transparent" 
			"PreviewType" = "Plane" 
		}

		Pass
		{
			Blend One OneMinusSrcAlpha
			ColorMask RGB
			Offset -1, -1
			Cull Off
			Lighting Off
			ZTest LEqual
			ZWrite Off

			CGPROGRAM
				#pragma vertex vert
				#pragma fragment frag
				#pragma fragmentoption ARB_precision_hint_nicest
				#include "UnityCG.cginc"

				uniform float	_GridLineThickness;
				uniform float	_CenterLineThickness; 
				uniform float2	_GridSpacing;
//				uniform float2	_GridSubdivisions;

				uniform float	_GridSize;
				uniform float4	_GridCenter;
				 
				uniform fixed4	_GridColor;
				uniform fixed4	_CenterColor;
//				uniform float	_StartLevel;
//				uniform float	_SubdivisionTransparency;
				uniform float	_OrthoInterpolation;
				uniform float	_ViewSize;

				#define ORTHO (1 - UNITY_MATRIX_P[3][3])

				struct vertexInput 
				{
					float4 vertex : POSITION;
				};

				struct vertexOutput 
				{
					float4 pos			: SV_POSITION;
					float4 uv			: TEXCOORD0;
					float4 objectPos	: TEXCOORD1;
					float4 normal		: NORMAL;
				};

				vertexOutput vert(vertexInput input) 
				{
					half3 worldGridCenter	= half4(_WorldSpaceCameraPos.x, 0, _WorldSpaceCameraPos.z, 1);
					float4 objectCenter		= mul(unity_ObjectToWorld, float4(0,0,0,1));
					//objectCenter.xyz /= objectCenter.w;
					//objectCenter.w = 0.0f;
					//objectCenter.xyz -= _GridCenter;
					
					vertexOutput output;

					float3 normal		= normalize(mul((float3x3)unity_ObjectToWorld, float3(0, 1, 0)));
					float4 gridPlane	= float4(normal, -dot(normal, objectCenter));
					float3 cameraPos	= _WorldSpaceCameraPos.xyz;
					float  distance		= (gridPlane.x * cameraPos.x) + (gridPlane.y * cameraPos.y) + (gridPlane.z * cameraPos.z) + (gridPlane.w);
					float  gridSize		= max(1, abs(distance))  * 10000;

					float4 position		= float4(input.vertex.xzy * gridSize, 0);

					output.objectPos	= mul(unity_ObjectToWorld, position);
					output.uv			= float4(input.vertex.xy, gridSize, 1);// + objectCenter.xz;

					output.normal		= normalize(mul(unity_ObjectToWorld, fixed4(0, 1, 0, 0)));

					position.y	= 0;
					output.pos	= UnityObjectToClipPos(position);
					return output;
				}

				// sample small and large grid in both x and y direction
				fixed4 lineSampler4(half4 uv, half4 lineSize)
				{
					fixed4 t = abs((frac(uv + lineSize) / lineSize) - 1);
					fixed4 s = saturate(exp(-pow(t, 80)));
					return s;
				}

				// sample center grid lines in both x and y direction
				fixed2 centerLineSampler2(half2 uv, half2 lineSize)
				{
					fixed2 t = abs(((uv + lineSize) / lineSize) - 1);
					fixed2 s = saturate(exp(-pow(t, 80)));
					return s;
				}

				fixed4 gridMultiSampler(float2 uv, half3 camDelta, float camDistance, float camHeight, float angleFadeOut, float gridLineThickness, float centerLineThickness, float subdivisionTransparency, float subdivisionLineFactor)
				{
					const float temp3 = 4.0f;
					const float temp4 = 32;
					const half2 temp = half2(temp3, temp3); //_GridSubdivisions.xy;

					const half4 subdivisions			= half4(1, 1, 1.0 / temp);
					const half4 gridspacing				= _GridSpacing.xyxy;
					const half  startDepthPow			= (8.0 * temp4) / temp3;// pow(2, _StartLevel - 1);

					const float orthoThicknessScale			= 0.5 - (_OrthoInterpolation * 0.5);
					const float orthoCenterThicknessScale	= 1   - (_OrthoInterpolation * 1);

					const half4 constantThickness		= (startDepthPow * ((gridLineThickness - orthoThicknessScale) / 1000)) * half4(1, 1, temp * subdivisionLineFactor);
					const half4 startDistance			= startDepthPow * gridspacing / 16;
					const half4 constantStepSize		= gridspacing * subdivisions;
					const half2 constantCenterThickness = ((startDepthPow * ((centerLineThickness - orthoCenterThicknessScale) / 1000)) * half2(1, 1) / angleFadeOut);


					half4 gridDepth			= lerp(
													log2(_ViewSize   / startDistance), 
												    log2(camDistance / startDistance),
													_OrthoInterpolation);

					half4 gridDepthClamp	= max(2, gridDepth);
					half4 gridDepthFloor	= floor(gridDepthClamp);
					half4 gridDepthFrac		= 1 - (gridDepthClamp - gridDepthFloor);

					half4 stepSize			= constantStepSize * pow(2, gridDepthFloor);
					half2 centerStepSize	= constantStepSize * min(camDistance / startDistance, camHeight / startDistance);

					half4 lineDepth			= gridDepth;
					half4 lineDepthClamp	= max(0, lineDepth);
					half4 lineSize			= (constantThickness       / max(1, pow(4, lineDepthClamp    - lineDepth))) / temp4;
					half2 centerLineSize	= constantCenterThickness / temp4;//constantCenterThickness / max(1, pow(2, lineDepthClamp.xy - lineDepth.xy));
					
#if SHADER_TARGET < 25 // no anti-aliasing on older hardware because no fwidth!
					float4	values		= uv.xyxy;

					// get the x/y lines for the thick and the thin grid lines
					fixed4	resultB		= lineSampler4(values / stepSize, lineSize);

					// get the x/y lines for the thick and the thin grid lines, one level higher
					stepSize *= 2;
					fixed4	resultA		= lineSampler4(values / stepSize, lineSize);

					// fade in between the grid line levels
					fixed4	result		= lerp(resultA, resultB, gridDepthFrac);

					// get the most visible line for this pixel
					float t  = saturate(max(max(result.x, result.y), max(result.z * subdivisionTransparency, result.w * subdivisionTransparency)));
					//float t2 = saturate(max(max(result.x, result.y), max(result.z, result.w)));
					float t2 = (result.x + result.y + (result.z) + (result.w)) / 4.0;


					// get the center line thickness
					fixed2	resultC		= centerLineSampler2(values / centerStepSize, centerLineSize.xy);
					float c = saturate(max(resultC.x, resultC.y));
#else

					half4	pixelSize	= fwidth(uv).xyxy;
					
					float4	values		= uv.xyxy;
					half4	position1	= values + (half4( 0.125, -0.375,  0.125, -0.375) * pixelSize);
					half4	position2	= values + (half4(-0.375, -0.125, -0.375, -0.125) * pixelSize);
					half4	position3	= values + (half4( 0.375,  0.125,  0.375,  0.125) * pixelSize);
					half4	position4	= values + (half4(-0.125,  0.375, -0.125,  0.375) * pixelSize);

					// get the x/y lines for the thick and the thin grid lines
					float4	resultB		= lineSampler4(position1 / stepSize, lineSize) +
										  lineSampler4(position2 / stepSize, lineSize) +
										  lineSampler4(position3 / stepSize, lineSize) +
										  lineSampler4(position4 / stepSize, lineSize);
					
					// get the x/y lines for the thick and the thin grid lines, one level higher
					stepSize *= 2;
					float4	resultA		= lineSampler4(position1 / stepSize, lineSize) +
										  lineSampler4(position2 / stepSize, lineSize) +
										  lineSampler4(position3 / stepSize, lineSize) +
										  lineSampler4(position4 / stepSize, lineSize);
					
					// fade in between the grid line levels
					fixed4	result		= lerp(resultA, resultB, gridDepthFrac);
					 
					// get the most visible line for this pixel
					float t  = max(max(result.x, result.y), max(result.z * subdivisionTransparency, result.w * subdivisionTransparency)) / 4.0;
					float t2 = max(max(result.x, result.y), max(result.z, result.w)) / 4.0;
					//float t2 = (result.x + result.y + (result.z ) + (result.w )) / (4.0 * 4.0);


					// get the center line thickness
					float2	resultC = centerLineSampler2(position1 / centerStepSize, centerLineSize.xy) +
									  centerLineSampler2(position2 / centerStepSize, centerLineSize.xy) +
									  centerLineSampler2(position3 / centerStepSize, centerLineSize.xy) +
									  centerLineSampler2(position4 / centerStepSize, centerLineSize.xy);
					float c = max(resultC.x, resultC.y) / 2.0;

#endif

					float centerFadeOut = max(0.25 * (0.5 + (_OrthoInterpolation * 0.5)), c * angleFadeOut);
					float lineFadeOut	= (t * angleFadeOut) * (1 - centerFadeOut);
					
					// interpolate between the center grid lines and the regular grid

					//return	(fixed4(gridLineColor.rgb,    gridLineColor.a  * lineFadeOut) * (1 - c)) +
					//			(fixed4(centerLineColor.rgb, centerLineColor.a * centerFadeOut) *      c)
					return saturate(fixed4(c, lineFadeOut, centerFadeOut, t2));
				}
				 
				half4 frag(vertexOutput input) : COLOR
				{
					float3	camDelta		= _WorldSpaceCameraPos.xyz - (input.objectPos.xyz * _OrthoInterpolation);
					float	camDistance		= length(camDelta);
					float	camHeight		= length(_WorldSpaceCameraPos.xyz);
					
					
					// fade out on grazing angles to hide line aliasing
					float	vertexAngle		= abs(dot(camDelta / camDistance, input.normal)) * _OrthoInterpolation;
					float	planeAngle		= abs(dot(normalize(UNITY_MATRIX_IT_MV[2].xyz), input.normal)) * (1 - _OrthoInterpolation);
					float	angleFadeOut	= sqrt(abs(max(vertexAngle, planeAngle)) * max(1.0f, 0.01f * abs(8.0f - min(8.0f, camDistance))));

					float2	uv = input.uv.xy * input.uv.z;

					// get the color for our pixel

					fixed4	shadowLerp	= gridMultiSampler(uv, camDelta, camDistance, camHeight, angleFadeOut, _GridLineThickness + 3.0, _CenterLineThickness + 3.0, 1.00, 1.00);
					fixed4	colorLerp	= gridMultiSampler(uv, camDelta, camDistance, camHeight, angleFadeOut, _GridLineThickness + 1.5, _CenterLineThickness + 1.0, 0.80, 0.75);

					fixed4	shadowColor = fixed4(0, 0, 0, lerp(shadowLerp.y, shadowLerp.z, shadowLerp.x) * 0.125);

					float	diff		= max(colorLerp.x, shadowLerp.x);

					fixed4	color		= lerp(fixed4(_GridColor.rgb,      _GridColor.a   * colorLerp.y),
										       fixed4(_CenterColor.rgb,    _CenterColor.a * colorLerp.z), diff);

					shadowColor.a   *= (1 - color.a);
					shadowColor.rgb *= shadowColor.a;
					color.rgb		*= color.a * 1.5f;

					//color += shadowColor;

					//color = lerp(color, shadowColor, (1 - colorLerp.w) * _OrthoInterpolation);
					//color.a = 1.0;
					//color.a *= _GridColor.a;
					//color.rgba = float4(uv, 0, 1);

					// pre-multiply our rgb color by our alpha
					//color.rgb *= color.a;

					return color;
				}
			ENDCG
		}
	}
}