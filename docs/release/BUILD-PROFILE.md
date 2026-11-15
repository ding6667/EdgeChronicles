# 档位参数落地映射（baseline JSON → Unity 工程设置）

> 归属：发布侧（路远行）。**参数真值源是 `Tools/build/baselines/*.json`**，
> 本文件只回答一个问题：**这些参数最终写到 Unity 的哪个位置、谁来写、哪些写不进去**。
>
> 配套：[`BASELINES.md`](./BASELINES.md)（参数含义与验收方法）、
> [`RELEASE-CHECKLIST.md`](./RELEASE-CHECKLIST.md)（发布与回滚）、
> [`CI-SETUP.md`](./CI-SETUP.md)（CI 怎么跑）。

---

## 0. 一句话链路

```
Tools/build/baselines/<档位>.json        ← 参数真值源（唯一）
        │  jsonq.sh --flatten → key=value
        ▼
Tools/build/build-player.sh --tier low
        │  -buildParams <摊平文件>
        ▼
工程侧构建入口（Assets/_Project/Editor/ReleaseOps/）
        │  写 PlayerSettings / QualitySettings / URP Asset
        ▼
玩家包 + build-manifest.json（记档位与基线 sha256）
```

**改档位参数的正确做法：改 JSON。** 改完重出包，manifest 里的 `build.baselineSha256` 会变，
排查性能问题时先对这一项——基线本身改过，也是「同样的代码更卡了」的一种成因。

---

## 1. Player Settings 映射

| 基线字段 | 落点 | Unity API | 备注 |
|---|---|---|---|
| `target.resolution[0]` | Resolution → Width | `PlayerSettings.defaultScreenWidth` | |
| `target.resolution[1]` | Resolution → Height | `PlayerSettings.defaultScreenHeight` | |
| `target.fullscreenMode` | Resolution → Fullscreen Mode | `PlayerSettings.fullScreenMode` | `FullScreenWindow`＝无边框全屏（集显友好） |
| `target.resizableWindow` | Resolution → Resizable Window | `PlayerSettings.resizableWindow` | |
| `target.runInBackground` | Other Settings → Run In Background | `PlayerSettings.runInBackground` | 单机游戏关掉，避免抢 CPU |
| `scripting.backend` | Scripting Backend | `PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, …)` | il2cpp / mono |
| `scripting.managedStrippingLevel` | Managed Stripping Level | `PlayerSettings.SetManagedStrippingLevel` | |
| `scripting.apiCompatibilityLevel` | Api Compatibility Level | `PlayerSettings.SetApiCompatibilityLevel` | |
| `scripting.il2cppCompilerConfiguration` | IL2CPP 编译配置 | `PlayerSettings.SetIl2CppCompilerConfiguration` | 仅 il2cpp 生效 |
| `scripting.stripEngineCode` | — | — | 由后端决定，不单独写 |
| `scripting.incrementalGC` | — | — | Unity 2022 默认开启，不单独写 |
| 固定 | Graphics API | `PlayerSettings.SetGraphicsAPIs(…, { Direct3D11 })` | Windows 目标固定 DX11，避免落到 DX12 特性集导致集显掉帧 |
| 版本号 | Player Settings → Version | `PlayerSettings.bundleVersion` | 取自仓库 `VERSION`，构建后随工程设置一起还原 |

## 2. Quality Settings 映射

| 基线字段 | 落点 | Unity API |
|---|---|---|
| `quality.unityQualityLevel` | Quality → 档位 | `QualitySettings.SetQualityLevel(index, true)`（按名字匹配） |
| `quality.shadows.enabled` | Quality → Shadows | `QualitySettings.shadows`（枚举 `ShadowQuality`：All / Disable） |
| `quality.shadows.distance` | Quality → Shadow Distance | `QualitySettings.shadowDistance` |
| `quality.shadows.resolution` | Quality → Shadow Resolution | `QualitySettings.shadowResolution`（512→Low / 1024→Medium / 2048→High） |
| `quality.shadows.cascades` | Quality → Cascades | `QualitySettings.shadowCascades`（**Unity 只接受 2 / 4**，基线写 1 时按 2 处理） |
| `quality.msaa` | Quality → Anti Aliasing | `QualitySettings.antiAliasing`（1→0 关 / 2→2x / 4→4x / 8→8x） |
| `target.vsync` | Quality → VSync Count | `QualitySettings.vSyncCount`（0 关 / 1 开） |
| `quality.lodBias` | Quality → LOD Bias | `QualitySettings.lodBias`（基线是倍数，API 是百分数，×100） |
| `quality.maximumLODLevel` | Quality → Maximum LOD Level | `QualitySettings.maximumLODLevel` |
| `quality.anisotropicFiltering` | Quality → Anisotropic Filtering | `QualitySettings.anisotropicFiltering`（≤1→Disable，否则 ForceEnable） |
| `quality.masterTextureLimit` | Quality → Texture Quality | `QualitySettings.masterTextureLimit`（**2022.3 已废弃**，同时写 `globalTextureMipmapLimit`） |
| `quality.textureMipmapLimit` | — | `QualitySettings.globalTextureMipmapLimit`（2022.3 的新名字） |
| `quality.softParticles` | Quality → Soft Particles | `QualitySettings.softParticles` |
| `quality.realtimeReflectionProbes` | Quality → Realtime Reflection Probes | `QualitySettings.realtimeReflectionProbes` |
| `quality.asyncUploadTimeSlice` | Quality → Async Upload Time Slice | 同名 |
| `quality.asyncUploadBufferSize` | Quality → Async Upload Buffer Size | 同名 |

## 3. Graphics Settings / URP Asset 映射

URP 资产用 `SerializedObject` 按字段名写（避免编译期依赖 URP 类型，URP 升级也不会炸）。
字段不存在时跳过并记入出包日志，不静默。

| 基线字段 | URP Asset 字段 | 说明 |
|---|---|---|
| `target.renderScale` | `m_RenderScale` | 低档靠它压分辨率压力 |
| `quality.hdr` | `m_SupportsHDR` | |
| `quality.msaa` | `m_MSAA` | |
| `quality.shadows.distance` | `m_ShadowDistance` | |
| `quality.shadows.cascades` | `m_ShadowCascadeCount` | |
| `quality.shadows.resolution` | `m_MainLightShadowmapResolution` | |
| `quality.additionalLights.perPixel` / `perObject` | `m_AdditionalLightsRenderingMode` | 0=Disabled / 1=PerVertex / 2=PerPixel |
| `quality.additionalLights.maxCount` | `m_AdditionalLightsPerObjectLimit` | |
| 同上 | `m_MainLightRenderingMode` | 主光同样受附加灯档位约束 |
| 固定 | `m_SoftShadowsSupported` | 低档关软阴影 |
| 固定 | `m_SupportsCameraDepthTexture` / `m_SupportsCameraOpaqueTexture` | 低档关，减少中间纹理 |

> **URP 资产本身必须先存在并被指定**：`GraphicsSettings.m_CustomRenderPipeline` 现在是 `{fileID: 0}`，
> 没有资产时上面这张表全部写不进去，严格门控会直接拒绝出包。

## 4. 只做判据或记录、不写进工程设置的基线字段

| 基线字段 | 为什么写不进去 | 归谁管 |
|---|---|---|
| `particles.maxParticles` | 是资产上的 `ParticleSystem.maxParticles`，不是工程设置 | 资产侧（资产 Gate） |
| `particles.trails / collision / lights / softParticles / meshParticles / subEmitters` | 同上，资产与特效规格 | 资产侧 |
| `textures.maxTextureSize / compression / compressionQuality / mipmaps / crunched` | 是每张贴图的导入设置（`TextureImporter`），构建期改会污染资产 | 资产侧（资产 Gate） |
| `textures.streamingMemoryMB` | 纹理流送额度是运行期内存管理行为 | **运行期引导**（见 §5） |
| `textures.uiIcons.*` | UI 图标母版与切片的规格 | 资产侧 |
| `memory.*` | 内存上限是**实测观测值**，不是可设置项 | 性能验收（实测） |
| `limits.*` | draw call / 三角形 / 包体 / 启动时间是**红线判据** | 验收（实测） |
| `acceptance.*` | 帧率与帧时判据 | 验收（采样） |
| `hardware.*` / `displayName` / `role` / `notes` | 文档与决策依据 | 人读 |

## 5. 运行期引导清单（**待工程侧补**）

> 这一节是本文件的核心：**这些项构建期写不进去，必须由工程侧在运行期补**。
> 每一条在出包日志里都有对应的 `[BUILD] warn`，不静默。

| # | 项 | 为什么构建期写不进去 | 归谁 | 怎么验证 | 不补的后果 |
|---|---|---|---|---|---|
| 1 | **帧率上限** `Application.targetFrameRate`（低档 30 / 中高档 60） | 运行期属性，构建期不写进包 | 工程侧运行期引导脚本 | 低档机器上 120 秒采样，fps ≥ 档位值 | vSync 关了但没人限帧 → 集显机会跑到 60fps 然后掉帧，帧时间尾部崩坏，p95 判据必挂 |
| 2 | **相机后期开关** `UniversalAdditionalCameraData.renderPostProcessing` | URP 的后期是**每台相机**的开关，不是全局工程设置 | 工程侧相机初始化 | 高档截图有后期、低档没有 | 「后期关」这条降档手段实际没生效，中低档白白付出开销 |
| 3 | **URP 的 FXAA** | URP 14 里 FXAA 是 Renderer Feature，不是 Asset 上的开关 | 工程侧 Renderer 配置 | 低档边缘锯齿不可见 | 低档白白多付了 MSAA 级别的边缘开销 |
| 4 | **纹理流送额度** `textures.streamingMemoryMB` | 流送额度是运行期内存行为 | 工程侧运行期引导 | 低档运行中贴图内存 ≤ 256MB | 贴图内存超限 → 集显机内存吃紧、卡顿甚至崩溃 |
| 5 | **粒子上限** `particles.maxParticles` | 资产上的 `ParticleSystem` 属性 | 资产侧 + 工程侧质量档 | Boss 三阶段粒子峰值 ≤ 档位上限 | Boss 战掉帧，且掉在最不该掉的地方 |
| 6 | **贴图压缩与导入设置** `textures.*` | `TextureImporter` 是资产设置，构建期改会污染资产 | 资产侧 | 资产 Gate G3 全绿 | 集显机加载纹理的显存与耗时超出档位上限 |
| 7 | **内存红线观测与降级** `memory.*` | 是实测值，不是设置 | 工程侧运行期观测 | 低档 120 秒采样，驻留内存 ≤ 1536MB | 不知道什么时候该降级，掉帧时没有兜底动作 |
| 8 | **分辨率/窗口模式兜底** `target.resolution` | 玩家可能改分辨率，运行期要有兜底策略 | 工程侧运行期 | 改分辨率后 UI 不越界、不黑屏 | 低配玩家自行调高分辨率 → 集显直接跪 |

### 5.1 运行期引导的最小接口建议

给工程侧一个可以直接调用的入口，发布侧在运行期引导脚本里按 manifest 的 `build.tier` 取参数：

```csharp
// 建议签名（工程侧实现，参数值从 build-manifest.json 的 build.tier 取）
public static class RuntimeQualityBootstrap
{
    /// <summary>按档位设置帧率上限与运行期质量参数。</summary>
    public static void Apply(int targetFrameRate, bool postProcessing, int streamingMemoryMB);
}
```

> 只要工程侧提供「能读 manifest 的档位 + 能设帧率/后期/流送额度」这三件事，
> 档位参数就不再需要构建期硬塞，两边的口径就统一了。

---

## 6. 版本容错写法（为什么有些参数会「被跳过」）

Unity 的 `QualitySettings` 成员在不同版本间增删过：

| 成员 | 2022.3 现状 |
|---|---|
| `QualitySettings.postProcessing` | **不存在** |
| `QualitySettings.textureMipmapLimit` | **不存在**（改名 `globalTextureMipmapLimit`） |
| `QualitySettings.masterTextureLimit` | 已废弃（`CS0618`），仍可用 |
| `QualitySettings.realtimeLightProbes` | **不存在** |
| `QualitySettings.shadows` | 类型是 `ShadowQuality`，不是 `ShadowCastingMode` |
| `ScriptingImplementation.Mono2` | **不存在**（枚举成员名不同） |

所以构建入口里写 `QualitySettings` 一律走**反射**：属性存在就写，不存在就**跳过并记进出包日志**
（`[BUILD] warn 跳过不兼容参数: <属性名>（本 Unity 版本无此属性）`）。

原则：**绝不因为某个 API 缺失就让整个构建失败，也绝不静默假装写了。**

---

## 7. 出包日志的 warn 与本文档的对应

| 出包日志 | 对应本文档 |
|---|---|
| `[BUILD] warn 帧率上限…需运行期引导脚本生效` | §5 第 1 条 |
| `[BUILD] warn 相机的后期开关…由运行期的相机配置决定` | §5 第 2 条 |
| `[BUILD] warn 跳过不兼容参数: xxx` | §6 |
| `[BUILD] warn 没有 URP 管线资产，档位的 URP 层参数未写入` | §3 末尾的说明 |
| `[BUILD] warn 找不到质量档 'xxx'，沿用当前档位` | §2 第一行 |
| `[BUILD] warn 切换构建目标: … -> …` | 目标平台切换的正常提示 |
| `[BUILD] info 脚本后端 / 质量档 / 开始构建 / ok` | 对应 §1、§2、§3 的落地确认 |
| `[BUILD] fail …` | 严格门控拒绝出包（URP 未挂载 / 场景缺失 / Unity 版本不符 / 构建报错） |

**判据**：出包日志里凡是「构建期保证不了的项」，都必须有一条对应的 warn。
没有 warn 也没有落地记录 = 静默漏项，属于门控失效。
