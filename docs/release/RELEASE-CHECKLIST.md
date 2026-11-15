# 发布与回滚清单（单机游戏的「发布」＝出包分发）

> 归属：发布侧（路远行）。本文件是发布动作的操作清单与判据来源。
> 版本号与提交规范见 [`VERSIONING.md`](./VERSIONING.md)，机型档位见 [`BASELINES.md`](./BASELINES.md)。

---

## 0. 一句话流程

```
改 VERSION → 提 CHANGELOG → 出包（build-player.sh）→ 校验（verify-artifact.sh）
→ 写补丁说明 → 归档（包 + manifest）→ 分发 → 出问题按 manifest 回滚
```

---

## 1. 出包前检查清单

> 每一项都有机器可判的判据；没有「人工确认」项。
> 打勾的意思是「该项的判据已被脚本或可复现的命令验证过」。

### 1.1 版本与来源

| # | 检查项 | 判据 | 怎么查 |
|---|---|---|---|
| 1 | `VERSION` 已更新 | 文件内容符合 `MAJOR.MINOR.PATCH[-预发布]` | `cat VERSION` |
| 2 | `CHANGELOG.md` 有本次版本条目 | 顶部存在 `## [<VERSION>]` 段 | `head -40 CHANGELOG.md` |
| 3 | 工作树干净 | `git status --porcelain` 输出为空 | `git status --porcelain` |
| 4 | 代码已入库 | `git rev-parse HEAD` 与远端一致 | `git log -1 --format=%H` |
| 5 | 合并冲突标记已清 | 仓库内无 `<<<<<<<` / `>>>>>>>` | `git grep -n '<<<<<<<' -- . \| wc -l` 为 0 |

### 1.2 自动化自证

| # | 检查项 | 判据 | 命令 |
|---|---|---|---|
| 6 | 编译 | 退出码 0，日志无 `error CS` | `bash Tools/ci/check-compile.sh` |
| 7 | 配表结构 | 退出码 0 | `bash Tools/ci/check-data.sh` |
| 8 | 养成数据完整性 | 日志含 `ProgressionAudit: tables=20 orphans=0 derivedOverwritten=100%` | `bash Tools/ci/check-progression.sh` |
| 9 | EditMode 测试 | `results.xml` 中 `failed="0"` 且 `passed>0` | `bash Tools/ci/check-tests.sh` |
| 10 | PlayMode 联动 | `results-playmode.xml` 中 `failed="0"` 且 `passed>0` | `bash Tools/ci/check-tests-playmode.sh` |
| 11 | 资产 Gate | 无 `[LINT] FAIL`，退出码 0 | `bash Tools/build/lint-assets.sh --tier low` |
| 12 | 存档往返与篡改检测 | EditMode 全绿（`Flow.Tests.SaveRoundTripTests` / `TamperDetectTests`） | 随第 9 项 |

> 上述脚本属于工程线（`Tools/ci/**`），发布侧只调用、不修改。

### 1.2b 档位参数落地核对

> 字段映射表见 [`BUILD-PROFILE.md`](./BUILD-PROFILE.md)。这一组检查防的是「档位参数名存实亡」。

| # | 检查项 | 判据 |
|---|---|---|
| 11b | 档位参数已落到 Unity | 出包日志有 `[BUILD] info 脚本后端` 与 `[BUILD] info 质量档` 两行 |
| 11c | 没有静默漏项 | 出包日志里每条 `[BUILD] warn 跳过不兼容参数` 都能在 BUILD-PROFILE §6 找到对应说明；出现**未登记**的跳过项即判红 |
| 11d | 运行期引导项已实现 | `BUILD-PROFILE.md` §5 的 8 条运行期引导清单，帧率上限与相机后期两项在当前档位机上可验证 |
| 11e | 基线与包一致 | `build-manifest.json` 的 `build.tier` = 出包档位，`build.baselineSha256` = 当前基线文件的 sha256 |

### 1.3 构建

| # | 检查项 | 判据 | 命令 |
|---|---|---|---|
| 13 | 出包成功 | 退出码 0 | `bash Tools/build/build-player.sh --tier low --backend il2cpp --target win64` |
| 14 | 产物存在 | `EdgeChronicles.exe` 与 `EdgeChronicles_Data/` 同时存在 | `ls <产物目录>` |
| 15 | manifest 已生成 | `build-manifest.json` 存在且含 `payload.sha256` | `python3 -c "import json;print(json.load(open('<产物目录>/build-manifest.json'))['payload']['sha256'])"` |
| 16 | 产物自校验 | 退出码 0 | `bash Tools/build/verify-artifact.sh <产物目录>` |
| 17 | 包体在档位上限内 | `payload.sizeMB ≤ limits.buildSizeMB` | 对照 `Tools/build/baselines/<档位>.json` |
| 18 | 构建后工作树仍干净 | `git status --porcelain` 与出包前一致 | `git status --porcelain` |

> 第 18 项特别重要：出包会临时写 `PlayerSettings.bundleVersion` 与质量档参数，
> 脚本会在构建后还原；还原失败会明确告警，不会静默留下污染。

### 1.4 性能（低档是验收底线）

| # | 检查项 | 判据 |
|---|---|---|
| 19 | 低档帧率 | 1080p 下 ≥ 30fps，p95 帧时 ≤ 33.4ms |
| 20 | 长帧比例 | 单帧 > 50ms 的比例 ≤ 1% |
| 21 | 档位参数落地 | `build-manifest.json` 的 `build.tier` = 出包档位，`build.baselineSha256` 与当前基线文件一致 |

> 性能采样的场景与时长见 [`BASELINES.md`](./BASELINES.md) 的「验收方法」。
> 帧率上限需要运行期引导脚本生效，该脚本归工程线；构建期已关闭 vSync。

### 1.6 CI 侧

| # | 检查项 | 判据 | 命令 |
|---|---|---|---|
| 22 | CI 预检 | 退出码 0 | `bash Tools/build/ci-unity-preflight.sh` |
| 23 | 仓库变量与 secret 已设 | `UNITY_PATH` 变量存在、`UNITY_LICENSE` secret 存在 | 仓库 → Settings → Secrets and variables → Actions |

> 搭建步骤见 [`CI-SETUP.md`](./CI-SETUP.md)。
> **不配 CI 也能守门**：上面 1.1–1.5 的命令在本机逐条跑一遍即可，CI 只是把它们搬到另一台机器上。

---

## 2. 版本归档

每次出包产出一个归档单元，三件套缺一不可：

```
<版本号>/
  player/            玩家包本体（EdgeChronicles.exe + EdgeChronicles_Data/ + ...）
  build-manifest.json   版本、commit、Unity 版本、档位、逐文件 sha256
  patch-notes.md       补丁说明（玩家语言）
```

归档规则：

| 项 | 规则 |
|---|---|
| 命名 | `releases/<版本号>/`，与 `VERSION` 完全一致 |
| 位置 | `Build/` 目录不入库（已在 `.gitignore`）；归档件放发布侧指定的存储位置 |
| manifest | **随包分发**：玩家拿到包就能自己校验，也能反查版本 |
| 校验和 | manifest 里的 `payload.sha256` 是整棵产物树的根校验和 |
| 保留 | 每个已发布版本的归档件**不删**，用于回滚与问题复现 |

---

## 3. 回滚步骤

回滚 = 让玩家手上的包回到「已知可用」的那一版。

### 3.1 触发回滚的条件

| 现象 | 判定依据 |
|---|---|
| 玩家无法启动 / 启动即崩 | 用对应版本的 `build-manifest.json` 复现：在同版本包上跑一遍 |
| 存档读不出来 | 存档 `save_version` 高于旧版本能读的版本（见 §5） |
| 帧率不达标 | 低档实测低于 30fps |
| 关键功能缺失 | 对照 `CHANGELOG.md` 与补丁说明 |

### 3.2 回滚动作

1. **定位「上一版」**：从 `build-manifest.json` 取 `version.full`，在归档区找到该版本的归档件。
2. **校验归档件**：
   ```bash
   bash Tools/build/verify-artifact.sh <归档件目录>
   ```
   退出码 0 才说明归档件没被损坏（逐文件 sha256 + 根校验和都对得上）。
3. **换包**：把上一版的 `player/` 覆盖到玩家安装目录（或让玩家重新下载上一版整包）。
4. **通知**：在补丁渠道发一条「已回滚到 <版本号>」的公告，写明原因与新版本号。
5. **归档事故记录**：把现象、判定依据、回滚动作写进 `CHANGELOG.md` 的对应版本段。

### 3.3 回滚时最容易出错的三点

| 点 | 说明 |
|---|---|
| 别只换 exe | `EdgeChronicles_Data/` 必须一起换。数据目录与 exe 版本不匹配会直接启动失败 |
| 别用「重装覆盖」当回滚 | 覆盖安装可能保留旧存档。回滚要显式告知玩家存档处理方式（见 §5） |
| 回滚后校验和要对 | 回滚后的包，其 `build-manifest.json` 必须仍是**上一版**的那份，不要用当前版本的 manifest 覆盖它 |

---

## 4. 补丁说明模板（玩家语言）

> 原则：写玩家能懂的话，不写内部术语；先说影响，再说原因，最后给动作。
> 涉及存档、存档兼容、性能退化的条目**必须**出现在说明里。

```markdown
# 《拾锋录》<版本号> 更新说明

<一两句说明这次更新是什么。玩家最关心的是"我该做什么"。>

## 这次更新了什么

- **<玩家视角的改动名>**：<一句话说清玩家能感知到的变化>
  例：新增「破势」派生效果，破罩类技能命中后会在原地留下一次余波。

- **<改动名>**：<说明>

## 修了什么问题

- **<问题现象>**：<原因一句话>。现在<修好的表现>。
  例：Boss 丛影的第三阶段偶发不结束。原因是一个护盾判定漏了一帧。现在已修。

## 存档与兼容性

- <不改存档结构时>：本次更新**不影响**已有存档，你的养成进度会完整保留。
- <改了存档结构时>：本次更新新增了 <字段>，旧存档会自动补齐默认值，无需操作。
- <存档版本不兼容时>：如果你从更高版本回退过，请注意旧版本无法读取更新的存档。

## 性能

- 最低配置（集显轻薄本）目标仍是 1080p / 30 帧。
  <若有变化>：本次更新在低配机上关闭了 <效果> 以保证帧率。

## 已知问题

- <问题>：<现状与临时应对>。

---

<版本号> · <日期>
```

### 4.1 补丁说明的禁止项

| 禁止 | 原因 |
|---|---|
| 写内部模块名、类名、字段名 | 玩家看不懂，也无法据此反馈 |
| 用「优化了若干问题」概括 | 玩家无法判断自己遇到的问题是否被修了 |
| 隐瞒存档不兼容 | 玩家读不出档时会以为游戏坏了，信任损耗极大 |
| 承诺未落地的功能 | 下一版必须兑现，否则版本号失去意义 |

---

## 5. 存档兼容性注意事项

存档结构与 `save_version` 策略见 `docs/impl/IMPL-v3.0-EL-03-progression.md` §4。发布侧需要盯住的是下面几条：

| 情形 | 旧版本读新存档 | 新版本读旧存档 | 发布动作 |
|---|---|---|---|
| 本次未改存档结构 | 正常 | 正常 | 补丁说明写「不影响已有存档」 |
| 本次**新增字段** | 正常 | 读旧档 → 走 `SaveMigrator` 补默认值 | 补丁说明写「旧存档自动补齐，无需操作」；确认 `save_version` 已 +1 且迁移链已补 |
| 本次**删除/改语义** | 正常 | 拒绝读取（`file.save_version > 3`） | 补丁说明必须写明「不兼容旧存档」；发版前评估是否要改走新档 |
| 从高版本回滚到低版本 | 旧版本**拒绝读取**新存档 | — | 回滚公告必须写「请先备份存档目录，必要时删除旧档重开」 |

### 5.1 存档目录位置（Windows）

```
%USERPROFILE%\AppData\LocalLow\<CompanyName>\<ProductName>\
```

- 当前 `CompanyName` / `ProductName` 取自 `ProjectSettings/ProjectSettings.asset`，出包前核对。
- 回滚前建议让玩家复制这个目录做备份；游戏内的存档备份文件名以 `save_01.sav` / `save_01.bak` 为主。

### 5.2 出包前的存档相关检查

| # | 检查项 | 判据 |
|---|---|---|
| 1 | 本次是否改了存档结构 | 对照 `CHANGELOG.md` 与 `SaveMigrator` 的改动；改了结构则 `save_version` 必须 +1 |
| 2 | 迁移链是否补齐 | `Migrate_vN_to_vN1` 存在且为幂等纯函数；EditMode 有对应往返测试 |
| 3 | 缺字段默认值 | 按 EL-03 §4 的默认值表逐项核对，不允许「读不到就崩」 |
| 4 | 补丁说明 | 涉及存档的版本，补丁说明必须含「存档与兼容性」段 |

---

## 6. 分发

单机游戏的分发方式由发行形态决定，但无论哪种形态，这三件事都要做：

| 项 | 要求 |
|---|---|
| 版本可见 | 玩家在主菜单能读到版本号（与 `VERSION` 一致），并能看到更新说明 |
| 可校验 | `build-manifest.json` 随包分发，玩家可自行校验包的完整性 |
| 可回退 | 每个已发布版本的归档件保留，回滚路径不依赖重新构建 |

分发渠道接入时，把「出包 + 校验 + 归档」三步自动化；渠道自身的审核与上架动作由发行侧执行，不在本清单范围内。
