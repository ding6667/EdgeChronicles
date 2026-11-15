# 变更日志

本文件记录《拾锋录》（EdgeChronicles）每个版本的**玩家可见**变更。
版本号规则与递增判据见 [`docs/release/VERSIONING.md`](docs/release/VERSIONING.md)。

格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，
版本号遵循语义化版本（预发布标识见版本规范）。

**约定**：
- 每个版本一段，段内按「新增 / 修复 / 变更 / 存档与兼容性 / 性能」分节。
- 涉及存档结构改动的版本，必须有「存档与兼容性」一节。
- 内部实现细节（类名、模块重构）不写入本文件，除非它改变了玩家的可见行为。

---

## [0.1.0-alpha.1] - 2026-11-15

首个内部可玩版：工程骨架落地，各子系统按分工并行推进。

### 新增

- **工程骨架**：8 个程序集分层（Core → Data → Kernel → Binding → Flow，Presentation / UI 只订阅事件），
  依赖方向由 `ArchitectureTests` 强制，接错依赖直接测试红。
- **CI 自证脚本**（`Tools/ci/`）：批处理编译、配表结构校验、养成层数据完整性审计、
  EditMode 测试（含架构守卫）、PlayMode 战斗与养成联动矩阵。
- **发布基础设施**：
  - 一键出包脚本 `Tools/build/build-player.sh`（IL2CPP / Mono 双后端，参数化输出目录与目标平台），
    出包后自动产出 `build-manifest.json`（版本、commit、Unity 版本、档位、包体大小、逐文件校验和）。
  - **三档机型基线**（`Tools/build/baselines/`）：低档为集显轻薄本 1080p30 验收底线，
    中档为默认发行档，高档为主流独显上限参照；参数写在 JSON 里，由脚本读取。
  - CI 工作流三条（出包 / 测试 / 资产 Gate），每条都有可脚本判定的通过判据。
  - 发布与回滚清单、补丁说明模板、存档兼容性处置口径。

### 变更

- 引擎与渲染管线对齐到实装版本：Unity **2022.3.62f3c1**（中国版 LTS）+ URP 14.0.11。
- 资产序列化采用 Force Text、Visible Meta Files；`.unity` / `.prefab` 保持文本不进 LFS，
  二进制资产走 LFS。
- 版本号采用 `MAJOR.MINOR.PATCH[-预发布]`；当前处于 `alpha` 阶段，存档格式版本为 `save_version = 3`。

### 存档与兼容性

- 本版本不改存档结构，已有存档（`save_version = 3`）可正常读取。
- 存档读取策略：高于当前版本一律拒绝读取并提示，不尝试降级、不删档；低于当前版本走迁移链补默认值。

### 性能

- 性能底线锚定为**集显轻薄本 1080p 30fps**。三档机型基线以该底线为验收依据，
  低档参数即为发行否决项的下界。

---

## [Unreleased]

（此处记录尚未发布到版本号的变更。发布时把内容并入对应的 `## [<版本号>]` 段，并更新 `VERSION`。）
