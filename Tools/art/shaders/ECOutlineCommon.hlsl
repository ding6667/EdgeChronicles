#ifndef EC_OUTLINE_COMMON_INCLUDED
#define EC_OUTLINE_COMMON_INCLUDED

// ============================================================================
// ECOutlineCommon.hlsl — 描边形态定义 / 遮罩编码 / 材质参数契约
// ----------------------------------------------------------------------------
// 归属：美术（art-director）
//
// 【描边形态三档】——由「形态」而非「颜色」承载状态语义（IMPL-v3.0 §3.4）
//   form 1 单边实描 4px      ：霸体。宽 4px、只在轮廓外侧、实心。
//   form 2 双边中空 2px      ：无敌。内外各 2px 实描，两描边之间留 2px 中空
//                              （= 角色本体沿轮廓被挖掉 2px，露出背景）。
//   form 3 反相压边 1px      ：单边实描（宽 3px）+ 外侧再压 1px 反明度色。
//   form 0 无描边
//
// 【5 材质槽约束下的实现方式】
//   描边形态**不新增材质槽**，靠 5 个共享材质上的**普通 float / color 属性**
//   + 运行时 MaterialPropertyBlock 逐渲染器覆写实现。
//   关键：属性必须是 float/color，**不得用 shader_feature 关键字**——
//   关键字是材质级的，无法逐渲染器切换，会迫使工程侧实例化材质（= 新增变体）。
//
// 【长度单位】
//   所有 px 宽度以 **1080p 基准画面的屏幕像素**表达；实际着色时按
//   _OutlinePixelScale（= 屏幕高度 / 1080）缩放，保证不同分辨率下粗细一致。
// ============================================================================

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "EC_Palette.hlsl"

// ---------------------------------------------------------------------------
// 八面体法线编码 / 解码（2 通道）
//   遮罩 RT 用 2 通道存视空间法线，省出通道给状态编码。
// ---------------------------------------------------------------------------
float2 EC_OctEncode(float3 n)
{
    n /= (abs(n.x) + abs(n.y) + abs(n.z));
    float2 o = n.xy;
    if (n.z < 0.0)
    {
        o = (1.0 - abs(n.yx)) * float2(n.x >= 0.0 ? 1.0 : -1.0,
                                      n.y >= 0.0 ? 1.0 : -1.0);
    }
    return o * 0.5 + 0.5;
}

float3 EC_OctDecode(float2 f)
{
    f = f * 2.0 - 1.0;
    float3 n = float3(f.x, f.y, 1.0 - abs(f.x) - abs(f.y));
    float t = saturate(-n.z);
    n.xy += float2(n.x >= 0.0 ? -t : t, n.y >= 0.0 ? -t : t);
    return normalize(n);
}

// ---------------------------------------------------------------------------
// 状态编码工具
//   enc = covered*1 + (form + 4*highlight)*2
//   覆盖位放在最低位，是为了让形态学的 max 同时完成「覆盖传播」与
//   「状态传播」——轮廓外侧像素也能知道该角色的形态。
// ---------------------------------------------------------------------------
float EC_CoverOf(float enc) { return frac(enc * 0.5) * 2.0; }

// ---------------------------------------------------------------------------
// 形态枚举
// ---------------------------------------------------------------------------
#define EC_OUTLINE_FORM_NONE    0
#define EC_OUTLINE_FORM_SOLID   1   // 单边实描 4px
#define EC_OUTLINE_FORM_HOLLOW  2   // 双边中空 2px（内外各 2px + 2px 中空）
#define EC_OUTLINE_FORM_PRESS   3   // 单边实描 3px + 外侧 1px 反相压边

// ---------------------------------------------------------------------------
// 宽度常量（1080p 基准像素）
// ---------------------------------------------------------------------------
#define EC_INK_WIDTH_PX          3.0   // 常规墨线（角色内部结构线 + 轮廓线）
#define EC_OUTLINE_SOLID_PX      4.0   // 霸体单边实描
#define EC_OUTLINE_HOLLOW_PX     2.0   // 无敌：内外各 2px
#define EC_OUTLINE_HOLLOW_GAP_PX 2.0   // 无敌：两描边之间的中空间隙
#define EC_OUTLINE_PRESS_PX      1.0   // 压边
#define EC_OUTLINE_PRESS_BASE_PX 3.0   // form 3 的基础描边宽度

// ---------------------------------------------------------------------------
// 材质属性契约（5 个共享材质上都要有这 8 个属性）
//   _OutlineForm        float  形态（0/1/2/3）
//   _OutlineWidthPx     float  描边宽度（可按形态覆写）
//   _OutlineHollowGapPx float  中空间隙
//   _OutlineColor       color  描边色（语义基色；空 = 走推导）
//   _OutlinePressColor  color  压边色（空 = 走推导）
//   _OutlineHighlight   float  入窗亮起 1 帧标记（0/1）
//   _OutlineInkDelta    float  墨线与背景的最小亮度差（0.60）
//   _OutlinePixelScale  float  像素缩放（屏幕高 / 1080）
//
//   ★ 声明位置（SRP Batcher 兼容）：
//     这 8 个属性必须加进材质 shader **已有的** `UnityPerMaterial` CBUFFER 里，
//     **不得**在本文件里单独声明、也不得新开一个同名 CBUFFER
//     —— 否则 SRP Batcher 失效，集显的 draw call 会失控。
//     本文件只提供常量与工具函数；属性的实际声明由包含本文件的 shader 负责。
// ---------------------------------------------------------------------------

float4 _ECOutlineMaskTexel;   // 由合成 Pass 设置：xy = 1/尺寸

// ---------------------------------------------------------------------------
// 遮罩编码
//   R = 角色覆盖（0 / 1）
//   G = 形态 id / 3（0 / 1/3 / 2/3 / 1）
//   B = 入窗亮起标记（0 / 1）
//   A = 1
// ---------------------------------------------------------------------------
#define EC_MASK_FORM_SCALE 3.0

float EC_DecodeForm(float g)
{
    return round(g * EC_MASK_FORM_SCALE);
}

// ---------------------------------------------------------------------------
// 形态 -> 宽度 / 间隙 查表（不做分支抖动，用 step 混合）
// ---------------------------------------------------------------------------
void EC_ResolveFormWidths(float form, out float strokeWidth, out float hollowGap)
{
    strokeWidth = EC_INK_WIDTH_PX;
    hollowGap   = 0.0;

    // form 1 = 单边实描 4px
    strokeWidth = lerp(strokeWidth, EC_OUTLINE_SOLID_PX, step(0.5, form) * step(form, 1.5));
    // form 2 = 双边中空 2px + 2px 中空
    strokeWidth = lerp(strokeWidth, EC_OUTLINE_HOLLOW_PX, step(1.5, form) * step(form, 2.5));
    hollowGap   = lerp(hollowGap,   EC_OUTLINE_HOLLOW_GAP_PX, step(1.5, form) * step(form, 2.5));
    // form 3 = 3px 实描 + 1px 压边
    strokeWidth = lerp(strokeWidth, EC_OUTLINE_PRESS_BASE_PX, step(2.5, form));
}

// ---------------------------------------------------------------------------
// 状态语义色（IMPL-v3.0 §3.4）
//   霸体 = 朱砂单边实描 4px；无敌 = 纸白双边中空 2px
//   两者靠**形态**区分，颜色是冗余通道。
// ---------------------------------------------------------------------------
float3 EC_SemanticStrokeColor(float form, float3 bg)
{
    // form 2（无敌）走两极推导（亮底自动倒相为墨黑），form 1（霸体）走朱砂基色
    float isHollow = step(1.5, form) * step(form, 2.5);
    float isSolid  = step(0.5, form) * step(form, 1.5);

    // 霸体：朱砂基色 + 明度调整到对比度 >= 3:1
    float3 solidCol = EC_AdjustValueForContrast(EC_Cinnabar(), bg, 3.0);

    // 无敌：纸白 / 墨黑 两极，按背景亮度取差 >= 0.60 的一端
    float bgL = EC_Luminance(bg);
    float3 hollowCol = EC_DeriveInkColor(bgL, 0.60);

    float3 col = lerp(float3(0, 0, 0), solidCol, isSolid);
    col = lerp(col, hollowCol, isHollow);

    // form 3（压边）无独立语义色，按常规墨线两极推导
    float3 pressOnly = EC_DeriveInkColor(bgL, 0.60);
    col = lerp(col, pressOnly, step(2.5, form));
    return col;
}

#endif // EC_OUTLINE_COMMON_INCLUDED
