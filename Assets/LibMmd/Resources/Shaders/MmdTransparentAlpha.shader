// Surface Shader's alpha:fade passes mask out alpha (ColorMask RGB).
// Preserve coverage separately for transparent RenderTextures / desktop compositing.
Shader "Hidden/LibMMD/TransparentAlpha"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Opacity ("Opacity", Float) = 1
    }
    CGINCLUDE
    #include "UnityCG.cginc"
    sampler2D _MainTex;
    float4 _MainTex_ST;
    float _Opacity;
    struct v2f_alpha { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
    v2f_alpha alpha_vert(appdata_base v)
    {
        v2f_alpha o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
        return o;
    }
    fixed4 alpha_frag(v2f_alpha i) : SV_Target
    {
        return fixed4(0, 0, 0, saturate(_Opacity * tex2D(_MainTex, i.uv).a));
    }
    fixed4 depth_frag(v2f_alpha i) : SV_Target
    {
        // Match MMD's depth-writing material layers, without allowing fully
        // invisible texels to occlude later layers.
        clip(_Opacity * tex2D(_MainTex, i.uv).a - 0.00001);
        return 0;
    }
    ENDCG
    SubShader
    {
        Pass
        {
            Name "DEPTH_CULLBACK"
            Tags { "LightMode" = "Always" }
            Cull Back
            ZWrite On
            ColorMask 0
            CGPROGRAM
            #pragma vertex alpha_vert
            #pragma fragment depth_frag
            ENDCG
        }
        Pass
        {
            Name "DEPTH_DOUBLE"
            Tags { "LightMode" = "Always" }
            Cull Off
            ZWrite On
            ColorMask 0
            CGPROGRAM
            #pragma vertex alpha_vert
            #pragma fragment depth_frag
            ENDCG
        }
        Pass
        {
            Name "CULLBACK"
            Tags { "LightMode" = "Always" }
            Cull Back
            ZWrite Off
            ColorMask A
            Blend One OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex alpha_vert
            #pragma fragment alpha_frag
            ENDCG
        }
        Pass
        {
            Name "DOUBLE"
            Tags { "LightMode" = "Always" }
            Cull Off
            ZWrite Off
            ColorMask A
            Blend One OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex alpha_vert
            #pragma fragment alpha_frag
            ENDCG
        }
    }
}
