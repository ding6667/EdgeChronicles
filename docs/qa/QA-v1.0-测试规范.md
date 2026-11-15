# QA-v1.0 · 测试规范

| 项 | 内容 |
|---|---|
| 文档状态 | v1.0 · 生效 |
| 适用范围 | 《拾锋录》全部自动化测试与判据 |
| 配套文档 | [`QA-v1.0-判据目录.md`](./QA-v1.0-判据目录.md) · [`QA-v1.0-人眼项录屏协议.md`](./QA-v1.0-人眼项录屏协议.md) · [`QA-v1.0-Bug分级与回归规范.md`](./QA-v1.0-Bug分级与回归规范.md) |
| 唯一真值源 | 判据正文与阈值以 `Assets/_Project/Tests/Shared/Qa/` 为准 |

---

## 1. 判据的写法（收录门槛）

### 1.1 判据四问（GDD §12.1）

一条判据要进目录，必须能回答：

1. **判据量的是什么？** —— 必须写清**量纲**与**时钟轴**。
2. **判据怎么量化？** —— 必须能落到「脚本判定」或「写死机位的人眼判定」。
3. **判据的假阳性是什么？** —— **必须能写出自己的误判条件**；写不出来就不收。
4. **证伪案例是什么？** —— 必须配一条负向用例（A 类灵敏度 / B 类特异性）。

> 第 3 条是本项目的铁律。判据能抓错（灵敏度）只是及格线，**判据不误杀（特异性）才是**。项目历史上三次翻车注入的都是合法设计，A 类一条都抓不到。

### 1.2 量纲与时钟轴

| 记号 | 含义 |
|---|---|
| `m` / `m/s` / `cm` / `帧` / `ms` / `°` / `比例` / `%` / `个数` / `px` | 量纲 |
| **W（墙钟帧）** | 硬直 / 顿帧 / 位移窗口 / hold / 取消窗口 |
| **C（剪辑帧）** | 姿态 / 收身 / 首尾双稳定 / 受击变体 |
| **LogicTick** | Kernel 逻辑 tick（固定 60Hz） |

铁律：

- **拿不同量纲的量相减或相比，结论永远是假的。**
- **帧号只用于标识对象，禁止参与算术。** 时间量一律用 `T_xxx`（elapsed 时刻，t=0 在命中帧）。
- 量纲与时钟轴写在判据里；**没写 = 不予收录**。

### 1.3 阈值必须可追溯

阈值只能来自三处，且**判据方法体内不许出现裸常量**：

1. `global_tuning` 表（环境相关的量，如 `vfx_graphic_budget`、`drop_pity_threshold`）。
2. `CombatSpec`（测试侧规格卡 = 跨域数字的冻结版本，见 `QaPrimitives.cs`）。
3. 文档中明确写出的推导式（如 `1000 ÷ (2 × hz)`、`0.15 × hitstop_frames ≤ 1`）。

### 1.4 漏挂即废判据，不是调阈值

某条负向用例不再报红时：

- ✅ 正确动作：**重写判据**（它量错了对象 / 缺量纲 / 阈值无来源），并更新该判据的"自身误判条件"。
- ❌ 错误动作：放宽阈值、放宽断言、删用例、改产品实现。

**判据与被测规格冲突时，正确动作是改判据或改规格，不是放宽阈值**；但改哪一边属于规格裁定权，不在 QA。QA 的职责是把它**显式登记**（`KnownSpecConflicts`），不让它以"静默通过"的形式留在生产代码里。

### 1.5 假通过比假失败危险

- 假失败 → 逼你重写判据。
- 假通过 → 只给虚假安心。

因此：**契约缺失、断言缺失、负向用例缺失，一律让它是红的**，不允许 Skip、不允许绿灯、不允许"人工确认"。

---

## 2. 目录结构

```
Assets/_Project/Tests/
├─ Shared/                          EdgeChronicles.Tests.Shared.asmdef（无平台限制，EditMode 与 PlayMode 共用）
│  └─ Qa/
│     ├─ QaPrimitives.cs            量纲 / 时钟轴 / 判据记录结构 / CombatSpec 规格卡 / mechanic_mod 枚举
│     ├─ QaScenario.cs              判据用的最小中立数据模型 + 场景字段
│     ├─ References.cs               参考实现（几何 / 曲线积分 / CRC32 / Boss 规则）+ 规格冲突登记 D-01…D-10
│     ├─ ScenarioFactory.cs         夹具工厂：受击 / 命中 / Boss 规则 / 存档（含非法态构造）
│     ├─ CriterionOracle.cs         判据参考执行器（可脚本判据的判定实现）
│     ├─ CriterionCatalog.cs        22 条走查 + 30 条负向用例（机器可读目录）
│     ├─ HumanEyeProtocols.cs       12 条人眼项录屏协议
│     ├─ ProductionContracts.cs     生产侧契约清单（反射绑定 + 缺失即红）
│     └─ ReferenceImplementations.cs 事件总线 / 取消窗口回放的参考实现
├─ EditMode/                        EdgeChronicles.Tests.EditMode.asmdef
│  ├─ ArchitectureTests.cs          依赖方向守卫（既有）
│  ├─ Smoke/                        SC-01…SC-08 冒烟（判据侧，可立即跑）
│  ├─ Criteria/                     目录完整性 / A 类 / B 类 / 人眼协议自检
│  └─ Contracts/                    生产契约 8 项（工程未实现前必红）
└─ PlayMode/                        EdgeChronicles.Tests.PlayMode.asmdef（新增）
   └─ PlayModeCombatLinkTests.cs    tick 8 阶段顺序与 no_writeback
```

### 2.1 夹具必须能构造非法态

每个夹具成对提供 `…Legal()` 与 `…Illegal()`，非法态与合法态**只差一处**（GDD §12.1 负向用例规则 1：一次只改一处）。
夹具不能构造非法态 → A / B 类用例就跑不起来 → 判据永远得不到证伪。

| 夹具 | 合法态 | 非法态（示例） |
|---|---|---|
| 受击 | 三档全部符合 IMPL-v3.0 §3.1/§3.2 | 重档顿帧 8 帧（越过因果式上限） |
| 命中 | 扇形 / 胶囊 / 矩形各 20 例边界 | 判定帧落在 active 段之外 |
| Boss 规则 | 三份 rule 冷启动 | ForcePhase 指向不存在的阶段 |
| 存档 | 写入 → CRC → 回读 | 篡改 1 字节（20 个固定偏移 + 本体全覆盖扫描） |

### 2.2 参考执行器（oracle）不是第二个真值源

- **判据的正文与阈值只有一份**：`CriterionCatalog` + `CombatSpec`。
- 参考执行器是**判据的证伪装置**：一条判据只有一份实现时，"抓不住错"和"误杀对的"都看不见。
- 生产侧实现同一套判据后，由 `ProductionContractTests` 强制"两份结论一致"；不一致 → 红。
- 参考执行器里出现的每一个阈值都来自 `CombatSpec`；方法体内**不允许裸常量**。

---

## 3. 生产侧契约（给工程侧的落名请求）

测试**不能**引用尚未实现的生产类型（会编译失败 → 整个测试程序集连同判据目录一起挂掉），
也**不能**把"契约缺失"处理成 Skip。因此生产侧的类型名与成员签名在此写死成契约，由反射绑定。

> 下列签名是 **QA 侧提出的请求**，不是设计决定。若工程侧已有更合适的命名，改
> `Assets/_Project/Tests/Shared/Qa/ProductionContracts.cs` 一处即可（契约集中在此，便于对齐）。

| 契约 | 类型全名 | 需要的成员 | 冒烟项 |
|---|---|---|---|
| C-EVENTBUS | `EdgeChronicles.Core.EventBus<TEvent>` **（已实现，测试直接绑定）** | `void Subscribe(Action<TEvent> handler)`<br>`void Publish(in TEvent evt)` | SC-01 事件总线 10k 事件零 GC |
| C-TABLE | `EdgeChronicles.Data.Validation.TableValidator` | `ValidationSummary ValidateAll()`<br>`ValidationSummary { int ErrorCount; int WarningCount; string[] FailedAssertionIds; }` | SC-02 导表器跑 A19–A43，0 error |
| C-SKILL | `EdgeChronicles.Kernel.SkillRuntime` | 冻结签名 `ISkillRuntime { bool TryCast(SkillId); SkillPhase Phase {get;} int PhaseFrame {get;} }` | SC-03 8 技能 × 取消窗口回放 |
| C-CURVE | `EdgeChronicles.Kernel.MovementCurveEvaluator` | `double IntegrateDistance(string curveId)`<br>`double GetDeclaredDistance(string curveId)` | SC-04 曲线积分偏差 ≤5% |
| C-HITGEO | `EdgeChronicles.Kernel.HitGeometry` | `static bool Contains(string shape, float[] shapeParams, float pointX, float pointY)`<br>`shapeParams` 顺序：`0 radius(m) 1 angleDeg(整角) 2 originX 3 originY 4 ax 5 ay 6 bx 7 by 8 halfX 9 halfY 10 yawDeg` | SC-05 命中几何边界各 20 例 |
| C-TUNING | `EdgeChronicles.Kernel.CombatTuning` | `float HitstopTimeScale { get; }`（0.15）<br>`float[] GetKnockbackSpec(string tier)` → `{ hitstun, hold, hitstop, distMin, distMax }` | SC-06 顿帧与受击三档 |
| C-BOSS | `EdgeChronicles.Kernel.BossRuleEvaluator` | `string[] Evaluate(string bossId)`（冷启动求值，顺序稳定） | SC-07 三份 rule 冷启动 hash 相同 |
| C-SAVE | `EdgeChronicles.Flow.SaveCodec` | `byte[] Write(byte[] body)`<br>`bool TryRead(byte[] blob, out byte[] body)`<br>`uint ComputeCrc(byte[] data)` | SC-08 写入→CRC→回读，篡改 1 字节必须失败 |

### 3.1 契约缺失时的行为

| 情况 | 行为 |
|---|---|
| 类型 / 成员不存在 | `ProductionContractTests` 对应测试**红**，报错信息写明"要实现什么" |
| 契约已实现但结论与判据不一致 | **红**，并列出不一致的用例 |
| 契约已实现且一致 | 绿 |

> **不允许把契约缺失改成 Skip。** 那是给未实现的系统发通行证。

### 3.2 需要工程侧确认的口径（会改变测试结论）

| # | 口径问题 | 影响 |
|---|---|---|
| 1 | `hit_shape.angle` 是**整角**还是半角 | 命中判定与 Boss 预警范围会相差一倍（D-01） |
| 2 | `movement_curve.keys` 的量纲（逐帧位移 m？归一化系数？） | A24 的积分算法无唯一解（D-02） |
| 3 | 技传 tier ↔ unlock_type 是否一一对应 | A33 与 §4.5.4 的 16 行数据冲突（D-03） |
| 4 | `allowTags` 能否使用 `dodge`（8 技能 entryTags 里没有它） | A22 会把规范给出的样例判为非法（D-10） |
| 5 | `hold` 的定义是"硬直剩余"还是"hold 段长" | 公式读数差 hold 段（3.3–3.9 帧）（D-04） |
| 6 | 逻辑坐标的**厘米定点**与判据的**米**之间的换算口径 | 击退距离类判据直接相减会差 100 倍（D-11） |
| 7 | `hit_shape.frame` 归属 C 轴还是逻辑帧 | 与 active 段比较属跨轴相减（A20 可能永远报绿）（D-12） |
| 8 | `movement_curve.keys` 是归一化速度还是归一化位移占比 | A24 没有唯一算法（D-13） |

> 这 5 条已登记在 `KnownSpecConflicts`。**在裁定前，相关负向用例只验证"判据能抓错"，不作为"实现正确"的最终结论。**

---

## 4. 当前状态与缺口

### 4.1 已完成

| 交付 | 状态 |
|---|---|
| 22 条走查判据目录（含量纲 / 时钟轴 / 误判条件 / 出处） | ✅ |
| 30 条负向用例（24 A 类 + 6 B 类）目录 | ✅ |
| 判据参考执行器（可脚本判据全部可判定） | ✅ |
| A 类 24 条全部通过 Python 镜像验证（注入 → 目标判据报红，无额外连带） | ✅ |
| B 类 6 条全部通过 Python 镜像验证（注入合法变体 → 判据通过，场景整体仍合法） | ✅ |
| 12 条人眼项录屏协议（机位 / 距离 / FOV / 慢放 / 判定动作写死） | ✅ |
| 8 项冒烟测试的**判据侧**实现（可立即运行） | ✅ |
| 生产契约 8 项（工程未实现前必红） | ✅ |
| Bug 分级与回归规范 | ✅ |

### 4.2 未完成 / 阻塞

| # | 项 | 原因 | 解锁条件 |
|---|---|---|---|
| 1 | 22 条走查 / 30 条负向的**权威编号** | GDD §10.1 / §13 引用了它们但无正文定义；§13 的计数自相矛盾（22+24+6=52≠30）（D-06） | 主理人 / 策划出具正本后做一次编号映射 |
| 2 | 生产契约 8 项转绿 | 工程侧尚未实现 `Core` / `Data` / `Kernel` / `Flow` 的对应类型 | 工程侧按 §3 落名 |
| 3 | PlayMode 战斗联动断言（tick 8 阶段顺序、no_writeback） | 战斗运行时未实现 | 生产契约 C-SKILL 落地后扩展 |
| 4 | 5 条口径歧义（D-01 / D-02 / D-03 / D-04 / D-10）的最终判定 | 属规格裁定权 | 主理人 / 策划裁定后，更新 `CombatSpec` 与夹具基线 |
| 5 | 人眼项实测结论（HP-01…HP-12） | 需要真实录屏与盲测人力 | Gate 时由 QA 组织执行 |
| 6 | Unity 版本基线不一致 | 文档锁定 6000.0.23f1，工程实际 2022.3.62f3c1 + URP 14（D-07） | 确认以哪个为准；影响 CI 脚本与 §9.6 视觉通道断言的期望值 |

### 4.3 明确不做的事

- **不替工程侧实现生产代码**（`Assets/_Project/Scripts/**` 与 `Assets/_Project/Editor/**` 不属于 QA 路径）。
- **不替策划裁定规格冲突**——只登记、只提供证据。
- **不把"人工确认"作为通过判据**——人眼项必须带协议编号与录屏记录。
- **不为了让测试变绿而改产品实现**——见 Bug 分级规范的禁止事项清单。

---

## 5. CI 入口

| 脚本 | 覆盖 |
|---|---|
| `Tools/ci/check-tests.sh` | EditMode 全量（L0）——含架构守卫、8 项冒烟（判据侧）、目录完整性、24 A + 6 B 负向用例 |
| `Tools/ci/check-tests-playmode.sh` | PlayMode 全量（L1） |
| `Tools/ci/check-progression.sh` | 养成层数据完整性（**注意**：脚本里的 `-executeMethod` 命名空间与工程不一致，见 D-08） |

> **L2（人眼项）不进 CI**：它需要录屏与盲测人力，由 QA 在 Gate 时组织，结果以带协议编号的录屏记录归档。

---

*文档结束 · QA-v1.0 · 测试规范*
