# CI 搭建指引（自托管 Windows runner）

> 归属：发布侧（路远行）。本文件是**用户在本机操作 CI 的照做清单**。
> 仓库设置（变量 / secret）由用户自己设，发布侧不碰仓库设置。

---

## 0. 为什么必须自托管 runner

| 项 | 事实 |
|---|---|
| Unity 版本 | `2022.3.62f3c1`（**中国版 LTS**） |
| 托管 runner | GitHub 托管镜像的预装列表里**没有**中国版 Unity。用托管 runner 必然在「准备 Unity」这步失败 |
| 本机 | 已装 `E:\UnityHub\2022.3.62f3c1\Editor\Unity.exe`，许可证已激活，能正常打开工程 |
| 结论 | 走**自托管 Windows runner**，跑在本机（或任何一台装了同版本 Unity 的 Windows 机器）上 |

三条工作流（`build.yml` / `test.yml` / `lint.yml`）的 `runs-on` 都写成：

```yaml
runs-on: ${{ fromJSON(vars.UNITY_RUNNER_LABELS || '["self-hosted","windows"]') }}
```

默认标签 `["self-hosted","windows"]` —— GitHub 给自托管 runner 自动打的 `self-hosted` / `Windows` 标签就能匹配上，**不需要额外配置**。

---

## 1. 前置条件

| # | 检查 | 怎么确认 |
|---|---|---|
| 1 | Unity 2022.3.62f3c1 已安装 | `E:\UnityHub\2022.3.62f3c1\Editor\Unity.exe` 存在 |
| 2 | Unity 许可证已激活 | Unity Hub → Preferences → Licenses 里能看到有效许可 |
| 3 | Git for Windows + Git LFS | `git --version` ≥ 2.40；`git lfs version` ≥ 3.5 |
| 4 | 磁盘空间 | 建议 **≥ 60 GB**（Unity 首次导入的 `Library/` + LFS 资产 + 构建产物） |
| 5 | runner 机器上**没有开着的 Unity 编辑器** | 工程被占用时 batchmode 会失败（脚本会明确报「工程被占用」） |

---

## 2. 三件事（照着点）

### 事项一 · 注册自托管 runner

**步骤 1：进入 runner 配置页**

仓库 → **Settings** → **Actions** → **Runners** → **New self-hosted runner** → 选 **Windows** → **x64**。
页面上会给出下载链接，下载 `actions-runner-win-x64-<版本>.zip`。

**步骤 2：解压**

解压到 `C:\actions-runner`（路径尽量不含空格，例如不要放在 `C:\Program Files\...` 下）。

**步骤 3：注册**

在 `C:\actions-runner` 下打开 PowerShell 或 cmd，运行：

```powershell
cd C:\actions-runner
.\config.cmd
```

逐项回答（括号里是要输入的内容）：

| 提问 | 输入 | 说明 |
|---|---|---|
| `How would you like to configure the runner?` | `1` | Just run（只跑本仓库）。选 2 会问工作组，多余 |
| `Enter the repository URL` | `https://github.com/ding6667/EdgeChronicles` | 本仓库的 HTTPS 地址 |
| `Enter the name of the runner group` | 直接回车 | 用默认组 |
| `Enter the name of the runner` | `edgechronicles-win-01` | **本机唯一**，重名会注册失败 |
| `Enter the labels of the runner (comma-separated)` | `windows,unity-2022.3.62f3c1` | `windows` / `self-hosted` / `X64` 是 GitHub 自动加的；这里加的 `unity-2022.3.62f3c1` 只是个便于人看的标记 |
| `Enter your choice for how the runner is used` | `1` | Just run（不装成系统服务）。装成服务需要管理员权限且会开机自启，资源占用常驻 |
| `Do you want to start the runner?` | `Y` | 立刻启动 |

**步骤 4：确认上线**

仓库 → **Settings** → **Actions** → **Runners**，能看到 `edgechronicles-win-01` 且状态为 **Online**。

**以后每次用之前**：在 `C:\actions-runner` 运行 `.\run.cmd`；不用了就 Ctrl+C 停掉（见 §4 资源占用）。

> 想让 `runs-on` 精确匹配自定义标签，就把仓库变量 `UNITY_RUNNER_LABELS` 设成
> `["self-hosted","windows","unity-2022.3.62f3c1"]`（见事项二）。

---

### 事项二 · 设仓库变量 `UNITY_PATH`

仓库 → **Settings** → **Secrets and variables** → **Actions** → 上方切到 **Variables** 标签 → **New repository variable**。

| 字段 | 填什么 |
|---|---|
| Name | `UNITY_PATH` |
| Value | `E:\UnityHub\2022.3.62f3c1\Editor\Unity.exe` |

**要点**：

- 用 **Windows 反斜杠形式**。脚本会自己转成 POSIX 形式再用（`Tools/build/ci-unity-preflight.sh` 里用 `cygpath` 转换），所以两种写法都能跑，但推荐和上面一致。
- 变量**不是 secret**，对有仓库读权限的人可见。路径本身不算敏感，可以接受。
- 不设也能跑：脚本会退回扫描 `E:/UnityHub/*/Editor/Unity.exe`、`C:\Program Files\Unity\Hub\Editor\...` 等常见位置。但显式设变量更稳（多版本共存时不会挑错）。
- 可选：再建一个变量 `UNITY_RUNNER_LABELS` = `["self-hosted","windows"]`，用于指定 runner 标签。

---

### 事项三 · 设 secret `UNITY_LICENSE`

仓库 → **Settings** → **Secrets and variables** → **Actions** → 上方切到 **Secrets** 标签 → **New repository secret**。

| 字段 | 填什么 |
|---|---|
| Name | `UNITY_LICENSE` |
| Secret | `.ulf` 许可证文件的**全文**（XML 文本） |

**怎么拿到 .ulf 全文**（三选一）：

| 方式 | 步骤 |
|---|---|
| A. 本机已激活（最省事） | 打开 `C:\ProgramData\Unity\Unity_lic.ulf`（机器级）或 `%APPDATA%\Unity\Unity_lic.ulf`（用户级），用文本编辑器打开，**全选复制** |
| B. 手动激活 | Unity Hub → Preferences → Licenses → **Manual activation** → 下载 `.alf` → 打开 <https://license.unity3d.com/manual> 上传 `.alf` → 下载 `.ulf` → 文本编辑器打开复制全文 |
| C. 账号激活（不用 ULF） | 改设三个 secret：`UNITY_EMAIL`、`UNITY_PASSWORD`、`UNITY_SERIAL`，工作流会用账号方式激活（见工作流里的 `激活 Unity 许可证` 逻辑） |

**安全须知**：

- secret 在日志里自动掩码（显示成 `***`）。
- **`.ulf` 绝不能进仓库**。资产 Gate 的 `G8` 会扫描仓库里的 `.ulf` / `.key` / `.p12` / `.env` 等文件，命中直接判红。
- 许可与机器绑定，同一台 runner 复用同一份 ULF 没有问题。

---

## 3. 验证

**方式一（推荐，最快）**：仓库 → **Actions** → 选 `build` → 右上 **Run workflow** → 选分支 → 运行。

**方式二**：本地推一个提交触发 `push` 事件。

看 `build.yml` 的第一个步骤「准备 Unity（解析路径 / 校验版本 / 激活许可证）」是否变绿。它跑的是：

```bash
bash Tools/build/ci-unity-preflight.sh
```

该脚本会依次做三件事，任何一件失败都以**非零退出码**结束，并把原因写进日志：

1. 解析 Unity 路径（`UNITY_PATH` 变量 → `cygpath` 转换 → 常见目录扫描）；
2. 比对 **Unity 版本必须等于** `ProjectSettings/ProjectVersion.txt` 的 `m_EditorVersion`（不一致直接拒绝，避免用错版本写出不同序列化版本的资产）；
3. 激活许可证（写 `%APPDATA%\Unity\Unity_lic.ulf` 用户级路径，普通权限即可写；不可写时退回机器级 `%ProgramData%\Unity\Unity_lic.ulf`；再不行就用账号方式）。

也可以在本机手动跑同一条命令自检：

```bash
bash Tools/build/ci-unity-preflight.sh
```

---

## 4. 资源占用与替代路径

### 4.1 自托管 runner 会占本机资源

| 占用项 | 量级 | 说明 |
|---|---|---|
| 磁盘 | 数十 GB | runner 的 work 目录会 checkout 整个仓库（含 LFS 二进制资产），Unity 首次导入还要建 `Library/` |
| 内存 | 2–8 GB | Unity 批处理编译 / 跑测试时的峰值 |
| CPU | 编译期满载 | 出包（IL2CPP）比跑测试更吃 CPU |

**建议**：不要开机自启（`config.cmd` 选 `1` 而不是 `2`），需要时手动 `run.cmd`，用完 Ctrl+C。

### 4.2 替代路径：不配 runner，本机直接守门

**CI 只是把这套命令搬到另一台机器上跑。命令本身就是判据**，所以不配 runner 一样能守门：

```bash
# 五项自证（工程线所有，发布侧只调用）
bash Tools/ci/check-compile.sh            # 退出码 0，日志无 error CS
bash Tools/ci/check-data.sh               # 退出码 0
bash Tools/ci/check-progression.sh        # 日志含 ProgressionAudit: tables=20 orphans=0 derivedOverwritten=100%
bash Tools/ci/check-tests.sh              # results.xml 中 failed="0" 且 passed>0
bash Tools/ci/check-tests-playmode.sh     # results-playmode.xml 中 failed="0" 且 passed>0

# 资产 Gate（发布侧）
bash Tools/build/lint-assets.sh --tier low # 退出码 0 且无 [LINT] FAIL

# 出包 + 产物校验（发布侧）
bash Tools/build/build-player.sh --tier low --backend il2cpp --target win64
bash Tools/build/verify-artifact.sh Build/low-il2cpp-win64
```

全部命令在 **Git Bash** 下执行即可。

> ⚠️ **跑这些命令前先关掉 Unity 编辑器**：工程被占用时 batchmode 会失败。
> 脚本会明确报「Unity Editor 正占用本工程」并以退出码 3 结束 —— 这是**预期行为**，不是 bug。

**两条路的关系**：

| | 本机直接跑 | 自托管 runner + CI |
|---|---|---|
| 判据 | 完全相同 | 完全相同 |
| 跑在哪 | 本机 | 本机或另一台机器 |
| 触发 | 手动 | push / PR / 手动 |
| 适合 | 开发期随手守门 | 提交后自动守门、多人协作时留痕 |

---

## 5. 故障排查

| 现象（日志原文） | 原因 | 处理 |
|---|---|---|
| `找不到 Unity：请设置仓库变量 UNITY_PATH` | 变量没设，且常见目录也没扫到 | 设 `UNITY_PATH` 变量 |
| `UNITY_PATH 指向的文件不存在: E:\...` | 路径写错 / Unity 换了版本目录 | 核对 `E:\UnityHub\` 下的实际版本目录名 |
| `Unity 版本不符：工程要求 2022.3.62f3c1，runner 上是 xxx` | runner 上的 Unity 版本不同 | 装同版本 Unity，或换一台机器 |
| `没有可用的 Unity 许可证来源` | secret 没设 | 设 `UNITY_LICENSE`（或三个账号 secret） |
| `ULF 许可证无处可写` | 用户级与机器级目录都写不了 | 改用账号方式激活（`UNITY_EMAIL` + `UNITY_PASSWORD` + `UNITY_SERIAL`） |
| `Unity Editor 正占用本工程` | runner 机器上开着 Unity 编辑器 | 关掉编辑器；runner 机器不要开 Unity |
| `缺少 LFS 二进制资产` / 出现 LFS 指针 | LFS 没拉下来 | `git lfs install` 后 `git lfs pull`；确认 runner 上装了 Git LFS |
| `包体超过档位上限` | 产物超过基线 `limits.buildSizeMB` | 对照 `build-manifest.json` 的 `payload.sizeMB` 与基线；该档位不合适就换档位出包 |
| `资产 Gate 未通过` | 有 `[LINT] FAIL` | 看 `Logs/lint-assets.log` 里的 `[LINT] FAIL` 行，逐条修 |

---

## 6. 仓库设置清单（用户要做的事汇总）

| 类型 | 名称 | 值 | 必需 |
|---|---|---|---|
| Variable | `UNITY_PATH` | `E:\UnityHub\2022.3.62f3c1\Editor\Unity.exe` | 是 |
| Variable | `UNITY_RUNNER_LABELS` | `["self-hosted","windows"]` | 否（默认值已可用） |
| Secret | `UNITY_LICENSE` | `.ulf` 全文 | 二选一 |
| Secret | `UNITY_EMAIL` / `UNITY_PASSWORD` / `UNITY_SERIAL` | 账号信息 | 二选一（不用 ULF 时） |

设置位置：仓库 → **Settings** → **Secrets and variables** → **Actions**。

> **发布侧不碰仓库设置**：上面四项由用户自己设置。
