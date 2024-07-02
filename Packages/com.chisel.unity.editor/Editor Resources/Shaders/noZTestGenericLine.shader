//UNITY_SHADER_NO_UPGRADE
Shader "Hidden/Chisel/internal/NoZTestGenericLine"
{
	Properties 
	{
		_thicknessMultiplier("thicknessMultiplier", Float) = 1
		_dashMultiplier("dashMultiplier", Float) = 1
		_pixelsPerPoint("pixelsPerPoint", Float) = 1
		_alphaMultiplier("alphaMultiplier", Float) = 1
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
		LOD 200
		ColorMask RGB
		Lighting Off
		ZWrite Off
		ZTest Off
		Cull Off
		Blend One OneMinusSrcAlpha
		Offset -1, -50
		
		Pass 
		{
			CGPROGRAM
				
				#pragma vertex vert
				#pragma fragment frag
				#pragma target 2.0
			
				#include "UnityCG.cginc"

				struct v2f 
				{
					float4 vertex			: SV_POSITION;
					float4 vertex2			: TEXCOORD6;
					fixed4 color			: COLOR0;
					float4 screenPosition0	: TEXCOORD1;
					float4 screenPosition1	: TEXCOORD2;
					float4 screenPosition2	: TEXCOORD3;
					float4 screenPosition3	: TEXCOORD4;
					float4 params			: TEXCOORD5;
				};

				float4 WorldSpaceToScreenSpace(float4 position)
				{
					position.xy *= 0.5f;
					position.y *= _ProjectionParams.x;
					position.xy += position.w;
#if defined(UNITY_HALF_TEXEL_OFFSET)
					position.xy *= _ScreenParams.zw;
#endif
					position.xy /= position.w;

					position.xy *= _ScreenParams.xy;
					return position;
				}

				float4 ScreenSpaceToWorldSpace(float4 position)
				{
					position.xy /= _ScreenParams.xy;
					
					position.xy *= position.w;
#if defined(UNITY_HALF_TEXEL_OFFSET)
					position.xy /= _ScreenParams.zw;
#endif
					position.xy -= position.w;
					position.y /= _ProjectionParams.x;
					position.xy *= 2.0f;

					return position;
				}

				float _thicknessMultiplier;
				float _dashMultiplier;
				float _pixelsPerPoint;
				float _alphaMultiplier;
				
				v2f vert(float4 vertex1  : POSITION,
						 float3 vertex2  : TEXCOORD0, // second vertex to compute angle with
						 float4 params   : TEXCOORD1,
						 fixed4 color	 : COLOR0)
				{
					v2f o;

					float thicknessSign = sign(params.x);
					float thickness		= (params.x * thicknessSign) * _thicknessMultiplier;

					const float kExtraExtrusion = 2.0f;


					float4	out_vertex1 = UnityObjectToClipPos(       vertex1    );
					float4	out_vertex2 = UnityObjectToClipPos(float4(vertex2, 1));

					float4 screenPosition0 = WorldSpaceToScreenSpace(out_vertex1);
					float4 screenPosition1 = WorldSpaceToScreenSpace(out_vertex2);
					{
						float2 delta = normalize((screenPosition1 - screenPosition0).xy)  * ((thickness < 0.65f) ? 0.65f : thickness) * _pixelsPerPoint;
						float2 offset2D;

						offset2D.x = -delta.y;
						offset2D.y =  delta.x;
					
						screenPosition0.xy += offset2D * kExtraExtrusion * thicknessSign;
					}
					float4 out_vertex3 = ScreenSpaceToWorldSpace(screenPosition0);

					o.color = color;
					o.screenPosition0 = ComputeScreenPos(out_vertex1);
					o.screenPosition1 = ComputeScreenPos(out_vertex2);
					o.screenPosition2 = ComputeScreenPos(out_vertex1);

					out_vertex1 = out_vertex3;
					o.vertex  = out_vertex1;
					o.vertex2 = out_vertex2;

					o.screenPosition3 = ComputeScreenPos(out_vertex3);
					params.x = thickness * 0.25f;
					params.y *= (_dashMultiplier * max(1,_ScreenParams.z)) / 0.125f;

					//float3 eyePos		= UnityObjectToViewPos(out_vertex3.xyz) - UnityObjectToViewPos(out_vertex1.xyz);
					float3  pos0		= (o.screenPosition0.xyz / o.screenPosition0.w) * float3(_ScreenParams.xy, 1);
					float3  pos1		= (o.screenPosition1.xyz / o.screenPosition1.w) * float3(_ScreenParams.xy, 1);
					//params.w = params.z * ((1.0f / screenPosition0.w) * _ScreenParams.x);
					//params.z = params.z * ((1.0f / screenPosition0.w) * _ScreenParams.y);

					//float2  pos0		= (screenPosition0.xy / screenPosition0.w) * _ScreenParams.xy;

					//params.z *= 100000;

					//const float scale = 1000.0f;

					//params.z *= length(eyePos.xy);
					//params.z *= length(pos1 - pos0);
					o.params = params;

					return o;
				}

				fixed4 frag (v2f i) : SV_Target
				{
					fixed4	color = i.color;

					float	thickness	= i.params.x;
					float	dashSize	= i.params.y;


					// goes back and forth between dash on / dash off
					float2  pos0		= (i.screenPosition0.xy / i.screenPosition0.w);
					float2  pos1		= (i.screenPosition1.xy / i.screenPosition1.w);
					float   len			= (dashSize <= 0) ? 1 : (length((pos1 - pos0) * _ScreenParams.xy) / dashSize);
					//len += i.params.z;
					float	dash		= (dashSize <= 1) ? 1 : (frac(step(frac(len > 0 ? len + 0.25 : len), 0.5f) * 0.5f) * 2.0f);


					// fades line out into alpha, necessary for very long lines that are close to the camera (they get too wide)
					float2	middle		= i.screenPosition2.xy / i.screenPosition2.w;
					float2	extend		= i.screenPosition3.xy / i.screenPosition3.w;
					float   width		= length((extend - middle) * _ScreenParams.xy);
					float	edgefade	= smoothstep(0, width, smoothstep(0, width, thickness + 0.25));

					float	alpha		= _alphaMultiplier * dash * edgefade;

					if (alpha <= (0.5 / 255.0))
						discard;

					//color = float4(1, 1, 1, 1);
					
					color.a *= alpha;
					color.rgb *= color.a;

					return color;
				}

			ENDCG
		}
	}
}
