//UNITY_SHADER_NO_UPGRADE
Shader "Hidden/Chisel/internal/customNoDepthSurface"
{
	Properties 
	{
	}
	SubShader 
	{
		Tags 
		{
			"ForceSupported" = "True"
			"Queue" = "Overlay+6000" 
			"IgnoreProjector" = "True"
			"DisableBatching" = "True"
			"ForceNoShadowCasting" = "True"
			"RenderType" = "Transparent"
			"PreviewType" = "Plane"
		}
		LOD 200
		Offset -1, -1
		ZTest Off
        Lighting Off
		Cull Off
        ZWrite Off
		Blend One OneMinusSrcAlpha

        Pass 
		{
			CGPROGRAM
				
				#pragma vertex vert
				#pragma fragment frag
			
				#include "UnityCG.cginc"

				struct v2f 
				{
 					float4 pos   : SV_POSITION;
 					fixed4 color : COLOR0;
				};

				v2f vert (appdata_full v)
				{
					v2f o;
					o.pos	= mul (UNITY_MATRIX_MVP, v.vertex);
					o.color = v.color;
					return o;
				}

				fixed4 frag (v2f input) : SV_Target
				{
					fixed4 col = input.color;
					col.rgb *= col.a;
					return col;
				}

			ENDCG
		}
	}
}
