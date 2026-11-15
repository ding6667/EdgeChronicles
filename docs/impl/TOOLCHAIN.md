# 开工清单 · 本机工具与环境

> 适用：《EdgeChronicles》（游戏内名《拾锋录》）单机 3D 动作 RPG，**Unity 2022.3.62f3c1（Unity 中国版 LTS）· URP 14**，团队为 AI 员工  
> 用法：照着装、照着勾。**T 组缺一项就动不了**；B 组影响效率；C 组是 AI 员工的运行条件。

---

## A. 硬性必需

| #   | 工具               | 版本 / 要求                               | 用途         | 说明                      |
| --- | ---------------- | ------------------------------------- | ---------- | ----------------------- |
| T1  | Windows          | 10 / 11 64 位                          | 目标平台       | —                       |
| T2  | Unity Hub        | 3.x                                   | 版本管理       | unity.com/download      |
| T3  | **Unity Editor** | **2022.3.62f3c1**（Unity 中国版 LTS）<br>可执行文件 `E:/UnityHub/2022.3.62f3c1/Editor/Unity.exe` | 引擎本体 | ✅ **本机实测已装**，见 H 组 |
| T4  | Unity 模块         | Windows Build Support (Mono + IL2CPP) | 出包         | Hub 勾选                  |
| T5  | **IDE**          | **Rider 2024.3 LTS**（首选）或 VS2022 17.x | 写 C#       | 见下方两条备选                 |
| T6  | MSVC             | v143 + Windows SDK 10.0.19041.0+      | 编译原生插件     | Rider 用户仍需装，Unity 编译依赖  |
| T7  | .NET SDK         | 8.0.4xx                               | 导表器与 CI 工具 |                         |
| T8  | Git for Windows  | **≥ 2.40**                            | 版本控制       |                         |
| T9  | Git LFS          | **≥ 3.5**                             | 大文件        | `git lfs install`       |
| T10 | 远端仓库             | GitHub / Gitee **私有库**                | 同步         |                         |
| T11 | Unity 许可证        | Personal（`.ulf`）或 Pro                 | 打开工程       | Hub 内登录                 |

**A5 两条备选**

- **Rider 2024.3 LTS**（首选）：Unity 官方深度集成，Assembly Definition 导航、Profiler、批量重命名都更顺手.
- **VS2022 17.x**：勾选「使用 .NET SDK 的 C#」+「.NET 桌面开发」+「游戏开发」三个组件

> 注：本文档附带的 CI 脚本是 **bash 版**（`Tools/ci/*.sh`），在 Git Bash / CI 容器里直接跑，**不需要额外装 PowerShell 7.4+**。

---

## B. 强烈建议

| #  | 工具                    | 用途                  |
| -- | --------------------- | ------------------- |
| B1 | **Blender 4.x**       | AI 生成资产 → 低模 → FBX  |
| B2 | **Krita** 或 Photoshop | 贴图编辑、墨线与顶点色修正       |
| B3 | **ffmpeg**            | 音视频批量转换             |
| B4 | **Audacity**          | 音效处理                |
| B5 | 图像生成                  | ComfyUI 本地 或 在线 API |
| B6 | 第二块显示器                | 一边看规格一边改代码          |

---

## C. AI 员工的运行条件

### C1. CLI 智能体

能读仓库、改文件、跑命令的 agent CLI（Claude Code / Codex CLI / Cursor / WorkBuddy 自身）。

### C2. 模型 API Key —— **至少配两家**

> 策划线曾被单一供应商的频率限制（429）打断，整条线停摆。**至少配置两个不同厂商的模型**并给 agent 配 fallback 顺序，否则一次限流 = 一次停工。

### C3. 图像生成接入

本地 ComfyUI（需 ≥8GB 显存）或在线 API，key 存环境变量。

### C4. Unity 批处理可执行（自证的命脉）

```
"<Unity>\Editor\Unity.exe" -batchmode -nographics -quit \
  -projectPath "<项目绝对路径>" -executeMethod <Class.Method> -logFile -
```

`-logFile -` 让日志进 stdout，agent 直接读。已配好五个脚本：

| 脚本                                 | 作用                              |
| ---------------------------------- | ------------------------------- |
| `Tools/ci/check-compile.sh`        | 批处理编译 + 日志扫 `error CS`          |
| `Tools/ci/check-data.sh`           | 配表生成物结构校验                       |
| `Tools/ci/check-progression.sh`    | 养成层数据完整性（孤儿外键 / 派生覆盖 / A31–A42） |
| `Tools/ci/check-tests.sh`          | EditMode 测试（含架构守卫）              |
| `Tools/ci/check-tests-playmode.sh` | PlayMode 战斗与养成联动矩阵              |

### C5. 存档序列化：**只用 Unity 内建 `JsonUtility`**

不要引入 Newtonsoft（或任何第三方 JSON 库）。理由：多一个包 = 多一个版本漂移源；存档结构扁平化后 `JsonUtility` 足够。CRC32 与版本迁移自己实现（约 50 行），比引包更可控。

---

## D. 仓库约定

### D1. Unity 项目设置（**入库前必须先改**）

| 项                    | 设置                     | 原因                     |
| -------------------- | ---------------------- | ---------------------- |
| Asset Serialization  | **Force Text**         | 二进制序列化无法 diff、无法合并     |
| Visible Meta Files   | **开启**                 | `.meta` 里存 GUID，丢了引用全断 |
| Version Control Mode | **Visible Meta Files** | 同上                     |

### D2. LFS 边界（**哪些进、哪些不进**）

| 进 LFS                                   | 不进 LFS（保持文本）                        | 原因                                 |
| --------------------------------------- | ----------------------------------- | ---------------------------------- |
| `*.fbx *.blend *.psd *.png *.jpg *.tga` | **`*.unity`**                       | UnityYAMLMerge **无法**合并 LFS 里的文本冲突 |
| `*.wav *.mp3 *.ogg *.ttf *.bank`        | **`*.prefab`**                      | 同上                                 |
| `*.unitypackage`                        | `*.asset *.controller *.anim *.mat` | 同上                                 |

> **踩过就记住**：`.unity`/`.prefab` 进 LFS 会让场景冲突无法合并，只能整份重做。

### D3. 必须入库 / 必须忽略

| 入库                                              | 忽略                                               |
| ----------------------------------------------- | ------------------------------------------------ |
| 所有 `.meta`                                      | `Library/ Temp/ Obj/ Build/ Logs/ UserSettings/` |
| `ProjectSettings/`                              | `*.csproj *.sln *.user *.pidb`                   |
| `Packages/manifest.json` + `packages-lock.json` | `.vs/ .idea/ .vscode/`                           |
| `Assets/_Project/Data/` 的**生成器**（非产物）           | `Assets/_Project/Data/*.json`（产物不入库）             |
| —                                               | `.env *.key *.keystore *.p12`（密钥绝不入库）            |

### D4. 分支

每个 AI 员工一条分支，合并走 PR，禁止直推 main；main 必须有 CI 保护。

---

## E. 首次拉取后的初始化

| #  | 步骤                                                        | 通过判据                                     |
| -- | --------------------------------------------------------- | ---------------------------------------- |
| E1 | `git clone` → `git lfs pull`                              | 文件不是指针文件                                 |
| E2 | **现场验证 Unity 版本与包版本**（见 H 组）                              | 能打开工程即可                                  |
| E3 | 用 Hub 指定 2022.3.62f3c1 打开工程，**手工打开一次**完成 Library 重建（10–30 分钟） | 无报错                                      |
| E4 | 等包还原                                                      | `packages-lock.json` 无变更；`git status` 干净 |
| E5 | `bash Tools/ci/check-compile.sh`                          | 退出码 0                                    |
| E6 | `bash Tools/ci/check-data.sh`                             | 通过或 data-skip                            |
| E7 | `bash Tools/ci/check-tests.sh`                            | 全绿                                       |
| E8 | `git status`                                              | **完全干净**                                 |

> **E8 最关键**：首次导入后不干净，说明 `.gitignore` / LFS 配错了，AI 员工之后每次提交都会带垃圾。

---

## F. 硬件

| 项  | 建议                                               |
| -- | ------------------------------------------------ |
| 内存 | **32 GB 起步，64 GB 更稳**（Unity 编译 + 多 agent 并行最吃内存） |
| 硬盘 | **SSD 1 TB**（`Library/` 轻易 20–40 GB，还要放 LFS 仓库）  |
| 显卡 | 非必需（URP + 低模，集显可跑；做美术资产或本地跑扩散模型才需要）              |
| 网络 | 稳定（LFS 拉取 + 模型 API）                              |

---

## G. 勾选清单

**A 硬性**

- [ ] Unity Hub / Unity **2022.3.62f3c1**（Unity 中国版 LTS，含 Windows Build Support）
- [ ] Rider 2024.3 LTS（或 VS2022 三个组件）
- [ ] MSVC v143 + Windows SDK 10.0.19041.0+
- [ ] .NET SDK 8.0.4xx
- [ ] Git ≥ 2.40 + Git LFS ≥ 3.5（`git lfs install`）
- [ ] 私有远端仓库
- [ ] Unity 许可证已激活

**B 建议**

- [ ] Blender 4.x / Krita / ffmpeg / Audacity / 图像生成 / 第二显示器

**C AI 员工**

- [ ] agent CLI 可用
- [ ] **模型 API Key ≥ 2 家**
- [ ] 批处理命令跑通、五个脚本就位
- [ ] 确认**不使用 Newtonsoft**（存档走 `JsonUtility`）

**D 仓库**

- [ ] Asset Serialization = Force Text
- [ ] Visible Meta Files = 开启
- [ ] `.gitattributes` LFS 边界正确（`.unity`/`.prefab` **不进** LFS）
- [ ] `.gitignore` 覆盖 Unity 生成物与密钥
- [ ] `manifest.json` + `packages-lock.json` 入库
- [ ] 分支保护已开

---

## H. 版本锁定清单（**开工第一步就要定死**）

| 项                 | 当前值         | 状态                                                                         |
| ----------------- | ----------- | -------------------------------------------------------------------------- |
| Unity 版本          | **2022.3.62f3c1**（Unity 中国版 LTS） | ✅ **本机实测已装并已激活**。真值为 `ProjectSettings/ProjectVersion.txt`（已入库）。可执行文件 `E:/UnityHub/2022.3.62f3c1/Editor/Unity.exe`。**注意：这是 Unity 中国版，GitHub 托管镜像无预装，CI 需自托管 runner** |
| URP               | **14.0.11** | ✅ 已装，以 `packages-lock.json` 为准 |
| Input System      | **1.7.0** | ✅ 已装，同上 |
| Test Framework    | **1.1.33** | ✅ 已装，同上 |
| Profiling Core    | **1.0.2** | ✅ 已装，同上 |
| Animation Rigging | **1.2.1** | ✅ 已装。**必需**：弓手上半身 Override Layer + AvatarMask + TwoBoneIK |
| AI Navigation     | **1.1.5** | ✅ 已装。敌人导航 |
| Cinemachine       | **不引入** | MVP 用自写定点相机；引入反而多一个版本漂移源 |
| Newtonsoft        | **不引入**     | 存档用内建 `JsonUtility`                                                        |

> **规则**：以上版本号已按**本机实测**锁定（Unity 2022.3.62f3c1 / Unity 中国版 LTS），**最终真值为 `Packages/packages-lock.json`**——Unity 解析出的实际版本以它为准。任何人单独升级包版本，**必须同步更新本表与 manifest**，否则又制造一份第二真值源。
> **Unity 中国版注意**：不在 GitHub 托管镜像的预装列表里，CI 必须用自托管 runner（见 `docs/release/CI-SETUP.md`）。

---

*本清单随工程演进更新；改动本文件时只改一处，其余文档引用本文件。*
