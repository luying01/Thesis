Shader "UI/RoundedPanel"
{
    // A rounded rectangle drawn with a signed distance field, so the corner
    // radius and border width are real numbers tuned in the Inspector rather
    // than baked into a sprite. Works on a world-space quad mesh and on a UGUI
    // Image alike.
    Properties
    {
        // UGUI's CanvasRenderer assigns a _MainTex to every material it draws
        // and errors if the shader does not declare one. The panel is drawn
        // entirely from the distance field, so the texture is never sampled.
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}

        _FillColor   ("Fill Colour",   Color) = (0.941, 0.890, 0.776, 1)
        _BorderColor ("Border Colour", Color) = (0.863, 0.780, 0.604, 1)

        // Both in units of half the panel height, so they stay consistent when
        // the panel is resized.
        _Radius      ("Corner Radius", Range(0, 1))   = 0.35
        _BorderWidth ("Border Width",  Range(0, 0.5)) = 0.05

        // Width / height of the quad. Kept in sync from script so the corners
        // stay circular instead of stretching into ellipses.
        _Aspect      ("Aspect (w/h)",  Float) = 2.0
        _Alpha       ("Master Alpha",  Range(0, 1)) = 1

        // Depth test as a material setting rather than hard-coded, so each
        // panel's backdrop can match its own text. 4 = LessEqual (hidden by
        // objects in front of it), 8 = Always (drawn through everything).
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4

        // Standard UGUI plumbing for masking.
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"           = "Transparent"
            "RenderType"      = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType"     = "Plane"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        ZWrite Off
        ZTest [_ZTest]
        ColorMask [_ColorMask]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // Required for single-pass instanced stereo. Without it the headset
            // draws this shader into the left eye only - invisible in the
            // desktop Game view, obvious in VR.
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                float4 color  : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos   : SV_POSITION;
                float2 uv    : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _FillColor;
            fixed4 _BorderColor;
            float  _Radius;
            float  _BorderWidth;
            float  _Aspect;
            float  _Alpha;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.pos   = UnityObjectToClipPos(v.vertex);
                o.uv    = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Panel space: _Aspect wide, 1 tall, centred on the origin.
                float2 half_size = float2(_Aspect, 1.0) * 0.5;
                float2 p = (i.uv - 0.5) * float2(_Aspect, 1.0);

                // The radius can never exceed the shorter side.
                float maxR = min(half_size.x, half_size.y);
                float r = min(_Radius, maxR);

                float2 inner = half_size - r;
                float dist = length(max(abs(p) - inner, 0.0)) - r;

                // About one pixel of antialiasing at any screen size.
                float aa = fwidth(dist) * 0.75 + 1e-6;

                float outside  = smoothstep(0.0, aa, dist);
                float inBorder = smoothstep(-_BorderWidth - aa,
                                            -_BorderWidth + aa, dist);

                fixed4 col = lerp(_FillColor, _BorderColor, inBorder);
                col.a *= (1.0 - outside) * _Alpha * i.color.a;
                col.rgb *= i.color.rgb;

                return col;
            }
            ENDCG
        }
    }

    Fallback Off
}
