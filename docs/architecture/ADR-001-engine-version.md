# ADR-001 · 引擎版本适配（Unity 2022.3 vs 设计规格的 Unity 6）

| 项 | 内容 |
|---|---|
| **状态** | 已决定（Accepted） |
| **日期** | 2026-01 |
| **决策人** | 主理人（游承峰） |
| **执行** | 工程线（程基岩） |
| **相关文档** | `docs/impl/IMPL-v3.0.md`、`docs/impl/IMPL-v3.0-EL-03-progression.md`、`docs/impl/TOOLCHAIN.md` |

---

## 1. 背景

设计规格（`IMPL-v3.0.md` / `EL-03`）按 **Unity 6（6000.0.x）+ URP 17** 撰写；工程实际落地的是
**Unity 2022.3.62f3c1（Unity 中国版 LTS）+ URP 14.0.11**。

版本差异不是「小版本升级」，而是**渲染管线架构代际差**：URP 17 的 RenderGraph 管线与 URP 14 的
ScriptableRenderPass 管线在扩展点、API、可用特性上不兼容。设计规格里凡是依赖渲染管线扩展、
包版本 API 或语言版本的内容，都必须逐条落到 2022.3 上。

本 ADR 的职责：**列出全部受影响项 + 每项的处理方式**，让「规格 → 实装」的偏差有据可查，
不留「以为兼容」的盲区。

---

## 2. 事实基线（实测，非推断）

| 项 | 实测值 | 取证方式 |
|---|---|---|
| Unity Editor | `2022.3.62f3c1` | `ProjectSettings/ProjectVersion.txt` |
| 安装路径 | `E:/UnityHub/2022.3.62f3c1/Editor/Unity.exe` | 磁盘 |
| URP | `14.0.11` | `Packages/manifest.json` / `packages-lock.json` |
| Input System | `1.7.0` | 同上 |
| Test Framework | `1.1.33` | 同上 |
| Animation Rigging | `1.2.1` | 同上 |
| AI Navigation | `1.1.5` | 同上 |
| Timeline | `1.7.7` | 同上 |
| Profiling Core | `1.0.2` | 同上 |
| uGUI | `1.0.0` | 同上 |
| Newtonsoft | **未引入** | 同上（存档用内建 `JsonUtility`） |
| Addressables | **未引入** | 同上（UI 图集随包加载） |
| Cinemachine | **未安装** | 同上（镜头自写定点相机） |
| C# 语言版本 | **C# 9**（2022.3） | Unity 编译设置 |
| .NET API 兼容 | .NET Standard 2.1（**无 `System.Text.Json`**） | 同上 |

### 2.1 已启用 URP（本次修复）

实测发现：**URP 14.0.11 包已安装，但渲染管线资产从未创建、也未挂到任何画质档**——
工程实际跑的是 Built-in 管线。本轮已修复：

| 动作 | 结果 |
|---|---|
| 创建 `UniversalRendererData` 资产 | `Assets/_Project/Settings/UniversalRenderer.asset` |
| 创建 URP 资产并挂 renderer | `Assets/_Project/Settings/EdgeChronicles_URP.asset` |
| 挂到 `GraphicsSettings.defaultRenderPipeline` | ✅ `defaultRP=EdgeChronicles_URP` |
| 挂到全部 6 个画质档 | ✅ Very Low / Low / Medium / High / Very High / Ultra 全部 = `EdgeChronicles_URP` |
| Build Settings 登记场景 | ✅ `Assets/Scenes/SampleScene.unity`（enabled = true） |

> **URP 14 的第一个实测坑**：`ForwardRendererData` 在 14.0.11 已标记 **obsolete**
> （`ForwardRendererData has been deprecated (UnityUpgradable) -> UniversalRendererData`），
> 必须用 `UniversalRendererData`。规格与工程笔记里写的「Forward Renderer」在 14 上已不是正确类型名。

---

## 3. 受影响项清单（核心交付）

三态口径：**需改**（规格或实现必须改）/ **受影响**（实现方式必须换，但规格数值与结论不变）/
**不受影响**（逐条核对后确认无需改动）。

### 3.1 URP 17 → 14

| # | 受影响点 | 状态 | 处理方式与依据 |
|---|---|---|---|
| U-1 | **RenderGraph** | **需改** | URP 14 **没有 RenderGraph**（RenderGraph 管线是 URP 17 / Unity 6 引入的）。任何按「RenderGraph 录制 → 播放」写的渲染扩展在 14 上**根本不存在**。描边等自定义 Pass 一律走 URP 14 的 `ScriptableRendererFeature` + `ScriptableRenderPass` 旧式 API。**不得引用 `UnityEngine.Rendering.RenderGraph` 命名空间下的任何类型**（在 14 上不存在，编译期即失败）。 |
| U-2 | **描边（墨线 / 霸体 / 无敌）实现路径** | **需改** | 规格（`IMPL-v3.0.md` §3.4、`AD-03`）只规定**表现结果**（赤色单边实描边 4px 常亮 / 纸白双边中空 2px + 描边 / 墨线 3px 屏幕空间按背景亮度差推导），未规定实现。URP 14 下落地为：**自写 `ScriptableRendererFeature`**，在 `Forward+`（Universal Renderer，Intermediate Texture = Always）下采样 `_CameraNormalsTexture` + `_CameraDepthTexture` 做边缘检测，按 `vfx_outline_style` 分支输出三种描边（墨线 / 霸体 / 无敌）。**结论：规格数值不变，实现方式必须换。** |
| U-3 | **ShaderGraph 版本** | **受影响** | ShaderGraph 随 URP 走 14.x，其自定义节点（`CustomFunctionNode`）的描述字段与 Unity 6 不同。描边 shader **手写 HLSL**，不走 ShaderGraph 节点——理由：描边要按**背景亮度差**推导颜色（GDD 治理规则 5 的固定常量错例表第 4 条），需要采样与按帧计算，节点化收益低而版本耦合高。 |
| U-4 | **Forward+ 的中间纹理要求** | **受影响** | 描边 Pass 依赖法线与深度纹理，URP 14 的 Forward+ 必须开 `Intermediate Texture = Always`（否则深度/法线不可用）。这是**渲染器资产配置项**，不是代码。已随 `UniversalRendererData` 默认值落地，需在美术确认描边效果时复核一次。 |
| U-5 | **URP 资产与画质档的绑定** | **需改**（已执行） | 见 §2.1。此前 `m_CustomRenderPipeline = {fileID: 0}`，等于整套美术规格（描边、5 材质槽、墨线倒相、Forward+）全部落空。发布侧的严格门控会直接拒绝出 Built-in 包——这个门控是对的，**不放宽**。 |
| U-6 | **渲染管线 API 差异**（`RenderPipelineManager` 新版用法） | **受影响** | 2022.3 的 `RenderPipelineManager` 仍是「事件 + 资源池」形态；Unity 6 的新版资源池写法（`RenderPipelineResource` 句柄）在 14 上不存在。**规范**：渲染扩展只使用 `ScriptableRenderPass` / `CommandBuffer` / `RTHandle` 之外的 14 版 API，禁用任何「新版资源句柄」写法。 |
| U-7 | 材质槽 / 顶点色 / 墨线倒相的**数值规格** | **不受影响** | 这些是美术规格，与引擎版本无关（`AD-03`）。 |

### 3.2 包版本 API 差异

| # | 受影响点 | 状态 | 处理方式与依据 |
|---|---|---|---|
| P-1 | **Input System 1.11 → 1.7** | **受影响** | 1.7 已具备项目需要的全部能力：`InputAction` / `InputActionAsset` / `PlayerInput` / `Action` 事件回调 / `InputDevice` 描述。**规格的「8 帧环缓冲 + 1 个单槽预输入」是自写数据结构，不依赖包版本**（属 Kernel / Binding 层）。差异点：1.11 新增的 **`InputActionTrace` / 事件追踪 API**（用于录制输入）在 1.7 不存在——本项目不依赖它。**结论：输入层实现方式按 1.7 写，规格不变。** |
| P-2 | **Test Framework 1.4 → 1.1.33** | **受影响** | 1.1.33 支持 EditMode / PlayMode、`[Test]` / `[UnityTest]` / `TestCaseSource`、断言 API，**足够覆盖本项目的全部测试形态**。不可用：1.4 新增的 **`[UnityTest]` 的 Timeout 增强 / 分布式测试 API**（本项目不用）。**结论：测试写法按 1.1.33 写，判据本身不变。** |
| P-3 | **Animation Rigging 1.3 → 1.2.1** | **受影响** | 弓手上半身 Override Layer + AvatarMask + TwoBoneIK 三件套在 1.2.1 全部可用且 API 一致。差异：1.3 新增的 **Constraint 组件化工作流（`TwoBoneIKConstraint` 的 Rig Builder 自动化）** 在 1.2.1 不存在——本项目是**手工装配 rig**，不依赖 Rig Builder。**结论：规格不变，实现按 1.2.1 手工装配。** |
| P-4 | **AI Navigation 2.0 → 1.1.5** | **需改** | 2.0 时代 `NavMeshSurface` / `NavMeshAgent` 的 API 与 1.1.5 有差异（2.0 引入新的 `NavMeshBuilding` 组件与运行时生成工作流）。1.1.5 的既有 API（`NavMeshSurface` 烘焙、`NavMeshAgent` 路径、`NavMeshObstacle`）**全部可用**，但**规格文档若引用 2.0 的工作流名称需改名**。本项目规格只写「敌人导航」，未指定组件 → **按 1.1.5 的 `NavMeshSurface` + `NavMeshAgent` 实现**。 |
| P-5 | **Cinemachine 未安装** | **需改（口径已定）** | `EL-03 §6` 原本写「可选：MVP 用自写定点相机」——**「可选」是第二个合法值**，与治理规则 11 冲突。已改为**不引入**（见 EL-03 §6 本轮修订）：镜头自写定点相机。**影响**：规格中「镜头微推 0.04s / 0.06s / 0.08s + 0.2° 抖动」「时停 0.2s + 镜头推近」「震屏」等**表现参数不变**，但实现全部落在自写相机组件里，**不引用任何 Cinemachine 类型**。 |
| P-6 | **Newtonsoft / Addressables / 2D Sprite 未引入** | **不受影响** | 三者都是**已裁定的「不引入」**，与引擎版本无关。存档走内建 `JsonUtility`；UI 图集用 Sprite Packing Tag + SpriteAtlas asset。 |

### 3.3 Unity 6 → 2022.3 的 API 漂移（发布侧在真实 Unity 上编译踩出，工程侧折进本 ADR）

以下 8 条在 2022.3 上**已删除 / 已改名 / 类型不同**，直接读写会编译失败或抛异常：

| # | Unity 6 写法 | 2022.3 实况 | 处理方式 |
|---|---|---|---|
| A-1 | `QualitySettings.postProcessing` | **已不存在** | 反射写入：属性存在就写，不存在就跳过并记进构建日志 |
| A-2 | `QualitySettings.textureMipmapLimit` | **已不存在** | 2022.3 的名字是 `globalTextureMipmapLimit`；同样走反射 |
| A-3 | `QualitySettings.realtimeLightProbes` | **已不存在** | 反射 + 跳过并记录 |
| A-4 | `QualitySettings.masterTextureLimit` | **已废弃** | 不再使用 |
| A-5 | `ScriptingImplementation.Mono2` | **已改名** | 实际成员为 `Mono2x`；用反射在 `Mono2x` / `Mono` 里择一 |
| A-6 | `QualitySettings.shadows` | 类型是 `ShadowQuality`，**不是** `ShadowCastingMode` | 显式类型转换 + 反射兜底 |
| A-7 | `ForwardRendererData`（URP 14 视角） | **obsolete** | 改用 `UniversalRendererData`（§2.1 已实测） |
| A-8 | `RenderGraph` 相关 API（URP 17） | **不存在** | 一律禁用（U-1） |

> **统一处理原则（写进工程的默认写法）**：**凡「Unity 6 有、2022.3 无」的成员，一律用反射访问；
> 属性缺失时跳过并把这件事写进日志，不静默、不抛异常。**
> 这样以后升引擎时，改动只落在「反射分支」一处，而不是散落在每个调用点。

### 3.4 语言与运行时

| # | 受影响点 | 状态 | 处理方式 |
|---|---|---|---|
| L-1 | **C# 语言版本 = 9**（非 Unity 6 的更高版本） | **需改** | 可用：`record` / `init` / 顶级语句 / 模式匹配。**不可用**：`required` 成员（C# 11）、`file` 类型（C# 11）、原始字符串字面量（C# 11）、列表模式（C# 11）。**本轮新增代码已按 C# 9 写**（见 `Core` / `Data` 两个程序集）。 |
| L-2 | **`System.Text.Json` 不可用** | **需改** | .NET Standard 2.0 / 2.1 均不带该包（需另引 NuGet）。**存档仍用内建 `JsonUtility`**；配表管线用工程自带的极简 JSON 读写器 `EdgeChronicles.Data.Serialization.Json`（纯 C#、零第三方依赖）。**已在本轮代码中落实**（EL-03 §6 已同步该口径）。 |
| L-3 | `FindObjectsByType` | **不受影响** | 2022.2+ 可用（2022.3 满足）；`FindObjectsOfType` 已废弃，新代码一律用 `FindObjectsByType`。 |
| L-4 | `JsonUtility` 的能力边界 | **不受影响** | 存档 JSON 结构扁平（无字典、无多态），`JsonUtility` 足够（TOOLCHAIN C5 的既定结论）。配表数据另走自带读写器（因为要支持「一张表 = 一个行数组」与「键 → 值 集」，`JsonUtility` 两者都不支持）。 |

---

## 4. 结论：哪些规格要改、哪些完全不受影响

| 规格条目 | 结论 |
|---|---|
| 战斗系统 10 键 / 八层联动 / 12 套养成的**数值与规则** | **完全不受影响**（纯设计规格，与引擎无关） |
| tick 9 阶段顺序 / 判定逻辑 / 帧语义 | **完全不受影响** |
| A19–A43 与 C1–C7 的**判定逻辑** | **完全不受影响**（本轮已按原口径实现为可执行判定） |
| 打击感规格（顿帧 / 镜头 / 粒子 / 音效 / 震动）**数值** | **不受影响**（数值不变） |
| 打击感规格的**镜头实现方式** | **受影响**：Cinemachine 不可用 → 自写定点相机（§3.2 P-5） |
| 描边 / 墨线 / 无敌可读性的**表现规格** | **不受影响**；**实现方式受影响**（URP 14 Renderer Feature，§3.1 U-2） |
| 工具链版本表（`EL-03 §6` / `TOOLCHAIN.md` H 组） | **需改**：已按实测基线改写（EL-03 §6 本轮修订；`TOOLCHAIN.md` 待同步，责任人：工程线） |
| 存档序列化（`JsonUtility` + CRC32 自实现） | **不受影响** |
| CI 脚本路径与 Unity 解析 | **需改（已执行）**：`Tools/ci/unity.sh` 现按实测路径解析（`/e/UnityHub/*/Editor/Unity.exe` 优先匹配 `ProjectVersion.txt`） |

---

## 5. 决策与后果

**决策**：以 **Unity 2022.3.62f3c1 + URP 14.0.11** 为工程基线，**不回迁到 Unity 6**；
设计规格的**数值与判定逻辑一律不改**，只调整**实现路径**与**工具链版本表**。

**后果（正面）**
- 工程在本机既有环境上可编译、可运行；CI 可在实测路径上找到 Unity。
- URP 管线已真正启用，美术规格（描边 / 材质槽 / 墨线倒相 / Forward+）不再落空。
- 版本差异被集中到本 ADR 与少量反射分支里，不再散落在业务代码中。

**后果（负面 / 必须持续承担）**
- **渲染扩展能力弱于 URP 17**：无 RenderGraph → 自定义 Pass 需手写资源生命周期管理（`ConfigureInput` / `OnCameraSetup` / `Execute`），未来升 Unity 6 时这一层要重写。
- **不能用 Unity 6 的新包 API**：任何新引入的包必须先核对 2022.3 是否有对应版本。
- **新同事容易按 Unity 6 的记忆写代码**：A-1…A-8 是已知踩坑点，代码评审须对照 §3.3 检查。

---

## 6. 复审触发条件（满足任一即重开本 ADR）

1. 升级 Unity 大版本（2022.3 → 6.x）或 URP 大版本（14 → 17）；
2. 新引入任何 Unity 包（须先核对 2022.3 可用版本）；
3. 描边 / 墨线实现路线从「自写 Renderer Feature」改为引擎内置方案；
4. 引入 Cinemachine / Addressables / Newtonsoft（任一被解除「不引入」裁定）。

---

## 7. 自证记录

| 项 | 判据 | 结果 |
|---|---|---|
| URP 已启用 | `GraphicsSettings.defaultRenderPipeline != null` 且 6 个画质档 rp 均非 null | ✅ 见 §2.1 |
| URP 资产可用 | renderer data 存在且为 `UniversalRendererData` | ✅ |
| 场景已登记 | `EditorBuildSettings.scenes` 含 `Assets/Scenes/SampleScene.unity` | ✅ |
| Unity 解析 | `bash Tools/ci/unity.sh` 的 `resolve_unity` | ✅ 解析到 `/e/UnityHub/2022.3.62f3c1/Editor/Unity.exe` |
| 编译自证 | `Tools/ci/check-compile.sh`（batchmode，日志无 `error CS`） | 见本轮交付说明 |

> **本 ADR 不含工期、成本、人日、预算、排期**（GDD 治理口径：这些不是本项目的约束轴）。

---

*本 ADR 是「引擎版本差异」的唯一描述点。其余文档引用本文件，不复述差异清单。*
