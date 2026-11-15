Shader "EC/OutlineDilate"
{
    // ========================================================================
    // EC/OutlineDilate.shader — 形态学链（两个 Pass）
    // ----------------------------------------------------------------------
    // Pass 0 "ECOutlineDownsample"：全分辨率遮罩 _ECOutlineMask -> 半分辨率 enc
    // Pass 1 "ECOutlineDilate"    ：半分辨率 enc -> 半分辨率 4 档形态学
    //
    // 【为什么在半分辨率上做形态学】
    //   集显 1080p30 的填充率上限是硬约束。形态学是纯采样密集型（每档 9 抽样），
    //   放半分辨率（960x540）后采样数降到 1/4；最终合成仍在全分辨率，
    //   轮廓边缘以全分辨率遮罩为界，不会软。
    //
    // 【长度量化】
    //   形态学在**全分辨率像素**上定义，但**采样发生在半分辨率**，
    //   因此最小可分辨单位 = 2 个全分辨率像素（@1080p）。
    //   实际实现：半分辨率下的 3x3 稀疏网格，间距 = 1 / 2 个半分辨率 texel。
    //
    // 【不新增资产】
    //   本 Shader 由 Renderer Feature 在运行时用 Shader.Find + CreateEngineMaterial
    //   创建，**工程内不存在对应 .mat 资产**，不占用任何模型材质槽。
    // ========================================================================
    Properties
    {
        _ECOutlineMaskSize ("Mask Size (w,h,1/w,1/h)", Vector) = (1920, 1080, 0.00052, 0.00092)
        _ECOutlineChainSize ("Chain Size (w,h,1/w,1/h)", Vector) = (960, 540, 0.00104, 0.00185)
        _ECOutlinePixelScale ("Outline Pixel Scale", Float) = 1
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D_X(_ECOutlineMask);
        SAMPLER(sampler_ECOutlineMask);

        TEXTURE2D_X(_ECOutlineEnc);
        SAMPLER(sampler_ECOutlineEnc);

        float4 _ECOutlineMaskSize;
        float4 _ECOutlineChainSize;
        float  _ECOutlinePixelScale;

        struct ECFSAttributes { uint vertexID : SV_VertexID; };
        struct ECFSVaryings   { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

        ECFSVaryings ECFSVert(ECFSAttributes input)
        {
            ECFSVaryings o;
            o.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
            o.uv = GetFullScreenTriangleTexCoord(input.vertexID);
            return o;
        }

        // enc 覆盖位：enc 为奇数 = 被覆盖
        float EC_CoverOf(float enc) { return frac(enc * 0.5) * 2.0; }
        ENDHLSL

        // --------------------------------------------------------------------
        // Pass 0：全分辨率遮罩 -> 半分辨率 enc（2x2 box max，保住 1px 细肢体）
        // --------------------------------------------------------------------
        Pass
        {
            Name "ECOutlineDownsample"
            Tags { "LightMode" = "ECOutlineDownsample" }

            Cull Off ZWrite Off ZTest Always Blend Off

            HLSLPROGRAM
            #pragma vertex ECFSVert
            #pragma fragment Frag
            #pragma target 3.0

            half4 Frag(ECFSVaryings input) : SV_Target
            {
                float2 texel = _ECOutlineMaskSize.zw;
                float2 o = texel * 0.5;

                float e0 = SAMPLE_TEXTURE2D_X_LOD(_ECOutlineMask, sampler_ECOutlineMask, input.uv + float2(-o.x, -o.y), 0).a;
                float e1 = SAMPLE_TEXTURE2D_X_LOD(_ECOutlineMask, sampler_ECOutlineMask, input.uv + float2(-o.x,  o.y), 0).a;
                float e2 = SAMPLE_TEXTURE2D_X_LOD(_ECOutlineMask, sampler_ECOutlineMask, input.uv + float2( o.x, -o.y), 0).a;
                float e3 = SAMPLE_TEXTURE2D_X_LOD(_ECOutlineMask, sampler_ECOutlineMask, input.uv + float2( o.x,  o.y), 0).a;

                float enc = max(max(e0, e1), max(e2, e3));
                return half4(enc, 0, 0, 1);
            }
            ENDHLSL
        }

        // --------------------------------------------------------------------
        // Pass 1：半分辨率 4 档形态学
        //   R = enc 外扩 2px（携带状态编码）
        //   G = enc 外扩 4px（携带状态编码）
        //   B = 覆盖 内缩 2px
        //   A = 覆盖 内缩 4px
        // --------------------------------------------------------------------
        Pass
        {
            Name "ECOutlineDilate"
            Tags { "LightMode" = "ECOutlineDilate" }

            Cull Off ZWrite Off ZTest Always Blend Off

            HLSLPROGRAM
            #pragma vertex ECFSVert
            #pragma fragment Frag
            #pragma target 3.0

            // 3x3 稀疏网格 max（近似方盒膨胀）。offset 单位 = 半分辨率 texel。
            float EncMax9(float2 uv, float spacing)
            {
                float m = 0.0;
                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 o = float2(x, y) * spacing * _ECOutlineChainSize.zw;
                        m = max(m, SAMPLE_TEXTURE2D_X_LOD(_ECOutlineEnc, sampler_ECOutlineEnc, uv + o, 0).a);
                    }
                }
                return m;
            }

            // 3x3 稀疏网格 min-of-coverage（近似方盒内缩）
            float CovMin9(float2 uv, float spacing)
            {
                float m = 0.0;
                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 o = float2(x, y) * spacing * _ECOutlineChainSize.zw;
                        float enc = SAMPLE_TEXTURE2D_X_LOD(_ECOutlineEnc, sampler_ECOutlineEnc, uv + o, 0).a;
                        m = max(m, 1.0 - EC_CoverOf(enc));
                    }
                }
                return 1.0 - m;
            }

            half4 Frag(ECFSVaryings input) : SV_Target
            {
                float2 uv = input.uv;

                // 半分辨率 texel 间距：
                //   间距 1 个半分辨率 texel = 2 个全分辨率像素（外扩/内缩 2px）
                //   间距 2 个半分辨率 texel = 4 个全分辨率像素（外扩/内缩 4px）
                float s1 = 1.0 * _ECOutlinePixelScale;
                float s2 = 2.0 * _ECOutlinePixelScale;

                float e2 = EncMax9(uv, s1);
                float e4 = EncMax9(uv, s2);
                float c2 = CovMin9(uv, s1);
                float c4 = CovMin9(uv, s2);

                return half4(e2, e4, c2, c4);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
