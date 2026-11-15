Shader "EC/OutlineMask"
{
    // ========================================================================
    // EC/OutlineMask.shader — 独立遮罩 Shader
    // ----------------------------------------------------------------------
    // 用途：
    //   1) 管线 bring-up：在 5 个共享材质尚未存在时，先用本 Shader 验证
    //      "遮罩 -> 形态学 -> 合成" 链路是否跑通。
    //   2) 兜底：若将来出现不属于 5 共享槽、需要进描边通道的物件，
    //      可临时改用本 Shader。
    //
    // 正式路径：5 个共享材质的 shader 各 #include 一份
    //           "ECOutlineMaskPass.hlsl" 里的同名 Pass
    //           （见 docs/art/outline-urp14.md §4.3）。
    //
    // 本 Shader **不作为模型材质槽使用**：它不带任何材质槽语义，
    // 也不新增 .mat 资产。
    // ========================================================================
    Properties
    {
        [HideInInspector] _OutlineForm ("Outline Form (0/1/2/3)", Float) = 0
        [HideInInspector] _OutlineWidthPx ("Outline Width (px @1080p)", Float) = 3
        [HideInInspector] _OutlineHollowGapPx ("Hollow Gap (px @1080p)", Float) = 2
        [HideInInspector] _OutlineColor ("Outline Color", Color) = (0, 0, 0, 0)
        [HideInInspector] _OutlinePressColor ("Outline Press Color", Color) = (0, 0, 0, 0)
        [HideInInspector] _OutlineHighlight ("Highlight 1 Frame", Float) = 0
        [HideInInspector] _OutlineInkDelta ("Ink Min Luma Delta", Float) = 0.6
        [HideInInspector] _OutlinePixelScale ("Outline Pixel Scale", Float) = 1
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }

        Pass
        {
            Name "ECOutlineMask"
            Tags { "LightMode" = "ECOutlineMask" }

            Cull Back
            ZWrite Off
            ZTest LEqual
            ColorMask R G B A

            HLSLPROGRAM
            #pragma vertex ECOutlineMaskVertex
            #pragma fragment ECOutlineMaskFragment
            #pragma target 3.0
            #pragma multi_compile_instancing

            // SRP Batcher：属性必须声明在 UnityPerMaterial CBUFFER 内。
            // 5 个共享材质要把这 8 个属性加进它们**已有的** UnityPerMaterial CBUFFER，
            // 不要在这里另开一个同名 CBUFFER（见 ECOutlineCommon.hlsl 顶部说明）。
            CBUFFER_START(UnityPerMaterial)
                float _OutlineForm;
                float _OutlineWidthPx;
                float _OutlineHollowGapPx;
                float4 _OutlineColor;
                float4 _OutlinePressColor;
                float _OutlineHighlight;
                float _OutlineInkDelta;
                float _OutlinePixelScale;
            CBUFFER_END

            #include "ECOutlineMaskPass.hlsl"
            ENDHLSL
        }
    }

    Fallback Off
}
