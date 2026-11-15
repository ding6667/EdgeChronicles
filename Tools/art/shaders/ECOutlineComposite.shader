Shader "EC/OutlineComposite"
{
    // ========================================================================
    // EC/OutlineComposite.shader — 全屏合成 Pass
    // ----------------------------------------------------------------------
    // 输入
    //   _ECOutlineMask   全分辨率 RGBA8：R=覆盖 G/B=八面体视法线 A=状态编码
    //   _ECOutlineDil    半分辨率 RGBA8：R=enc(2px) G=enc(4px) B=覆盖内缩2px A=覆盖内缩4px
    //   _ECTempColor     全分辨率：相机颜色副本（合成前的画面）
    //
    // 输出：叠加描边后的最终颜色
    //
    // 【三种形态的带位（全分辨率像素，1080p 基准）】
    //   form 0 常规墨线    外 2px + 内 2px（跨轮廓，合计 4px）
    //   form 1 单边实描    外 4px 实心；开压边时 外侧 2px 为压边色、内侧 2px 为描边色
    //   form 2 双边中空    外：2px 中空(0~2) + 2px 描边(2~4)
    //                       内：2px 挖空(0~2) + 2px 描边(2~4)
    //   form 3 反相压边    常规墨线 + 外侧 2px 反相压边
    //
    // 【形态学量化说明】
    //   形态学在半分辨率上做，最小可分辨单位 = 2 个全分辨率像素（@1080p）。
    //   故 3px 墨线实现为 4px 跨轮廓带，1px 压边实现为 2px。
    //   依据见 docs/art/outline-urp14.md §6「长度量化表」。
    //
    // 【不新增资产】
    //   由 Renderer Feature 运行时创建，工程内无对应 .mat，不占材质槽。
    // ========================================================================
    Properties
    {
        _ECOutlineInkDelta ("Ink Min Luma Delta", Float) = 0.6
        _ECOutlinePressOn ("Global Press Edge On", Float) = 1
        _ECOutlineEdgeThreshold ("Internal Edge Threshold", Float) = 0.35
        _ECOutlineEdgeSoftness ("Internal Edge Softness", Float) = 0.25
        _ECOutlineBgRadius ("Background Sample Radius (px @1080p)", Float) = 56
        _ECOutlineQuality ("Quality (0=outline only, 1=+internal lines)", Float) = 1
        _ECOutlineUseSemanticColor ("Use Semantic Stroke Color", Float) = 1
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "ECOutlineComposite"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Off ZWrite Off ZTest Always Blend Off

            HLSLPROGRAM
            #pragma vertex ECFSVert
            #pragma fragment Frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "ECOutlineCommon.hlsl"

            TEXTURE2D_X(_ECOutlineMask);
            SAMPLER(sampler_ECOutlineMask);

            TEXTURE2D_X(_ECOutlineDil);
            SAMPLER(sampler_ECOutlineDil);

            TEXTURE2D_X(_ECTempColor);
            SAMPLER(sampler_ECTempColor);

            float4 _ECOutlineMaskSize;   // (w, h, 1/w, 1/h)  全分辨率遮罩
            float4 _ECOutlineDilSize;    // (w, h, 1/w, 1/h)  半分辨率形态学
            float4 _ECOutlineTempSize;   // (w, h, 1/w, 1/h)  相机颜色副本

            // 全局量（由 Renderer Feature 每帧 SetGlobal 写入，非材质属性）
            float _ECOutlinePixelScale;

            CBUFFER_START(UnityPerMaterial)
                float _ECOutlineInkDelta;
                float _ECOutlinePressOn;
                float _ECOutlineEdgeThreshold;
                float _ECOutlineEdgeSoftness;
                float _ECOutlineBgRadius;
                float _ECOutlineQuality;
                float _ECOutlineUseSemanticColor;
            CBUFFER_END

            struct ECFSAttributes { uint vertexID : SV_VertexID; };
            struct ECFSVaryings   { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            ECFSVaryings ECFSVert(ECFSAttributes input)
            {
                ECFSVaryings o;
                o.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                o.uv = GetFullScreenTriangleTexCoord(input.vertexID);
                return o;
            }

            // 状态解码：enc -> (covered, form, highlight)
            void EC_DecodeEnc(float enc, out float covered, out float form, out float highlight)
            {
                covered   = frac(enc * 0.5) * 2.0;
                float t   = floor(enc * 0.5);
                form      = t - 4.0 * floor(t * 0.25);
                highlight = floor(t * 0.25);
            }

            half4 Frag(ECFSVaryings input) : SV_Target
            {
                float2 uv  = input.uv;
                float2 texel = _ECOutlineMaskSize.zw;

                // ---------------- 采样 ----------------
                float4 maskC = SAMPLE_TEXTURE2D_X_LOD(_ECOutlineMask, sampler_ECOutlineMask, uv, 0);
                float4 dil   = SAMPLE_TEXTURE2D_X_LOD(_ECOutlineDil,  sampler_ECOutlineDil,  uv, 0);
                float3 color = SAMPLE_TEXTURE2D_X_LOD(_ECTempColor, sampler_ECTempColor, uv, 0).rgb;

                float covC, formC, highC;
                EC_DecodeEnc(maskC.a, covC, formC, highC);

                // 外侧像素自己身上没有状态编码，形态由形态学的 max 随膨胀一起传播过来。
                float cov2, cov4, formO2, formO4, highO2, highO4;
                EC_DecodeEnc(dil.r, cov2, formO2, highO2);
                EC_DecodeEnc(dil.g, cov4, formO4, highO4);

                // ---------------- 带位 ----------------
                // 外侧
                float outer2 = saturate(cov2 - covC);   // 0~2px 外
                float outer4 = saturate(cov4 - covC);   // 0~4px 外
                float mid2   = saturate(cov4 - cov2);   // 2~4px 外
                // 内侧（形态学内缩结果自带覆盖语义）
                float inner2   = saturate(covC - dil.b); // 0~2px 内
                float innerMid = saturate(dil.b - dil.a); // 2~4px 内

                // ---------------- 形态判定 ----------------
                // 轮廓内用中心像素的形态；轮廓外用膨胀携带的形态。
                bool  inside = (covC > 0.5);
                float form   = inside ? formC : formO4;
                float highlt = inside ? highC : highO4;

                float isF0 = 1.0 - step(0.5, form);
                float isF1 = step(0.5, form) * step(form, 1.5);
                float isF2 = step(1.5, form) * step(form, 2.5);
                float isF3 = step(2.5, form);

                // ---------------- 背景取样（推导墨线色 / 中空填充） ----------------
                // 6 个环采样，排除被角色覆盖的样本；环半径远大于角色半宽。
                const float2 kBgDirs[6] = {
                    float2( 1.0, 0.0), float2(-1.0, 0.0), float2(0.0,  1.0),
                    float2( 0.0,-1.0), float2( 0.7, 0.7), float2(-0.7,-0.7)
                };
                float bgRadius = _ECOutlineBgRadius * _ECOutlinePixelScale;
                float3 bgAcc = float3(0, 0, 0);
                float  bgWeight = 0.0;
                [unroll]
                for (int i = 0; i < 6; i++)
                {
                    float2 o = kBgDirs[i] * bgRadius * texel;
                    float4 m = SAMPLE_TEXTURE2D_X_LOD(_ECOutlineMask, sampler_ECOutlineMask, uv + o, 0);
                    float nc = EC_CoverOf(m.a);
                    float3 c = SAMPLE_TEXTURE2D_X_LOD(_ECTempColor, sampler_ECTempColor, uv + o, 0).rgb;
                    float w = 1.0 - nc;
                    bgAcc += c * w;
                    bgWeight += w;
                }
                float3 bgColor = (bgWeight > 0.001) ? (bgAcc / bgWeight) : color;

                // ---------------- 颜色推导 ----------------
                float bgL = EC_Luminance(bgColor);

                // 常规墨线：两极推导（暗底->纸白，亮底->墨黑）
                float3 inkCol = EC_DeriveInkColor(bgL, _ECOutlineInkDelta);
                // 霸体：朱砂基色 + 明度调整到 >= 3:1
                float3 solidCol = EC_AdjustValueForContrast(EC_Cinnabar(), bgColor, 3.0);
                // 无敌：两极推导（亮底自动倒相为墨黑）
                float3 hollowCol = EC_DeriveInkColor(bgL, _ECOutlineInkDelta);

                float3 strokeCol = inkCol;
                strokeCol = lerp(strokeCol, solidCol, isF1 * _ECOutlineUseSemanticColor);
                strokeCol = lerp(strokeCol, hollowCol, isF2);
                strokeCol = lerp(strokeCol, inkCol, isF3);

                float3 pressCol = EC_DerivePressColor(strokeCol);

                // 入窗亮起 1 帧：抬到该形态的明度极值一端（不闪、不脉冲）
                float3 highCol = (EC_Luminance(strokeCol) >= 0.5)
                               ? float3(1.0, 1.0, 1.0)
                               : float3(0.0, 0.0, 0.0);
                strokeCol = lerp(strokeCol, highCol, highlt * step(0.5, form));

                // ---------------- 内部结构线（角色本体内的墨线） ----------------
                float internal = 0.0;
                if (_ECOutlineQuality > 0.5)
                {
                    float3 nC = EC_OctDecode(maskC.gb);
                    float edge = 0.0;
                    const float2 kEdgeDirs[4] = {
                        float2( 1.0,  1.0), float2(-1.0,  1.0),
                        float2( 1.0, -1.0), float2(-1.0, -1.0)
                    };
                    [unroll]
                    for (int i = 0; i < 4; i++)
                    {
                        float2 o = kEdgeDirs[i] * (2.0 * _ECOutlinePixelScale) * texel;
                        float4 m = SAMPLE_TEXTURE2D_X_LOD(_ECOutlineMask, sampler_ECOutlineMask, uv + o, 0);
                        float nc = EC_CoverOf(m.a);
                        float3 nN = EC_OctDecode(m.gb);
                        edge = max(edge, (1.0 - dot(nC, nN)) * nc);
                    }
                    internal = saturate((edge - _ECOutlineEdgeThreshold)
                                        / max(_ECOutlineEdgeSoftness, 1e-3)) * covC;
                    // 挖空带内不画内部线
                    internal *= (1.0 - inner2);
                }

                // ---------------- 装配 ----------------
                float3 result = color;

                // (1) 中空 / 挖空：填背景色
                float hollow = isF2 * saturate(outer2 + inner2);
                result = lerp(result, bgColor, hollow);

                // (2) 描边带
                //   f0 / f3：常规墨线（外 2px + 内 2px）
                //   f1：外 4px；开压边时外侧 2px 归压边
                //   f2：外侧 2px(2~4) + 内侧 2px(2~4)
                float pressOn = _ECOutlinePressOn;

                float bandF0 = saturate(outer2 + inner2);
                float bandF1 = pressOn > 0.5 ? outer2 : outer4;
                float bandF2 = saturate(mid2 + innerMid);
                float bandF3 = bandF0;

                float strokeMask = isF0 * bandF0
                                 + isF1 * bandF1
                                 + isF2 * bandF2
                                 + isF3 * bandF3;
                result = lerp(result, strokeCol, saturate(strokeMask));

                // (3) 压边带（最外侧 2px，反明度）
                float pressMask = 0.0;
                pressMask += isF0 * mid2 * pressOn;                       // 墨线外侧
                pressMask += isF1 * saturate(mid2) * pressOn;              // 霸体外侧 2px
                pressMask += isF3 * mid2;                                 // form 3 恒带压边
                result = lerp(result, pressCol, saturate(pressMask));

                // (4) 内部结构线
                result = lerp(result, strokeCol, internal * (1.0 - isF2));

                return half4(result, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
