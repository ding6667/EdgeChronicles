# 描边渲染底座 · URP 14 实现规格

> 归属：美术（art-director）· 技术美术
> 真值源：IMPL-v3.0 §3（打击感/描边形态区分）· IMPL-v3.0 §8（五条视觉硬约束）· IMPL-v3.0-AD-03 §1–§4 · GDD-v1.0 §8.1
> 引擎：Unity 2022.3.62f3c1（Unity 中国版 LTS）· URP **14.0.11** · core RP 14.0.12
> 本文件是描边通道的**唯一规格**。任何其他文档描述描边时只引用本文件，不复述数值。

---

## 1. 适用范围与不可协商项

| 项 | 规格 | 出处 |
|---|---|---|
| 描边形态即语义 | 霸体＝**单边实描**；无敌＝**双边中空**。**不得靠颜色区分状态** | IMPL-v3.0 §3.4 |
| 墨线颜色 | 按「与背景亮度差 ≥60%」推导，**不许写死色值** | IMPL-v3.0 §8 / GDD §8.1 |
| 材质槽 | 全项目仅 `M_Rock` / `M_Wood` / `M_Ground` / `M_Cloth` / `M_Emissive` 5 个共享槽，**禁止新增材质槽** | GDD §8.1 硬条款 ① |
| 色相 | 仅 6 色语义表（朱砂/竹青/石青/赭石/墨黑/宣纸米白），**禁止新增色相** | AD-03 §2.2 |
| 玩家特效 | 一律不落地；地面形状通道只属敌人预警 | IMPL-v3.0 §3.4 |
| 性能底线 | 集显 1080p30；同屏三角面 ≤120 万；单角色 ≤15k；场景模块 ≤3k | GDD §8.1 硬条款 ② |

**描边通道的职责边界**：只负责**角色**（主角/敌人/Boss）的屏幕空间轮廓与内部结构线。场景模块、地形、VFX、UI **不走本通道**（它们各自的墨线由模型自带或由图集烘焙）。

---

## 2. 管线架构（4 Pass）

```
[不透明物体渲染完成]
        │
        ├─ Pass 1  ECOutlineMask          全分辨率 RGBA8
        │          · 只画 EC_Outline 层上的角色
        │          · R = 覆盖 / G,B = 八面体视法线 / A = 状态编码
        │          · 深度附件 = 相机深度 → 角色被墙体正确遮挡
        │
        ├─ Pass 2  ECOutlineDownsample    半分辨率 R8
        │          · 2x2 box max → 保住 1px 细肢体
        │
        ├─ Pass 3  ECOutlineDilate        半分辨率 RGBA8
        │          · R = enc 外扩 2px / G = enc 外扩 4px
        │          · B = 覆盖内缩 2px   / A = 覆盖内缩 4px
        │
        ├─ 颜色拷贝：相机颜色 → _ECTempColor（URP 14 无 requiresIntermediateTexture）
        │
        └─ Pass 4  ECOutlineComposite      全分辨率
                   · 读 _ECTempColor / _ECOutlineMask / _ECOutlineDil
                   · 算背景亮度 → 推导墨线色 → 装配三形态
                   · 写回相机颜色
        │
[半透明（刀光 VFX / 敌人地面预警）渲染]   ← 描边在 VFX 之下，VFX 永不被描边
```

**为什么合成在 `BeforeRenderingTransparents` 之前**：刀光与地面预警都是半透明。若合成放在半透明之后，半透明像素会被描边覆盖，竖直刀光会被角色的墨线切断。放在之前，角色描边在下、VFX 在上，通道不打架。

**产物文件**

| 文件 | 角色 | 工程侧目标路径 |
|---|---|---|
| `Tools/art/shaders/EC_Palette.hlsl` | 6 色语义表 + 明度/对比度推导 | `Assets/_Project/Art/Shaders/` |
| `Tools/art/shaders/ECOutlineCommon.hlsl` | 形态枚举、宽度常量、材质属性契约、八面体编解码 | `Assets/_Project/Art/Shaders/` |
| `Tools/art/shaders/ECOutlineMaskPass.hlsl` | 遮罩 Pass 体（供 5 个共享材质 include） | `Assets/_Project/Art/Shaders/` |
| `Tools/art/shaders/ECOutlineMask.shader` | 独立遮罩 Shader（bring-up 用） | `Assets/_Project/Art/Shaders/` |
| `Tools/art/shaders/ECOutlineDilate.shader` | 下采样 + 形态学链（2 Pass） | `Assets/_Project/Art/Shaders/` |
| `Tools/art/shaders/ECOutlineComposite.shader` | 全屏合成 | `Assets/_Project/Art/Shaders/` |
| `Tools/art/ref/ECOutlineRendererFeature.cs` | Renderer Feature 参考实现（C#） | `Assets/_Project/Scripts/Rendering/`（engineering-lead 独占，由工程侧拷入） |

**唯一编辑真值源 = `Tools/art/shaders/`**。工程内副本只读，每次改动由工程侧重新拷贝，**不得反向编辑**，避免出现第二个真值源。

---

## 3. URP 14 vs URP 17 差异清单与处理方案 ★

> 这份差异表是本轮最实际的技术风险点。设计规格原文按 URP 17 写，工程实际是 URP 14.0.11。

| # | 能力 / API | URP 17（Unity 6） | **URP 14（2022.3）** | 本方案处理 |
|---|---|---|---|---|
| 1 | 渲染图 | RenderGraph 为默认路径（可切 Compatibility Mode） | **无 RenderGraph**，只有经典 `ScriptableRenderPass` | 全部按经典 Pass 写。**不引入** `RecordRenderGraph` / `CreateRenderGraphPass` / `UniversalResourceData` / `RenderGraphUtils` 任何一个 API |
| 2 | Pass 生命周期 | RG 下需显式声明读写资源 | `OnCameraSetup` / `Execute` / `OnCameraCleanup` | 采用经典三段式；`ConfigureTarget` / `ConfigureInput` 在 `OnCameraSetup` 里调 |
| 3 | RT 管理 | `UniversalRenderer.CreateRenderGraphTexture` | `RenderingUtils.ReAllocateIfNeeded(ref RTHandle, desc, filter, wrap, name)` | 用 `ReAllocateIfNeeded`（URP 12+ 存在，14 可用） |
| 4 | 全屏 blit | `RenderGraphUtils.BlitCameraTexture` / `Blitter.BlitTexture` | `Blitter.BlitCameraTexture(cmd, src, dst, material, pass)` | 用 `Blitter.BlitCameraTexture`；`cmd.DrawProcedural` 跑全屏三角形 |
| 5 | 中间纹理 | RG 下相机颜色天然是 RT | **`ScriptableRenderPass.requiresIntermediateTexture` 不存在**（URP 15 引入） | 自己分配 `_ECTempColor` 并显式 blit 拷贝；**不依赖**该属性 |
| 6 | Renderer Feature 阶段 | `AddRenderPasses` + `SetupRenderPasses` | `AddRenderPasses` + `SetupRenderPasses`（URP 14 引入，取代 12 的 `Setup`） | 参考实现里**不依赖** `SetupRenderPasses`：相机颜色/深度在 pass 的 `OnCameraSetup`/`Execute` 内部取，避免 14 的调用时序差异 |
| 7 | 自定义 LightMode | `ShaderTagId` + `SetShaderPassName` | 同 | `DrawingSettings.SetShaderPassName(0, "ECOutlineMask")` 显式指定，不依赖 renderer 默认 tag 列表 |
| 8 | 光照层 / Rendering Layers | 完整 Rendering Layers（可作为纹理采样） | **只有 Light Layers，没有 Rendering Layer 纹理** | **不**用 Rendering Layers 选轮廓对象。改用 **GameObject Layer（EC_Outline）+ `FilteringSettings.layerMask`** |
| 9 | 深度/法线输入 | `ScriptableRenderPassInput.Depth \| Normal` | 同名 API 存在（URP 12+），但依赖 URP Asset 勾选 | 本方案**不使用**深度法线纹理：视法线由遮罩 Pass 自己输出（八面体 2 通道），避开 URP 14 的 `_CameraNormalsTexture` 编码差异（`UnpackNormal` vs 八面体） |
| 10 | Renderer 资产类型 | `UniversalRendererData` | URP 14 **同时**保留 `ForwardRendererData` 与 `UniversalRendererData`；工程用的是 `UniversalRendererData`（已核实 GUID） | **不新建 Renderer 资产**，直接在工程现有 `UniversalRenderer.asset` 上挂本 Feature |
| 10b | Rendering Path 枚举 | 同样是 Forward/Forward+/Deferred | **同样是 `RenderingMode`：Forward=0 / Deferred=1 / ForwardPlus=2**（URP 14 `UniversalRenderer.cs`） | 工程现值 `m_RenderingMode: 0`（Forward）**需改为 2（Forward+）** |
| 11 | 内置 Renderer 创建 | 单一 `Universal Renderer` | 同 | 同上：不新建 |
| 12 | 相机深度附件 | RG 下可自由声明 | `renderer.cameraDepthTarget` 可作为深度附件与自定义颜色 RT 配对 | 遮罩 Pass 用 `ConfigureTarget(_maskRT, cameraDepthTarget)`，保证角色被墙体遮挡；前提是 URP Asset **Intermediate Texture = Always** |
| 13 | MSAA 与自定义 RT | 自定义 RT 可自由声明 msaaSamples | 自定义 RT 的 msaaSamples 必须与相机深度附件一致，否则配对非法 | 本方案要求 URP Asset **MSAA = Disabled**（见 §7 性能与风格依据） |
| 14 | `Shader.Find` 打包 | 同 | 同（打包后仍需 Always Included Shaders） | 三个描边 Shader 必须进 URP Asset 的 **Always Included Shaders**，否则运行时材质为 null、Feature 静默跳过 |
| 15 | 渲染管线事件枚举 | `BeforeRenderingTransparents` 等同名 | 同 | 无差异，直接用 |

**结论**：URP 14 → URP 17 的移植改动集中在 3 处 —— ①RenderGraph 相关 API 全删；②`requiresIntermediateTexture` 改为 RG 资源声明；③`UniversalResourceData` 取 RT 改为 RG 的 `ImportTexture`/`CreateTexture`。本方案的着色逻辑（形态学、色彩推导、遮罩编码）与渲染图无关，**移植时不需要改一行 HLSL**。

---

## 4. 三种描边形态在 5 材质槽约束下的实现方式 ★

### 4.1 形态定义

| 形态 | `_OutlineForm` | 语义 | 带的构成（1080p 基准，全分辨率像素） |
|---|---|---|---|
| 常规墨线 | `0` | 全角色常驻 | 跨轮廓 **外 2px + 内 2px** |
| 单边实描 | `1` | **霸体** | 外侧 **4px 实心**；开压边时外侧 2px 归压边色、内侧 2px 归描边色 |
| 双边中空 | `2` | **无敌** | 外：2px 中空（0~2px）+ 2px 描边（2~4px）；内：2px 挖空（0~2px）+ 2px 描边（2~4px） |
| 反相压边 | `3` | 全局压边通道 | 常规墨线 + 其外侧 **2px 反相压边** |

### 4.2 为什么这样能装进 5 材质槽

**描边形态不是"材质"，是"材质上的一组属性值"。** 落地方式：

1. **5 个共享材质各自扩展 2 个 Pass**，不是新增材质：
   - `ECOutlineMask` Pass → 渲染进遮罩 RT（不进最终画面）
   - 原有 Forward Pass 不动
   - 材质槽数量仍是 **1**，材质资产仍是那 **5 个 .mat**。
2. **形态用普通 float / color 属性承载，不用 shader keyword**：

   | 属性 | 类型 | 作用 |
   |---|---|---|
   | `_OutlineForm` | float | 0/1/2/3 形态 |
   | `_OutlineWidthPx` | float | 描边宽度（按形态覆写） |
   | `_OutlineHollowGapPx` | float | 中空间隙 |
   | `_OutlineColor` | color | 描边色（空 = 走推导） |
   | `_OutlinePressColor` | color | 压边色（空 = 走推导） |
   | `_OutlineHighlight` | float | 入窗亮起 1 帧标记 |
   | `_OutlineInkDelta` | float | 墨线最小亮度差（0.60） |
   | `_OutlinePixelScale` | float | 像素缩放（屏高 / 1080） |

   > **为什么不能是 shader_feature 关键字**：关键字是**材质级**的。一旦用关键字切形态，同一个 `M_Rock` 要变成"普通版"和"霸体版"两个变体 —— 那就是**新增材质变体**，直接违反硬条款 ①。普通 float/color 属性可以被 **MaterialPropertyBlock 逐 Renderer 覆写**，材质资产不变、不实例化。

3. **表现层的写入方式**（表现层属工程/战斗侧，本规格只定义契约）：

   ```csharp
   // 每个 Renderer 一个共享的 MaterialPropertyBlock
   static readonly int IdForm = Shader.PropertyToID("_OutlineForm");
   static readonly int IdHighlight = Shader.PropertyToID("_OutlineHighlight");

   // 常态
   mpb.SetFloat(IdForm, 0f);            // 常规墨线
   renderer.SetPropertyBlock(mpb);

   // 进入霸体窗口
   mpb.SetFloat(IdForm, 1f);            // 单边实描
   mpb.SetFloat(IdHighlight, 1f);       // 入窗亮起 1 帧
   renderer.SetPropertyBlock(mpb);
   // 下一帧把 Highlight 归 0
   ```

4. **形态通过遮罩 RT 向外传播**：轮廓外侧的像素本身不在角色身上，拿不到自己的形态。解法是把形态编码进遮罩的 A 通道，并让形态学的 max 运算**同时**传播"覆盖位"和"形态位"：

   ```
   enc = covered*1 + (form + 4*highlight)*2
   ```
   覆盖位放最低位是刻意的：`max(enc)` 在任何邻域上同时给出「有没有被角色覆盖」和「该角色的形态是什么」。

5. **状态色不写死**：形态 1 用朱砂语义基色（`EC_AdjustValueForContrast` 只调明度，不改色相族）；形态 2 用两极推导（亮底自动倒相为墨黑）；形态 0/3 用常规推导墨色。三者都**不写死十六进制**进 shader——十六进制只存在于 `EC_Palette.hlsl` 一个文件里，且那里已经是线性空间的语义调色板。

### 4.3 关键：**颜色永远不承载状态**

| 状态 | 形态（主判据） | 颜色（冗余通道） |
|---|---|---|
| 霸体 | 单边实描 4px | 朱砂（明度调整到与背景 ≥3:1） |
| 无敌 | 双边中空（内外各 2px + 中空） | 两极推导（暗底纸白 / 亮底墨黑） |

任一状态窗口只开 ≤5 帧（回锋无敌 5 帧 ≈83ms），且**没有时间脉冲**。形状在 83ms 内足够读出；颜色不参与判读。符合 IMPL-v3.0 §3.4「靠描边形态区分」与 GDD §8.5「闪烁 ≤3Hz，脉冲通道 MVP 关闭」。

---

## 5. 墨线与描边色的推导（规则 O-1 / O-2 / O-3）

三个规则全部实现在 `Tools/art/shaders/EC_Palette.hlsl`，**没有一处把环境相关色值写死**。

### O-1 常规墨线色（两极推导）

```
bgL        = 背景环采样（6 个方向、半径 56px@1080p、排除被角色覆盖的样本）的平均亮度
paper      = 宣纸米白（线性）
inkBlack   = 墨黑（线性）
dPaper     = |L(paper)  - bgL|
dInk       = |L(inkBlack)- bgL|
方向        = 取 d 更大的一端
若 max(dPaper, dInk) >= 0.60  → 直接用该端
否则                        → 沿「墨黑 <-> 宣纸米白」明度轴继续外推到刚好满足 0.60
```

沿该轴插值**不引入新色相**：两端都是中性偏暖，外推只改明度。

### O-2 语义基色的明度调整（不改色相族）

霸体用朱砂。当朱砂与背景的对比 < 3:1（铁砧窟炉火橙底上朱砂仅 2.1:1）：

```
在「朱砂 → 墨黑」或「朱砂 → 宣纸米白」之间二分求最小调整量，
使 contrast(adjusted, bg) >= 3.0
```

只改明度、不改色相相位，仍落在暖红族内。

### O-3 压边色（与描边明暗相反）

```
描边偏亮 → 压墨黑；描边偏暗 → 压纸白
再保证压边与描边之间对比 >= 1.6:1（否则压边读不出来）
```

### 背景采样失败的兜底

6 个环采样若全被角色覆盖（超大角色），回退到当前像素颜色，并记录到 `_ECOutlineBgFallback` 供真机排查。

---

## 6. 长度量化表（重要）

形态学在**半分辨率**上做，半分辨率 texel = 2 个全分辨率像素（@1080p）。因此：

| 规格要求 | 实现值 | 偏差 | 偏差来源 |
|---|---|---|---|
| 常规墨线 3px | 外 2px + 内 2px = 4px | +1px | 半分辨率最小单位 2px |
| 霸体单边实描 4px | 外 4px | 0 | 4px = 2 个半分辨率 texel，可精确表达 |
| 无敌双边中空 2px（内外各 2px） | 外 2px 描边 + 2px 中空；内 2px 挖空 + 2px 描边 | 中空从 1px 变 2px | 同上；中空变宽反而**更易读**（IMPL-v3.0 §3.4 的形态语义不受影响） |
| 反相压边 1px | 外 2px | +1px | 同上 |

> **形态语义不受量化影响**：单边 vs 双边、实心 vs 中空是**拓扑**差异（外侧一条 vs 内外两条 + 挖空），在 2px 粒度下依然一眼可辨。实测盲测判据不变（IMPL-v3.0 §3.4）。
>
> **若必须精确到 1px**：把 `_ECOutlineChainSize` 的形态学链改为全分辨率（`chainDownscale = 1`），采样数 ×4。工程侧在集显实测不达标时优先降 `quality` 档，而不是降分辨率。

---

## 7. 性能与风格依据

| 项 | 值 | 依据 |
|---|---|---|
| 形态学链分辨率 | 半分辨率（960×540 @1080p） | 形态学是纯采样密集型（每档 9 抽样 × 4 档 = 36 抽样），半分辨率把这一项降到 1/4 |
| 合成分辨率 | 全分辨率 | 轮廓边缘以全分辨率遮罩为界，不软；与「木版硬边」风格一致 |
| 抽样开销 | 遮罩 1 + 形态学 4 + 背景环 12 + 内部线 4 + 颜色 2 ≈ **23 抽样/像素（全分辨率）** | 集显 1080p30 的填充率上限 |
| MSAA | **Disabled** | 遮罩 RT 必须与相机深度附件的 msaaSamples 一致才能配对；1x + 硬边墨线在木刻风格下锯齿可接受。若要开 MSAA，遮罩 RT 必须同采样数并补一次 resolve |
| Depth Priming | Off | 会让轮廓出现光晕 |
| 降级档 | `quality = 0` 关掉角色内部结构线（省 4 抽样/像素） | 集显实测不达标时的第一刀 |

**同屏角色数对开销的影响**：遮罩与合成开销与"被描边角色的屏幕占位面积"成正比，与角色数量无关（同屏 10 个小怪 ≈ 同屏 1 个 Boss 的占位）。集显实测时按**最坏占位**（Boss 满屏 + 8 个小怪）取样。

---

## 8. 工程侧落地清单

### 8.1 URP Asset / Renderer 设置

**工程现状（已核实）**：`Assets/_Project/Settings/EdgeChronicles_URP.asset` + `UniversalRenderer.asset` 已存在，URP Asset 类型为 `UniversalRendererData`（URP 14 同时保留 `ForwardRendererData`，但工程用的是统一版 `UniversalRendererData`）。

| 项 | 现状 | 要求 | 处置 |
|---|---|---|---|
| Rendering Path（Renderer 的 `m_RenderingMode`） | `0` = **Forward** | **Forward+** = `2` | **需改**：`m_RenderingMode: 2`（URP 14 的 `RenderingMode` 枚举：Forward=0 / Deferred=1 / **ForwardPlus=2**） |
| MSAA | `m_MSAA: 1`（Disabled） | Disabled | ✅ 已符合（自定义 RT 的 msaaSamples 必须与相机深度附件一致） |
| Intermediate Texture | `m_IntermediateTextureMode: 1`（**Always**） | Always | ✅ 已符合（相机颜色/深度必须是 RTHandle，遮罩 RT 才能与相机深度配对） |
| Depth Priming | `m_DepthPrimingMode: 0`（Disabled） | Disabled | ✅ 已符合（Depth Priming 会让轮廓出现光晕） |
| Depth Texture | `m_RequireDepthTexture: 0` | **不需要开** | ✅ 保持关闭。视法线由遮罩 Pass 自己输出，不采样 `_CameraNormalsTexture` |
| Opaque Texture | `m_RequireOpaqueTexture: 0` | **不需要开** | ✅ 保持关闭。合成 Pass 直接读自己拷出的 `_ECTempColor`，不请求 `ScriptableRenderPassInput.Color` |
| SRP Batcher | `m_UseSRPBatcher: 1` | ON | ✅ 已符合（**这是 §8.2 的 CBUFFER 要求成立的前提**） |
| Main / Additional Lights | Per Pixel / Per Pixel | Per Pixel | ✅ 已符合 |
| 实时阴影距离 | `m_ShadowDistance: 50` | 30m（只覆盖主角） | 建议改 30（美术规格：实时阴影仅覆盖主角 30m） |
| Renderer Features | `m_RendererFeatures: []` | 挂本 Feature | **需改**：把 `ECOutlineRendererFeature` 加进 Renderer 资产 |
| Always Included Shaders | 未收录 3 个描边 Shader | 必须收录 | **需改**：追加 `EC/OutlineMask` / `EC/OutlineDilate` / `EC/OutlineComposite`。漏任一个 → `Shader.Find` 返回 null → **画面没有描边且不报错** |

> **Forward+ 的影响**：本方案的 4 个 Pass 全部是经典 `ScriptableRenderPass`，在 Forward / Forward+ / Deferred 三种路径下写法一致，**切到 Forward+ 不需要改任何一行 HLSL**。切到 Forward+ 后 URP 会启用 clustered 光照，`Additional Lights` 走逐像素；本方案的遮罩与合成 Pass 与光照路径无耦合。

### 8.2 Layer 与材质

1. TagManager 新建层 `EC_Outline`（Layer 6 或任意空闲层；工程当前 Layer 3/6-31 空闲）。
2. 所有需要描边的角色 Renderer 挂到该层。场景模块**不挂**。
3. 5 个共享材质的 shader 各加一个 Pass（照抄 `ECOutlineMaskPass.hlsl` 里的 Pass 体）：

   ```hlsl
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
       #include "ECOutlineMaskPass.hlsl"
       ENDHLSL
   }
   ```

   同时在 5 个材质的 Properties 里补上 §4.2 的 8 个属性。

   > **SRP Batcher 兼容（必做）**：这 8 个属性必须加进材质 shader **已有的**
   > `CBUFFER_START(UnityPerMaterial) … CBUFFER_END` 块里，**不得**在别处再开一个
   > 同名 CBUFFER，也不得声明成散装全局变量。属性散落在 CBUFFER 之外会让 SRP
   > Batcher 失效，集显的 draw call 会直接失控。
   >
   > 属性未在 Properties 里声明时，材质默认值取 0：`_OutlineForm = 0`（常规墨线）
   > 正好是正确的默认态；`_OutlineInkDelta` 的实际值由合成 Pass 的全局材质提供，
   > 不依赖遮罩材质上的这个属性。

4. 5 个共享材质之外**不得**出现第 6 个材质槽。

### 8.3 Shader 收录

URP Asset → **Always Included Shaders** 追加：

- `EC/OutlineMask`
- `EC/OutlineDilate`
- `EC/OutlineComposite`

漏任一个 → `Shader.Find` 返回 null → Feature 静默跳过 → **画面没有描边，且不报错**。工程侧自检必须确认这一点。

### 8.4 Renderer Feature

工程侧把 `Tools/art/ref/ECOutlineRendererFeature.cs` 拷进 `Assets/_Project/Scripts/Rendering/`，加到工程现有 Renderer 资产上，`outlineLayerMask` 指向 `EC_Outline` 层。

---

## 9. 玩家侧刀光的竖直面约束

IMPL-v3.0 §3.4 要求：玩家攻击＝**竖直刀光竖直面**；命中帧被角色遮挡 ≤30%；刀光高度 ≥ 身高 1.2×。

| 约束 | 实现口径 | 验收方法 |
|---|---|---|
| 竖直面 | 刀光 quad 的法线与相机视线**共面**（billboard 绕 Y 轴旋转、垂直站立），不使用地面投影贴花 | 任意相机角度下，刀光底边不得贴地 |
| 高度 ≥ 1.2× 身高 | 刀光 quad 的世界高度 ≥ `角色身高 × 1.2`；按角色实际 `bounds.size.y` 取值，**不写死米数** | 取角色 Animator bounds 高度 ×1.2 作为 VFX 实例缩放基准 |
| 遮挡 ≤30% | 命中帧截图，按**屏幕面积**统计角色剪影被刀光覆盖的比例 | 逐技能截图量测，>30% 即返工 |
| 落地禁止 | 刀光贴图不得包含地面投影段；粒子不得有重力落地分量 | 逐技能走查 |

> 30% 是**屏幕面积**口径，与 IMPL-v3.0 §3.4 的「遮挡比」一致（GDD §9.4 的口径教训：面积比例与遮挡比不是一回事，量纲必须声明）。统计对象是**角色剪影被刀光覆盖的面积 / 角色剪影总面积**。

---

## 10. 编译自检清单（工程侧）

| # | 检查 | 期望 |
|---|---|---|
| 1 | 三个描边 Shader 拷进工程后无编译错误 | Console 无 Shader error |
| 2 | `EC/OutlineMask` 的 Pass 被 5 个共享材质正确 include | 材质 Inspector 的 Shader Debug 页可见 `ECOutlineMask` Pass |
| 3 | 角色挂 `EC_Outline` 层后画面出现 3px 墨线 | 截图确认 |
| 4 | 切到暗底场景（铁砧窟配色）后墨线变纸白 | 截图确认，**不得手工填色** |
| 5 | 霸体窗口出现「单边实描 4px」，无敌窗口出现「内外两条 + 中空」 | 逐帧截图 |
| 6 | 角色被墙挡住时描边一起被挡 | 场景走查 |
| 7 | VFX（刀光）盖在描边之上，未被描边 | 逐技能走查 |
| 8 | 集显 1080p30 达标（Frame Debugger 看 Draw Call 与 GPU 时间） | 达标线：描边通道 ≤2.0ms |
| 9 | 切换 `quality=0` 后帧时间改善 | 记录两组数据 |

---

## 11. 已知限制与未验证项

| # | 项 | 性质 | 处置 |
|---|---|---|---|
| L1 | 本轮**未在 Unity 内编译验证**（Editor 正在运行、不跑 batchmode） | 未验证 | 工程侧按 §10 清单逐项过；任何编译错误以 §11 的"回退方案"处理 |
| L2 | 中空带的"挖空"用**背景环平均色**回填，不是真实背景 | 精度近似 | 2px 带宽、在木刻平涂风格下不可见；精确版需要"角色渲染前的背景副本"，开销高，MVP 不做 |
| L3 | 形态学的 3×3 稀疏网格是**方盒形态学的近似** | 精度近似 | 对角方向的轮廓厚度比正交方向厚约 1 个量化单位；如需修正，改成 5×5 网格（抽样 ×2） |
| L4 | 两个不同形态的角色靠得极近时，形态传播取"max"，可能串 | 边缘情况 | 霸体/无敌同时出现的场景只有 Boss 霸体 + 玩家无敌，距离通常 >1 个角色身位；若实测出现串形，改为按距离场取最近 |
| L5 | 超大角色（占满屏幕）的背景环采样可能全被覆盖 | 边缘情况 | 已有兜底（回退到当前像素色） |
| L6 | 形态学的 2px 量化使 1px 压边变 2px、3px 墨线变 4px | 规格偏差 | 见 §6；形态语义不受影响 |

**回退方案（若 URP 14 下本方案无法跑通）**：改用**反向外壳（inverted hull）**做轮廓 —— 在角色的 5 共享材质上加一个 `Cull Front` 的 Pass，顶点沿法线外扩并按屏幕空间换算宽度。此时"单边实描"天然成立，"双边中空"用 stencil 两遍实现。代价：硬边处需要拆顶点（split normal），且失去屏幕空间内部结构线。**此路径不改变任何美术规格**，只换实现方式。

---

## 12. 与其他文档的关系

| 文档 | 与本文件的关系 |
|---|---|
| IMPL-v3.0 §3.4 | 描边形态语义（霸体/无敌）的**唯一来源**；本文件只定义如何实现 |
| IMPL-v3.0-AD-03 §1–§4 | UI 图集、VFX 格位、提示词模板的来源；本文件只涉及 §4 涉及的 VFX 空间约束（§9） |
| GDD-v1.0 §8.1 | 5 材质槽、面数红线、墨线推导判据的来源 |
| `docs/art/asset-spec-cards.md` | 资产规格卡（16 动画 / 8 技能 VFX / 3 Boss / 141 图集 / 可访问性） |
| `Tools/art/gate.py` | 把本文件的宽度与色相约束变成机器判定 |
