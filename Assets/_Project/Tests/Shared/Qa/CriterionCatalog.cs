using System;
using System.Collections.Generic;

namespace EdgeChronicles.Tests.Qa
{
    /// <summary>
    /// 判据目录。
    ///
    /// 【收录规则（GDD §12.1 判据四问）】
    /// ① 判据量的是什么？—— 必须写清量纲与时钟轴。
    /// ② 判据怎么量化？—— 必须能落到"脚本判定"或"写死机位的人眼判定"。
    /// ③ 判据的假阳性是什么？—— **必须能写出自己的误判条件**，写不出就不收。
    /// ④ 证伪案例是什么？—— 每条判据必须配一条负向用例（A 类灵敏度 / B 类特异性）。
    ///
    /// 【编号说明】
    /// 22 条走查表与 30 条负向用例的**权威编号清单在现有文档中不存在**（见 KnownSpecConflicts D-06），
    /// 因此本目录使用 QA 内部编号：
    ///   WC-xx   走查判据（Walkthrough Criterion）
    ///   NC-Axx  A 类负向用例（灵敏度：注入错误 → 判据必须报错）
    ///   NC-Bxx  B 类负向用例（特异性：注入合法但不同的设计 → 判据必须通过）
    /// 导表断言沿用其既有编号 A19–A43（与 IMPL-v2.0 §5 / EL-03 §2 一致）。
    ///
    /// 【可执行化分布】
    /// 纯脚本 9 / 混合 7 / 必须人眼 6 —— 与 GDD §10.1 W4「9 条纯脚本 / 7 条混合 / 6 条必须人眼」一致，
    /// 可作为本目录重建合理性的旁证（不作为编号正确性的证据）。
    /// </summary>
    public static class CriterionCatalog
    {
        // ─────────────────────────────────────────────────────────────────
        // 编号正本与三层算术
        //
        // 【正本】本文件（CriterionCatalog.cs）就是判据编号的唯一真值源。
        // GDD §10.1 / §12 / §13 与 IMPL §7 一律改为**引用这里的 id**，不再各自编号。
        // 【GDD 已有 ID】设计线裁定"正本编号沿用 GDD 已有 ID"，
        // 因此每条记录额外保留 <see cref="CriterionRecord.CanonicalId"/> 映射列
        // （C1 / C2 / D1a / D1b / D1b-obs / D2 / D4 / D5 / B1 / B2 / B3 / E2 / E3），
        // 供 GDD / IMPL 改成引用时一一对回。
        //   · A2 / A4 在 GDD §10.1 W4 被提到（"A2/A4/C2/D1a 自动判定"），
        //     但 **GDD 里没有它们的正文定义** → 暂留空，等正本补齐后再填。
        //
        // 【三层算术】数字写在代码里，文档只引用：
        //   · 全量判据 70（在册）
        //   ·   ├─ 负向用例 30（24 A 类 + 6 B 类）        ⊆ 全量判据
        //   ·   └─ 正向走查 22（9 脚本 + 7 混合 + 6 人眼） ⊆ 全量判据
        //   · 负向用例 ∩ 正向走查 = ∅（30 + 22 = 52 条用例与走查互不重叠）
        //   · 注：全量数从 69 变 70 是因为 A39 拆成 A39-1 / A39-2（语义变了，判据条数 +1）。
        // ─────────────────────────────────────────────────────────────────

        // ─────────────────────────────────────────────────────────────────
        // 22 条走查判据
        // ─────────────────────────────────────────────────────────────────
        public static readonly List<CriterionRecord> Walkthrough = new List<CriterionRecord>
        {
            new CriterionRecord
            {
                Id = "WC-01",
                Title = "闪避位移曲线：前 2 帧为蹬地窗口，位移 ≤5%",
                Statement = "闪避剪辑的前 2 帧为蹬地窗口，该窗口内产生的位移占总位移的比例 ≤5%（比例，W 轴，逻辑 60Hz）。整条剪辑重心垂直位移 ≤10cm；末帧与待机首帧姿势差 ≤5°。",
                Unit = QaUnit.Percent, Axis = QaClockAxis.WallClock,
                Executability = CriterionExecutability.Mixed,
                ExecutableAs = "自动：SC-01（曲线前 2 帧积分 / 总积分）；人眼：HP-01（末帧-待机首帧姿势差，固定机位与慢放）",
                FalsePositiveCondition = "判据只量前 2 帧的位移占比。若实现把蹬地窗口定义为「前 2 帧内速度最大的一段」而不是「前 2 个逻辑帧」，同一段曲线会被判为违规——合法的「快速起步」设计会被误杀。此外总位移的取数口径（逻辑位移 vs 动画根位移）不一致时，占比会整体偏移。",
                SourceRef = "GDD-v1.0.md §7.3 动画硬指标 ①",
                CanonicalId = "C1",
                RelatedAssertionIds = new List<string> { CriterionOracle.A24 },
                LinkedOracleIds = new List<string> { CriterionOracle.FDodgePush },
                Veto = false,
            },
            new CriterionRecord
            {
                Id = "WC-02",
                Title = "闪避无敌帧：窗口存在且长度合法",
                Statement = "闪避的无敌窗口长度（invuln_end − invuln_start）落在 1–14 帧，且 invuln_start ≥1，invuln_end ≤ 位移段帧数（帧，W 轴）。窗口生效期间敌方攻击判定必须落空。闪避是**门派共有动作**（无 entryTags 行），其标签 dodge 只出现在取消窗口的 allowTag 侧。",
                Unit = QaUnit.Frame, Axis = QaClockAxis.WallClock,
                Executability = CriterionExecutability.Mixed,
                ExecutableAs = "自动：SC-02（窗口长度与位置，判据 F-DODGE-INVULN）+ PlayMode 无敌内不受伤；人眼：HP-02（对哑刀纵斩 10/10 成功）",
                FalsePositiveCondition = "若某实现把「无敌」实现为「技能释放瞬间的一次免疫判定」而不是帧窗口，窗口长度会读成 0–1 帧，本判据会把它判为违规——而这可能是一个合法的防御性设计。反之，若实现允许多段无敌窗口，本判据只取第一段，漏判后面的窗口。",
                SourceRef = "GDD-v1.0.md §7.2 / §7.3；IMPL-v2.0.md §9.4 导表硬断言 #2 #3",
                CanonicalId = "C2",
                LinkedOracleIds = new List<string> { CriterionOracle.FDodgeInvuln },
                Veto = true
            },
            new CriterionRecord
            {
                Id = "WC-03",
                Title = "命中 → 受击起始 ≤3 帧",
                Statement = "从命中判定成立的那一帧（t=0）到受击动画起始的那一帧，间隔 ≤3 帧（帧，W 轴，60Hz）。",
                Unit = QaUnit.Frame, Axis = QaClockAxis.WallClock,
                Executability = CriterionExecutability.AutoScript,
                ExecutableAs = "SC-03（时间戳差，逻辑时钟）",
                FalsePositiveCondition = "若受击动画由「受击事件」触发而非「命中事件」触发，两者之间会插入一层表现层排队，合法实现可能被判成 4–5 帧。判据必须按事件时间戳而非帧号相减，且两个时间戳必须同源（见 E3 同源问题的同一条纪律）。",
                SourceRef = "GDD-v1.0.md §1.2 P1；§10.1 W4（一票否决）",
                CanonicalId = "D1a",
                LinkedOracleIds = new List<string> { CriterionOracle.FHitFeedback },
                Veto = true
            },
            new CriterionRecord
            {
                Id = "WC-04",
                Title = "受击开始 12 帧内可判「被打中 + 这一下多重」",
                Statement = "受击动画开始后的 12 帧（帧，W 轴）内，玩家必须能确认两件事：被击中了；这一下的力度档位（轻/中/重）。力度档位由击退距离区分（轻 0.3–0.6m / 中 0.8–1.2m / 重 1.2–1.5m）。",
                Unit = QaUnit.Frame, Axis = QaClockAxis.WallClock,
                Executability = CriterionExecutability.Mixed,
                ExecutableAs = "自动：SC-04（三档击退距离落区间，判据 F-KB-TIER）；人眼：HP-03（盲测：不给数值，测试者判断轻重）",
                FalsePositiveCondition = "盲测里若测试者能从受击动画的力度感本身判断档位，则本判据测的是「动画表现」而非「12 帧内可读」；必须固定只播前 12 帧且不显示任何数值，否则结论不可复现。",
                SourceRef = "GDD-v1.0.md §10.1 W4",
                CanonicalId = "D1b",
                LinkedOracleIds = new List<string> { CriterionOracle.FKbTier },
                Veto = false,
            },
            new CriterionRecord
            {
                Id = "WC-05",
                Title = "4 正轴向受击动画方向可读（观察项）",
                Statement = "正/后/左/右 4 个正轴向上，受击动画能表达受击方向，5/5 通过。斜向不计入（无对应动画变体）。本项为**观察项，不进验收**。",
                Unit = QaUnit.Count, Axis = QaClockAxis.Clip,
                Executability = CriterionExecutability.HumanEye,
                ExecutableAs = "HP-04（4 轴 × 5 次，固定 5m 正侧面机位）",
                FalsePositiveCondition = "测试者若事先被告知方向，会按预期猜对；必须用打乱顺序的 20 段编号录屏，且不透露目标方向。斜向必须排除（45° 复合击退会被无动画变体直接判死，属判据量错对象）。",
                SourceRef = "GDD-v1.0.md §10.1 W4（观察项）；§13 附注",
                CanonicalId = "D1b-obs",
                NegativeCaseWaiver = "观察项，不进验收：GDD 10.1 明确 4 正轴向方向可读只是观察项（斜向无动画变体，判据量错对象会误杀）。",
                Veto = false,
            },
            new CriterionRecord
            {
                Id = "WC-06",
                Title = "击退方向与伤害来源一致",
                Statement = "击退方向必须与伤害来源方向相反，8 个方向（含 4 斜向）全部可判（方向向量，逻辑平面）。",
                Unit = QaUnit.Meter, Axis = QaClockAxis.WallClock,
                Executability = CriterionExecutability.AutoScript,
                ExecutableAs = "SC-05（击退向量 vs 伤害来源向量，取 8 向）",
                FalsePositiveCondition = "取「8 向」时必须把斜向映射到最近的 45° 扇区；若攻击方与受击方存在高度差（俯视/仰角），向量在同一平面内的投影会偏，斜向用例可能落在错误的扇区边界上。判据必须先投影到逻辑平面再取扇区。",
                SourceRef = "GDD-v1.0.md §7.3 硬判据；§10.1 W4（一票否决）",
                CanonicalId = "D2",
                LinkedOracleIds = new List<string> { CriterionOracle.FKbDirection },
                Veto = true
            },
            new CriterionRecord
            {
                Id = "WC-07",
                Title = "击退距离落在三档区间",
                Statement = "轻档 0.3–0.6m、中档 0.8–1.2m、重档 1.2–1.5m（米，W 轴）。区间含端点。取数口径：逻辑坐标统一用米（定点精度 1/1000 m），判据直接按米读数，不写换算式。",
                Unit = QaUnit.Meter, Axis = QaClockAxis.WallClock,
                Executability = CriterionExecutability.AutoScript,
                ExecutableAs = "SC-06（判据 F-KB-TIER）",
                FalsePositiveCondition = "距离若按「位移曲线的积分」取，会包含顿帧期间被 time_scale 缩放的那一段，与「视觉上被顶开的距离」不是同一个量；两种取法在同一实现上能差 1–2 倍。判据必须声明取数口径（本判据取逻辑位移积分，含顿帧缩放）。",
                SourceRef = "GDD-v1.0.md §7.3；IMPL-v3.0.md §3.2",
                CanonicalId = "D2",
                LinkedOracleIds = new List<string> { CriterionOracle.FKbTier },
                Veto = false,
            },
            new CriterionRecord
            {
                Id = "WC-08",
                Title = "击退不得推入墙内 / 推落平台",
                Statement = "受击者的击退位移不得穿入场景碰撞体，也不得越过可站立区域的边界（米，W 轴）。该判据优先级高于方向与距离——被推入墙内时距离可以缩短。",
                Unit = QaUnit.Meter, Axis = QaClockAxis.WallClock,
                Executability = CriterionExecutability.AutoScript,
                ExecutableAs = "SC-07（场景查询，8 向 × 3 档）",
                FalsePositiveCondition = "场景碰撞体是运行时生成还是关卡预置会改变查询结果；若判据在空场景（无碰撞体）下运行则永远通过。判据必须绑定到「真实关卡 + 预置碰撞体」，否则等于没测。",
                SourceRef = "GDD-v1.0.md §7.3 D2 优先级",
                CanonicalId = "D2",
                LinkedOracleIds = new List<string> { CriterionOracle.FKbWall },
                Veto = false,
            },
            new CriterionRecord
            {
                Id = "WC-09",
                Title = "受击三档的硬直 / hold / 顿帧",
                Statement = "轻 / 中 / 重三档：硬直 18 / 20 / 22 帧，顿帧 2 / 4 / 6 帧（帧，W 轴）。hold 的**设计意图区间**是 [2,4] 帧（三档实测落在 3.3–3.9 帧；区间与实况两句都要写，否则会误以为能取到端点），按**同轴**公式重算：clip_W = anim_clip_length × 60 ÷ anim_fps，hold = hitstun_frames − clip_W − hitstop_frames × (1 − hitstop_time_scale)；换算后 hold < 0 即报错（不取 0）。顿帧上限以因果式判定：hitstop_time_scale × hitstop_frames ≤ 1。",
                Unit = QaUnit.Frame, Axis = QaClockAxis.WallClock,
                Executability = CriterionExecutability.AutoScript,
                ExecutableAs = "SC-08（判据 F-HOLD + F-HITSTOP-CAUSAL）",
                FalsePositiveCondition = "硬直 18/20/22 是**名义值**还是「含 hold 的总时长」在文档中未统一；两种解释下同一实现的读数差 hold 段（3.3–3.9 帧）。判据必须声明读数口径（本判据取「从受击起始到可再次行动的总帧数」）。",
                SourceRef = "GDD-v1.0.md §7.3；IMPL-v3.0.md §3.1；GDD-v1.0.md §9.4 导表硬断言 #11 #12",
                CanonicalId = "D2",
                RelatedAssertionIds = new List<string> { CriterionOracle.A30 },
                LinkedOracleIds = new List<string> { CriterionOracle.FHold, CriterionOracle.FHitstopCausal },
                Veto = false,
            },
            new CriterionRecord
            {
                Id = "WC-10",
                Title = "顿帧语义：卡住且被顶开",
                Statement = "hitstop_time_scale = 0.15（无量纲倍率，作用在表现层时间）；击退起于命中帧并贯穿整个顿帧；顿帧把动画速度降到 15%，但**不冻结位移曲线**；顿帧不暂停 tick（PhaseFrame 继续走）。收身起始进度是**参数**（受击剪辑元数据注入，默认 C9），后段窗口帧数读击退曲线的派生量——两者都不是写死的常数。",
                Unit = QaUnit.Ratio, Axis = QaClockAxis.WallClock,
                Executability = CriterionExecutability.Mixed,
                ExecutableAs = "自动：SC-09（time_scale 采样 + PhaseFrame 连续性）；人眼：HP-05（固定机位慢放，判「卡住 + 被顶开」同时成立）",
                FalsePositiveCondition = "若实现把顿帧实现为 time_scale=0（完全冻结），本判据的数值部分仍能通过（0 ≤ 0.15），必须由人眼项补上「被顶开」这一半；而人眼项又依赖机位与慢放倍数写死，否则两人判定不一致。",
                SourceRef = "GDD-v1.0.md §7.3 顿帧语义（D4）；IMPL-v2.0.md §7.1 歧义点 2",
                CanonicalId = "D4",
                RelatedAssertionIds = new List<string> { CriterionOracle.A30 },
                LinkedOracleIds = new List<string> { CriterionOracle.FHitstopCausal },
                Veto = false,
            },
            new CriterionRecord
            {
                Id = "WC-11",
                Title = "霸体与无敌必须结构不同（数据层）",
                Statement = "玩家必须能在不看任何数值的前提下分辨「霸体」与「无敌」两种状态：霸体 = 赤色单边实描 4px，无敌 = 纸白双边中空 2px+2px。两者的描边结构必须不同（数据层由导表断言 F-VFX-STRUCTURE 保证；px 差 ≥2px 只记录，不参与判定），可读性由 B2 关键组判定。",
                Unit = QaUnit.Pixel, Axis = QaClockAxis.None,
                Executability = CriterionExecutability.HumanEye,
                ExecutableAs = "数据前提由 SC-10 自动保证（判据 F-VFX-STRUCTURE）；可读性由 HP-07 的 B2 关键组判定",
                FalsePositiveCondition = "本判据只比「结构」不不比「颜色」：如果实现用同一套描边结构 + 不同颜色来区分霸体与无敌，本判据会判为违规——而那正是 IMPL §3.4 明文禁止的做法（不得用颜色通道），因此该误杀是设计意图内的。真正的风险在反方向：若描边结构参数存在但运行时未生效，数据层通过而画面不变——需配合 WC-13 的盲测兜住。",
                SourceRef = "IMPL-v3.0.md §3.4；GDD-v1.0.md §9.4 导表硬断言 #6",
                CanonicalId = "—",
                LinkedOracleIds = new List<string> { CriterionOracle.FVfxStructure },
                Veto = false,
                AxisNotApplicable = true,
            },
            new CriterionRecord
            {
                Id = "WC-12",
                Title = "B1 仅色差组（定位通道用）",
                Statement = "仅颜色不同的两套描边，测试者仍能分辨（比例，10 段盲测）。本组是**定位通道**：结果不达标不改方案，只用于说明「哪条通道在起作用」。",
                Unit = QaUnit.Ratio, Axis = QaClockAxis.None,
                Executability = CriterionExecutability.HumanEye,
                ExecutableAs = "HP-06（5 人 × 10 张，仅色差组）",
                FalsePositiveCondition = "色差组若在显示器色准偏差的环境下录制，结论会随机器变化；必须记录显示器型号与色彩配置，且结论只用于定位、不作为 Gate。",
                SourceRef = "GDD-v1.0.md §8.5 / §10.1",
                CanonicalId = "B1",
                NegativeCaseWaiver = "定位通道（B1 仅色差组）：按 GDD 10.1，其结果不达标不改方案，因此不配 Gate 级负向用例；采集规范由 NC-A12 的对照组协议覆盖。",
                Veto = false,
                AxisNotApplicable = true,
            },
            new CriterionRecord
            {
                Id = "WC-13",
                Title = "B2 仅形态组（关键组）",
                Statement = "仅描边形态不同、颜色完全相同的两套描边，5 人 × 10 张打乱图序，每人 ≥9/10，且 ≥4 人达标；20 张图必须包含铁砧窟橙底 ≥2 张与雾隐竹海浓雾 ≥2 张（比例，无量纲）。",
                Unit = QaUnit.Ratio, Axis = QaClockAxis.None,
                Executability = CriterionExecutability.HumanEye,
                ExecutableAs = "HP-07（关键组盲测；对照组 D5 先跑）",
                FalsePositiveCondition = "对照组若只消除颜色通道而保留描边形态与入窗时机，测试者会改用时序线索猜对，「通过」是伪的。必须先跑 D5 对照组并同时消除颜色 / 描边形态 / 入窗时机三个通道，否则本判据无效。",
                SourceRef = "GDD-v1.0.md §8.5 / §10.1 / §12.1 规则 3",
                CanonicalId = "B2",
                LinkedOracleIds = new List<string> { CriterionOracle.FD5Control },
                Veto = true
            },
            new CriterionRecord
            {
                Id = "WC-14",
                Title = "B3 相机跟随（观察项）",
                Statement = "相机跟随的微推与阻尼手感（观察项计数，无量纲）。本项**不毙 Gate**，不达标转后续修复处理。",
                Unit = QaUnit.Count, Axis = QaClockAxis.None,
                Executability = CriterionExecutability.HumanEye,
                ExecutableAs = "HP-08（观察项记录）",
                FalsePositiveCondition = "本项没有可写的误判条件——正因如此它只能是观察项。若有人把它升级为 Gate，必须先补上可量化的判据与阈值，否则就会变成「看谁嗓门大」。",
                SourceRef = "GDD-v1.0.md §8.5 / §10.1",
                CanonicalId = "B3",
                NegativeCaseWaiver = "观察项（B3 相机跟随）：GDD 10.1 明确不毙 Gate、转后续修复处理，且本条写不出量化的误判条件。",
                Veto = false,
                AxisNotApplicable = true,
            },
            new CriterionRecord
            {
                Id = "WC-15",
                Title = "Boss 攻击范围 30 帧内可读出",
                Statement = "Boss 攻击的预警必须在攻击生效前 30 帧（0.5s，帧，W 轴）内让玩家读出范围。哑刀「断崖一刀」的前方大扇形（6m）是本判据的**唯一承载**（地面大扇形 + 形状 + 音效三重编码）。",
                Unit = QaUnit.Frame, Axis = QaClockAxis.WallClock,
                Executability = CriterionExecutability.Mixed,
                ExecutableAs = "自动：SC-11（预警数据帧数 ≥30 且形状为扇形）；人眼：HP-09（固定机位，判「能否读出范围」）",
                FalsePositiveCondition = "预警帧数达标不等于可读：若预警形状是地面圆而实际攻击是扇形，玩家读到的是错的形状。判据必须同时校验「预警形状 = 实际判定形状」，否则会把「有预警」当成「预警正确」。",
                SourceRef = "GDD-v1.0.md §7.3 / §8.5 / §9.4",
                CanonicalId = "E2",
                LinkedOracleIds = new List<string> { CriterionOracle.FBossTelegraph, CriterionOracle.FTelegraphChannels },
                Veto = false,
            },
            new CriterionRecord
            {
                Id = "WC-16",
                Title = "输入延迟 ≤50ms 且两条时间戳非同源",
                Statement = "从设备输入回调到游戏内帧开始的延迟 ≤50ms（毫秒，墙钟）。测量用的两个时间戳必须来自**两条独立管线**（如 Input System 回调时间戳 vs Animator 状态进入时间戳），差值不得恒为 0。",
                Unit = QaUnit.Millisecond, Axis = QaClockAxis.WallClock,
                Executability = CriterionExecutability.AutoScript,
                ExecutableAs = "SC-12（判据 F-E3-LATENCY + F-E3-SAME-SOURCE）",
                FalsePositiveCondition = "两个时间戳同源时差值恒为 0，判据永远通过——这是「最容易假通过的一条」。判据必须先证明两条管线非同源，再读差值；只读差值的实现等于没测。",
                SourceRef = "GDD-v1.0.md §12.1 示例（E3）",
                CanonicalId = "E3",
                LinkedOracleIds = new List<string> { CriterionOracle.FE3Latency, CriterionOracle.FE3SameSource },
                Veto = false,
            },
            new CriterionRecord
            {
                Id = "WC-17",
                Title = "刀光：竖直面、离地 ≥0.5m、不落地、被剪影遮挡 ≤30%",
                Statement = "刀光为竖直面；离地高度 ≥0.5m；不落地（除地面大扇形预警外）；命中判定帧被角色剪影遮挡的比例 ≤30%（比例）。",
                Unit = QaUnit.Meter, Axis = QaClockAxis.WallClock,
                Executability = CriterionExecutability.Mixed,
                ExecutableAs = "自动：SC-13（命中帧截图像素占比，判据 F-BLADE-OCCLUSION）；人眼：HP-10（竖直面与离地）",
                FalsePositiveCondition = "遮挡比必须是「被角色剪影遮挡的面积 / 刀光总面积」，不是「刀光占屏面积」——竖直刀光在俯视角下投影只有 0.5% 占屏，若量错对象会误杀全部竖直刀光（B-01 专测这一点）。像素分析必须固定命中判定帧，否则同一刀光不同帧的遮挡比差异可达 2 倍。",
                SourceRef = "GDD-v1.0.md §7.2 / §12.1 ① / §12.3 错例 5",
                CanonicalId = "—",
                LinkedOracleIds = new List<string> { CriterionOracle.FBladeOcclusion },
                Veto = false,
            },
            new CriterionRecord
            {
                Id = "WC-18",
                Title = "攻击预警三重编码，不依赖颜色",
                Statement = "敌方的攻击预警必须同时具备：地面预警圈、形状差异、音效提示（三通道）。颜色不承担信息（无障碍 WCAG 要求）。",
                Unit = QaUnit.Count, Axis = QaClockAxis.None,
                Executability = CriterionExecutability.HumanEye,
                ExecutableAs = "HP-11（三通道逐条勾选）",
                FalsePositiveCondition = "若某 Boss 的攻击本身没有落点（比如全身 AOE），「地面预警圈」这一通道物理上不存在，判据会把它判为违规。判据必须允许「全身 AOE 用全屏色调 + 音效」这一替代编码，并写明替代条件。",
                SourceRef = "GDD-v1.0.md §8.5",
                CanonicalId = "—",
                LinkedOracleIds = new List<string> { CriterionOracle.FTelegraphChannels },
                Veto = false,
                AxisNotApplicable = true,
            },
            new CriterionRecord
            {
                Id = "WC-19",
                Title = "墨线 3px 屏幕空间，颜色按背景亮度差推导",
                Statement = "所有角色 3px 屏幕空间墨线（像素）；墨线颜色取「与背景亮度差 ≥60% 的一端」（比例）：底亮用墨黑 #1E1B18，底暗（铁砧窟）倒相为纸白 #E8DFC9。禁止把墨色写死为墨黑。",
                Unit = QaUnit.Pixel, Axis = QaClockAxis.None,
                Executability = CriterionExecutability.Mixed,
                ExecutableAs = "自动：SC-14（亮度差推导函数，5 档背景亮度 × 2 预设）；人眼：HP-12（3px 墨线实际观感）",
                FalsePositiveCondition = "亮度差必须在**该副本的实拍背景**上取，而不是在纯色底或无背景的测试场景取；测试场景太干净会让「倒相」分支永远不触发，用例形同虚设。",
                SourceRef = "GDD-v1.0.md §8.1 / §12.3 错例 4",
                CanonicalId = "—",
                LinkedOracleIds = new List<string> { CriterionOracle.FInkContrast },
                Veto = false,
                AxisNotApplicable = true,
            },
            new CriterionRecord
            {
                Id = "WC-20",
                Title = "视觉通道分离：一个渲染器一个 Material 槽",
                Statement = "单个渲染器只允许一个 Material 槽；禁止 AdditionalMaterial；Clear Coat 全局关闭（材质配置计数）。",
                Unit = QaUnit.Count, Axis = QaClockAxis.None,
                Executability = CriterionExecutability.AutoScript,
                ExecutableAs = "SC-15（材质扫描；asmdef 反向引用扫描见 ArchitectureTests）",
                FalsePositiveCondition = "扫描必须覆盖所有已导入材质而不是只扫角色目录；只扫角色的实现会漏掉 VFX / 场景材质里的 AdditionalMaterial。另外「全局关闭 Clear Coat」若只在资产层检查，被运行时 Instantiate 的材质会绕过。",
                SourceRef = "GDD-v1.0.md §9.6",
                CanonicalId = "—",
                LinkedOracleIds = new List<string> { CriterionOracle.FVfxChannel },
                Veto = false,
                AxisNotApplicable = true,
            },
            new CriterionRecord
            {
                Id = "WC-21",
                Title = "精英 1.4× 缩放必须同步缩放受击盒与胶囊体",
                Statement = "精英怪缩放 1.4×（倍率）时，受击盒与碰撞胶囊体必须同步缩放；只缩渲染体会导致打不到。",
                Unit = QaUnit.Ratio, Axis = QaClockAxis.None,
                Executability = CriterionExecutability.AutoScript,
                ExecutableAs = "SC-16（Prefab 检查：Collider 尺寸 / 视觉尺寸 比值）",
                FalsePositiveCondition = "判据量的是「碰撞体尺寸 / 视觉尺寸」的比例，而不是绝对尺寸——若只查「Collider 尺寸 == 1.4 × 基础值」，遇到基础值本身有偏差的实现就会被误杀。",
                SourceRef = "GDD-v1.0.md §7.4",
                CanonicalId = "—",
                LinkedOracleIds = new List<string> { CriterionOracle.FEliteHitbox },
                Veto = false,
                AxisNotApplicable = true,
            },
            new CriterionRecord
            {
                Id = "WC-22",
                Title = "受击动画 4 变体齐备 + 反向变体几何稳定",
                Statement = "受击动画必须齐备 4 个变体（正向/反向 × 左右镜像）。反向变体的脚底接触点必须离地 >3cm（厘米，W 轴）且持续 ≥2 帧（帧，W 轴），否则判定为「不稳」、不可反向播放。",
                Unit = QaUnit.Centimeter, Axis = QaClockAxis.Clip,
                Executability = CriterionExecutability.AutoScript,
                ExecutableAs = "SC-17（AnimationClip 采样：变体齐备性 + 脚底接触点高度序列）",
                FalsePositiveCondition = "脚底接触点的取法必须写死（本判据取绑定骨架最低顶点到根骨平面的距离）；若取「包围盒底面」，脚部 IK 造成的包围盒抖动会让连续帧全部判为离地 >3cm，从而把好变体误判为不稳。",
                SourceRef = "GDD-v1.0.md §7.3 受击变体；GDD-v0.1.md §9",
                CanonicalId = "—",
                LinkedOracleIds = new List<string> { CriterionOracle.FHitClipVariants },
                Veto = false
            }
        };

        // ─────────────────────────────────────────────────────────────────
        // 30 条负向用例（24 A 类 · 6 B 类）
        // ─────────────────────────────────────────────────────────────────
        public static readonly List<NegativeCaseRecord> NegativeCases = new List<NegativeCaseRecord>
        {
            // ── A 类 · 灵敏度：注入一个错误，判据应当报错 ─────────────────
            new NegativeCaseRecord
            {
                Id = "NC-A01", Class = NegativeCaseClass.Sensitivity, TargetCriterionId = CriterionOracle.FDodgeInvuln,
                Injection = "闪避的 invuln_start 与 invuln_end 同时置 0（无敌窗口长度 = 0 帧）。",
                Expected = "判据必须报错：无敌窗口长度 0 < 1 帧（F-DODGE-INVULN 报红）。",
                MeaningIfNotFired = "无敌只在配表生效、战斗里没接上——运行时根本没有无敌逻辑，配置正确也打不出来。",
                SourceRef = "GDD-v1.0.md §12.1 示例（C2 闪避无敌帧）",
                Channel = CriterionExecutability.AutoScript
            },
            new NegativeCaseRecord
            {
                Id = "NC-A02", Class = NegativeCaseClass.Sensitivity, TargetCriterionId = CriterionOracle.FHitFeedback,
                Injection = "把命中帧到受击动画起始的间隔从 2 帧改成 5 帧。",
                Expected = "判据必须报错：间隔 5 帧 > 3 帧（F-HIT-FEEDBACK 报红）。",
                MeaningIfNotFired = "反馈延迟失守 → 玩家先看到「挥空」再看到「被打中」，因果顺序反了；这是最基础的一票否决项。",
                SourceRef = "GDD-v1.0.md §1.2 P1；§10.1 W4（一票否决）",
                Channel = CriterionExecutability.AutoScript
            },
            new NegativeCaseRecord
            {
                Id = "NC-A03", Class = NegativeCaseClass.Sensitivity, TargetCriterionId = CriterionOracle.FKbDirection,
                Injection = "把击退方向从伤害来源的反方向（180°）改成与伤害来源同向（0°）。",
                Expected = "判据必须报错：击退方向与伤害来源方向的夹角超过 45° 容差（F-KB-DIRECTION 报红）。",
                MeaningIfNotFired = "方向判据形同虚设 → 角色朝伤害来的方向飞出去，玩家读不出「是谁打的我、从哪来」。",
                SourceRef = "GDD-v1.0.md §7.3 硬判据；§10.1 W4（一票否决）",
                Channel = CriterionExecutability.AutoScript
            },
            new NegativeCaseRecord
            {
                Id = "NC-A04", Class = NegativeCaseClass.Sensitivity, TargetCriterionId = CriterionOracle.FKbTier,
                Injection = "把轻档的击退距离上限从 0.6m 改成 0.8m（越出轻档区间 [0.3, 0.6]）。",
                Expected = "判据必须报错：击退距离必须落在档位区间内（F-KB-TIER 报红）。",
                MeaningIfNotFired = "三档区分度塌陷 → 玩家读不出「这一下多重」，D1b 的「12 帧内可判轻重」随之失效。",
                SourceRef = "GDD-v1.0.md §7.3；IMPL-v3.0.md §3.2",
                Channel = CriterionExecutability.AutoScript
            },
            new NegativeCaseRecord
            {
                Id = "NC-A05", Class = NegativeCaseClass.Sensitivity, TargetCriterionId = CriterionOracle.FKbWall,
                Injection = "让一次重档击退把受击者推进墙内（场景查询命中墙体）。",
                Expected = "判据必须报错：不得推入墙内 / 推落平台（F-KB-WALL 报红）。",
                MeaningIfNotFired = "穿墙与掉平台不会被拦 → 会出现「隔墙被推走」和「被击退掉下悬崖」两类不可解释的位移。",
                SourceRef = "GDD-v1.0.md §7.3 D2 优先级",
                Channel = CriterionExecutability.AutoScript
            },
            new NegativeCaseRecord
            {
                Id = "NC-A06", Class = NegativeCaseClass.Sensitivity, TargetCriterionId = CriterionOracle.FBossTelegraph,
                Injection = "把 Boss 攻击的预警提前帧数从 30 帧改成 18 帧。",
                Expected = "判据必须报错：预警必须提前 ≥30 帧（F-BOSS-TELEGRAPH 报红）。",
                MeaningIfNotFired = "预警窗口过短 → 玩家来不及侧移，E2 的「30 帧内读出范围」名存实亡；这条判据一旦放宽就再也收不回来。",
                SourceRef = "GDD-v1.0.md §7.3 / §9.4",
                Channel = CriterionExecutability.AutoScript
            },
            new NegativeCaseRecord
            {
                Id = "NC-A07", Class = NegativeCaseClass.Sensitivity, TargetCriterionId = CriterionOracle.FTelegraphChannels,
                Injection = "拿掉 Boss 攻击预警的音效通道（只保留地面圈 + 形状）。",
                Expected = "判据必须报错：地面圈 / 形状 / 音效三通道必须齐备（F-TELEGRAPH-CHANNELS 报红）。",
                MeaningIfNotFired = "预警退化成依赖视觉的单一通道 → 屏幕外攻击、闭眼听声与色觉障碍玩家同时失去预警。",
                SourceRef = "GDD-v1.0.md §8.5",
                Channel = CriterionExecutability.AutoScript
            },
            new NegativeCaseRecord
            {
                Id = "NC-A08", Class = NegativeCaseClass.Sensitivity, TargetCriterionId = CriterionOracle.FInkContrast,
                Injection = "把墨线与背景的亮度差从 72% 改成 45%（例如在铁砧窟橙底下仍用固定墨黑）。",
                Expected = "判据必须报错：墨线与背景亮度差必须 ≥60%（F-INK-CONTRAST 报红）。",
                MeaningIfNotFired = "墨线与背景糊在一起 → 剪影消失、角色轮廓读不出；这类缺陷在亮背景下看不出来，只有切到暗副本才复现。",
                SourceRef = "GDD-v1.0.md §8.1 / §12.3 错例 4",
                Channel = CriterionExecutability.AutoScript
            },
            new NegativeCaseRecord
            {
                Id = "NC-A09", Class = NegativeCaseClass.Sensitivity, TargetCriterionId = CriterionOracle.FVfxChannel,
                Injection = "给角色渲染器加一个 AdditionalMaterial（材质槽数变成 2）。",
                Expected = "判据必须报错：一渲染器一材质槽、禁 AdditionalMaterial、Clear Coat 全局关闭（F-VFX-CHANNEL 报红）。",
                MeaningIfNotFired = "视觉通道被合并 → 描边、脉冲、霸体标记开始互相串色或互相覆盖，形态可辨（WC-13）的数据前提被破坏。",
                SourceRef = "GDD-v1.0.md §9.6",
                Channel = CriterionExecutability.AutoScript
            },
            new NegativeCaseRecord
            {
                Id = "NC-A10", Class = NegativeCaseClass.Sensitivity, TargetCriterionId = CriterionOracle.FEliteHitbox,
                Injection = "把精英受击盒的缩放从 1.4× 改成 1.0×（只缩渲染体）。",
                Expected = "判据必须报错：受击盒缩放必须与渲染体缩放一致（F-ELITE-HITBOX 报红）。",
                MeaningIfNotFired = "打不到 → 玩家在精英身上反复挥空，判定为「手感问题」而不是「配置问题」，会一直找不到根因。",
                SourceRef = "GDD-v1.0.md §7.4",
                Channel = CriterionExecutability.AutoScript
            },
            new NegativeCaseRecord
            {
                Id = "NC-A11", Class = NegativeCaseClass.Sensitivity, TargetCriterionId = CriterionOracle.FHitClipVariants,
                Injection = "把受击动画的变体数从 4 减到 3，并让反向变体的脚底接触点只离地 2cm、持续 1 帧。",
                Expected = "判据必须报错：变体必须齐备 4 个，且反向变体接触点离地 >3cm 且持续 ≥2 帧（F-HITCLIP-VARIANTS 报红）。",
                MeaningIfNotFired = "反向变体「不稳」 → 反向播放时脚底会滑，斜向受击（不计入 D1b 采样）立刻穿帮。",
                SourceRef = "GDD-v1.0.md §7.3 受击变体",
                Channel = CriterionExecutability.AutoScript
            },
            new NegativeCaseRecord
            {
                Id = "NC-A12", Class = NegativeCaseClass.Sensitivity, TargetCriterionId = CriterionOracle.FD5Control,
                Injection = "给盲测组的 20 张图做对照处理：两套描边**只差颜色**（描边形态相同、入窗时机相同）。",
                Expected = "判据必须报错：对照组必须同时消除颜色 / 描边形态 / 入窗时机三个通道，否则形态可辨判据无效（F-D5-CONTROL 报红）。",
                MeaningIfNotFired = "测试无效——测试者改用体态或时序线索猜对了；对照组若只消颜色不消形态与时机，得到的通过率不能作为关键组的依据。",
                SourceRef = "GDD-v1.0.md §12.1 示例（D5 形态可辨）；§10.1 规则 3",
                Channel = CriterionExecutability.HumanEye
            },
            new NegativeCaseRecord
            {
                Id = "NC-A13", Class = NegativeCaseClass.Sensitivity, TargetCriterionId = CriterionOracle.FE3Latency,
                Injection = "给输入注入 10 帧固定延迟（10 ÷ 60 × 1000 = 166.7ms），实测延迟读到 166.7ms。",
                Expected = "判据必须报错：实测 166.7ms > 50ms（F-E3-LATENCY 报红）。",
                MeaningIfNotFired = "延迟超限却没报错 → 两个时间戳同源、差值恒为 0；这是「最容易假通过的一条」，读差值而不验管线独立性等于没测。",
                SourceRef = "GDD-v1.0.md §12.1 示例（E3 输入延迟）",
                Channel = CriterionExecutability.AutoScript
            },
            new NegativeCaseRecord
            {
                Id = "NC-A14", Class = NegativeCaseClass.Sensitivity, TargetCriterionId = CriterionOracle.FE3SameSource,
                Injection = "保持注入的 10 帧延迟，但让两个时间戳取自同一条管线（实测恒为 0ms）。",
                Expected = "判据必须报错：两条时间戳非同源（F-E3-SAME-SOURCE 报红），而**不能**只靠 F-E3-LATENCY 报红。",
                MeaningIfNotFired = "只有差值判据没有独立性判据 → 注入 10 帧延迟后仍读到 0ms，判据安静地通过，延迟被永久掩盖。",
                SourceRef = "GDD-v1.0.md §12.1 示例（E3 的「没报说明」列）",
                Channel = CriterionExecutability.AutoScript
            },
            new NegativeCaseRecord
            {
                Id = "NC-A15", Class = NegativeCaseClass.Sensitivity, TargetCriterionId = CriterionOracle.FHold,
                Injection = "把轻档的受击剪辑从 13 C 帧加长到 17 C 帧（@60fps）→ clip_W = 17 → hold 重算 = 18 − 17 − 2×0.85 = −0.7 帧。",
                Expected = "判据必须报错：换算后 hold < 0 必须报错、**不取 0**（F-HOLD 报红）。",
                MeaningIfNotFired = "hold 变负 → 若实现按「取 0」处理，受击硬直会短于剪辑本身，玩家感到「打了但没僵住」，回手节奏被吃掉；且负值本身说明硬直与剪辑长度不匹配，属数据层错误。",
                SourceRef = "GDD-v1.0.md §7.3 / §9.4 导表硬断言 #11",
                Channel = CriterionExecutability.AutoScript
            },
            new NegativeCaseRecord
            {
                Id = "NC-A16", Class = NegativeCaseClass.Sensitivity, TargetCriterionId = CriterionOracle.FHitstopCausal,
                Injection = "把重档的 hitstop_frames 从 6 改成 8（hitstop_time_scale = 0.15 不变）。",
                Expected = "判据必须报错：0.15 × 8 = 1.2 > 1（F-HITSTOP-CAUSAL 报红）。注意判据是因果式，「≤6 帧」的硬编码写法在这里**不会**报红——这正是必须用因果式的理由。",
                MeaningIfNotFired = "顿帧上限写成常数 → time_scale 一旦被调低（更慢）上限不会跟着放宽，或调高（更快）时上限失效。",
                SourceRef = "GDD-v1.0.md §9.4 导表硬断言 #12",
                Channel = CriterionExecutability.AutoScript
            },
            new NegativeCaseRecord
            {
                Id = "NC-A17", Class = NegativeCaseClass.Sensitivity, TargetCriterionId = CriterionOracle.FVfxStructure,
                Injection = "把某个技能的无敌描边资源改成与霸体描边相同的结构（双方都改成单边实描，只靠颜色区分）。",
                Expected = "判据必须报错：霸体与无敌的 outline_style 必须不同（F-VFX-STRUCTURE 报红）。",
                MeaningIfNotFired = "结构差异缺失 → 依赖颜色区分两种状态，色觉障碍玩家与低质屏幕下完全无法分辨；且这与 IMPL §3.4 「不得用颜色通道」直接冲突。",
                SourceRef = "IMPL-v3.0.md §3.4；GDD-v1.0.md §9.4 导表硬断言 #6",
                Channel = CriterionExecutability.AutoScript
            },
            new NegativeCaseRecord
            {
                Id = "NC-A18", Class = NegativeCaseClass.Sensitivity, TargetCriterionId = CriterionOracle.FPulseHz,
                Injection = "把某个特效资源的 vfx_pulse_hz 从 0 改成 4（上限 3Hz）。",
                Expected = "判据必须报错：脉冲频率必须 ∈ [0, 3]Hz（F-PULSE-HZ 报红）。",
                MeaningIfNotFired = "4Hz 脉冲接近闪烁频率边界 → 无敌状态的提示会变成频闪，既是体验问题也是可访问性问题（WCAG 闪烁阈值）。",
                SourceRef = "GDD-v1.0.md §9.4 导表硬断言 #7",
                Channel = CriterionExecutability.AutoScript
            },
            new NegativeCaseRecord
            {
                Id = "NC-A19", Class = NegativeCaseClass.Sensitivity, TargetCriterionId = CriterionOracle.FPulseWindow,
                Injection = "把某个特效资源改成 vfx_pulse_hz = 2 且窗口时长 3 帧（50ms），小于一个脉冲周期 1000÷(2×2) = 250ms。",
                Expected = "判据必须报错：窗口必须容得下一个脉冲周期（F-PULSE-WINDOW 报红）。",
                MeaningIfNotFired = "窗口短于周期 → 无敌提示一帧都没显示就结束，玩家根本看不到「我处于无敌」。注意：把阈值写死成 250ms 的实现在本例**恰好也会报红**，但会在 hz=1 时误杀（见 NC-B02）——这正是本项目第三次翻车的形态。",
                SourceRef = "GDD-v1.0.md §9.4 导表硬断言 #8；§12.3 错例 3",
                Channel = CriterionExecutability.AutoScript
            },
            new NegativeCaseRecord
            {
                Id = "NC-A20", Class = NegativeCaseClass.Sensitivity, TargetCriterionId = CriterionOracle.FBladeOcclusion,
                Injection = "把刀光在命中判定帧被角色剪影遮挡的比例从 18% 改成 45%。",
                Expected = "判据必须报错：遮挡比 45% > 30%（F-BLADE-OCCLUSION 报红）。",
                MeaningIfNotFired = "刀光被角色挡掉 → 命中瞬间看不到刀光，「打到了」这件事在视觉上消失；只量占屏比会漏掉这一类（见 NC-B01）。",
                SourceRef = "GDD-v1.0.md §7.2 / §12.3 错例 5",
                Channel = CriterionExecutability.AutoScript
            },
            new NegativeCaseRecord
            {
                Id = "NC-A21", Class = NegativeCaseClass.Sensitivity, TargetCriterionId = CriterionOracle.FDodgePush,
                Injection = "把闪避前 2 帧蹬地窗口的位移占比从 3% 改成 12%。",
                Expected = "判据必须报错：蹬地窗口位移占比 ≤5%（F-DODGE-PUSH 报红）。",
                MeaningIfNotFired = "蹬地窗口占比失控 → 闪避的「起步」变成「起步位移」，重心前冲，破坏「原地中性收身」的硬指标。",
                SourceRef = "GDD-v1.0.md §7.3 动画硬指标 ①",
                Channel = CriterionExecutability.AutoScript
            },
            new NegativeCaseRecord
            {
                Id = "NC-A22", Class = NegativeCaseClass.Sensitivity, TargetCriterionId = CriterionOracle.A19,
                Injection = "把某个技能的 phases.startup 改成 0。",
                Expected = "判据必须报错：phases 各段 ≥1（A19 报红）。",
                MeaningIfNotFired = "前摇为 0 的技能在数据层被放行 → 玩家没有任何可读的反应时间。",
                SourceRef = "IMPL-v2.0.md §5 断言 A19",
                RelatedAssertionIds = new List<string> { CriterionOracle.A19 },
                Channel = CriterionExecutability.AutoScript
            },
            new NegativeCaseRecord
            {
                Id = "NC-A23", Class = NegativeCaseClass.Sensitivity, TargetCriterionId = CriterionOracle.A20,
                Injection = "把某个技能的 hitShape.frame 从 0 改成等于 active 段长度（判定帧落到判定段之外）。",
                Expected = "判据必须报错：hitShape.frame ≥ active（A20 报红）。",
                MeaningIfNotFired = "判定帧落在 active 段之外 → 攻击要么永不生效，要么在错误时机生效；A20 已经「永远报绿」。",
                SourceRef = "IMPL-v2.0.md §5 断言 A20",
                RelatedAssertionIds = new List<string> { CriterionOracle.A20 },
                Channel = CriterionExecutability.AutoScript
            },
            new NegativeCaseRecord
            {
                Id = "NC-A24", Class = NegativeCaseClass.Sensitivity, TargetCriterionId = CriterionOracle.A22,
                Injection = "把某条取消窗口的 allowTag 改成 \"not_a_real_tag\"（既不在任何 skill.entryTags，也不在门派共有动作标签 {dodge, attack} 里）。",
                Expected = "判据必须报错：allowTags 必须落在标签全集内 = 8 技能 entryTags ∪ {dodge, attack}（A22 报红）。",
                MeaningIfNotFired = "引用了不存在的标签 → 取消窗口永远匹配不上，或被静默忽略，产生「看起来有窗口、实际取消不了」的死窗口。",
                SourceRef = "IMPL-v2.0.md §5 断言 A22；【D-10 裁定】标签全集 = 技能 entryTags ∪ 门派共有动作标签",
                RelatedAssertionIds = new List<string> { CriterionOracle.A22 },
                Channel = CriterionExecutability.AutoScript
            },

            // ── B 类 · 特异性 / 防误杀：注入一个合法但不同的设计，判据应当通过 ──
            new NegativeCaseRecord
            {
                Id = "NC-B01", Class = NegativeCaseClass.Specificity, TargetCriterionId = CriterionOracle.FBladeOcclusion,
                Injection = "注入一条合法的竖直刀光：命中判定帧的**遮挡比** 12%，但因为是竖直面、在俯视角下**投影占屏只有 0.5%**。",
                Expected = "判据必须通过：遮挡比 12% ≤ 30%，不得报红。",
                MeaningIfNotFired = "若判据量的是「占屏面积 ≥3%」这种旧口径，这条完全合法的竖直刀光会被判为违规——项目历史上已经因此翻车一次。",
                LegalDesignAtRisk = "**任何非「全屏大特效」型的合法刀光，尤其是竖直刀光。** IMPL §3.4 明确要求刀光为竖直面，这类刀光在俯视角下投影天然很小；任何以「占屏比」为阈值的判据都会把它们全部误杀。",
                SourceRef = "GDD-v1.0.md §12.1 ① + §12.3 错例 5；IMPL-v3.0.md §3.4",
                Channel = CriterionExecutability.AutoScript
            },
            new NegativeCaseRecord
            {
                Id = "NC-B02", Class = NegativeCaseClass.Specificity, TargetCriterionId = CriterionOracle.FPulseWindow,
                Injection = "注入一个合法的低频脉冲：vfx_pulse_hz = 1，窗口 600ms（≥ 1000÷(2×1) = 500ms）；以及 MVP 默认值 vfx_pulse_hz = 0（不脉冲）。",
                Expected = "判据必须通过：不得报红。",
                MeaningIfNotFired = "把脉冲窗口阈值写死成 250ms 的判据，会把所有非 2Hz 的合法频率判为违规——项目第三次翻车正是这个形态。",
                LegalDesignAtRisk = "**任何非 2Hz/3Hz 的合法脉冲频率，含 MVP 默认的 hz=0（不脉冲）。** 脉冲频率是设计自由度，判据只能约束「窗口容得下周期」这一条因果关系，绝不能约束频率本身等于某个具体值。",
                SourceRef = "GDD-v1.0.md §12.3 错例 3；§9.4 导表硬断言 #8",
                Channel = CriterionExecutability.AutoScript
            },
            new NegativeCaseRecord
            {
                Id = "NC-B03", Class = NegativeCaseClass.Specificity, TargetCriterionId = CriterionOracle.FKbPeak,
                Injection = "注入一个合法的 60fps 实现：峰值速度 13.2 m/s，实际运行帧率 60（逐帧位移 0.22m）。",
                Expected = "判据必须通过：不得报红。",
                MeaningIfNotFired = "若判据把「30fps 推导出来的每帧位移」当作等式（每帧位移 == 13.2÷30）来比，60fps 的合法实现会被误杀——项目第四次翻车正是「帧率依赖单位」。",
                LegalDesignAtRisk = "**任何在 60fps / 120fps 档实现的合法击退。** 逐帧量必须是「该帧率下的位移 ≤ 上限」这样的不等式，不能是「等于 30fps 推导值」的等式；量纲必须写成 m/s 而不是 m/帧。",
                SourceRef = "GDD-v1.0.md §12.3 错例 5 的同类；治理规则 5（环境会变的量不许写死）",
                Channel = CriterionExecutability.AutoScript
            },
            new NegativeCaseRecord
            {
                Id = "NC-B04", Class = NegativeCaseClass.Specificity, TargetCriterionId = CriterionOracle.FEdgeLevels,
                Injection = "注入一个刻意的难度断崖：把铁砧窟的 cp_threshold 从 900 改成 700（相对落霞渡 100 是 7.0 倍，仍 ≥1.3 倍且 ≤ 预期总战力 930）。",
                Expected = "判据必须通过：不得报红。",
                MeaningIfNotFired = "把「每档 ≥1.3×」误实现为「每档 ≥1.7×」，会把刻意的难度断崖判为违规——这正是文档里明确写下的已修误杀案例。",
                LegalDesignAtRisk = "**任何刻意设计的档位断崖（倍率显著高于 1.3 的设计）。** 判据只能约束「递增 + ≥1.3× + ≤ 该档预期总战力」，不能额外假设倍率落在某个窄区间。",
                SourceRef = "GDD-v1.0.md §12.1 ③（已修误杀案例）；§9.4 导表硬断言 #11",
                Channel = CriterionExecutability.AutoScript
            },
            new NegativeCaseRecord
            {
                Id = "NC-B05", Class = NegativeCaseClass.Specificity, TargetCriterionId = CriterionOracle.A331,
                Injection = "给聚锋的 tier=3 阶（该阶的派生效果类型）挂一个清单外的新派生 id，并同步写进 skill.derivative_ids（双向引用齐备、阶位行数仍为 5、new_graphic 条数不变）。",
                Expected = "判据必须通过：A33（每技能 5 阶恰好覆盖 5 种类型各一次）/ A33-1 / A33-2（至多 2 条）/ A33-5 / A43 全部不得报红。",
                MeaningIfNotFired = "派生条目数被写死成 8 / 6 之后，任何新增派生都会被判为违规——而 §4.8.5 明确「不写死条数」。",
                LegalDesignAtRisk = "**任何新增的技传派生条目（尤其是视觉层级为 generic / param 的那类）。** 派生清单是可以增长的；判据只能校验双向引用一致性，不能校验条数本身。",
                SourceRef = "EL-03 §2 A33-1 / A33-5；IMPL-v3.0.md §4.8.5",
                RelatedAssertionIds = new List<string> { CriterionOracle.A331 },
                Channel = CriterionExecutability.AutoScript
            },
            new NegativeCaseRecord
            {
                Id = "NC-B06", Class = NegativeCaseClass.Specificity, TargetCriterionId = CriterionOracle.FHold,
                Injection = "把三档的击退距离取到区间端点（轻 0.3 / 0.6m，重 1.2 / 1.5m），并把 hold 的**重算值**取到区间内最接近端点的可达值（轻档 2.3 帧、重档 3.7 帧）。",
                Expected = "判据必须通过：区间判据必须**含端点**，不得报红。",
                MeaningIfNotFired = "区间写成开区间（< / >）后，正好落在规格边界上的合法取值会被判为违规——而边界值恰恰是设计最常选的地方。",
                LegalDesignAtRisk = "**任何恰好落在规格边界上的合法取值。** 所有区间型判据（hold、击退距离、hitstop、门槛比例）必须显式声明端点归属；GDD §9.4 的写法是「∈ [2,4]」（闭区间）。注：hold 的端点 2.0 / 4.0 在 time_scale = 0.15 下**不可达**（小数部分恒为 .3/.6/.7），该缺口已登记为 D-14，故本用例用「最接近端点的可达值」代替。",
                SourceRef = "GDD-v1.0.md §9.4（hold ∈ [2,4] 闭区间写法）；§7.3 击退区间",
                Channel = CriterionExecutability.AutoScript
            }
        };

        /// <summary>
        /// 判据目录的**三层关系**（D-06 裁定：代码即正本）。
        ///
        /// | 层 | 数量 | 含义 |
        /// |---|---|---|
        /// | 全量判据 | <see cref="AllCriterionIds"/> 的长度 | 所有可判定项（正向走查 + 导表断言 + 打击感判据） |
        /// | **负向用例** | <see cref="NegativeCases"/> 的长度 = 30 | 其中 A 类（灵敏度）与 B 类（特异性）的子集；**30 与 22 不相交** |
        /// | **正向走查** | <see cref="Walkthrough"/> 的长度 = 22 | 人工 / 脚本走查项（9 纯脚本 + 7 混合 + 6 必须人眼） |
        ///
        /// 推导关系：**30 属于全量判据**、**22 属于全量判据**、**30 与 22 不相交**。
        /// 三条数字不是并列的三个总数，而是**同一本目录的三个切面**——写在这里就是为了防止它们各自漂移。
        /// </summary>
        /// <remarks>做成方法而不是静态字段：静态字段的初始化是**按文本顺序**的，
        /// 若它排在 Walkthrough / NegativeCases 之前，取到的会是 null。</remarks>
        public static string DescribeRelations()
        {
            int total = AllCriterionIds().Count;
            int walk = Walkthrough.Count;
            int neg = NegativeCases.Count;
            return "全量判据 " + total + " 条（= " + walk + " 条正向走查 + "
                + (total - walk) + " 条导表 / 养成 / 打击感判据）；"
                + "负向用例 " + neg + " 条（24 A 类 + 6 B 类）；"
                + "正向走查 " + walk + " 条（9 纯脚本 + 7 混合 + 6 必须人眼）。"
                + "关系：负向用例与正向走查都从属于全量判据，且两者互不相交。";
        }

        // ─────────────────────────────────────────────────────────────────
        // 查询
        // ─────────────────────────────────────────────────────────────────

        public static CriterionRecord FindCriterion(string id)
        {
            for (int i = 0; i < Walkthrough.Count; i++)
            {
                if (Walkthrough[i].Id == id) { return Walkthrough[i]; }
            }
            return null;
        }

        public static NegativeCaseRecord FindNegativeCase(string id)
        {
            for (int i = 0; i < NegativeCases.Count; i++)
            {
                if (NegativeCases[i].Id == id) { return NegativeCases[i]; }
            }
            return null;
        }

        public static List<NegativeCaseRecord> CasesOfClass(NegativeCaseClass cls)
        {
            var list = new List<NegativeCaseRecord>();
            for (int i = 0; i < NegativeCases.Count; i++)
            {
                if (NegativeCases[i].Class == cls) { list.Add(NegativeCases[i]); }
            }
            return list;
        }

        /// <summary>所有判据编号（含导表断言编号），用于覆盖率检查。</summary>
        public static List<string> AllCriterionIds()
        {
            var list = new List<string>();
            for (int i = 0; i < Walkthrough.Count; i++) { list.Add(Walkthrough[i].Id); }
            list.Add(CriterionOracle.A19); list.Add(CriterionOracle.A20); list.Add(CriterionOracle.A21);
            list.Add(CriterionOracle.A22); list.Add(CriterionOracle.A23); list.Add(CriterionOracle.A24);
            list.Add(CriterionOracle.A25); list.Add(CriterionOracle.A26); list.Add(CriterionOracle.A27);
            list.Add(CriterionOracle.A28); list.Add(CriterionOracle.A29); list.Add(CriterionOracle.A30);
            list.Add(CriterionOracle.A31); list.Add(CriterionOracle.A33);
            list.Add(CriterionOracle.A331); list.Add(CriterionOracle.A332); list.Add(CriterionOracle.A333);
            list.Add(CriterionOracle.A334); list.Add(CriterionOracle.A335); list.Add(CriterionOracle.A35);
            list.Add(CriterionOracle.A391); list.Add(CriterionOracle.A392);
            list.Add(CriterionOracle.A40); list.Add(CriterionOracle.A43);
            list.Add(CriterionOracle.FHold); list.Add(CriterionOracle.FHoldReachable);
            list.Add(CriterionOracle.FHitstopCausal);
            list.Add(CriterionOracle.FRecoveryGap); list.Add(CriterionOracle.FKbTier);
            list.Add(CriterionOracle.FKbPeak); list.Add(CriterionOracle.FDodgeInvuln);
            list.Add(CriterionOracle.FVfxStructure); list.Add(CriterionOracle.FPulseHz);
            list.Add(CriterionOracle.FPulseWindow); list.Add(CriterionOracle.FBladeOcclusion);
            list.Add(CriterionOracle.FE3Latency); list.Add(CriterionOracle.FE3SameSource);
            list.Add(CriterionOracle.FD5Control); list.Add(CriterionOracle.FEdgeLevels);
            list.Add(CriterionOracle.FDodgePush); list.Add(CriterionOracle.FHitFeedback);
            list.Add(CriterionOracle.FKbDirection); list.Add(CriterionOracle.FKbWall);
            list.Add(CriterionOracle.FBossTelegraph); list.Add(CriterionOracle.FTelegraphChannels);
            list.Add(CriterionOracle.FInkContrast); list.Add(CriterionOracle.FVfxChannel);
            list.Add(CriterionOracle.FEliteHitbox); list.Add(CriterionOracle.FHitClipVariants);
            return list;
        }
    }
}
