# IMPL-v2.0 · 《拾锋录》实现方案

> **本文件已被 `IMPL-v3.0.md` 取代。** v3.0 是完整野心版（普攻 + 闪避 + 8 技能 + 八层联动 + 12 套养成 + 打击感规格）。
> 本文件的**架构部分（模块划分 / tick 顺序 / 数据驱动 / Boss 三段式 / 13 表契约 / 并行切分）全部有效**；
> 但**战斗内容与养成层以 v3.0 为准**，本文件第 9 章的 8 技能数据已被 v3.0 第 1–3 章覆盖。
> 单规格只在一处描述：养成系统矩阵、打击感规格表**只在 v3.0 定义**，本文件不再复述。

| 项 | 内容 |
|---|---|
| 文档性质 | **实现方案**（怎么建），非立项文档 |
| 明确不含 | 工期、周数、人日、预算、报价、产能——团队为 AI 员工，不存在这些约束 |
| 判定标准 | 一份合格的实现方案 = **多个 AI 员工能并行开工、互不阻塞、且能自证完成** |
| 上游文档 | `design/gdd/GDD-v1.0.md` 降级为设计参考（美术风格、修为数值、副本主题、治理规则继续有效） |
| 本项目范围 | 1 角色 8 技能 · 3 副本 3 Boss（机制三维度不同）· 6 条修为线 · 2 类小怪 · 3 部位 18 词条 |

---

## 1. 模块划分与依赖方向

依赖方向**单向无环**：`Core → Data → Kernel → Binding → Flow`；`Presentation`、`UI` 为**汇点**（只出不进）；`Tools.Editor` 旁挂只读。

| asmdef | 职责一句话 | 允许依赖 | 禁止依赖 |
|---|---|---|---|
| `EdgeChronicles.Core` | 定点数学、逻辑帧时钟、事件总线、对象池 | 无 | 全部其他 |
| `EdgeChronicles.Data` | 13 张表 POCO、枚举、校验器、快照 DTO | Core | Kernel 及以上 |
| `EdgeChronicles.Kernel` | tick 编排、技能状态机、位移积分、命中、资源、Boss 规则求值 | Core, Data | UI / Flow / Binding / Presentation / UnityEngine.UI / Addressables |
| `EdgeChronicles.Binding` | MonoBehaviour 驱动、Animator 事件桥、**Transform 唯一写入点** | Core, Data, Kernel | UI / Flow |
| `EdgeChronicles.Presentation` | VFX、音频、震屏、顿帧表现、特效池 | Core, Data | Kernel（只订事件） |
| `EdgeChronicles.UI` | HUD、冷却条、Boss 血条 | Core, Data | Kernel / Binding |
| `EdgeChronicles.Flow` | 场景加载、副本流程、存档 JSON + CRC | 以上全部 | — |
| `EdgeChronicles.Tools.Editor` | 曲线编辑、命中盒可视化、导表断言 | Core, Data, Kernel（只读） | Binding / UI / Flow |

### 冻结的 public 签名（第 1 步之后不许改）

```csharp
// EdgeChronicles.Kernel
public readonly struct FrameInput {
  public readonly sbyte MoveX, MoveY;
  public readonly SkillId Pressed;
  public readonly bool Dodge, Lock;
}
public interface ICombatTick   { void Tick(in FrameInput input); }
public interface ISkillRuntime { bool TryCast(SkillId id); SkillPhase Phase { get; } int PhaseFrame { get; } }
public interface ICombatEvents {
  event Action<HitEvent> OnHit;
  event Action<PhaseEvent> OnPhase;
  event Action<ResourceEvent> OnResource;
}
public interface IBossBrain    { void Evaluate(in BossContext ctx, IRuleSink sink); }

// EdgeChronicles.Data
public readonly struct CombatSnapshot { public readonly int Hp, MaxHp, Resource; }
public interface ISnapshotReader { CombatSnapshot Read(); }
```

**三条边界铁律**
1. 战斗内核不引用 UI、不引用场景加载、不引用 Addressables。
2. **Binding 是 Transform 的唯一写入者**——Kernel 只写逻辑坐标 `Vec2`。
3. UI 只读 `CombatSnapshot`，**不订阅 Kernel 事件**（否则 UI 与战斗帧序耦合）。

---

## 2. 战斗 tick 的确定性顺序

一次逻辑帧内固定执行序。**顺序本身是规格**，不能由实现者自行调整。

| # | 阶段 | 读 | 写 | 硬边界 |
|---|---|---|---|---|
| **0** | **养成快照刷新**（v3.0 新增） | 存档态养成数据（仅当 `snapshotDirty`） | `ProgressionSnapshot` / `StatBlock` / `MechanicModSet` / `DerivativeMask` / `SkillTagMod` | **必须先于 #1**——#1 明文规定"不读任何逻辑状态"，快照必须在此之前就位 |
| 1 | 输入采样 | 设备原始输入 | `InputFrame` 环缓冲(8) | 不读任何逻辑状态 |
| 2 | 预输入缓冲 | 环缓冲 | `bufferedCommand`（单槽） | 新命令覆盖旧命令 |
| 3 | 技能状态机推进 | skill 表、当前 Phase、bufferedCommand | Phase、PhaseFrame、cancel 开关 | **不写 Transform** |
| 4 | 位移积分 | movementCurve、Phase、dt | 逻辑坐标 `Vec2` | 只写逻辑坐标 |
| 5 | 命中判定 | hitShapes、逻辑坐标、帧号 | `HitResult` 列表 | **只产结果，不改 HP** |
| 6 | 受击 / 击退 | HitResult、霸体表 | Hp、knockbackVel、hitstopFrames | 不触表现 |
| 7 | 资源结算 | resourceDelta、命中数 | resource、cooldown | 不改 Phase |
| 8 | 表现层派发 | 本帧事件队列 | `ICombatEvents` | **只读，禁止回写** |

表现层若要影响逻辑，只能等下一帧从 #1 以 `FrameInput` 重新进入。跨层写入由 PlayMode 断言 `no_writeback` 拦截。

---

## 3. 技能系统的数据驱动实现

**目标：加第 9 个技能只改数据、不改代码。**

### 3.1 技能字段

| 字段 | 类型 | 取值范围 | 必填 |
|---|---|---|---|
| `id` | string | `sk_*` 全局唯一 | 是 |
| `animClip` | string | 资源键 | 是 |
| `phases` | {startup, active, recovery} u16 帧 | 各 ≥1，合计 ≤90 | 是 |
| `cancelWindows[]` | 见 3.2 | 0–6 条 | 是（可为空数组） |
| `hitShapes[]` | shape / radius / angle / offsetY / frame / damage / knockback / hitstop / multiHit | frame < active；hitstop ∈ [0.05, 0.25] | 是，≥1 |
| `resourceDelta` | {onCast, onHit, onKill} i16 | `onCast ≤ 0` | 是 |
| `coreLink` | {requiresCoreState, writesCoreState, consumeStacks} | `writes` 与 `consumeStacks` **互斥** | 否 |
| `movementCurve` | string | `mc_*` | 是 |
| `vfxId` / `audioId` | string | `vfx_*` / `sfx_*` | 是 |

### 3.2 取消窗口：不做 8×8 逐对表，改「标签对」

技能声明 `entryTags`（我可被谁取消进入），窗口声明 `allowTags`。8×8 收敛为 **24 条 `(sourceTag, allowTag, fromFrame, toFrame)` 记录**——同标签技能共享一条规则，**加技能不再加窗口数量**。

### 3.3 输入缓冲

8 帧环缓冲 + 1 个单槽命令。**消费时机** = 状态机进入允许窗口的那一帧；**丢弃时机** = 超 8 帧 / 窗口关闭 / Phase 进入不可取消段。

### 3.4 一个真实技能样例

```json
{
  "id": "sk_pojun_03", "animClip": "sword_pojun_03", "entryTags": ["heavy"],
  "phases": { "startup": 8, "active": 6, "recovery": 14 },
  "cancelWindows": [
    { "fromPhase": "recovery", "fromFrame": 0, "toFrame": 8,
      "allowTags": ["light", "dodge"], "resourceCost": 0 }
  ],
  "hitShapes": [
    { "shape": "sector", "radius": 2.4, "angle": 120, "offsetY": 1.2,
      "frame": 0, "damage": 34, "knockback": "heavy", "hitstop": 0.15, "multiHit": 0 }
  ],
  "resourceDelta": { "onCast": -30, "onHit": 8, "onKill": 0 },
  "coreLink": { "requiresCoreState": "empowered", "writesCoreState": null, "consumeStacks": 1 },
  "movementCurve": "mc_pojun_03", "vfxId": "vfx_pojun_03", "audioId": "sfx_pojun_03"
}
```

**加第 9 个技能 = 1 行 skill + 0–2 条窗口 + 1 条曲线。** 若需新 shape 或新击退档，属框架缺口——先扩枚举，再加行。

---

## 4. Boss 机制的可组合框架

三段式 `Trigger → Condition[] → Effect[]`，**全为内置枚举，规则表里不出现自由字符串**。

| 段 | 内置类型 |
|---|---|
| Trigger | OnFightStart / OnTimer / OnHpBelow / OnPhaseEnter / OnCastComplete / OnAddSpawned / OnPlayerSkillHit / OnPlayerEnterZone / OnAnchorStateChanged |
| Condition | HpBelow / HpAbove / PhaseEquals / PlayerInZone / AnchorInState / CooldownReady / StackCountGte / RandomPct / AggroCountGte |
| Effect | SpawnHazard / MovePlayer / ModifyTerrain / ForcePhase / ApplyBuff / SpawnAdd / PlayVfx / ShakeCamera / LockZone |

### 4.1 三个 Boss 的规则配置（机制不同，零新增代码）

```json
[ { "boss": "boss_01_shifo", "phase": 2, "rules": [
    { "trigger": "OnAnchorStateChanged", "args": { "anchor": "anchor://puzzle/pillar_02", "state": "Broken" },
      "conditions": [ { "type": "PhaseEquals", "args": { "phase": 2 } } ],
      "effects": [ { "type": "ModifyTerrain", "args": { "zone": "zone_pit_a", "op": "Open" } },
                   { "type": "SpawnHazard", "args": { "hazard": "hz_rock", "count": 3 } } ] } ] },

  { "boss": "boss_02_gumu", "phase": 1, "rules": [
    { "trigger": "OnTimer", "args": { "periodFrames": 240 },
      "conditions": [ { "type": "StackCountGte", "args": { "stack": "brood", "n": 3 } } ],
      "effects": [ { "type": "SpawnAdd", "args": { "enemy": "em_broodling", "count": 2 } },
                   { "type": "MovePlayer", "args": { "mode": "PullTo", "anchor": "anchor://arena/web", "speed": 3.0 } } ] },
    { "trigger": "OnAddSpawned", "args": {}, "conditions": [],
      "effects": [ { "type": "ApplyBuff", "args": { "target": "Self", "buff": "bf_frenzy", "stacks": 1 } } ] } ] },

  { "boss": "boss_03_tieyao", "phase": 3, "rules": [
    { "trigger": "OnPlayerSkillHit", "args": { "tag": "heavy", "count": 5 },
      "conditions": [ { "type": "HpBelow", "args": { "pct": 0.3 } } ],
      "effects": [ { "type": "LockZone", "args": { "zone": "zone_arena", "op": "Close" } },
                   { "type": "ForcePhase", "args": { "to": 4 } },
                   { "type": "ShakeCamera", "args": { "amp": 0.6, "frames": 18 } } ] } ] } ]
```

三者触发源分别是**机关状态 / 定时+召唤 / 玩家技能命中计数**——机制维度不同，共用一套求值器。

### 4.2 场景机关接口

规则里用 URI 寻址，Kernel **不持有场景引用**；Binding 把状态映射到 Transform / Animator。

```csharp
public interface IMechanismAnchor {
  string AnchorId { get; }
  AnchorState State { get; }
  void Activate(in AnchorCommand cmd);
  event Action<string, AnchorState> StateChanged;
}
public interface IAnchorRegistry {
  bool TryGet(string uri, out IMechanismAnchor a);
  IReadOnlyList<string> Query(string tag);
}
```

**禁止 Boss 直接写 MonoBehaviour**——一旦允许，3 个 Boss 就无法共用框架。`boss_rule` 只产出 Effect 指令，由 Binding 执行。

---

## 5. 数据契约（13 张表）

| # | 表 | 主键 | 关键字段 | 派生值（禁手填） | 外键 |
|---|---|---|---|---|---|
| 1 | `global_tuning` | key | value, min, max | — | — |
| 2 | `skill` | skillId | animClip, phases, entryTags, resourceDelta, coreLink, movementCurve, vfxId, audioId | `totalFrames` | movementCurve, coreLink |
| 3 | `cancel_window` | (sourceTag, allowTag, fromFrame) | toFrame, resourceCost | `windowLength` | — |
| 4 | `hit_shape` | shapeId | shape, radius, angle, offsetY, damage, knockbackTier, hitstop, multiHit | `arcLength`, `expectedDps` | — |
| 5 | `movement_curve` | curveId | keys[], distance, clampMode | `integralDistance` | — |
| 6 | `resource` | id | max, regenPerTick, decayPerTick | — | — |
| 7 | `core_link` | stateId | buffId, durationFrames, conditions | — | — |
| 8 | `enemy` | enemyId | hp, poise, aiGraph, dropTable | — | — |
| 9 | `boss` | bossId | hp, phaseCount, arenaAnchor, brainRef | `totalHp` | anchor |
| 10 | `boss_rule` | ruleId | bossId, phase, trigger, conditions[], effects[] | `ruleHash` | boss, anchor, enemy |
| 11 | `anchor` | anchorId | tag, initState, interactable | — | — |
| 12 | `dungeon` | dungeonId | waves[], anchors[], bossId | `enemyCount` | enemy, anchor, boss |
| 13 | `progression` | lineId | tiers[], affixes[] | `expectedPower` | — |

**新增断言 A19–A30（导表阶段失败即阻断构建）**

| # | 断言 |
|---|---|
| A19 | `phases` 各 ≥1 且 `totalFrames ≤ 90` |
| A20 | `hitShape.frame < active` |
| A21 | `fromFrame ≤ toFrame < totalFrames` |
| A22 | `cancelWindow` 的标签必须存在于某 `skill.entryTags` |
| A23 | 每 `skill` ≥1 条 `hitShape` |
| A24 | 曲线积分距离与声明偏差 ≤5% |
| A25 | `coreLink` 的 `writes` 与 `consumeStacks` 互斥 |
| A26 | `trigger`/`condition`/`effect` 必在枚举内 |
| A27 | `ForcePhase` 目标阶段存在 |
| A28 | 所有 `anchor://` 可解析 |
| A29 | 连段模拟不产生负资源 |
| A30 | `hitstop ∈ [0.05, 0.25]` |

原有的 18 条（受击/击退/闪避/脉冲等）继续生效。

> **新增断言时的强制回扫**：每加一条规则或断言，必须同时列出「**受影响的存量条目**」并逐条复核，否则新规则只约束未来。
> 本项目已有实证：我们先把「禁止跨轴相减」写进治理规则，而断言 5 仍在用 `knockback_curve_frames`（W 轴，8/10/12）比 `hitstun_hold_from`（C 轴，13）——**8 ≤ 13 只是碰巧通过**，语义早已不成立。它的危害不是报错，而是**永远报绿**，给「收身开始时位移未归零」这类缺陷发永久通行证。
> 该条已替换为符号式 **`T(位移归零) + 2 ≤ T(收身起始)`**，其中 `T(归零) = hitstop + 6`、`T(收身) = 0.85 × hitstop + 9` → 8+2≤10.7 ／ 10+2≤12.4 ／ 12+2≤14.1。
> 另补顿帧上限断言 **`0.15 × hitstop_frames ≤ 1`**（因果式，不是 `≤ 6`）：三档 2/4/6 是用满边界、gap 2.1 帧贴线，改 8 会立刻挂——这正是上限该起的作用。

---

## 6. 每模块的自证方式

**不允许写"人工确认"**——必须是脚本或断言能判的。

| 模块 | 可执行自证 | 判定阈值 |
|---|---|---|
| Core | EditMode：事件总线 10k 事件 | `GC.Alloc == 0` |
| Data | 导表跑 A19–A30 | 0 error / 0 warning |
| Kernel 技能 | PlayMode：8 技能 × 24 窗口回放脚本 | 窗口命中率 100%，无非法切相 |
| Kernel 位移 | 曲线积分 vs 声明距离 | 偏差 ≤5% |
| Kernel 命中 | 几何单测：扇形/胶囊/矩形边界各 20 例 | 全通过 |
| Kernel 顿帧 | 打点日志 `hitstop=0.15, frames=N` | N 与配置一致 |
| Boss 框架 | 3 份 rule 冷启动，记录 Effect 触发序列 | 与期望序列 hash 相同 |
| Anchor | 状态机单测 Open→Broken→Lock | 无非法迁移 |
| Presentation | 事件订阅计数 vs 派发计数 + asmdef 依赖扫描 | 差值 0 且无反向引用 |
| UI | 快照 DTO 序列化比对 | 字段全等 |
| Flow / 存档 | 写入 → CRC → 回读 | **篡改 1 字节必须失败** |

---

## 7. AI 并行开工切分

| 路 | 模块 | 目录 | 依赖先完成 | 签名冻结点 | 集成顺序 |
|---|---|---|---|---|---|
| **A** | Core + Data + Tools | `Scripts/Core/`, `Scripts/Data/`, `Editor/Import/` | 无 | 第 1 步起冻结 DTO 与全部枚举 | 1 |
| **B** | Kernel 技能 | `Kernel/Skill/`, `Kernel/Hit/`, `Kernel/Move/` | A | 第 1 步后冻结 `ISkillRuntime` | 2 |
| **C** | Kernel Boss + Anchor | `Kernel/Boss/`, `Kernel/Anchor/` | A | 第 2 步后冻结 `boss_rule` schema | 3 |
| **D** | Presentation + UI | `Presentation/`, `UI/` | A（事件 DTO） | 第 2 步后冻结 `ICombatEvents` / `CombatSnapshot` | 4 |
| **S** | Binding + Flow | `Binding/`, `Flow/` | B + C | — | 5（串行） |

**可 4 路并行：A / B / C / D。必须串行：Binding（Transform 唯一出口）与 Flow（总装）**；Flow 之后才允许跑集成断言。

### 7.1 五个会让人做错的歧义点（已裁定）

| # | 歧义 | 裁定 | 理由 |
|---|---|---|---|
| 1 | 帧还是秒 | Kernel 内**全整数帧**（逻辑 60Hz），秒只出现在表现层插值 | 用秒则取消窗口无法逐帧对齐 |
| 2 | 顿帧是否暂停 tick | **不暂停**，只缩放位移积分系数与表现层 | 暂停会让 `PhaseFrame` 停走、取消窗口错位 |
| 3 | 命中判定用不用 Collider | **纯数学判定**（capsule/sector/rect vs 逻辑坐标），物理层仅用于场景遮挡 | 挂 Collider 引入物理不确定性与 GC |
| 4 | Boss 能否直接写 MonoBehaviour | **禁止** | 一旦允许，3 个 Boss 就无法共用框架 |
| 5 | UI 数据来源 | 只读 `CombatSnapshot`，每帧最多一次 | 订阅事件会让 UI 与战斗帧序耦合 |

---

## 8. 资产规格与生成管线

### 8.1 主角动画：16 条

全项目 `rootMotion = false`，剪辑里只保留归一化曲线 `cv_spd`；位移由代码按 `MP_HERO_<NAME>` 曲线资产驱动——**这是让「8 个技能的动作」与「每个技能的位移手感」能分开调的关键**，否则改一个位移就得重做动画。

| 类别 | 条目 | 说明 |
|---|---|---|
| 基础 8 条 | 待机 / 移动 blend / 普攻 1-3 / 闪避 / 受击 / 死亡 | 沿用 v1 规格 |
| 技能 8 条 | 1 核心 + 7 服务 | 见下 |

| 可复用 | 复用自 | 理由 |
|---|---|---|
| 扫云 | 普攻二段 | 只改曲线幅度与特效；玩家靠**特效形状 + 描边状态**区分，不靠姿态 |
| 穿林 | 回锋 | 同上 |

| **禁止复用** | 理由 |
|---|---|
| 闪避 × 任何位移技能 | 闪避要"原地中性收身"（禁前倾迈步），位移技能必须前倾蹬地——共用会同时毁掉两者 |
| 受击 × 死亡 | 受击要求首尾帧稳定、可反向播放；死亡末帧必须落定 |
| 破锋 | 霸体帧号绑在起手上，复用会错位 |
| 归元 × 待机 | 归元必须上半身分层，才能边走边回气 |

### 8.2 特效

8 个技能共用一张 **2048 图集，占 50/64 格**，余 14 格缓冲。Boss 机制特效**另开一张 1024**，不并表——同屏叠 4K 会超出集显显存预算。

### 8.3 三个 Boss：2 套 rig 覆盖，机制不靠模型

| Boss | rig | 机制靠什么承载 | 独占视觉元素 |
|---|---|---|---|
| 铁砧窟「炉喉」 | 重甲 A | 3 座**可打灭的熔炉**（场景机关）+ 橙雾 | 熔渣裂纹地画 + 熔口熄灭 |
| 断碑古刹「碑守」 | 重甲 A（1.15×） | **碎碑投射** + 灰白雾 | 纯顶点色碑纹（0 新资产） |
| 孤锋崖「哑刀」 | 敏捷 B（2.2×） | **断崖风压** + 风切雾层 | 唯一允许的非通用预警形：纵斩地面大扇形 |

**成本原则**：机制差异化主要靠**场景与特效**，不靠新模型。

### 8.4 AI 生成管线

6 步 Gate：概念图 → 低模 → UV → 顶点色 → 导入 → 后处理，**每一步不过不进下一步**。

| 后处理固定参数 | 值 |
|---|---|
| 墨线 | 按背景亮度差推导；铁砧窟倒相为纸白 |
| 色域 | ΔE ≤ 8 |
| 顶点色偏移 | ±10% 以内 |
| 材质槽 | 5 槽枚举（Rock / Wood / Ground / Cloth / Emissive） |
| 变换 | 位置/旋转/缩放归零 |

**入库 6 项自动 Gate**：面数、贴图尺寸与通道、材质槽数量、命名合规、UV 象限、无第三方素材标记。失败自动退回 `_WIP/` 并写 `_fail.json`，**不进人工队列**。

命名：`AN_HERO_SKL_04_F`——**只有 `_F` 能被预制体引用**，`_D`/`_R` 进 `_WIP/` 且不进构建。

**提示词模板**：必含「水墨留白 / 木版硬边 / 低多边形 / 宣纸米白底 / 色块 ≤8」；必排「写实 / PBR / 毛发 / 渐变 / 景深 / 水印」。

---

## 9. 8 技能初始数据（接入 §3 schema）

### 9.1 门派与资源

**门派**：拾锋门（占位名，上线前商标检索）——拾战场断锋、以残器养己身的没落刀门。

**单一资源「锋芒」**：上限 **5 层**；命中按表给层、单次 ≤1 层；**8 秒未获新层则每 2 秒掉 1 层**（这条衰减是逼玩家在 12 秒内做决断的关键，不是装饰）。8 个技能只做两件事：**生成层数 / 保护层数**；核心只沿「层数」一轴放大，**不叠持续时间、不叠范围、不叠段数**——这是「3 局内看懂」的前提。

### 9.2 技能表（8 行）

| 技能（键） | 定位 | entryTags | 前摇 | 判定 | 后摇 | 可取消 | resourceDelta | coreLink | CD |
|---|---|---|---|---|---|---|---|---|---|
| **拾锋式**（右键） | **核心·兑现** | `core` | 16 | 6（落阵） | 22 | **不可** | 消耗全部 0–5 层 | 展「锋域」半径 4m / 8s，每 0.6s 一跳，单跳 = `18 × (1 + 0.45n)` | 14s |
| 迅锋（左键） | 生成 | `light` | 8·10·14 | 3·3·4 | 10·12·18 | 可（第 2 段判定后 4 帧起） | 每段命中 +1 | 锋域内每段 +2 | 无 |
| 裂石（Q） | 生成 | `heavy` | 20 | 8 | 24 | 仅命中可（命中后 6 帧） | 命中 +2 | 锋域内 +3 | 8s |
| 回锋（E） | 位移·生成 | `dash` | 10 | 6 | 16 | 可（第 4 帧起） | 穿身 +1 | 锋域内 +2（伤害为零，以层数补偿） | 9s |
| 缚锋（F） | 控场 | `control` | 12 | 5（3m） | 18 | 仅命中可 | 命中 +1 | 锋域内对被缚目标 +2 | 12s |
| 抱元（R） | 防御 | `guard` | 8 | 12（自身霸体） | 14 | 可（霸体结束帧） | 被击 +1，单次限 2 | 锋域内被击 +2 | 15s |
| 聚锋（1） | 生成·引导 | `channel` | 10 | 引导 45（1.5s，每 15 帧结算） | 12 | 仅命中可（被击退即断） | 每次结算 +1，最多 3 | 锋域内结算间隔 ×0.5 | 20s |
| 破锋（2） | 终结 | `finisher` | 14 | 8 | 20 | **不可** | 消耗 3 层 | 锋域在场：引爆 1.8× 单跳并延长锋域 2s；无锋域：改 +2 层 | 18s |

### 9.3 取消窗口：25 组有效规则（其余放行）

不写 8×8 逐对表，落到 `cancel_window` 表时只需 **16 组 `不可` + 9 组 `仅命中可`**，其他组合默认放行。

| 类型 | 组数 | 具体 |
|---|---|---|
| **不可**（非自身） | **16** | 拾锋式→其余全部 7 组；破锋→除迅锋外 6 组；裂石→抱元 1 组；抱元→拾锋式 1 组；聚锋→裂石 1 组 |
| **仅命中可** | **9** | 裂石→拾锋式/回锋/缚锋/聚锋/破锋 5 组；缚锋→拾锋式/聚锋 2 组；抱元→裂石/破锋 2 组 |
| 其余 | — | 默认「可」 |

**这三处硬直是故意留的，实现时不得当 bug 修掉**：① 拾锋式落阵 22 帧不可取消——**落错位置必须付代价**；② 破锋后摇 20 帧是唯一的贪刀惩罚窗；③ 重击与引导不能立刻转防御，**空挥裂石必定吃一下**。

### 9.4 两条必须被玩家学会的循环

| 链路 | 步骤 | 一轮 | 教学目的 |
|---|---|---|---|
| **新手（≤4 技能）** | 迅锋三段攒 3 层 → 拾锋式落阵 → 域内继续迅锋（每段 +2）→ 破锋引爆 | 9s | 先攒再放，放了还要攒 |
| **熟练（8 技能）** | 缚锋定身 → 聚锋（域内 0.75s 满 3 层）→ 裂石 +3 → 满 5 层落阵 → 域内迅锋补层 → 破锋引爆 +2s → 抱元吃反打 → 回锋穿身 +2 → 二次拾锋式 | 14s（对齐 CD） | 每个键只为让锋域更早、更久、更疼 |

### 9.5 三个副本与三套 Boss 机制

小怪 **2 类**：持刀匪（近战）/ 弩手（远程）——每本只换皮 + 换 1 个行为参数，**不新增动画**。

| 副本 | 主题 | 时长 | 门槛 | 机制一句话 | 复刷价值 |
|---|---|---|---|---|---|
| 落霞渡 | 落日废渡口，潮水未退 | 7–9min | **100** | 潮位改变可站立地面，**Boss 泡水里几乎打不动，必须逼上干滩** | 气海草药、凡品 |
| 铁砧窟 | 废弃矿洞，炉口仍在鼓风 | 10–13min | **900** | 周期性「鼓风」烧掉锋芒与冷却，**必须跑去封炉才能拿回资源** | 寒铁 / 陨铁残片 |
| 孤锋崖 | 高崖剑冢，万刃插地 | 13–16min | **2400** | 三影真假识别，**打错身给 Boss 加护盾，只能用控场技去试** | 秘品 / 传说 |

**三套机制的维度互不重叠，落进同一副框架**

| Boss | 维度 | 阶段机制（触发 → 场面变化 → 玩家必须做的动作 → 失败后果） |
|---|---|---|
| **退潮翁**（落霞渡） | **地形压力** | 潮水 30s 周期 → 洼地进水、移速 −40%、每 2s 掉 3% 生命，Boss 站水减伤 60% 且每次攻击回 2% → 读浮木刻度、退潮窗上干滩、缚锋定身 → 淹死回门、Boss 满血。三阶段潮线不再退满，**永远只剩一块 6s 干滩** |
| **鼓风·铁奴**（铁砧窟） | **资源节奏** | 每 20s 进「鼓风」8s → 锋芒每 1.5s 掉 1 层、技能 CD ×1.5、Boss 得 30% 减伤 → 跑去 3 座炉口按 E 封炉（1.5s 引导、可打断）→ 未封则熔渣喷发 35% 生命 + Boss 回 5% 血。三阶段要求 3 座全封才破盾，破盾 10s 内受伤 +40% |
| **三影·无面客**（孤锋崖） | **目标不确定** | 3 影 1 真 2 假，假身受击不掉血反而给 Boss +1 层「丛影」（每层减伤 +10%，上限 5）→ 唯一判据是**缚锋试探**：定住假身即消散，定住本体触发 12s 破绽（受伤 +50%、不再换位）→ 盲打等于给 Boss 上护盾；丛影满 5 层触发「影噬」40% 生命 |

**共用基座**：场景状态机 + 可交互物件 + 可锁定实体分组——**一个框架、三份配置**，对应 §4 的 `Trigger / Condition / Effect`。

### 9.6 数值分布

| 项 | 值 |
|---|---|
| DPS 占比 | 拾锋式（锋域）**38%** · 迅锋 20% · 破锋 14% · 裂石 11% · 缚锋 5% · 回锋 5% · 抱元 4% · 聚锋 3% |
| 三档门槛 | 100（六线 Lv0–2）→ 900（Lv3–6）→ 2400（Lv7–10） |
| 单周目 | 约 27 次有效副本、10–13 小时 |

**核心占 38% 而不是更高**，是为了不让服务技能沦为“按顺序点一遍”；但也不能再低，否则核心的兑现地位不成立。

### 9.7 三处已按建议定稿（若你否决，只改对应一处）

| # | 项 | 当前决定 | 影响面 |
|---|---|---|---|
| 1 | 门派名 | **拾锋门**（备选「砺雪堂」仅换名，技能表不动） | 只影响文案 |
| 2 | 门槛五档 → 三档 | **100 / 900 / 2400**，与 §4.3 修为表按 Lv0–2 / Lv3–6 / Lv7–10 对齐，**单级 EU 与权重未改** | 影响副本表与 UI 显示 |
| 3 | 三 Boss 的退让顺序 | 若机制实现超预期，**先砍落霞渡 Boss → 降级为无 Boss 教学本**（唯二保留铁砧窟与孤锋崖） | 这是唯一可无损回退的砍法 |

---

*文档结束 · IMPL-v2.0 · 不含工期与成本*
