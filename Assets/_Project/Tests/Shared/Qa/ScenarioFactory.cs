using System;
using System.Collections.Generic;
using System.Text;

namespace EdgeChronicles.Tests.Qa
{
    /// <summary>
    /// 夹具工厂。
    ///
    /// 【硬要求】夹具必须能构造**非法态**——否则 A / B 类负向用例无法跑，
    /// 判据就永远得不到证伪（GDD §12.1 ④）。
    /// 因此每个工厂都成对提供：`…Legal()` 与 `…Illegal()`，
    /// 非法态与合法态只差**一处**（GDD §12.1 负向用例规则 1：一次只改一处）。
    /// </summary>
    public static class ScenarioFactory
    {
        // ─────────────────────────────────────────────────────────────────
        // 技能表（IMPL-v3.0 §1.2）
        // ─────────────────────────────────────────────────────────────────
        public static readonly string[] SkillIds =
        {
            "sk_shifeng",  // 拾锋式 · 核心·兑现
            "sk_xunfeng",  // 迅锋 · 生成
            "sk_lieshi",   // 裂石 · 生成
            "sk_huifeng",  // 回锋 · 位移·生成
            "sk_fufeng",   // 缚锋 · 控场
            "sk_baoyuan",  // 抱元 · 防御
            "sk_jufeng",   // 聚锋 · 生成·引导
            "sk_pofeng"    // 破锋 · 终结
        };

        public static readonly string[] SkillTags =
        {
            "core", "light", "heavy", "dash", "control", "guard", "channel", "finisher"
        };

        /// <summary>
        /// 取消窗口规则（IMPL-v3.0 §1.3：16 组「不可」+ 9 组「仅命中可」，其余默认放行）。
        /// 键为 "sourceTag→allowTag"。
        /// </summary>
        public static readonly KeyValuePair<string, CancelRuleKind>[] CancelRules =
        {
            // —— 不可（16 组）——
            new KeyValuePair<string, CancelRuleKind>("core→light", CancelRuleKind.Forbidden),
            new KeyValuePair<string, CancelRuleKind>("core→heavy", CancelRuleKind.Forbidden),
            new KeyValuePair<string, CancelRuleKind>("core→dash", CancelRuleKind.Forbidden),
            new KeyValuePair<string, CancelRuleKind>("core→control", CancelRuleKind.Forbidden),
            new KeyValuePair<string, CancelRuleKind>("core→guard", CancelRuleKind.Forbidden),
            new KeyValuePair<string, CancelRuleKind>("core→channel", CancelRuleKind.Forbidden),
            new KeyValuePair<string, CancelRuleKind>("core→finisher", CancelRuleKind.Forbidden),
            new KeyValuePair<string, CancelRuleKind>("finisher→heavy", CancelRuleKind.Forbidden),
            new KeyValuePair<string, CancelRuleKind>("finisher→dash", CancelRuleKind.Forbidden),
            new KeyValuePair<string, CancelRuleKind>("finisher→control", CancelRuleKind.Forbidden),
            new KeyValuePair<string, CancelRuleKind>("finisher→guard", CancelRuleKind.Forbidden),
            new KeyValuePair<string, CancelRuleKind>("finisher→channel", CancelRuleKind.Forbidden),
            new KeyValuePair<string, CancelRuleKind>("finisher→finisher", CancelRuleKind.Forbidden),
            new KeyValuePair<string, CancelRuleKind>("heavy→guard", CancelRuleKind.Forbidden),
            new KeyValuePair<string, CancelRuleKind>("guard→core", CancelRuleKind.Forbidden),
            new KeyValuePair<string, CancelRuleKind>("channel→heavy", CancelRuleKind.Forbidden),
            // —— 仅命中可（9 组）——
            new KeyValuePair<string, CancelRuleKind>("heavy→core", CancelRuleKind.HitOnly),
            new KeyValuePair<string, CancelRuleKind>("heavy→dash", CancelRuleKind.HitOnly),
            new KeyValuePair<string, CancelRuleKind>("heavy→control", CancelRuleKind.HitOnly),
            new KeyValuePair<string, CancelRuleKind>("heavy→channel", CancelRuleKind.HitOnly),
            new KeyValuePair<string, CancelRuleKind>("heavy→finisher", CancelRuleKind.HitOnly),
            new KeyValuePair<string, CancelRuleKind>("control→core", CancelRuleKind.HitOnly),
            new KeyValuePair<string, CancelRuleKind>("control→channel", CancelRuleKind.HitOnly),
            new KeyValuePair<string, CancelRuleKind>("guard→heavy", CancelRuleKind.HitOnly),
            new KeyValuePair<string, CancelRuleKind>("guard→finisher", CancelRuleKind.HitOnly)
        };

        public static CancelRuleKind RuleOf(string sourceTag, string allowTag)
        {
            for (int i = 0; i < CancelRules.Length; i++)
            {
                if (CancelRules[i].Key == sourceTag + "→" + allowTag) { return CancelRules[i].Value; }
            }
            return CancelRuleKind.Allowed;
        }

        // ─────────────────────────────────────────────────────────────────
        // 通用合法基线场景（所有负向用例的起点）
        // ─────────────────────────────────────────────────────────────────

        /// <summary>一张"全部合法"的表集合。负向用例在其上**只改一处**。</summary>
        public static QaScenario LegalBaseline()
        {
            var s = new QaScenario();

            // ── 8 个技能（IMPL-v3.0 §1.2 的前摇 / 判定 / 后摇）──
            int[,] phases =
            {
                { 16, 6, 22 },   // 拾锋式
                { 8, 3, 10 },    // 迅锋 第 1 段
                { 20, 8, 24 },   // 裂石
                { 10, 6, 16 },   // 回锋
                { 12, 5, 18 },   // 缚锋
                { 8, 12, 14 },   // 抱元
                { 10, 45, 12 },  // 聚锋
                { 14, 8, 20 }    // 破锋
            };
            for (int i = 0; i < SkillIds.Length; i++)
            {
                var sk = new QaSkillRow();
                sk.Id = SkillIds[i];
                sk.Startup = phases[i, 0];
                sk.Active = phases[i, 1];
                sk.Recovery = phases[i, 2];
                sk.EntryTags.Add(SkillTags[i]);
                sk.MovementCurve = "mc_" + SkillIds[i].Substring(3);

                var hit = new QaHitShapeRow();
                hit.Shape = (i == 5) ? "capsule" : "sector";  // 抱元 = 自身霸体 → 胶囊
                hit.Radius = (i == 5) ? 1.2f : 2.4f;
                hit.Angle = 120f;
                hit.OffsetY = 1.2f;
                hit.Frame = 0;
                hit.Damage = 34f;
                hit.Knockback = (i == 2 || i == 7) ? "heavy" : ((i == 1 || i == 3) ? "medium" : "light");
                hit.Hitstop = 0.15f;
                sk.HitShapes.Add(hit);

                // coreLink 的 writes 与 consumeStacks 互斥：核心写状态，终结消耗层数，其余两者都不做。
                sk.WritesCoreState = (i == 0);
                sk.ConsumeStacks = (i == 7);
                s.Skills.Add(sk);
            }

            // ── 取消窗口（25 组规则落到 8 个 sourceTag 的默认窗口）──
            for (int i = 0; i < SkillIds.Length; i++)
            {
                int total = phases[i, 0] + phases[i, 1] + phases[i, 2];
                var w = new QaCancelWindowRow();
                w.SourceTag = SkillTags[i];
                // 【口径声明】A22 要求 allowTags 命中某个 skill.entryTags。
                // 8 个技能的 entryTags 为 core/light/heavy/dash/control/guard/channel/finisher，
                // **不含 "dodge"** —— IMPL-v2.0 §3.4 的样例却用了 "dodge"。
                // 该冲突已登记为 D-10；本基线取合法标签 "dash"，使基线本身是"可判定的合法态"。
                w.AllowTag = "dash";
                // 窗口必须落在该技能的可取消段（recovery）内，否则回放会记为非法相变。
                w.FromFrame = phases[i, 0] + phases[i, 1];
                w.ToFrame = total - 1;
                s.CancelWindows.Add(w);
            }

            // ── 位移曲线（积分 = 声明）──
            var mc = new QaMovementCurveRow();
            mc.CurveId = "mc_xunfeng";
            mc.Keys.AddRange(new List<float> { 0.20f, 0.40f, 0.25f, 0.10f, 0.05f }); // Σ = 1.00（归一化位移占比）
            mc.DeclaredDistance = 3.0f;
            s.MovementCurves.Add(mc);

            var mc2 = new QaMovementCurveRow();
            mc2.CurveId = "mc_huifeng";
            mc2.Keys.AddRange(new List<float> { 0.00f, 0.40f, 0.45f, 0.15f });   // Σ = 1.00（归一化位移占比）
            mc2.DeclaredDistance = 3.0f;
            s.MovementCurves.Add(mc2);

            // ── 副本门槛（IMPL-v3.0 §6 三副本：100 / 900 / 2400）──
            s.Dungeons.Add(new QaDungeonRow { DungeonId = "dungeon_luoxia", CpThreshold = 100, ExpectedTotalPower = 390 });
            s.Dungeons.Add(new QaDungeonRow { DungeonId = "dungeon_tiekuang", CpThreshold = 900, ExpectedTotalPower = 930 });
            s.Dungeons.Add(new QaDungeonRow { DungeonId = "dungeon_gufeng", CpThreshold = 2400, ExpectedTotalPower = 2604 });

            // ── Boss 与机关（IMPL-v2.0 §4.1）──
            s.Anchors.Add(new QaAnchorRow { AnchorId = "anchor://puzzle/pillar_01", Tag = "puzzle", InitState = "Intact" });
            s.Anchors.Add(new QaAnchorRow { AnchorId = "anchor://puzzle/pillar_02", Tag = "puzzle", InitState = "Intact" });
            s.Anchors.Add(new QaAnchorRow { AnchorId = "anchor://arena/web", Tag = "arena", InitState = "Idle" });
            s.Anchors.Add(new QaAnchorRow { AnchorId = "anchor://arena/main", Tag = "arena", InitState = "Idle" });

            s.Bosses.Add(new QaBossRow { BossId = "boss_01_shifo", Hp = 1000, PhaseCount = 3, ArenaAnchor = "anchor://arena/main", BrainRef = "boss_01_shifo" });
            s.Bosses.Add(new QaBossRow { BossId = "boss_02_gumu", Hp = 1200, PhaseCount = 3, ArenaAnchor = "anchor://arena/web", BrainRef = "boss_02_gumu" });
            // 哑刀共 4 段：IMPL-v2.0 §4.1 的规则在 phase 3 触发 ForcePhase → 4。
            s.Bosses.Add(new QaBossRow { BossId = "boss_03_tieyao", Hp = 1800, PhaseCount = 4, ArenaAnchor = "anchor://arena/main", BrainRef = "boss_03_tieyao" });

            s.BossRules.Add(new QaBossRuleRow
            {
                RuleId = "br_shifo_01", BossId = "boss_01_shifo", Phase = 2,
                Trigger = "OnAnchorStateChanged",
                Conditions = new List<string> { "PhaseEquals:2" },
                Effects = new List<string> { "ModifyTerrain:zone_pit_a:Open", "SpawnHazard:hz_rock:3" },
                ReferencedAnchors = new List<string> { "anchor://puzzle/pillar_02" }
            });
            s.BossRules.Add(new QaBossRuleRow
            {
                RuleId = "br_gumu_01", BossId = "boss_02_gumu", Phase = 1,
                Trigger = "OnTimer",
                Conditions = new List<string> { "StackCountGte:brood:3" },
                Effects = new List<string> { "SpawnAdd:em_broodling:2", "MovePlayer:PullTo:anchor://arena/web:3.0" },
                ReferencedAnchors = new List<string> { "anchor://arena/web" }
            });
            s.BossRules.Add(new QaBossRuleRow
            {
                RuleId = "br_gumu_02", BossId = "boss_02_gumu", Phase = 1,
                Trigger = "OnAddSpawned",
                Conditions = new List<string>(),
                Effects = new List<string> { "ApplyBuff:Self:bf_frenzy:1" }
            });
            s.BossRules.Add(new QaBossRuleRow
            {
                RuleId = "br_tieyao_01", BossId = "boss_03_tieyao", Phase = 3,
                Trigger = "OnPlayerSkillHit",
                Conditions = new List<string> { "HpBelow:0.3" },
                Effects = new List<string> { "LockZone:zone_arena:Close", "ForcePhase:4", "ShakeCamera:0.6:18" },
                ForcePhaseTarget = 4
            });

            // ── 特效规格（IMPL v3.0 §3.4 + 导表断言 #6/#7/#8）──
            // 霸体 = 赤色单边实描 4px；无敌 = 纸白双边中空 2px+2px（两者结构必须不同）。
            s.VfxAssets.Add(new QaVfxAssetRow { VfxId = "vfx_armor_generic", OutlinePx = CombatSpec.OutlinePxArmor, OutlineStyle = CombatSpec.OutlineStyleSingleSolid, PulseHz = 0f, WindowFrames = 12 });
            s.VfxAssets.Add(new QaVfxAssetRow { VfxId = "vfx_invuln_generic", OutlinePx = CombatSpec.OutlinePxInvuln, OutlineStyle = CombatSpec.OutlineStyleDoubleHollow, PulseHz = 0f, WindowFrames = 12 });
            s.VfxAssets.Add(new QaVfxAssetRow { VfxId = "vfx_armor_pofeng", OutlinePx = CombatSpec.OutlinePxArmor, OutlineStyle = CombatSpec.OutlineStyleSingleSolid, PulseHz = 0f, WindowFrames = 12 });
            s.VfxAssets.Add(new QaVfxAssetRow { VfxId = "vfx_invuln_pofeng", OutlinePx = CombatSpec.OutlinePxInvuln, OutlineStyle = CombatSpec.OutlineStyleDoubleHollow, PulseHz = 0f, WindowFrames = 12 });
            s.VfxAssets.Add(new QaVfxAssetRow { VfxId = "vfx_armor_xunfeng", OutlinePx = CombatSpec.OutlinePxArmor, OutlineStyle = CombatSpec.OutlineStyleSingleSolid, PulseHz = 0f, WindowFrames = 12 });
            s.VfxAssets.Add(new QaVfxAssetRow { VfxId = "vfx_invuln_xunfeng", OutlinePx = CombatSpec.OutlinePxInvuln, OutlineStyle = CombatSpec.OutlineStyleDoubleHollow, PulseHz = 0f, WindowFrames = 12 });

            for (int i = 0; i < SkillIds.Length; i++)
            {
                string armor = (SkillIds[i] == "sk_pofeng") ? "vfx_armor_pofeng" : ((SkillIds[i] == "sk_xunfeng") ? "vfx_armor_xunfeng" : "vfx_armor_generic");
                string invuln = (SkillIds[i] == "sk_pofeng") ? "vfx_invuln_pofeng" : ((SkillIds[i] == "sk_xunfeng") ? "vfx_invuln_xunfeng" : "vfx_invuln_generic");
                s.VfxSpecs.Add(new QaVfxSpecRow { SkillId = SkillIds[i], ArmorVfxId = armor, InvulnVfxId = invuln });
            }

            // ── 受击 / 击退三档（IMPL-v3.0 §3.1/§3.2）──
            s.Knockbacks.Add(MakeKnockback(KnockbackTier.Light));
            s.Knockbacks.Add(MakeKnockback(KnockbackTier.Medium));
            s.Knockbacks.Add(MakeKnockback(KnockbackTier.Heavy));

            // ── 闪避（GDD §7.2/§7.3）──
            s.Dodge = new QaDodgeSpec { TotalFrames = 22, MoveFrames = 16, InvulnStart = 2, InvulnEnd = 16, Distance = 3.4f };

            // ── 技传 40 阶（IMPL-v3.0 §4.5.4；阶位类型按 §4.5.3 通用模板，见 D-03）──
            BuildDerivatives(s);

            // ── 多来源效果合并（EL-03 §2 A39-1 / A39-2）──
            //   上限类 → 取最大；其余数值类 → 取最小不叠加；离散类 → 只生效一次。
            var cap = new QaEffectMergeRow { EffectKey = "resource_cap", MergeClass = EffectMergeClass.CapUpper };
            cap.SourceValues.Add(1f); cap.SourceKinds.Add("specialty");
            cap.SourceValues.Add(1f); cap.SourceKinds.Add("meridian");
            cap.MergedValue = 1f; // max(1,1)
            s.EffectMerges.Add(cap);

            var num = new QaEffectMergeRow { EffectKey = "atk_flat", MergeClass = EffectMergeClass.Numeric };
            num.SourceValues.Add(5f); num.SourceKinds.Add("affix_pool");
            num.SourceValues.Add(8f); num.SourceKinds.Add("star_node");
            num.MergedValue = 5f; // min(5,8)，不叠加
            s.EffectMerges.Add(num);

            var disc = new QaEffectMergeRow { EffectKey = "crit_flag", MergeClass = EffectMergeClass.Discrete };
            disc.SourceValues.Add(1f); disc.SourceKinds.Add("meridian");
            disc.MergedValue = 1f; // 离散类只生效一次
            s.EffectMerges.Add(disc);

            // ── 络脉节点（数值列恒空，只承载机制项）──
            s.Meridians.Add(new QaMeridianRow { NodeId = "mn_hub_1", EffectKey = "eff_resource_cap", MechanicMod = "MM_MER_CONDUCE_STACK" });
            s.Meridians.Add(new QaMeridianRow { NodeId = "mn_branch_1", EffectKey = "eff_atk_flat", MechanicMod = "MM_MER_CONDUCE_RANGE" });

            // ── 掉落保底（A40）──
            s.DropPity.Add(new QaDropPityRow { MaterialKey = "mat_vein_point", Counter = 0, Threshold = CombatSpec.DefaultDropPityThreshold });

            // ── 星盘（A31）──
            s.StarNodes.Add(new QaStarNodeRow { NodeId = "sn_blade_1", Series = "blade", UnlockTier = 1, EffectKey = "eff_combo_2hit_stack" });
            s.StarNodes.Add(new QaStarNodeRow { NodeId = "sn_blade_2", Series = "blade", UnlockTier = 2, MechanicMod = "MM_STAR_BREAKSTATE", PrereqIds = new List<string> { "sn_blade_1" } });

            // ── E3（默认：两条时间戳管线独立，延迟 0）──
            s.InputTimestampPipelinesIndependent = true;
            s.InjectedInputLatencyFrames = 0;
            s.MeasuredInputLatencyMs = 0f;

            // ── D5 对照组（默认：三个通道全部消除）──
            s.D5ControlEliminatedColor = true;
            s.D5ControlEliminatedOutlineForm = true;
            s.D5ControlEliminatedEntryTiming = true;

            s.ResourceAfterCombo = 0;
            s.BladeLightOcclusionRatio = 0.18f;
            s.BladeLightScreenCoverageRatio = 0.005f; // 竖直刀光在俯视角下投影很小——正是 B-01 要防的误杀
            return s;
        }

        public static QaKnockbackRow MakeKnockback(KnockbackTier tier)
        {
            var k = new QaKnockbackRow();
            k.Tier = tier;
            switch (tier)
            {
                case KnockbackTier.Light:
                    k.HitstunFrames = CombatSpec.HitstunFramesLight;
                    k.HitstopFrames = CombatSpec.HitstopFramesLight;
                    k.HoldDuration = CombatSpec.HoldDurationLight;
                    k.DistMin = CombatSpec.KnockbackDistMinLight;
                    k.DistMax = CombatSpec.KnockbackDistMaxLight;
                    k.PeakSpeed = 5.3f;
                    break;
                case KnockbackTier.Medium:
                    k.HitstunFrames = CombatSpec.HitstunFramesMedium;
                    k.HitstopFrames = CombatSpec.HitstopFramesMedium;
                    k.HoldDuration = CombatSpec.HoldDurationMedium;
                    k.DistMin = CombatSpec.KnockbackDistMinMedium;
                    k.DistMax = CombatSpec.KnockbackDistMaxMedium;
                    k.PeakSpeed = 10.6f;
                    break;
                default:
                    k.HitstunFrames = CombatSpec.HitstunFramesHeavy;
                    k.HitstopFrames = CombatSpec.HitstopFramesHeavy;
                    k.HoldDuration = CombatSpec.HoldDurationHeavy;
                    k.DistMin = CombatSpec.KnockbackDistMinHeavy;
                    k.DistMax = CombatSpec.KnockbackDistMaxHeavy;
                    k.PeakSpeed = 13.2f;
                    break;
            }
            k.HitstopTimeScale = CombatSpec.HitstopTimeScale;
            k.SplitFrame = k.HitstopFrames;
            k.CurveFrames = k.HitstopFrames + CombatSpec.KnockbackRearWindowFrames;
            k.RecoveryStartProgress = CombatSpec.RecoveryStartProgressBase;
            return k;
        }

        /// <summary>
        /// 技传 40 阶。
        /// 【口径声明】阶位类型按 §4.5.3 通用模板（1 stat / 2 resource / 3 derivative / 4 window / 5 mechanic），
        /// 与 A33 一致。§4.5.4 中迅锋 / 裂石 / 回锋 / 缚锋的阶位类型与此模板冲突 —— 已登记为 D-03，
        /// 本夹具按 A33 构造，使基线场景本身是"可判定的合法态"。
        /// 派生 id 与 tier 的对应按 §4.8.5，且受 A33-2 约束（派生只能落在 tier 3 或 5）。
        /// </summary>
        private static void BuildDerivatives(QaScenario s)
        {
            // 6 条 new_graphic 派生及其归属（IMPL-v3.0 §4.8.5）
            var newGraphicTier = new Dictionary<string, int>
            {
                { "sk_shifeng", 3 },  // 末跳翻倍 · 拾锋式 3 阶
                { "sk_xunfeng", 3 },  // 回响 · 迅锋 3 阶
                { "sk_fufeng", 3 },   // 破势 · 缚锋 3 阶（按 A33-2 对齐）
                { "sk_baoyuan", 5 },  // 破罩冲击 · 抱元 5 阶
                { "sk_jufeng", 5 },   // 聚锋簇 · 聚锋 5 阶
                { "sk_pofeng", 5 }    // 引爆再展域 · 破锋 5 阶
            };
            var newGraphicId = new Dictionary<string, string>
            {
                { "sk_shifeng", "dv_last_jump_double" },
                { "sk_xunfeng", "dv_echo" },
                { "sk_fufeng", "dv_po_shi" },
                { "sk_baoyuan", "dv_shield_break" },
                { "sk_jufeng", "dv_gather_cluster" },
                { "sk_pofeng", "dv_finish_extra_domain" }
            };

            for (int i = 0; i < SkillIds.Length; i++)
            {
                string skillId = SkillIds[i];
                for (int tier = 1; tier <= 5; tier++)
                {
                    var d = new QaDerivativeRow();
                    d.SkillId = skillId;
                    d.Tier = tier;
                    d.CostEu = tier <= 2 ? tier : (tier == 3 ? 3 : (tier == 4 ? 4 : 6));

                    switch (tier)
                    {
                        case 1: d.UnlockType = "stat"; d.VisualTier = "none"; break;
                        case 2: d.UnlockType = "resource"; d.VisualTier = "none"; break;
                        case 3: d.UnlockType = "derivative"; d.VisualTier = "generic"; break;
                        case 4: d.UnlockType = "window"; d.VisualTier = "param"; break;
                        default: d.UnlockType = "mechanic"; d.VisualTier = "generic"; break;
                    }

                    if (tier == 5) { d.MechanicTargets.Add("loop_l4"); } // A41：长度 ≤ 1

                    int gTier;
                    if (newGraphicTier.TryGetValue(skillId, out gTier) && gTier == tier)
                    {
                        d.VisualTier = "new_graphic";
                        d.UnlockDerivativeId = newGraphicId[skillId];
                    }

                    s.Derivatives.Add(d);
                }
            }

            // skill.derivative_ids 与 tech_skill 双向承载（EL-03 §2 A33-1）
            for (int i = 0; i < s.Skills.Count; i++)
            {
                QaSkillRow sk = s.Skills[i];
                for (int k = 0; k < s.Derivatives.Count; k++)
                {
                    if (s.Derivatives[k].SkillId == sk.Id && !string.IsNullOrEmpty(s.Derivatives[k].UnlockDerivativeId))
                    {
                        sk.DerivativeIds.Add(s.Derivatives[k].UnlockDerivativeId);
                    }
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // 夹具 1 · 受击夹具（可构造非法态）
        // ─────────────────────────────────────────────────────────────────

        /// <summary>合法受击夹具：三档全部符合 IMPL-v3.0 §3.1/§3.2。</summary>
        public static QaScenario KnockbackLegal()
        {
            return LegalBaseline();
        }

        /// <summary>
        /// 非法受击夹具：把**重档**的顿帧改成 8 帧。
        /// 这是"只改一处"的 A 类注入（NC-A17）：0.15 × 8 = 1.2 &gt; 1，因果式上限必须拦下。
        /// </summary>
        public static QaScenario KnockbackIllegalHitstopTooLong()
        {
            var s = LegalBaseline();
            for (int i = 0; i < s.Knockbacks.Count; i++)
            {
                if (s.Knockbacks[i].Tier == KnockbackTier.Heavy)
                {
                    s.Knockbacks[i].HitstopFrames = 8;
                    s.Knockbacks[i].SplitFrame = 8;
                    s.Knockbacks[i].CurveFrames = 8 + CombatSpec.KnockbackRearWindowFrames;
                }
            }
            return s;
        }

        // ─────────────────────────────────────────────────────────────────
        // 夹具 2 · 命中夹具（可构造非法态）
        // ─────────────────────────────────────────────────────────────────

        /// <summary>命中几何用例：扇形 / 胶囊 / 矩形各 20 例。</summary>
        public sealed class HitGeometryCase
        {
            public string Shape = "";
            public bool Expected = false;
            public string Note = "";
            public QaVec2 Origin;
            public QaVec2 Forward;
            public float Radius = 0f;
            public float AngleDeg = 0f;
            public QaVec2 A;
            public QaVec2 B;
            public float CapRadius = 0f;
            public QaVec2 Center;
            public float HalfX = 0f;
            public float HalfY = 0f;
            public float YawDeg = 0f;
            public QaVec2 Point;

            public bool Actual()
            {
                switch (Shape)
                {
                    case "sector": return HitGeometryReference.SectorContains(Origin, Forward, Radius, AngleDeg, Point);
                    case "capsule": return HitGeometryReference.CapsuleContains(A, B, CapRadius, Point);
                    default: return HitGeometryReference.RectContains(Center, HalfX, HalfY, YawDeg, Point);
                }
            }
        }

        private static HitGeometryCase NewCase(string shape, bool expected, string note)
        {
            var c = new HitGeometryCase();
            c.Shape = shape;
            c.Expected = expected;
            c.Note = note;
            return c;
        }

        /// <summary>扇形 20 例（半径 3m、整角 120°、中心线 +X）。</summary>
        public static List<HitGeometryCase> SectorCases()
        {
            var list = new List<HitGeometryCase>();
            QaVec2 o = new QaVec2(0f, 0f);
            QaVec2 f = new QaVec2(1f, 0f);

            var c = NewCase("sector", true, "圆心与原点重合");
            c.Origin = o; c.Forward = f; c.Radius = 3f; c.AngleDeg = 120f; c.Point = new QaVec2(0f, 0f);
            list.Add(c);

            c = NewCase("sector", true, "正前 2.9m（半径内）");
            c.Origin = o; c.Forward = f; c.Radius = 3f; c.AngleDeg = 120f; c.Point = new QaVec2(2.9f, 0f);
            list.Add(c);

            c = NewCase("sector", false, "正前 3.1m（半径外）");
            c.Origin = o; c.Forward = f; c.Radius = 3f; c.AngleDeg = 120f; c.Point = new QaVec2(3.1f, 0f);
            list.Add(c);

            c = NewCase("sector", true, "正前 3.0m（半径边界，含端点）");
            c.Origin = o; c.Forward = f; c.Radius = 3f; c.AngleDeg = 120f; c.Point = new QaVec2(3f, 0f);
            list.Add(c);

            c = NewCase("sector", true, "左边界角 60° @2m（角度边界，含端点）");
            c.Origin = o; c.Forward = f; c.Radius = 3f; c.AngleDeg = 120f; c.Point = Polar(2f, 60f);
            list.Add(c);

            c = NewCase("sector", true, "右边界角 −60° @2m（角度边界，含端点）");
            c.Origin = o; c.Forward = f; c.Radius = 3f; c.AngleDeg = 120f; c.Point = Polar(2f, -60f);
            list.Add(c);

            c = NewCase("sector", false, "左外角 61° @2m");
            c.Origin = o; c.Forward = f; c.Radius = 3f; c.AngleDeg = 120f; c.Point = Polar(2f, 61f);
            list.Add(c);

            c = NewCase("sector", false, "右外角 −61° @2m");
            c.Origin = o; c.Forward = f; c.Radius = 3f; c.AngleDeg = 120f; c.Point = Polar(2f, -61f);
            list.Add(c);

            c = NewCase("sector", true, "正前 0.5m");
            c.Origin = o; c.Forward = f; c.Radius = 3f; c.AngleDeg = 120f; c.Point = new QaVec2(0.5f, 0f);
            list.Add(c);

            c = NewCase("sector", false, "正后 1.0m（180°）");
            c.Origin = o; c.Forward = f; c.Radius = 3f; c.AngleDeg = 120f; c.Point = new QaVec2(-1f, 0f);
            list.Add(c);

            c = NewCase("sector", true, "左前 45° @2.5m");
            c.Origin = o; c.Forward = f; c.Radius = 3f; c.AngleDeg = 120f; c.Point = Polar(2.5f, 45f);
            list.Add(c);

            c = NewCase("sector", true, "右前 −45° @2.5m");
            c.Origin = o; c.Forward = f; c.Radius = 3f; c.AngleDeg = 120f; c.Point = Polar(2.5f, -45f);
            list.Add(c);

            c = NewCase("sector", false, "左前 45° @3.5m（半径外）");
            c.Origin = o; c.Forward = f; c.Radius = 3f; c.AngleDeg = 120f; c.Point = Polar(3.5f, 45f);
            list.Add(c);

            c = NewCase("sector", true, "近边缘 59° @2.99m");
            c.Origin = o; c.Forward = f; c.Radius = 3f; c.AngleDeg = 120f; c.Point = Polar(2.99f, 59f);
            list.Add(c);

            c = NewCase("sector", false, "近边缘 60.5° @2.99m");
            c.Origin = o; c.Forward = f; c.Radius = 3f; c.AngleDeg = 120f; c.Point = Polar(2.99f, 60.5f);
            list.Add(c);

            c = NewCase("sector", false, "左后 150° @2m");
            c.Origin = o; c.Forward = f; c.Radius = 3f; c.AngleDeg = 120f; c.Point = Polar(2f, 150f);
            list.Add(c);

            c = NewCase("sector", false, "右后 −150° @2m");
            c.Origin = o; c.Forward = f; c.Radius = 3f; c.AngleDeg = 120f; c.Point = Polar(2f, -150f);
            list.Add(c);

            c = NewCase("sector", true, "正前 0.001m（近原点）");
            c.Origin = o; c.Forward = f; c.Radius = 3f; c.AngleDeg = 120f; c.Point = new QaVec2(0.001f, 0f);
            list.Add(c);

            c = NewCase("sector", true, "偏心 30° @2m");
            c.Origin = o; c.Forward = f; c.Radius = 3f; c.AngleDeg = 120f; c.Point = Polar(2f, 30f);
            list.Add(c);

            c = NewCase("sector", false, "偏心 70° @2m");
            c.Origin = o; c.Forward = f; c.Radius = 3f; c.AngleDeg = 120f; c.Point = Polar(2f, 70f);
            list.Add(c);

            return list;
        }

        /// <summary>胶囊 20 例（线段 (0,0)–(4,0)，半径 0.8m）。</summary>
        public static List<HitGeometryCase> CapsuleCases()
        {
            var list = new List<HitGeometryCase>();
            QaVec2 a = new QaVec2(0f, 0f);
            QaVec2 b = new QaVec2(4f, 0f);
            const float r = 0.8f;

            AddCapsule(list, true, "中点", a, b, r, new QaVec2(2f, 0f));
            AddCapsule(list, true, "端点 A", a, b, r, new QaVec2(0f, 0f));
            AddCapsule(list, true, "端点 B", a, b, r, new QaVec2(4f, 0f));
            AddCapsule(list, true, "A 上方 0.8m（半径边界，含端点）", a, b, r, new QaVec2(0f, 0.8f));
            AddCapsule(list, false, "A 上方 0.81m", a, b, r, new QaVec2(0f, 0.81f));
            AddCapsule(list, true, "B 上方 0.8m（半径边界，含端点）", a, b, r, new QaVec2(4f, 0.8f));
            AddCapsule(list, false, "B 上方 0.81m", a, b, r, new QaVec2(4f, 0.81f));
            AddCapsule(list, true, "中点上方 0.79m", a, b, r, new QaVec2(2f, 0.79f));
            AddCapsule(list, false, "中点上方 0.81m", a, b, r, new QaVec2(2f, 0.81f));
            AddCapsule(list, false, "A 左 1.0m", a, b, r, new QaVec2(-1f, 0f));
            AddCapsule(list, false, "B 右 1.0m", a, b, r, new QaVec2(5f, 0f));
            AddCapsule(list, true, "A 左 0.8m（边界，含端点）", a, b, r, new QaVec2(-0.8f, 0f));
            AddCapsule(list, true, "B 右 0.8m（边界，含端点）", a, b, r, new QaVec2(4.8f, 0f));
            AddCapsule(list, true, "斜上方 (1, 0.5)", a, b, r, new QaVec2(1f, 0.5f));
            AddCapsule(list, false, "斜上方 (1, 0.9)", a, b, r, new QaVec2(1f, 0.9f));
            AddCapsule(list, true, "斜下方 (3, −0.5)", a, b, r, new QaVec2(3f, -0.5f));
            AddCapsule(list, false, "斜下方 (3, −0.9)", a, b, r, new QaVec2(3f, -0.9f));
            AddCapsule(list, true, "近端 (0.1, 0.78)", a, b, r, new QaVec2(0.1f, 0.78f));
            AddCapsule(list, false, "近端 (0.1, 0.85)", a, b, r, new QaVec2(0.1f, 0.85f));
            AddCapsule(list, true, "远端 (3.9, 0.78)", a, b, r, new QaVec2(3.9f, 0.78f));
            return list;
        }

        /// <summary>矩形 20 例（中心原点、半长 1.5 / 半宽 0.8、旋转 30°）。</summary>
        public static List<HitGeometryCase> RectCases()
        {
            var list = new List<HitGeometryCase>();
            QaVec2 c0 = new QaVec2(0f, 0f);
            const float hx = 1.5f;
            const float hy = 0.8f;
            const float yaw = 30f;

            AddRect(list, true, "中心", c0, hx, hy, yaw, new QaVec2(0f, 0f));
            AddRect(list, true, "局部 +X 边界 1.5", c0, hx, hy, yaw, LocalToWorld(1.5f, 0f, yaw));
            AddRect(list, false, "局部 +X 越界 1.51", c0, hx, hy, yaw, LocalToWorld(1.51f, 0f, yaw));
            AddRect(list, true, "局部 −X 边界 −1.5", c0, hx, hy, yaw, LocalToWorld(-1.5f, 0f, yaw));
            AddRect(list, false, "局部 −X 越界 −1.51", c0, hx, hy, yaw, LocalToWorld(-1.51f, 0f, yaw));
            AddRect(list, true, "局部 +Y 边界 0.8", c0, hx, hy, yaw, LocalToWorld(0f, 0.8f, yaw));
            AddRect(list, false, "局部 +Y 越界 0.81", c0, hx, hy, yaw, LocalToWorld(0f, 0.81f, yaw));
            AddRect(list, true, "局部 −Y 边界 −0.8", c0, hx, hy, yaw, LocalToWorld(0f, -0.8f, yaw));
            AddRect(list, false, "局部 −Y 越界 −0.81", c0, hx, hy, yaw, LocalToWorld(0f, -0.81f, yaw));
            AddRect(list, true, "局部角点 (1.5, 0.8)", c0, hx, hy, yaw, LocalToWorld(1.5f, 0.8f, yaw));
            AddRect(list, false, "局部角外 (1.6, 0.8)", c0, hx, hy, yaw, LocalToWorld(1.6f, 0.8f, yaw));
            AddRect(list, true, "局部角点 (1.5, −0.8)", c0, hx, hy, yaw, LocalToWorld(1.5f, -0.8f, yaw));
            AddRect(list, true, "局部 (−1.5, 0.8)", c0, hx, hy, yaw, LocalToWorld(-1.5f, 0.8f, yaw));
            AddRect(list, true, "局部 (−1.5, −0.8)", c0, hx, hy, yaw, LocalToWorld(-1.5f, -0.8f, yaw));
            AddRect(list, true, "内部 (0.7, −0.3)", c0, hx, hy, yaw, LocalToWorld(0.7f, -0.3f, yaw));
            AddRect(list, false, "外部 (0.7, −1.2)", c0, hx, hy, yaw, LocalToWorld(0.7f, -1.2f, yaw));
            AddRect(list, true, "局部 +X 边界（旋转后复验）", c0, hx, hy, yaw, LocalToWorld(1.5f, 0f, yaw));
            AddRect(list, false, "局部 +X 越界（旋转后复验）", c0, hx, hy, yaw, LocalToWorld(1.52f, 0f, yaw));
            AddRect(list, true, "内部 (0, 0.79)", c0, hx, hy, yaw, LocalToWorld(0f, 0.79f, yaw));
            AddRect(list, false, "外部 (0, 0.82)", c0, hx, hy, yaw, LocalToWorld(0f, 0.82f, yaw));
            AddRect(list, false, "外部 (1.2, 0.81)", c0, hx, hy, yaw, LocalToWorld(1.2f, 0.81f, yaw));
            return list;
        }

        private static void AddCapsule(List<HitGeometryCase> list, bool expected, string note, QaVec2 a, QaVec2 b, float r, QaVec2 p)
        {
            var c = NewCase("capsule", expected, note);
            c.A = a; c.B = b; c.CapRadius = r; c.Point = p;
            list.Add(c);
        }

        private static void AddRect(List<HitGeometryCase> list, bool expected, string note, QaVec2 center, float hx, float hy, float yaw, QaVec2 p)
        {
            var c = NewCase("rect", expected, note);
            c.Center = center; c.HalfX = hx; c.HalfY = hy; c.YawDeg = yaw; c.Point = p;
            list.Add(c);
        }

        // ─────────────────────────────────────────────────────────────────
        // 夹具 3 · Boss 规则夹具（可构造非法态）
        // ─────────────────────────────────────────────────────────────────

        /// <summary>三份 rule 冷启动（IMPL-v2.0 §4.1）。</summary>
        public static QaScenario BossLegal()
        {
            return LegalBaseline();
        }

        /// <summary>非法态：ForcePhase 指向不存在的阶段 5（Boss 只有 3 段）。</summary>
        public static QaScenario BossIllegalForcePhase()
        {
            var s = LegalBaseline();
            for (int i = 0; i < s.BossRules.Count; i++)
            {
                if (s.BossRules[i].BossId == "boss_03_tieyao")
                {
                    s.BossRules[i].ForcePhaseTarget = 5;
                    s.BossRules[i].Effects = new List<string> { "LockZone:zone_arena:Close", "ForcePhase:5", "ShakeCamera:0.6:18" };
                }
            }
            return s;
        }

        // ─────────────────────────────────────────────────────────────────
        // 夹具 4 · 存档夹具（可构造非法态 = 篡改 1 字节）
        // ─────────────────────────────────────────────────────────────────

        /// <summary>存档二进制布局（参考）：4B 魔数 + 4B save_version + 4B CRC32 + JSON 本体。</summary>
        public sealed class SaveBlob
        {
            public byte[] Header = new byte[16];
            public byte[] Body = new byte[0];
            public byte[] Bytes = new byte[0];
        }

        public static SaveBlob MakeSaveBlob(string json, uint crc)
        {
            var blob = new SaveBlob();
            byte[] magic = new byte[] { 0x45, 0x43, 0x48, 0x43 }; // "ECHC"
            int version = 3;
            blob.Header = new byte[16];
            Array.Copy(magic, 0, blob.Header, 0, 4);
            blob.Header[4] = (byte)((version >> 24) & 0xFF);
            blob.Header[5] = (byte)((version >> 16) & 0xFF);
            blob.Header[6] = (byte)((version >> 8) & 0xFF);
            blob.Header[7] = (byte)(version & 0xFF);
            blob.Header[8] = (byte)((crc >> 24) & 0xFF);
            blob.Header[9] = (byte)((crc >> 16) & 0xFF);
            blob.Header[10] = (byte)((crc >> 8) & 0xFF);
            blob.Header[11] = (byte)(crc & 0xFF);
            byte[] body = Encoding.UTF8.GetBytes(json);
            blob.Body = body;
            blob.Bytes = new byte[16 + body.Length];
            Array.Copy(blob.Header, 0, blob.Bytes, 0, 16);
            Array.Copy(body, 0, blob.Bytes, 16, body.Length);
            return blob;
        }

        /// <summary>合法存档夹具（CRC 正确）。</summary>
        public static SaveBlob SaveLegal()
        {
            string json = "{\"header\":{\"save_version\":3,\"content_version\":\"3.0\"},\"core\":{\"hero\":{\"hp\":100}},\"progression\":{\"cultivation\":{\"line_cuigu\":{\"level\":0,\"raw_value\":0}}}}";
            byte[] body = Encoding.UTF8.GetBytes(json);
            uint crc = Crc32Reference.Compute(body);
            return MakeSaveBlob(json, crc);
        }

        /// <summary>
        /// 篡改 1 字节的非法态。byteOffset 相对整个文件（含 16B 头）。
        /// 覆盖：头部魔数 / 版本 / CRC 字段 / 本体第 1,7,19,37,101 字节（EL-03 §5 ④）。
        /// </summary>
        public static SaveBlob SaveTampered(int byteOffset)
        {
            SaveBlob blob = SaveLegal();
            byte[] copy = (byte[])blob.Bytes.Clone();
            int idx = byteOffset;
            if (idx < 0) { idx = 0; }
            if (idx >= copy.Length) { idx = copy.Length - 1; }
            copy[idx] = (byte)(copy[idx] ^ 0xFF);
            blob.Bytes = copy;
            return blob;
        }

        /// <summary>EL-03 §5 ④ 规定的 20 个篡改偏移。</summary>
        public static readonly int[] TamperOffsets =
        {
            0, 1, 2, 3,          // 魔数
            4, 5, 6, 7,          // save_version
            8, 9, 10, 11,        // crc32
            16, 17, 19, 37,      // 本体前段
            20, 25, 41, 101, 128 // 本体中后段
        };

        // ─────────────────────────────────────────────────────────────────
        // 辅助
        // ─────────────────────────────────────────────────────────────────

        public static QaVec2 Polar(float radius, float deg)
        {
            double rad = deg * Math.PI / 180.0;
            return new QaVec2((float)(radius * Math.Cos(rad)), (float)(radius * Math.Sin(rad)));
        }

        public static QaVec2 LocalToWorld(float lx, float ly, float yawDeg)
        {
            double rad = yawDeg * Math.PI / 180.0;
            double cosR = Math.Cos(rad);
            double sinR = Math.Sin(rad);
            return new QaVec2((float)(lx * cosR - ly * sinR), (float)(lx * sinR + ly * cosR));
        }

        /// <summary>深拷贝（负向用例要基于同一基线各改一处，不能互相污染）。</summary>
        public static QaScenario Clone(QaScenario src)
        {
            var s = new QaScenario();
            s.GlobalTuning = new QaGlobalTuningRow
            {
                VfxGraphicBudget = src.GlobalTuning.VfxGraphicBudget,
                VfxGenericBudget = src.GlobalTuning.VfxGenericBudget,
                DropPityThreshold = src.GlobalTuning.DropPityThreshold
            };
            s.Dodge = new QaDodgeSpec
            {
                TotalFrames = src.Dodge.TotalFrames,
                MoveFrames = src.Dodge.MoveFrames,
                InvulnStart = src.Dodge.InvulnStart,
                InvulnEnd = src.Dodge.InvulnEnd,
                Distance = src.Dodge.Distance,
                PushWindowRatioMax = src.Dodge.PushWindowRatioMax
            };
            s.ResourceAfterCombo = src.ResourceAfterCombo;
            s.InputTimestampPipelinesIndependent = src.InputTimestampPipelinesIndependent;
            s.InjectedInputLatencyFrames = src.InjectedInputLatencyFrames;
            s.MeasuredInputLatencyMs = src.MeasuredInputLatencyMs;
            s.BladeLightOcclusionRatio = src.BladeLightOcclusionRatio;
            s.BladeLightScreenCoverageRatio = src.BladeLightScreenCoverageRatio;
            s.RuntimeFps = src.RuntimeFps;
            s.D5ControlEliminatedColor = src.D5ControlEliminatedColor;
            s.D5ControlEliminatedOutlineForm = src.D5ControlEliminatedOutlineForm;
            s.D5ControlEliminatedEntryTiming = src.D5ControlEliminatedEntryTiming;
            s.DodgePushWindowRatio = src.DodgePushWindowRatio;
            s.HitFeedbackFrames = src.HitFeedbackFrames;
            s.KnockbackDirectionDeg = src.KnockbackDirectionDeg;
            s.DamageSourceDirectionDeg = src.DamageSourceDirectionDeg;
            s.KnockbackPushesIntoWall = src.KnockbackPushesIntoWall;
            s.KnockbackFallsOffPlatform = src.KnockbackFallsOffPlatform;
            s.BossTelegraphFrames = src.BossTelegraphFrames;
            s.TelegraphHasGroundCircle = src.TelegraphHasGroundCircle;
            s.TelegraphHasShape = src.TelegraphHasShape;
            s.TelegraphHasAudio = src.TelegraphHasAudio;
            s.InkContrastRatio = src.InkContrastRatio;
            s.InkOutlinePx = src.InkOutlinePx;
            s.MaterialSlotCount = src.MaterialSlotCount;
            s.HasAdditionalMaterial = src.HasAdditionalMaterial;
            s.ClearCoatEnabled = src.ClearCoatEnabled;
            s.EliteColliderScale = src.EliteColliderScale;
            s.EliteVisualScale = src.EliteVisualScale;
            s.HitClipVariantCount = src.HitClipVariantCount;
            s.HitClipReverseContactHeightCm = src.HitClipReverseContactHeightCm;
            s.HitClipReverseContactFrames = src.HitClipReverseContactFrames;

            for (int i = 0; i < src.Skills.Count; i++)
            {
                var k = new QaSkillRow();
                k.Id = src.Skills[i].Id;
                k.Startup = src.Skills[i].Startup;
                k.Active = src.Skills[i].Active;
                k.Recovery = src.Skills[i].Recovery;
                k.EntryTags = new List<string>(src.Skills[i].EntryTags);
                k.OnCastResource = src.Skills[i].OnCastResource;
                k.OnHitResource = src.Skills[i].OnHitResource;
                k.WritesCoreState = src.Skills[i].WritesCoreState;
                k.ConsumeStacks = src.Skills[i].ConsumeStacks;
                k.MovementCurve = src.Skills[i].MovementCurve;
                k.DerivativeIds = new List<string>(src.Skills[i].DerivativeIds);
                k.SegmentCount = src.Skills[i].SegmentCount;
                for (int h = 0; h < src.Skills[i].HitShapes.Count; h++)
                {
                    QaHitShapeRow sh = src.Skills[i].HitShapes[h];
                    var nh = new QaHitShapeRow();
                    nh.Shape = sh.Shape; nh.Radius = sh.Radius; nh.Angle = sh.Angle; nh.OffsetY = sh.OffsetY;
                    nh.Frame = sh.Frame; nh.Damage = sh.Damage; nh.Knockback = sh.Knockback;
                    nh.Hitstop = sh.Hitstop; nh.MultiHit = sh.MultiHit;
                    k.HitShapes.Add(nh);
                }
                s.Skills.Add(k);
            }
            for (int i = 0; i < src.CancelWindows.Count; i++)
            {
                var w = new QaCancelWindowRow();
                w.SourceTag = src.CancelWindows[i].SourceTag;
                w.AllowTag = src.CancelWindows[i].AllowTag;
                w.FromFrame = src.CancelWindows[i].FromFrame;
                w.ToFrame = src.CancelWindows[i].ToFrame;
                w.ResourceCost = src.CancelWindows[i].ResourceCost;
                s.CancelWindows.Add(w);
            }
            for (int i = 0; i < src.MovementCurves.Count; i++)
            {
                var m = new QaMovementCurveRow();
                m.CurveId = src.MovementCurves[i].CurveId;
                m.Keys = new List<float>(src.MovementCurves[i].Keys);
                m.DeclaredDistance = src.MovementCurves[i].DeclaredDistance;
                m.ClampMode = src.MovementCurves[i].ClampMode;
                s.MovementCurves.Add(m);
            }
            for (int i = 0; i < src.Bosses.Count; i++)
            {
                var b = new QaBossRow();
                b.BossId = src.Bosses[i].BossId; b.Hp = src.Bosses[i].Hp; b.PhaseCount = src.Bosses[i].PhaseCount;
                b.ArenaAnchor = src.Bosses[i].ArenaAnchor; b.BrainRef = src.Bosses[i].BrainRef;
                s.Bosses.Add(b);
            }
            for (int i = 0; i < src.BossRules.Count; i++)
            {
                var r = new QaBossRuleRow();
                r.RuleId = src.BossRules[i].RuleId; r.BossId = src.BossRules[i].BossId; r.Phase = src.BossRules[i].Phase;
                r.Trigger = src.BossRules[i].Trigger;
                r.Conditions = new List<string>(src.BossRules[i].Conditions);
                r.Effects = new List<string>(src.BossRules[i].Effects);
                r.ForcePhaseTarget = src.BossRules[i].ForcePhaseTarget;
                r.ReferencedAnchors = new List<string>(src.BossRules[i].ReferencedAnchors);
                s.BossRules.Add(r);
            }
            for (int i = 0; i < src.Anchors.Count; i++)
            {
                s.Anchors.Add(new QaAnchorRow
                {
                    AnchorId = src.Anchors[i].AnchorId, Tag = src.Anchors[i].Tag, InitState = src.Anchors[i].InitState
                });
            }
            for (int i = 0; i < src.Dungeons.Count; i++)
            {
                s.Dungeons.Add(new QaDungeonRow
                {
                    DungeonId = src.Dungeons[i].DungeonId,
                    CpThreshold = src.Dungeons[i].CpThreshold,
                    ExpectedTotalPower = src.Dungeons[i].ExpectedTotalPower
                });
            }
            for (int i = 0; i < src.VfxSpecs.Count; i++)
            {
                var v = src.VfxSpecs[i];
                s.VfxSpecs.Add(new QaVfxSpecRow { SkillId = v.SkillId, ArmorVfxId = v.ArmorVfxId, InvulnVfxId = v.InvulnVfxId });
            }
            for (int i = 0; i < src.VfxAssets.Count; i++)
            {
                var v = src.VfxAssets[i];
                s.VfxAssets.Add(new QaVfxAssetRow
                {
                    VfxId = v.VfxId, OutlinePx = v.OutlinePx, OutlineStyle = v.OutlineStyle,
                    PulseHz = v.PulseHz, WindowFrames = v.WindowFrames
                });
            }
            for (int i = 0; i < src.Knockbacks.Count; i++)
            {
                var k = src.Knockbacks[i];
                s.Knockbacks.Add(new QaKnockbackRow
                {
                    Tier = k.Tier, HitstunFrames = k.HitstunFrames, HitstopFrames = k.HitstopFrames,
                    HitstopTimeScale = k.HitstopTimeScale, HoldDuration = k.HoldDuration,
                    DistMin = k.DistMin, DistMax = k.DistMax, SplitFrame = k.SplitFrame,
                    CurveFrames = k.CurveFrames, RecoveryStartProgress = k.RecoveryStartProgress, PeakSpeed = k.PeakSpeed
                });
            }
            for (int i = 0; i < src.Derivatives.Count; i++)
            {
                var d = src.Derivatives[i];
                s.Derivatives.Add(new QaDerivativeRow
                {
                    SkillId = d.SkillId, Tier = d.Tier, UnlockType = d.UnlockType, VisualTier = d.VisualTier,
                    UnlockDerivativeId = d.UnlockDerivativeId, MechanicTargets = new List<string>(d.MechanicTargets),
                    CostEu = d.CostEu
                });
            }
            for (int i = 0; i < src.EffectMerges.Count; i++)
            {
                var e = src.EffectMerges[i];
                s.EffectMerges.Add(new QaEffectMergeRow
                {
                    EffectKey = e.EffectKey,
                    MergeClass = e.MergeClass,
                    SourceValues = new List<float>(e.SourceValues),
                    SourceKinds = new List<string>(e.SourceKinds),
                    MergedValue = e.MergedValue
                });
            }
            for (int i = 0; i < src.Meridians.Count; i++)
            {
                var m = src.Meridians[i];
                s.Meridians.Add(new QaMeridianRow
                {
                    NodeId = m.NodeId, Series = m.Series, EffectKey = m.EffectKey,
                    MechanicMod = m.MechanicMod, NumericValue = m.NumericValue, UnlockTier = m.UnlockTier
                });
            }
            for (int i = 0; i < src.DropPity.Count; i++)
            {
                var p = src.DropPity[i];
                s.DropPity.Add(new QaDropPityRow
                {
                    MaterialKey = p.MaterialKey, Counter = p.Counter, Threshold = p.Threshold,
                    RecentDrops = new List<bool>(p.RecentDrops)
                });
            }
            for (int i = 0; i < src.StarNodes.Count; i++)
            {
                var n = src.StarNodes[i];
                s.StarNodes.Add(new QaStarNodeRow
                {
                    NodeId = n.NodeId, Series = n.Series, UnlockTier = n.UnlockTier,
                    EffectKey = n.EffectKey, MechanicMod = n.MechanicMod,
                    PrereqIds = new List<string>(n.PrereqIds)
                });
            }
            return s;
        }
    }
}
