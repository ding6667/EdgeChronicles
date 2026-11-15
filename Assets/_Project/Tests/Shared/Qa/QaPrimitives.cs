using System;
using System.Collections.Generic;

namespace EdgeChronicles.Tests.Qa
{
    /// <summary>
    /// 量纲。GDD §13 治理规则 9：任何判据必须声明量纲。
    /// 拿不同量纲的量相减或相比，结论永远是假的。
    /// </summary>
    public enum QaUnit
    {
        None = 0,
        Meter,             // m
        Centimeter,        // cm
        MeterPerSecond,    // m/s
        Frame,             // 帧（必须同时声明时钟轴）
        Millisecond,       // ms
        Second,            // s
        Degree,            // °
        Ratio,             // 0..1 的比值（遮挡比 / 比例）
        Percent,           // %
        Count,             // 个数
        Pixel              // px
    }

    /// <summary>
    /// 时钟轴。GDD §7.3：
    /// W = 墙钟帧（硬直 / 顿帧 / 位移窗口 / hold / 取消窗口）
    /// C = 剪辑帧（姿态 / 收身 / 首尾双稳定 / 受击变体）
    /// LogicTick = Kernel 逻辑 tick（固定 60Hz）
    /// 跨轴相减必错。
    /// </summary>
    public enum QaClockAxis
    {
        None = 0,
        WallClock,   // W
        Clip,        // C
        LogicTick    // 60Hz 逻辑帧
    }

    /// <summary>判据的量纲 + 时钟轴声明。二者缺一，判据不予收录。</summary>
    public readonly struct QaQuantity
    {
        public readonly QaUnit Unit;
        public readonly QaClockAxis Axis;

        public QaQuantity(QaUnit unit, QaClockAxis axis)
        {
            Unit = unit;
            Axis = axis;
        }

        public bool IsDeclared
        {
            get { return Unit != QaUnit.None && Axis != QaClockAxis.None; }
        }

        public override string ToString()
        {
            return string.Format("{0}@{1}", Unit, Axis);
        }
    }

    /// <summary>判据可执行化等级。</summary>
    public enum CriterionExecutability
    {
        /// <summary>可完全脚本判定。</summary>
        AutoScript = 0,
        /// <summary>数值部分脚本判定，感知部分人眼判定（两者都要有固定协议）。</summary>
        Mixed,
        /// <summary>只能人眼判定（必须有写死的机位 / 距离 / 慢放倍数 / 判定动作）。</summary>
        HumanEye
    }

    /// <summary>负向用例的两类。</summary>
    public enum NegativeCaseClass
    {
        /// <summary>A 类 · 灵敏度：注入一个错误，判据应当报错。抓不住 = 判据废。</summary>
        Sensitivity = 0,
        /// <summary>B 类 · 特异性 / 防误杀：注入一个合法但不同的设计，判据应当通过。</summary>
        Specificity = 1
    }

    /// <summary>
    /// 一条判据（走查项）。字段全部必填——
    /// 缺 <see cref="FalsePositiveCondition"/> 的判据不予收录（GDD §12.1 判据四问 ③）。
    /// </summary>
    /// <summary>
    /// 判据目录的**三层算术**（裁定后写入代码，文档只引用这里，不复述数字）。
    ///
    /// | 层 | 数量 | 含义 | 关系 |
    /// |---|---|---|---|
    /// | 全量判据 | <see cref="TotalCriterionCount"/> | 所有可判定项（走查 + 导表断言 + 打击感判据） | — |
    /// | **负向用例** | <see cref="NegativeCaseCount"/> | 属 A 类（灵敏度）/ B 类（特异性）的用例集 | ⊆ 全量判据 |
    /// | **正向走查** | <see cref="WalkthroughCount"/> | 走查判据（人工 / 脚本走查项） | ⊆ 全量判据 |
    ///
    /// **负向用例与正向走查不相交**（30 ∩ 22 = ∅）：
    /// 正向走查回答"要满足什么"，负向用例回答"注入错误后判据还报不报"。
    /// 同一个编号不会既是走查项又是用例 id。
    /// </summary>
    public static class CriterionCounts
    {
        /// <summary>全量在册判据数（22 走查 + 47 断言 / 打击感判据 = 69）。</summary>
        public const int TotalCriterionCount = 69;
        /// <summary>负向用例数（24 A 类 + 6 B 类 = 30）。</summary>
        public const int NegativeCaseCount = 30;
        /// <summary>正向走查数（9 纯脚本 + 7 混合 + 6 必须人眼 = 22）。</summary>
        public const int WalkthroughCount = 22;
    }

    public sealed class CriterionRecord
    {
        /// <summary>判据编号。WC-xx = 走查判据；AX = 导表断言；FX = 打击感判据。</summary>
        public string Id;
        /// <summary>一句话标题。</summary>
        public string Title;
        /// <summary>判据正文，必须写清量纲与时钟轴。</summary>
        public string Statement;
        /// <summary>量纲。</summary>
        public QaUnit Unit = QaUnit.None;
        /// <summary>时钟轴。</summary>
        public QaClockAxis Axis = QaClockAxis.None;
        /// <summary>可执行化等级。</summary>
        public CriterionExecutability Executability = CriterionExecutability.AutoScript;
        /// <summary>可执行化落点：自动 → oracle 方法；人眼 → 录屏协议编号。</summary>
        public string ExecutableAs = "";
        /// <summary>本判据自己的误判条件：什么情况下它会误杀一个正确实现。</summary>
        public string FalsePositiveCondition = "";
        /// <summary>出处（文档 + 章节）。判据必须可追溯到唯一真值源。</summary>
        public string SourceRef = "";
        /// <summary>是否一票否决项。</summary>
        public bool Veto = false;
        /// <summary>
        /// 本条走查判据由哪些**可脚本判据**落地（oracle 判据编号）。
        /// 空数组 = 该条是观察项 / 定位通道，本身不构成 Gate（必须在文档里写明豁免理由）。
        /// </summary>
        public List<string> LinkedOracleIds = new List<string>();
        /// <summary>豁免负向用例的理由（仅观察项填写）。</summary>
        public string NegativeCaseWaiver = "";
        /// <summary>
        /// 正本编号（GDD / IMPL 里已有的判据 ID，如 C2、D1a、D2、B2、E3）。
        /// 【口径】<see cref="Id"/> 是**内部稳定引用键**（用于负向用例、文档、日志的交叉引用），
        /// 本字段是**对外正本编号**。两者都要留：正本编号会随规格改版变化，
        /// 内部键不能跟着变（否则历史回归用例全部失效）。
        /// 没有正本编号的填 "—"（表示该走查项在 GDD 里只有文字条款、尚未被编号）。
        /// </summary>
        public string CanonicalId = "—";
        /// <summary>
        /// 对位断言（EL-03 §2 的 A19–A43）。
        /// 【为什么要有这一列】EL-03 §5 的 CI 判据矩阵已改为**引用式**：
        /// 判据逻辑的唯一真值源是 EL-03 §2。凡与 A19–A43 重叠的部分，这里只**引用编号**，
        /// 不重新定义判据——否则同一个数会有两个来源。没有对位断言的填空数组。
        /// </summary>
        public List<string> RelatedAssertionIds = new List<string>();
        /// <summary>
        /// GDD / IMPL 里已有的判据编号（如 C2、D1a、D1b、D2、D5、B1、B2、E3）。
        /// 【为什么要保留这一列】设计线裁定「正本编号沿用 GDD 已有 ID，内部临时号映射后替换」；
        /// 主理人裁定「CriterionCatalog.cs 就是编号的唯一真值源」。两条不冲突的正确做法是：
        /// **代码目录是正本**，同时**把 GDD 已有编号作为映射列保留在同一条记录上**——
        /// 这样 GDD / IMPL / 走查表三处只需改成引用本目录的 id，而不必各自再维护一套编号。
        /// 没有 GDD 对应编号的填空串。
        /// </summary>
        public string LegacyDocId = "";
        /// <summary>
        /// 该判据是否**确实不涉及任何时钟轴**（纯数据层判据，如描边结构、材质槽数量）。
        /// 为 false 时，时钟轴必须显式声明 —— 这是"量纲 + 时钟轴"铁律的例外通道，
        /// 用它来防止把"忘了写时钟轴"伪装成"不需要时钟轴"。
        /// </summary>
        public bool AxisNotApplicable = false;

        public QaQuantity Quantity { get { return new QaQuantity(Unit, Axis); } }
    }

    /// <summary>
    /// 一条负向用例。与判据成对入库，缺一不可（GDD §12.1 负向用例强制）。
    /// </summary>
    public sealed class NegativeCaseRecord
    {
        public string Id;
        public NegativeCaseClass Class = NegativeCaseClass.Sensitivity;
        /// <summary>被验证的判据编号。</summary>
        public string TargetCriterionId = "";
        /// <summary>注入什么（一次只改一处）。</summary>
        public string Injection = "";
        /// <summary>期望结果。</summary>
        public string Expected = "";
        /// <summary>没有报错说明什么。</summary>
        public string MeaningIfNotFired = "";
        /// <summary>仅 B 类必填：哪条合法设计会被这条判据误杀。B 类的收录原则。</summary>
        public string LegalDesignAtRisk = "";
        /// <summary>出处。</summary>
        public string SourceRef = "";
        /// <summary>执行通道：脚本 / 人眼协议。</summary>
        public CriterionExecutability Channel = CriterionExecutability.AutoScript;
        /// <summary>对位断言（同 <see cref="CriterionRecord.RelatedAssertionIds"/>）。</summary>
        public List<string> RelatedAssertionIds = new List<string>();
    }

    /// <summary>
    /// 规格常量（跨域数字的唯一冻结版本）。
    /// README 规矩 1：跨域数字只认规格卡与单测里的版本——
    /// 因此这份常量表就是测试侧的规格卡，生产侧必须与它逐项相等（由 ProductionParityTests 强制）。
    /// </summary>
    public static class CombatSpec
    {
        // ── 时基 ───────────────────────────────────────────────────────────
        public const int LogicFps = 60;
        /// <summary>最低硬件档帧率。峰值类判据一律用最坏档验算（GDD 治理规则 5）。</summary>
        public const float WorstCaseFps = 30f;

        // ── 顿帧 / 硬直 / hold（IMPL v3.0 §3.1） ─────────────────────────
        public const float HitstopTimeScale = 0.15f;
        public const int HitstopFramesLight = 2;
        public const int HitstopFramesMedium = 4;
        public const int HitstopFramesHeavy = 6;
        public const int HitstunFramesLight = 18;
        public const int HitstunFramesMedium = 20;
        public const int HitstunFramesHeavy = 22;
        public const float HoldDurationMin = 2.0f;
        public const float HoldDurationMax = 4.0f;
        public const float HoldDurationLight = 3.3f;
        public const float HoldDurationMedium = 3.6f;
        public const float HoldDurationHeavy = 3.9f;

        // ── 击退三档（IMPL v3.0 §3.2） ────────────────────────────────────
        public const float KnockbackDistMinLight = 0.3f;
        public const float KnockbackDistMaxLight = 0.6f;
        public const float KnockbackDistMinMedium = 0.8f;
        public const float KnockbackDistMaxMedium = 1.2f;
        public const float KnockbackDistMinHeavy = 1.2f;
        public const float KnockbackDistMaxHeavy = 1.5f;
        public const float KnockbackPeakSpeedMax = 18f;          // m/s
        public const float KnockbackPerFrameMaxAtWorstFps = 0.5f; // m/帧 @30fps
        public const float KnockbackFrontRatio = 0.12f;
        /// <summary>后段窗口固定 6 帧（GDD §7.3 曲线形状）。</summary>
        public const int KnockbackRearWindowFrames = 6;
        /// <summary>位移归零 → 收身起始 的最小空档（帧，W 轴）。</summary>
        public const float RecoveryGapMin = 2.0f;

        // ── 命中反馈（IMPL v3.0 §3.1、GDD §1.2 P1） ─────────────────────
        public const int HitFeedbackMaxFrames = 3;
        public const int HitWeightReadMaxFrames = 12;
        public const int DodgeInvulnMaxSpanFrames = 14;  // 0.23s 上限（导表断言 #3）
        public const int PerfectDodgeWindowFrames = 6;

        // ── 前摇 / 可读性 ───────────────────────────────────────────────
        public const int MinWindupFrames = 8;
        public const int BossRangeReadMaxFrames = 30;
        public const int EnemyTelegraphMinFrames = 24;

        // ── 刀光（GDD §7.2 / §12.3 错例 5） ─────────────────────────────
        public const float BladeLightOcclusionMax = 0.30f;   // 遮挡比，不是占屏比
        public const float BladeLightHeightRatio = 1.2f;
        public const float BladeLightMinClearance = 0.5f;

        // ── 描边 / 脉冲（导表断言 #6/#7/#8） ────────────────────────────
        public const int OutlinePxArmor = 4;
        public const int OutlinePxInvuln = 2;
        public const float PulseHzMax = 3f;
        public const int OutlineStyleSingleSolid = 0;
        public const int OutlineStyleDoubleHollow = 1;

        // ── 输入延迟（E3） ──────────────────────────────────────────────
        public const int InputLatencyMaxMs = 50;

        // ── 副本门槛（GDD §9.4 #11 / §12.1 ③） ─────────────────────────
        public const float CpThresholdMinRatio = 1.3f;

        // ── 精英（IMPL v3.0 §4 / GDD §7.4） ─────────────────────────────
        public const float EliteScale = 1.4f;

        // ── 闪避蹬地窗口（GDD §7.3） ─────────────────────────────────────
        public const float DodgePushWindowRatioMax = 0.05f;

        // ── 击退方向容差（8 向含斜向，最近 45° 扇区）──────────────────────
        public const float KnockbackDirectionToleranceDeg = 45f;

        // ── 墨线（GDD §8.1） ─────────────────────────────────────────────
        public const int InkOutlinePx = 3;
        public const float InkContrastMin = 0.6f;

        // ── 受击变体（GDD §7.3） ─────────────────────────────────────────
        public const float HitClipContactHeightMinCm = 3.0f;
        public const int HitClipContactFramesMin = 2;
        public const int HitClipVariantCountMin = 4;

        // ── 曲线积分（A24） ─────────────────────────────────────────────
        public const float CurveIntegralTolerance = 0.05f;

        // ── hitstop 区间（A30） ──────────────────────────────────────────
        public const float HitstopMin = 0.05f;
        public const float HitstopMax = 0.25f;

        // ── 技传 / 图集配额（EL-03 §2 A43） ─────────────────────────────
        public const int DefaultVfxGraphicBudget = 8;
        public const int DefaultVfxGenericBudget = 2;
        public const int DefaultDropPityThreshold = 5;

        /// <summary>
        /// 派生条目清单中标 new_graphic 的条数（IMPL-v3.0 §4.8.5，实况 6）。
        /// A33-5 拿它比对；A43 的配额账另在 A43。
        /// </summary>
        public const int DerivativeListNewGraphicCount = 6;

        /// <summary>收身起始进度基准（帧）。D-05 指出它不该被写死，故在此集中登记。</summary>
        public const float RecoveryStartProgressBase = 9f;
    }

    /// <summary>
    /// A35 的 <c>mechanic_mod</c> 合法枚举全集。
    ///
    /// 【唯一真值源】枚举本体在生产侧 <c>EdgeChronicles.Data.MechanicMod</c>（28 项，位序即顺序）。
    /// 本测试侧**不再复制一份清单**——复制即制造第二个真值源（GDD 治理规则 4「同一规格只在一处描述」）。
    /// 下面是它的**只读断言**：生产侧枚举一旦增删项，这条测试立刻红，要求同步更新判据目录。
    /// </summary>
    public static class MechanicModCatalog
    {
        /// <summary>EL-03 §2.1 规定的 28 项（顺序 = 位序）。这是断言基准，不是第二份实现。</summary>
        public static readonly string[] Expected =
        {
            // 通用机制项（16）
            "MM_ATK_NO_INTERRUPT_IN_DOMAIN",
            "MM_FINISH_COST_MINUS1",
            "MM_PERFECT_DODGE_REFUND1",
            "MM_RESOURCE_CAP_PLUS1",
            "MM_CORE_CD_MINUS_FRAMES",
            "MM_DOMAIN_LAST_JUMP_DOUBLE",
            "MM_CHANNEL_INTERVAL_HALF",
            "MM_DASH_IFRAME_PLUS_FRAMES",
            "MM_BIND_TRIGGER_VARIANT",
            "MM_FINISH_EXTRA_DOMAIN",
            "MM_DOMAIN_HIT_GAIN1",
            "MM_GUARD_REFUND_IN_DOMAIN",
            "MM_HEAVY_GAIN_EXTRA",
            "MM_CANCEL_WINDOW_WIDEN",
            "MM_DODGE_CD_MINUS_FRAMES",
            "MM_CRIT_IN_DOMAIN",
            // 星盘机制项（6）
            "MM_STAR_BREAKSTATE",
            "MM_STAR_SHIELD_BREAK_FIRST",
            "MM_STAR_LIFESTEAL_CONTINUE",
            "MM_STAR_CHANNEL_MOVE",
            "MM_STAR_OVERCAP_TO_DAMAGE",
            "MM_STAR_CANCEL_ALL_AFTER_N",
            // 络脉机制项（6）
            "MM_MER_CONDUCE_SKILL",
            "MM_MER_CONDUCE_RANGE",
            "MM_MER_CONDUCE_STACK",
            "MM_MER_STACK_REFUND",
            "MM_MER_CONDUCE_FINISH",
            "MM_MER_FINISH_CHAIN"
        };

        /// <summary>判定：给定 id 是否在合法集合内（空串 = 无修改，合法）。</summary>
        public static bool IsLegal(string mechanicMod)
        {
            if (string.IsNullOrEmpty(mechanicMod)) { return true; }
            for (int i = 0; i < Expected.Length; i++)
            {
                if (Expected[i] == mechanicMod) { return true; }
            }
            return false;
        }
    }

    /// <summary>
    /// 取消窗口规则类别（IMPL v3.0 §1.3：25 组 = 16 不可 + 9 仅命中可，其余默认放行）。
    /// </summary>
    public enum CancelRuleKind
    {
        Allowed = 0,     // 默认放行
        HitOnly = 1,     // 仅命中可
        Forbidden = 2    // 不可
    }

    /// <summary>3 档击退档位。</summary>
    public enum KnockbackTier
    {
        Light = 0,
        Medium = 1,
        Heavy = 2
    }

    /// <summary>
    /// 判据触发的结果集合。Oracle 返回被触发的判据编号集合（"报红"的判据）。
    /// </summary>
    public sealed class CriterionVerdict
    {
        public readonly HashSet<string> Fired = new HashSet<string>(StringComparer.Ordinal);

        public bool IsFired(string criterionId)
        {
            return Fired.Contains(criterionId);
        }

        public void Add(string criterionId)
        {
            if (!string.IsNullOrEmpty(criterionId)) { Fired.Add(criterionId); }
        }

        public List<string> Sorted()
        {
            var list = new List<string>(Fired);
            list.Sort(StringComparer.Ordinal);
            return list;
        }

        public override string ToString()
        {
            return string.Join(",", Sorted().ToArray());
        }
    }
}
