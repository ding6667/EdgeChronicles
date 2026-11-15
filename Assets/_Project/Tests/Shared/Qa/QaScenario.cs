using System;
using System.Collections.Generic;

namespace EdgeChronicles.Tests.Qa
{
    // ─────────────────────────────────────────────────────────────────────
    // 判据用的最小中立数据模型。
    //
    // 【为什么是"中立模型"而不是生产 POCO】
    // 本项目里判据与判据的**证伪装置**都必须存在（见 GDD §12.1 ④）：
    //   · 生产侧实现 = 断言本体（engineering-lead 落地）
    //   · 测试侧实现 = 参考执行器（oracle）——用来证明"这条判据本身能抓错、也能不误杀"
    // 两边若只存在一个实现，这条判据就无法被证伪，等于没有。
    //
    // 【它不是第二个真值源】
    // 这些行模型只承载**负向用例注入所需的最小字段**；列的取值范围与枚举一律引自
    // IMPL-v2.0 §5 / IMPL-v3.0-EL-03 §1。生产侧 parity 测试（ProductionParityTests）
    // 负责保证"两边对同一场景给出同一结论"——结论一致才允许合入。
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>命中形状（IMPL v2.0 §3.1 hitShapes[]）。</summary>
    public sealed class QaHitShapeRow
    {
        public string Shape = "sector";       // sector / capsule / rect
        public float Radius = 2.4f;           // m
        public float Angle = 120f;            // °（sector）
        public float OffsetY = 1.2f;          // m
        public int Frame = 0;                 // 判定帧（相对 active 段入点）
        public float Damage = 34f;
        public string Knockback = "heavy";
        public float Hitstop = 0.15f;         // s
        public int MultiHit = 0;
    }

    /// <summary>技能行（IMPL v2.0 §3.1 skill）。</summary>
    public sealed class QaSkillRow
    {
        public string Id = "sk_xxx";
        public int Startup = 8;
        public int Active = 6;
        public int Recovery = 14;
        public List<string> EntryTags = new List<string>();
        public List<QaHitShapeRow> HitShapes = new List<QaHitShapeRow>();
        public int OnCastResource = 0;
        public int OnHitResource = 0;
        public bool WritesCoreState = false;
        public bool ConsumeStacks = false;
        public string MovementCurve = "mc_xxx";
        public List<string> DerivativeIds = new List<string>();
        public int SegmentCount = 1;

        public int TotalFrames { get { return Startup + Active + Recovery; } }
    }

    /// <summary>取消窗口行（IMPL v2.0 §5 表 3 cancel_window）。</summary>
    public sealed class QaCancelWindowRow
    {
        public string SourceTag = "light";
        public string AllowTag = "dodge";
        public int FromFrame = 0;
        public int ToFrame = 8;
        public int ResourceCost = 0;
    }

    /// <summary>
    /// 位移曲线行（IMPL v2.0 §5 表 5 movement_curve；生产侧 <c>EdgeChronicles.Data.MovementCurveRow</c>）。
    ///
    /// 【口径声明 · D-02】生产 schema 把 keys.value 写成"归一化速度（0–1）"，
    /// 但 A24（积分距离 vs 声明距离）只有在 keys 是**逐帧归一化位移占比（Σ = 1）**时才成立。
    /// 本测试侧按**归一化位移占比**实现：积分距离 = Σ(keys) × 声明距离。
    /// 若最终裁定为"速度"，则 A24 的等式不成立（Σv × d ≠ d），必须重写该判据而不是改阈值。
    /// </summary>
    public sealed class QaMovementCurveRow
    {
        public string CurveId = "mc_xxx";
        /// <summary>逐帧归一化位移占比（无量纲；Σ 应为 1）。</summary>
        public List<float> Keys = new List<float>();
        /// <summary>声明的位移距离（m）。</summary>
        public float DeclaredDistance = 3.0f;
        public string ClampMode = "clamp";
    }

    /// <summary>场景机关行（IMPL v2.0 §5 表 11 anchor）。</summary>
    public sealed class QaAnchorRow
    {
        public string AnchorId = "anchor://puzzle/pillar_01";
        public string Tag = "puzzle";
        public string InitState = "Intact";
    }

    /// <summary>Boss 行（IMPL v2.0 §5 表 9 boss）。</summary>
    public sealed class QaBossRow
    {
        public string BossId = "boss_01";
        public int Hp = 1000;
        public int PhaseCount = 3;
        public string ArenaAnchor = "anchor://arena/main";
        public string BrainRef = "boss_01_shifo";
    }

    /// <summary>Boss 规则行（IMPL v2.0 §4 Trigger/Condition/Effect）。</summary>
    public sealed class QaBossRuleRow
    {
        public string RuleId = "rule_01";
        public string BossId = "boss_01";
        public int Phase = 1;
        public string Trigger = "OnTimer";
        public List<string> Conditions = new List<string>();
        public List<string> Effects = new List<string>();
        /// <summary>ForcePhase 的目标阶段（解析自 Effects）。</summary>
        public int ForcePhaseTarget = 0;
        /// <summary>规则引用到的 anchor:// URI。</summary>
        public List<string> ReferencedAnchors = new List<string>();
    }

    /// <summary>副本门槛行（GDD §9.4 #11）。</summary>
    public sealed class QaDungeonRow
    {
        public string DungeonId = "dungeon_01";
        public int CpThreshold = 100;
        /// <summary>该档预期总战力（判据 #11 的第二个条件）。</summary>
        public int ExpectedTotalPower = 390;
    }

    /// <summary>特效资源规格（IMPL v3.0 §3.4、导表断言 #6/#7/#8）。</summary>
    public sealed class QaVfxAssetRow
    {
        public string VfxId = "vfx_xxx";
        public int OutlinePx = 2;
        /// <summary>0 = 单边实描；1 = 双边中空。</summary>
        public int OutlineStyle = CombatSpec.OutlineStyleDoubleHollow;
        public float PulseHz = 0f;
        /// <summary>窗口时长（帧，W 轴）。</summary>
        public int WindowFrames = 5;
    }

    /// <summary>技能行上的两列特效引用（armor_vfx_id / invuln_vfx_id）。</summary>
    public sealed class QaVfxSpecRow
    {
        public string SkillId = "sk_xxx";
        public string ArmorVfxId = "vfx_armor";
        public string InvulnVfxId = "vfx_invuln";
    }

    /// <summary>受击 / 击退规格行（IMPL v3.0 §3.1/§3.2、GDD §7.3）。</summary>
    public sealed class QaKnockbackRow
    {
        public KnockbackTier Tier = KnockbackTier.Light;
        public int HitstunFrames = CombatSpec.HitstunFramesLight;
        public int HitstopFrames = CombatSpec.HitstopFramesLight;
        public float HitstopTimeScale = CombatSpec.HitstopTimeScale;
        public float HoldDuration = CombatSpec.HoldDurationLight;
        public float DistMin = CombatSpec.KnockbackDistMinLight;
        public float DistMax = CombatSpec.KnockbackDistMaxLight;
        /// <summary>派生列：必须等于 hitstop_frames。</summary>
        public int SplitFrame = CombatSpec.HitstopFramesLight;
        /// <summary>派生列：hitstop_frames + 6。</summary>
        public int CurveFrames = CombatSpec.HitstopFramesLight + CombatSpec.KnockbackRearWindowFrames;
        /// <summary>收身起始进度（剪辑帧 C 轴，进度 = 帧号 − 1）。</summary>
        public float RecoveryStartProgress = 9f;
        /// <summary>峰值速度（m/s）。</summary>
        public float PeakSpeed = 5.3f;
    }

    /// <summary>闪避规格行（GDD §7.2/§7.3、导表断言 #2/#3）。</summary>
    public sealed class QaDodgeSpec
    {
        public int TotalFrames = 22;
        public int MoveFrames = 16;
        public int InvulnStart = 2;
        public int InvulnEnd = 16;
        public float Distance = 3.4f;
        /// <summary>前 2 帧蹬地窗口的位移占比上限。</summary>
        public float PushWindowRatioMax = 0.05f;
    }

    /// <summary>技传阶位行（EL-03 §1.3 tech_skill）。</summary>
    public sealed class QaDerivativeRow
    {
        public string SkillId = "sk_shifeng";
        public int Tier = 1;
        /// <summary>stat / resource / derivative / window / mechanic。</summary>
        public string UnlockType = "stat";
        /// <summary>none / param / generic / new_graphic。</summary>
        public string VisualTier = "none";
        public string UnlockDerivativeId = "";
        public List<string> MechanicTargets = new List<string>();
        public int CostEu = 1;
    }

    /// <summary>多来源效果来源（EL-03 §2 A39）。</summary>
    public sealed class QaEffectSourceRow
    {
        /// <summary>affix_pool / meridian / star_node / tech_skill / specialty。</summary>
        public string SourceKind = "affix_pool";
        public string EffectKey = "";
        public float EffectValue = 0f;
    }

    /// <summary>掉落保底状态（EL-03 §2 A40）。</summary>
    public sealed class QaDropPityRow
    {
        public string MaterialKey = "mat_vein_point";
        public int Counter = 0;
        public int Threshold = CombatSpec.DefaultDropPityThreshold;
        /// <summary>最近 N 次结算是否掉到过该材料。</summary>
        public List<bool> RecentDrops = new List<bool>();
    }

    /// <summary>星盘节点行（EL-03 §1.1）。</summary>
    public sealed class QaStarNodeRow
    {
        public string NodeId = "sn_blade_1";
        public string Series = "blade";
        public List<string> PrereqIds = new List<string>();
        public int UnlockTier = 1;
        public string EffectKey = "";
        public string MechanicMod = "";
    }

    /// <summary>全局调参行（EL-03 §1.9 global_tuning）。</summary>
    public sealed class QaGlobalTuningRow
    {
        public int VfxGraphicBudget = CombatSpec.DefaultVfxGraphicBudget;
        public int VfxGenericBudget = CombatSpec.DefaultVfxGenericBudget;
        public int DropPityThreshold = CombatSpec.DefaultDropPityThreshold;
    }

    /// <summary>
    /// 一次判据求值的完整场景。所有字段都有"合法基线"默认值——
    /// 负向用例只需改**一处**（GDD §12.1 负向用例规则 1）。
    /// </summary>
    public sealed class QaScenario
    {
        public List<QaSkillRow> Skills = new List<QaSkillRow>();
        public List<QaCancelWindowRow> CancelWindows = new List<QaCancelWindowRow>();
        public List<QaMovementCurveRow> MovementCurves = new List<QaMovementCurveRow>();
        public List<QaBossRow> Bosses = new List<QaBossRow>();
        public List<QaBossRuleRow> BossRules = new List<QaBossRuleRow>();
        public List<QaAnchorRow> Anchors = new List<QaAnchorRow>();
        public List<QaDungeonRow> Dungeons = new List<QaDungeonRow>();
        public List<QaVfxSpecRow> VfxSpecs = new List<QaVfxSpecRow>();
        public List<QaVfxAssetRow> VfxAssets = new List<QaVfxAssetRow>();
        public List<QaKnockbackRow> Knockbacks = new List<QaKnockbackRow>();
        public List<QaDerivativeRow> Derivatives = new List<QaDerivativeRow>();
        public List<QaEffectSourceRow> EffectSources = new List<QaEffectSourceRow>();
        public List<QaDropPityRow> DropPity = new List<QaDropPityRow>();
        public List<QaStarNodeRow> StarNodes = new List<QaStarNodeRow>();
        public QaGlobalTuningRow GlobalTuning = new QaGlobalTuningRow();
        public QaDodgeSpec Dodge = new QaDodgeSpec();

        /// <summary>A29：连段模拟结束后的资源值。</summary>
        public int ResourceAfterCombo = 0;
        /// <summary>E3：两条时间戳管线是否独立（非同源）。</summary>
        public bool InputTimestampPipelinesIndependent = true;
        /// <summary>E3：注入的输入延迟（帧，W 轴）。</summary>
        public int InjectedInputLatencyFrames = 0;
        /// <summary>E3：实测出来的输入延迟（ms）。同源时恒为 0。</summary>
        public float MeasuredInputLatencyMs = 0f;
        /// <summary>刀光被角色剪影遮挡的比例（0..1，命中判定帧采样）。</summary>
        public float BladeLightOcclusionRatio = 0f;
        /// <summary>刀光投影占屏比例（0..1）——只用于证明判据量的是遮挡比而不是占屏比。</summary>
        public float BladeLightScreenCoverageRatio = 0f;
        /// <summary>运行帧率档（用于逐帧量的最坏档 / 实际档双验算）。</summary>
        public float RuntimeFps = CombatSpec.WorstCaseFps;
        /// <summary>D5 对照组是否同时消除了颜色 / 描边形态 / 入窗时机三个通道。</summary>
        public bool D5ControlEliminatedColor = true;
        public bool D5ControlEliminatedOutlineForm = true;
        public bool D5ControlEliminatedEntryTiming = true;

        // ── 走查判据所需的补充场景字段 ────────────────────────────────
        /// <summary>闪避前 2 帧蹬地窗口的位移占比（比例，W 轴）。</summary>
        public float DodgePushWindowRatio = 0.03f;
        /// <summary>命中帧 → 受击动画起始的间隔（帧，W 轴）。</summary>
        public int HitFeedbackFrames = 2;
        /// <summary>实际击退方向（度，逻辑平面，0 = +X）。</summary>
        public float KnockbackDirectionDeg = 180f;
        /// <summary>伤害来源方向（度，0 = 伤害来自 +X 侧）。</summary>
        public float DamageSourceDirectionDeg = 0f;
        /// <summary>击退是否把受击者推进了墙内。</summary>
        public bool KnockbackPushesIntoWall = false;
        /// <summary>击退是否把受击者推落了平台。</summary>
        public bool KnockbackFallsOffPlatform = false;
        /// <summary>Boss 攻击预警的提前帧数（帧，W 轴）。</summary>
        public int BossTelegraphFrames = 30;
        /// <summary>预警的三通道：地面圈 / 形状 / 音效。</summary>
        public bool TelegraphHasGroundCircle = true;
        public bool TelegraphHasShape = true;
        public bool TelegraphHasAudio = true;
        /// <summary>墨线与背景的亮度差（比例，0..1）。</summary>
        public float InkContrastRatio = 0.72f;
        /// <summary>墨线宽度（px，屏幕空间）。</summary>
        public int InkOutlinePx = CombatSpec.InkOutlinePx;
        /// <summary>单个渲染器使用的 Material 槽数。</summary>
        public int MaterialSlotCount = 1;
        /// <summary>是否存在 AdditionalMaterial（额外材质）。</summary>
        public bool HasAdditionalMaterial = false;
        /// <summary>是否启用了 Clear Coat。</summary>
        public bool ClearCoatEnabled = false;
        /// <summary>精英受击盒的缩放倍率。</summary>
        public float EliteColliderScale = CombatSpec.EliteScale;
        /// <summary>精英渲染体的缩放倍率。</summary>
        public float EliteVisualScale = CombatSpec.EliteScale;
        /// <summary>受击动画变体数量（正向 / 反向 × 左右镜像）。</summary>
        public int HitClipVariantCount = CombatSpec.HitClipVariantCountMin;
        /// <summary>反向变体脚底接触点的最低离地高度（cm）。</summary>
        public float HitClipReverseContactHeightCm = 4.0f;
        /// <summary>反向变体中接触点持续离地 ≥3cm 的连续帧数。</summary>
        public int HitClipReverseContactFrames = CombatSpec.HitClipContactFramesMin;
    }
}
