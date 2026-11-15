# Tools/build · 出包与校验

《拾锋录》（EdgeChronicles）的构建基础设施。**单机游戏的「发布」＝出包分发**，
本目录负责把「从某个 commit 出出一份带完整溯源信息的玩家包」这件事变成一条可重复、
可判定的命令。

配套文档：[`docs/release/RELEASE-CHECKLIST.md`](../../docs/release/RELEASE-CHECKLIST.md)（发布与回滚清单）、
[`docs/release/BASELINES.md`](../../docs/release/BASELINES.md)（三档机型基线）、
[`docs/release/VERSIONING.md`](../../docs/release/VERSIONING.md)（版本号与提交规范）。

---

## 1. 目录

```
Tools/build/
├── build-player.sh          一键出包（主入口）
├── verify-artifact.sh       产物校验（用 manifest 的校验和判定「是不是这一版」）
├── lint-assets.sh           资产 Gate（命名 / 贴图 / 材质槽 / LFS 边界 / 版本 / 密钥）
├── baselines/
│   ├── low.json             低档 · 集显轻薄本（验收底线）
│   ├── medium.json          中档 · 默认发行档
│   └── high.json            高档 · 上游参照
├── lib/
│   ├── common.sh            日志、退出码、JSON 读取、校验和
│   ├── unity-resolve.sh     Unity 解析、版本一致性、工程占用检测
│   └── manifest.sh          build-manifest.json 生成与校验
├── tools/jsonq.py           JSON 取值 / 校验 / 摊平（jq 不可用时的退路）
└── unity/
    └── EdgeChronicles.ReleaseOps.Editor.cs   构建入口 + 资产 Gate（构建期临时注入）
```

---

## 2. 快速上手

```bash
# 1) 看一眼要出什么（不调用 Unity）
bash Tools/build/build-player.sh --tier low --dry-run

# 2) 出包（默认 low / il2cpp / win64）
bash Tools/build/build-player.sh --tier low --backend il2cpp --target win64

# 3) 校验产物
bash Tools/build/verify-artifact.sh Build/low-il2cpp-win64

# 4) 跑资产 Gate
bash Tools/build/lint-assets.sh --tier low
```

### 2.1 `build-player.sh` 参数

| 参数 | 说明 | 默认 |
|---|---|---|
| `--tier <low\|medium\|high>` | 机型档位 | 必填 |
| `--backend <il2cpp\|mono>` | 脚本后端 | 取基线的 `scripting.backend` |
| `--target <win64\|linux64\|mac>` | 目标平台 | `win64` |
| `--output <目录>` | 产物目录 | `Build/<tier>-<backend>-<target>` |
| `--version <版本号>` | 覆盖 `VERSION` | 读 `VERSION` |
| `--unity <路径>` | Unity 可执行 | `UNITY_PATH` 环境变量 → 自动扫描 |
| `--scenes "<a,b>"` | 指定构建场景 | 读 `EditorBuildSettings` |
| `--non-strict` | 关闭严格门控（仅开发包） | 严格 |
| `--no-stage-entrypoint` | 禁止注入构建入口 | 允许注入 |
| `--dry-run` | 只预检与打印计划 | — |

### 2.2 产物目录结构

```
Build/low-il2cpp-win64/
├── EdgeChronicles.exe        玩家包本体
├── EdgeChronicles_Data/      玩家包本体
├── UnityPlayer.dll 等        玩家包本体
├── build-manifest.json       版本 / commit / Unity 版本 / 档位 / 逐文件 sha256
├── unity-build.log           Unity 构建日志
└── unity-build-result.json   Unity 侧回执（应用了哪些参数）
```

`build-manifest.json` 里的 `payload.sha256` 是**整棵产物树的根校验和**：
任何一个文件被改动一个字节，这个值就变。它是回滚与问题定位的唯一依据。

---

## 3. 三档机型基线

参数**只写在 JSON 里**（`baselines/*.json`），文档只解释含义。

| 档位 | 定位 | 目标机型 | 分辨率 / 帧率 |
|---|---|---|---|
| `low` | **验收底线** | Intel Iris Xe 集显 / i5-1135G7 / 8GB | 1920×1080 @ 30fps |
| `medium` | 默认发行档 | GTX 1650 / Iris Xe 锐酷 / 16GB | 1920×1080 @ 60fps |
| `high` | 上限参照 | RTX 3060 / RX 6600 / 32GB | 2560×1440 @ 60fps |

每档给的是**具体参数**：分辨率与帧率、阴影（开关/分辨率/级联/距离）、后期（开关与各项）、
粒子（数量/拖尾/碰撞/灯）、贴图（最大边/压缩/流送额度）、内存（工作集/贴图/网格/GC）、
以及上限类红线（draw call / 三角形 / 包体 / 启动时间）。

改档位参数的正确做法：**改 JSON，不改文档**。manifest 会记下基线文件的 sha256，
排查性能问题时先对这一项——基线本身改过，也是「同样的代码更卡了」的一种成因。

---

## 4. 构建入口的注入机制（请先读这一节）

`Assets/**` 归工程线与美术线所有，发布侧**不长期占用**这个目录。
但出包需要一个 Editor 入口来设置档位参数并调用 `BuildPipeline`。

因此采取**注入式**方案：

1. `Tools/build/unity/EdgeChronicles.ReleaseOps.Editor.cs` 是入口的**唯一真值源**，放在发布侧目录。
2. 出包 / Gate 前，脚本检查工程里是否已存在 `namespace EdgeChronicles.ReleaseOps`：
   - **已存在** → 直接用工程里的实现，不注入（工程侧接管后本目录的副本自动让位）；
   - **不存在** → 临时复制到 `Assets/_Project/Editor/ReleaseOps/`，执行完立刻删除（含 `.meta`）。
3. 无论成功失败，清理逻辑都在 `trap` 里执行；工作树不会留下残留。

工程侧若要长期接管这个入口，约定如下（发布侧按此调用，不关心实现细节）：

| 项 | 约定 |
|---|---|
| 命名空间 | `EdgeChronicles.ReleaseOps` |
| 类与方法 | `PlayerBuild.Build()`（出包）、`AssetGate.Lint()`（资产 Gate） |
| 参数 | 见文件头的「命令行约定」段（`-buildTier` / `-buildOutput` / `-buildBackend` / `-buildTarget` / `-buildVersion` / `-buildParams` / `-buildScenes` / `-buildStrict`） |
| 日志 | `[BUILD] ok` / `[BUILD] fail` / `[BUILD] warn` / `[LINT] PASS|FAIL|WARN|SUMMARY` |
| 回执 | 出包成功后在输出目录写 `unity-build-result.json` |

---

## 5. 失败即非零退出码

所有脚本的失败路径都返回非零码，**没有静默通过**。退出码跨脚本含义统一：

| 码 | 含义 | 典型触发 |
|---|---|---|
| 0 | 成功 | — |
| 1 | 未分类失败 | — |
| 2 | 参数 / 用法错误 | 未知参数、档位名不存在 |
| 3 | 环境不可用 | 找不到 Unity、Unity 版本与工程不符、**工程被占用** |
| 4 | 机型基线非法 | JSON 不合法、必填字段缺失 |
| 5 | 构建失败 | Unity 报错、产物缺失、没有构建场景 |
| 6 | 产物校验失败 | 与 manifest 的 sha256 对不上 |
| 7 | 资产 Gate 未通过 | 有 `[LINT] FAIL` |
| 8 | 缺少依赖 | 没有 jq / python3 / sha256sum |
| 9 | 归档或回滚步骤失败 | — |
| 10 | 版本源不可信 | 工作树非干净、版本号格式非法 |

### 5.1 工程被占用的处理

Unity Editor 打开工程时会持有 `Temp/UnityLockfile`，此时 batchmode 会因「工程被占用」失败。
脚本在调用 Unity **之前**就检测这一点：

```bash
bash Tools/build/build-player.sh --tier low
# [FAIL ] Unity Editor 正占用本工程，构建无法进行：工程锁文件存在: Temp/UnityLockfile
# [FAIL ] 请先关闭 Unity Editor（含 Hub 里的残留实例），再重新执行本脚本。
```

退出码 3。**不会**伪装成构建成功。

### 5.2 构建期对工程设置的临时改动

出包会临时改 `PlayerSettings`（版本号、后端、分辨率）与 `QualitySettings`（档位参数）。
脚本在构建前备份、构建后还原；还原前会比对文件修改时间：

- 文件在构建后**没有**被再次修改 → 正常还原；
- 文件在构建后**又被改过**（并行工作树里可能发生）→ **跳过还原**并明确告警，交人工核对。

无论哪种情况，CI 的「工作树未被构建污染」这一步都会给出判据。

---

## 6. 依赖

| 依赖 | 必需 | 说明 |
|---|---|---|
| Git Bash / bash | 是 | 脚本是 bash 版 |
| Unity 2022.3.62f3c1（中国版 LTS） | 是 | 版本必须与 `ProjectSettings/ProjectVersion.txt` 一致，否则脚本拒绝构建 |
| `sha256sum` 或 `shasum` | 是 | 计算校验和 |
| `jq` 或 `python3` | 是 | 读机型基线 JSON（优先 jq，退化到 python3） |
| `find` / `sed` / `awk` / `sort` | 是 | 常规工具链 |

CI 侧还需要仓库 secret：`UNITY_LICENSE`（或 `UNITY_EMAIL` + `UNITY_PASSWORD` + `UNITY_SERIAL`）。
**任何真实密钥都不入库**；资产 Gate `G8` 会扫描仓库里的密钥文件并判红。

---

## 7. 与 CI 的关系

| 工作流 | 作用 | 判据 |
|---|---|---|
| `.github/workflows/build.yml` | 编译 + 出包 | 出包退出码 0；玩家包存在；manifest 存在且校验和可复算；包体不超档位上限；出包后工作树干净 |
| `.github/workflows/test.yml` | 调 `Tools/ci/check-*.sh` | 每个脚本的退出码与日志关键行 |
| `.github/workflows/lint.yml` | 资产 Gate | `lint-assets.sh` 退出码 0 且无 `[LINT] FAIL`；另有不依赖 Unity 的仓库卫生检查 |

分支/PR 出的是**开发包**（非严格门控）；打 tag 出的是**正式包**（严格门控）。
严格门控的额外判定：渲染管线必须是 URP、构建场景必须已登记、工作树必须干净。
