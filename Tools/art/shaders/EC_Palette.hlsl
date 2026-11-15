#ifndef EC_PALETTE_INCLUDED
#define EC_PALETTE_INCLUDED

// ============================================================================
// EC_Palette.hlsl — 6 色语义表 + 明度/对比度推导工具
// ----------------------------------------------------------------------------
// 归属：美术（art-director）· 唯一色相来源
// 规则：
//   1. 本文件是全项目**唯一**定义色相的地方。任何 shader / 材质 / 脚本都不得
//      再写一次十六进制色值。
//   2. 工程 m_ActiveColorSpace = 1（Linear），故下列常量一律存**线性空间**值，
//      由 sRGB 源值经 EC_SRGBToLinear 换算得到。
//   3. 环境相关量（墨线色、描边色、压边色）**一律走推导函数**，不写死。
//      6 色语义色本身是美术圣经的固定语义（不是环境相关量），可以常量。
// ============================================================================

// ---------------------------------------------------------------------------
// sRGB -> Linear（精确分段式，非快速近似）
// ---------------------------------------------------------------------------
float3 EC_SRGBToLinear(float3 c)
{
    float3 lo = c / 12.92;
    float3 hi = pow((c + 0.055) / 1.055, 2.4);
    return lerp(hi, lo, step(c, 0.04045));
}

// ---------------------------------------------------------------------------
// 6 色语义表（sRGB 源值，对应美术圣经 §2.2 / GDD §8.1）
//   朱砂 = 选中 / 危险 / 霸体
//   竹青 = 已达成 / 满级 / 已点亮
//   石青 = 改机制而非加数值
//   赭石 = 材料 / 消耗 / 未激活
//   墨黑 = 正文 / 主干线 / 禁用
//   宣纸米白 = 底板 / 留白 / 反白
// ---------------------------------------------------------------------------
#define EC_CINNABAR_SRGB   float3(0.752941, 0.223529, 0.168627) // #C0392B 朱砂
#define EC_BAMBOO_SRGB     float3(0.486275, 0.603922, 0.356863) // #7C9A5B 竹青
#define EC_STONE_AZURE_SRGB float3(0.243137, 0.419608, 0.470588) // #3E6B78 石青
#define EC_OCHRE_SRGB      float3(0.549020, 0.352941, 0.231373) // #8C5A3B 赭石
#define EC_INK_BLACK_SRGB  float3(0.117647, 0.105882, 0.094118) // #1E1B18 墨黑
#define EC_PAPER_SRGB      float3(0.909804, 0.874510, 0.788235) // #E8DFC9 宣纸米白

float3 EC_Cinnabar()   { return EC_SRGBToLinear(EC_CINNABAR_SRGB); }
float3 EC_Bamboo()     { return EC_SRGBToLinear(EC_BAMBOO_SRGB); }
float3 EC_StoneAzure() { return EC_SRGBToLinear(EC_STONE_AZURE_SRGB); }
float3 EC_Ochre()      { return EC_SRGBToLinear(EC_OCHRE_SRGB); }
float3 EC_InkBlack()   { return EC_SRGBToLinear(EC_INK_BLACK_SRGB); }
float3 EC_Paper()      { return EC_SRGBToLinear(EC_PAPER_SRGB); }

// ---------------------------------------------------------------------------
// 明度 / 对比度工具
// ---------------------------------------------------------------------------

// 相对亮度（Rec.709 线性域）
float EC_Luminance(float3 linearColor)
{
    return dot(linearColor, float3(0.2126, 0.7152, 0.0722));
}

// WCAG 对比度（线性域 → 感知对比度）
float EC_ContrastRatio(float3 a, float3 b)
{
    float la = EC_Luminance(a);
    float lb = EC_Luminance(b);
    float hi = max(la, lb);
    float lo = min(la, lb);
    return (hi + 0.05) / (lo + 0.05);
}

// ---------------------------------------------------------------------------
// 规则 O-1：墨线 / 描边色的推导
// ---------------------------------------------------------------------------
// 语义：**不许写死**。墨线色由「与背景的亮度差 ≥ 60%」推导：
//   · 暗底 -> 取纸白极值
//   · 亮底 -> 取墨黑极值
//   · 若两极都不足 0.60 的亮度差 -> 沿「墨黑 <-> 宣纸米白」明度轴继续外推，
//     刚好满足 0.60 为止（沿该轴插值**不引入新色相**：两端均为中性偏暖）。
//
// 参数
//   bgLuminance : 背景（角色轮廓外围一圈采样）相对亮度，0..1
//   minDelta    : 要求的最小亮度差，0..1（本项目墨线取 0.60）
// 返回：线性空间描边色
float3 EC_DeriveInkColor(float bgLuminance, float minDelta)
{
    float3 inkBlack = EC_InkBlack();
    float3 paper    = EC_Paper();

    float lInk   = EC_Luminance(inkBlack);
    float lPaper = EC_Luminance(paper);

    float dInk   = abs(lInk   - bgLuminance);
    float dPaper = abs(lPaper - bgLuminance);

    // 取与背景差异更大的一端作为方向
    bool  goPaper = (dPaper >= dInk);
    float3 base   = goPaper ? paper : inkBlack;
    float  dBest  = max(dInk, dPaper);

    if (dBest >= minDelta)
        return base;

    // 差异不足：沿该端继续外推，直到刚好满足 minDelta
    float targetLuminance = goPaper ? min(1.0, bgLuminance + minDelta)
                                    : max(0.0, bgLuminance - minDelta);
    float baseL = goPaper ? lPaper : lInk;

    // 在 base 与「同族极值」之间按明度插值（不跨色相族）
    float3 extreme = goPaper ? float3(1.0, 1.0, 1.0) : float3(0.0, 0.0, 0.0);
    float t = (abs(targetLuminance - baseL) < 1e-4)
            ? 0.0
            : saturate((targetLuminance - baseL) /
                       (abs(EC_Luminance(extreme) - baseL) + 1e-4));
    return lerp(base, extreme, t);
}

// ---------------------------------------------------------------------------
// 规则 O-2：语义基色的**明度**调整（不改色相族）
// ---------------------------------------------------------------------------
// 霸体描边用朱砂（语义基色）。当朱砂与背景对比 < minContrast 时，
// 沿「朱砂 -> 墨黑」或「朱砂 -> 宣纸米白」**只改明度**推到满足对比度。
// 这是明度调整，不是新增色相：色相仍落在朱砂所在的暖红族内。
//
// 推导式（无写死色值）：
//   目标 = 使 contrast(baseAdjusted, bg) >= minContrast 的最小调整量
//   baseAdjusted = base * k        (k < 1 压暗)
//   baseAdjusted = base + (white - base) * k   (k > 0 提亮)
float3 EC_AdjustValueForContrast(float3 base, float3 bg, float minContrast)
{
    if (EC_ContrastRatio(base, bg) >= minContrast)
        return base;

    float lBase = EC_Luminance(base);
    float lBg   = EC_Luminance(bg);

    // 背景更亮 -> 压暗基色；背景更暗 -> 提亮基色
    bool  darken = (lBg >= lBase);
    float3 target = darken ? EC_InkBlack() : EC_Paper();

    float lo = 0.0;
    float hi = 1.0;
    // 二分求最小调整量（16 次足够，shader 内可接受）
    for (int i = 0; i < 16; i++)
    {
        float mid = (lo + hi) * 0.5;
        float3 cand = lerp(base, target, mid);
        if (EC_ContrastRatio(cand, bg) >= minContrast)
            hi = mid;
        else
            lo = mid;
    }
    return lerp(base, target, hi);
}

// ---------------------------------------------------------------------------
// 规则 O-3：压边色（与描边自身明暗相反的一端）
// ---------------------------------------------------------------------------
// GDD §8.1「全局反相压边」：所有描边外侧压 1px 与自身亮暗相反的颜色。
// 压边色同样是推导值：取与描边色明度相反的一极。
float3 EC_DerivePressColor(float3 strokeColor)
{
    float lStroke = EC_Luminance(strokeColor);
    float lInk    = EC_Luminance(EC_InkBlack());
    float lPaper  = EC_Luminance(EC_Paper());

    // 描边偏亮 -> 压墨黑；描边偏暗 -> 压纸白
    bool  pressDark = (lStroke >= (lInk + lPaper) * 0.5);
    float3 press = pressDark ? EC_InkBlack() : EC_Paper();

    // 压边与描边之间也要有对比度，否则压边读不出来
    return EC_AdjustValueForContrast(press, strokeColor, 1.6);
}

#endif // EC_PALETTE_INCLUDED
