#ifndef EC_OUTLINE_MASK_PASS_INCLUDED
#define EC_OUTLINE_MASK_PASS_INCLUDED

// ============================================================================
// ECOutlineMaskPass.hlsl — 遮罩 Pass 的 Pass 体
// ----------------------------------------------------------------------------
// 归属：美术（art-director）
//
// 【用途】
//   把「需要描边的角色」渲染进一张独立的遮罩 RT，供合成 Pass 做屏幕空间描边。
//   该 Pass 以自定义 LightMode "ECOutlineMask" 挂在 **5 个共享材质** 上，
//   因此：
//     · 不新增任何材质资产
//     · 不新增任何模型材质槽（仍是那 5 个 .mat）
//     · 角色用哪一槽（Rock/Wood/Ground/Cloth/Emissive）都能进遮罩
//
// 【谁会被画进遮罩】
//   由 Renderer Feature 的 FilteringSettings.layerMask 控制：
//   只有挂在 **EC_Outline** 层（GameObject Layer）上的渲染器会进遮罩。
//   场景模块虽然也用 M_Rock / M_Ground，但不在该层 -> 天然被排除。
//
// 【遮罩编码（单张 RGBA8）】
//   R = 覆盖位（1 = 被角色覆盖）
//   G = 八面体编码的视空间法线 X
//   B = 八面体编码的视空间法线 Y
//   A = 状态编码 enc = covered*1 + (form + 4*highlight)*2
//       解码： covered = enc & 1
//              t      = (enc - 1) >> 1
//              form   = t & 3
//              highlt = (t >> 2) & 1
//   为什么把「状态」而不是「覆盖」放在 A：
//   合成 Pass 需要在**轮廓外侧**（未被覆盖的像素）知道该角色的状态形态，
//   所以状态必须能随形态学一起向外传播。A 通道存状态、覆盖位作为最低位
//   一起传播，一次 max 就能同时得到「覆盖」与「形态」。
//
// 【逐渲染器的形态覆写】
//   _OutlineForm 等属性是普通 float/color，运行时由表现层用
//   MaterialPropertyBlock 逐 Renderer 写入，**不实例化材质**。
// ============================================================================

#include "ECOutlineCommon.hlsl"

struct ECOutlineMaskAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct ECOutlineMaskVaryings
{
    float4 positionCS : SV_POSITION;
    float3 normalVS   : TEXCOORD0;
    UNITY_VERTEX_OUTPUT_STEREO
};

ECOutlineMaskVaryings ECOutlineMaskVertex(ECOutlineMaskAttributes input)
{
    ECOutlineMaskVaryings output = (ECOutlineMaskVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
    output.positionCS = positionInputs.positionCS;

    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
    output.normalVS = TransformWorldToViewDir(normalWS, true);
    return output;
}

half4 ECOutlineMaskFragment(ECOutlineMaskVaryings input) : SV_Target
{
    // 状态编码
    float form      = clamp(_OutlineForm, 0.0, 3.0);
    float highlight = step(0.5, _OutlineHighlight);
    float enc       = 1.0 + 2.0 * (form + 4.0 * highlight);

    float2 oct = EC_OctEncode(normalize(input.normalVS));
    return half4(1.0, oct.x, oct.y, enc);
}

#endif // EC_OUTLINE_MASK_PASS_INCLUDED
