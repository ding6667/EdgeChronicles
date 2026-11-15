using System;
using System.Collections.Generic;

namespace EdgeChronicles.Tests.Qa
{
    /// <summary>
    /// 判据参考执行器（oracle）。
    ///
    /// 【它是什么】
    /// 把每条可脚本化判据写成一个**能返回"报红 / 不报红"的纯函数**。
    /// 生产侧会实现同一套判据（导表断言 A19–A43、战斗内核的硬判据）。
    /// 两份实现在同一场景上结论必须一致（由 ProductionParityTests 强制）。
    ///
    /// 【它不是什么】
    /// 它不是"放宽版断言"。阈值一律取自 <see cref="CombatSpec"/>，
    /// 方法体内不允许出现裸常量——否则会变成第三份真值源。
    ///
    /// 【为什么要有它】
    /// GDD §12.1 ④：每条判据必须能写出自己的误判条件，并配一个证伪案例。
    /// 只有一份实现时，判据无法被证伪——"判据抓不住错"和"判据误杀对的"都看不见。
    /// </summary>
    public static class CriterionOracle
    {
        /// <summary>
        /// 判据逻辑的**契约版本号**。
        /// 【为什么需要】一个自检如果不会因为自己过期而变红，它就不是自检。
        /// `docs/qa/qa_oracle_mirror.py` 声明自己对应的版本号，运行时与本常量比对，
        /// 不一致即红 —— 这样"镜像落后于 oracle"会立刻暴露，而不是安静地报绿。
        /// 任何改动 oracle 判定逻辑的提交，**必须同时更新本常量与镜像的版本声明**。
        /// </summary>
        public const string ContractVersion = "2026-11-15.r4";

        // ── 导表断言编号（IMPL-v2.0 §5 A19–A30 / EL-03 §2 A31–A43）──
        public const string A19 = "A19"; // phases 各 ≥1 且 totalFrames ≤ 90
        public const string A20 = "A20"; // hitShape.frame < active
        public const string A21 = "A21"; // fromFrame ≤ toFrame < totalFrames
        public const string A22 = "A22"; // cancelWindow 标签必须存在于某 skill.entryTags
        public const string A23 = "A23"; // 每 skill ≥1 条 hitShape
        public const string A24 = "A24"; // 曲线积分距离与声明偏差 ≤5%
        public const string A25 = "A25"; // coreLink 的 writes 与 consumeStacks 互斥
        public const string A26 = "A26"; // trigger/condition/effect 必在枚举内
        public const string A27 = "A27"; // ForcePhase 目标阶段存在
        public const string A28 = "A28"; // 所有 anchor:// 可解析
        public const string A29 = "A29"; // 连段模拟不产生负资源
        public const string A30 = "A30"; // hitstop ∈ [0.05, 0.25]
        public const string A31 = "A31"; // 星盘无环
        public const string A33 = "A33"; // tier ↔ unlock_type 阶位不变量（每技能 5 阶覆盖 5 种类型各一次）
        public const string A331 = "A33-1"; // 派生条目双向承载
        public const string A332 = "A33-2"; // 每技能至多 2 条派生，且 tier ∈ {1,2,3,5}
        public const string A333 = "A33-3"; // 裂石 / 回锋 派生列全空
        public const string A334 = "A33-4"; // derivative ∧ new_graphic ⇒ 必须有 id
        public const string A335 = "A33-5"; // new_graphic 行数 == 清单条数
        public const string A35 = "A35"; // mechanic_mod ∈ 28 项枚举
        public const string A391 = "A39-1"; // 上限类多来源取最大
        public const string A392 = "A39-2"; // 其余数值类取最小不叠加；离散类只生效一次
        public const string A40 = "A40"; // 掉落保底
        public const string A43 = "A43"; // 图集格位配额（两层）

        // ── 打击感 / 可读性判据编号 ──
        public const string FHold = "F-HOLD";            // hold ∈ [2,4] 帧（设计意图区间）
        public const string FHoldReachable = "F-HOLD-REACHABLE"; // hold 区间上下限必须永远可测
        public const string FHitstopCausal = "F-HITSTOP-CAUSAL"; // 0.15 × hitstop_frames ≤ 1
        public const string FRecoveryGap = "F-RECOVERY-GAP";     // T(归零)+2 ≤ T(收身)
        public const string FKbTier = "F-KB-TIER";         // 击退距离落在档位区间
        public const string FKbPeak = "F-KB-PEAK";         // 峰值速度 / 逐帧位移上限
        public const string FDodgeInvuln = "F-DODGE-INVULN"; // 闪避无敌窗口
        public const string FVfxStructure = "F-VFX-STRUCTURE"; // 霸体 / 无敌描边结构必须不同
        public const string FPulseHz = "F-PULSE-HZ";       // 脉冲频率上限
        public const string FPulseWindow = "F-PULSE-WINDOW"; // 脉冲窗口必须容得下周期
        public const string FBladeOcclusion = "F-BLADE-OCCLUSION"; // 刀光遮挡比 ≤30%
        public const string FE3Latency = "F-E3-LATENCY";   // 输入延迟 ≤50ms
        public const string FE3SameSource = "F-E3-SAME-SOURCE"; // 两条时间戳必须非同源
        public const string FD5Control = "F-D5-CONTROL";   // 对照组必须同时消三通道
        public const string FEdgeLevels = "F-EDGE-LEVELS"; // 副本门槛递增 / ≥1.3× / ≤ 预期战力
        public const string FDodgePush = "F-DODGE-PUSH";   // 闪避蹬地窗口位移占比 ≤5%
        public const string FHitFeedback = "F-HIT-FEEDBACK"; // 命中 → 受击起始 ≤3 帧
        public const string FKbDirection = "F-KB-DIRECTION"; // 击退方向与伤害来源相反
        public const string FKbWall = "F-KB-WALL";         // 不推入墙 / 不推落平台
        public const string FBossTelegraph = "F-BOSS-TELEGRAPH"; // 预警提前 ≥30 帧
        public const string FTelegraphChannels = "F-TELEGRAPH-CHANNELS"; // 预警三重编码
        public const string FInkContrast = "F-INK-CONTRAST"; // 墨线与背景亮度差 ≥60%
        public const string FVfxChannel = "F-VFX-CHANNEL"; // 一渲染器一材质槽
        public const string FEliteHitbox = "F-ELITE-HITBOX"; // 精英缩放同步受击盒
        public const string FHitClipVariants = "F-HITCLIP-VARIANTS"; // 受击变体齐备 + 几何稳定

        /// <summary>求值一次场景，返回所有"报红"的判据编号。</summary>
        public static CriterionVerdict Evaluate(QaScenario s)
        {
            var v = new CriterionVerdict();
            if (s == null) { return v; }

            EvalA19(s, v);
            EvalA20(s, v);
            EvalA21(s, v);
            EvalA22(s, v);
            EvalA23(s, v);
            EvalA24(s, v);
            EvalA25(s, v);
            EvalA26(s, v);
            EvalA27(s, v);
            EvalA28(s, v);
            EvalA29(s, v);
            EvalA30(s, v);
            EvalA31(s, v);
            EvalA33Family(s, v);
            EvalA35(s, v);
            EvalA39(s, v);
            EvalA40(s, v);
            EvalA43(s, v);
            EvalFeel(s, v);
            EvalEdgeLevels(s, v);
            return v;
        }

        // ─────────────────────────────────────────────────────────────────
        // 导表断言
        // ─────────────────────────────────────────────────────────────────

        private static void EvalA19(QaScenario s, CriterionVerdict v)
        {
            for (int i = 0; i < s.Skills.Count; i++)
            {
                QaSkillRow sk = s.Skills[i];
                if (sk.Startup < 1 || sk.Active < 1 || sk.Recovery < 1 || sk.TotalFrames > 90)
                {
                    v.Add(A19);
                    return;
                }
            }
        }

        private static void EvalA20(QaScenario s, CriterionVerdict v)
        {
            for (int i = 0; i < s.Skills.Count; i++)
            {
                QaSkillRow sk = s.Skills[i];
                for (int h = 0; h < sk.HitShapes.Count; h++)
                {
                    if (sk.HitShapes[h].Frame >= sk.Active) { v.Add(A20); return; }
                }
            }
        }

        private static void EvalA21(QaScenario s, CriterionVerdict v)
        {
            for (int i = 0; i < s.CancelWindows.Count; i++)
            {
                QaCancelWindowRow w = s.CancelWindows[i];
                if (w.FromFrame > w.ToFrame) { v.Add(A21); return; }
                int total = TotalFramesOfTag(s, w.SourceTag);
                if (total > 0 && w.ToFrame >= total) { v.Add(A21); return; }
            }
        }

        /// <summary>
        /// 门派共有动作标签（普攻 / 闪避 / 轻功）。
        /// 【D-10 裁定】闪避与普攻是**门派共有层**的动作：8 技能表里没有闪避行，
        /// 所以闪避没有 entryTags 行；但闪避**可以取消进入任何技能**。
        /// 因此 A22 的判定集合 = 门派技能 entryTags ∪ 门派共有动作标签。
        /// </summary>
        public static readonly string[] SharedActionTags = { "dodge", "attack" };

        private static void EvalA22(QaScenario s, CriterionVerdict v)
        {
            var known = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < s.Skills.Count; i++)
            {
                for (int t = 0; t < s.Skills[i].EntryTags.Count; t++) { known.Add(s.Skills[i].EntryTags[t]); }
            }
            for (int t = 0; t < SharedActionTags.Length; t++) { known.Add(SharedActionTags[t]); }
            for (int i = 0; i < s.CancelWindows.Count; i++)
            {
                if (!known.Contains(s.CancelWindows[i].AllowTag)) { v.Add(A22); return; }
            }
        }

        private static void EvalA23(QaScenario s, CriterionVerdict v)
        {
            for (int i = 0; i < s.Skills.Count; i++)
            {
                if (s.Skills[i].HitShapes.Count < 1) { v.Add(A23); return; }
            }
        }

        private static void EvalA24(QaScenario s, CriterionVerdict v)
        {
            for (int i = 0; i < s.MovementCurves.Count; i++)
            {
                float dev = CurveIntegratorReference.RelativeDeviation(s.MovementCurves[i]);
                if (dev > CombatSpec.CurveIntegralTolerance) { v.Add(A24); return; }
            }
        }

        /// <summary>
        /// A24 的判定结果明细（供报告用）：口径、积分距离、声明距离、偏差。
        /// </summary>
        public static string DescribeA24(QaMovementCurveRow curve)
        {
            float integral = CurveIntegratorReference.IntegrateDistance(curve);
            float dev = CurveIntegratorReference.RelativeDeviation(curve);
            return "口径=" + CurveSemantics.Current
                + " 积分=" + integral.ToString("F4") + "m 声明=" + curve.DeclaredDistance.ToString("F4") + "m"
                + " 偏差=" + (dev * 100f).ToString("F2") + "%";
        }

        private static void EvalA25(QaScenario s, CriterionVerdict v)
        {
            for (int i = 0; i < s.Skills.Count; i++)
            {
                if (s.Skills[i].WritesCoreState && s.Skills[i].ConsumeStacks) { v.Add(A25); return; }
            }
        }

        private static void EvalA26(QaScenario s, CriterionVerdict v)
        {
            for (int i = 0; i < s.BossRules.Count; i++)
            {
                QaBossRuleRow r = s.BossRules[i];
                if (!BossRuleReference.IsLegalTrigger(r.Trigger)) { v.Add(A26); return; }
                for (int c = 0; c < r.Conditions.Count; c++)
                {
                    if (!BossRuleReference.IsLegalCondition(Head(r.Conditions[c]))) { v.Add(A26); return; }
                }
                for (int e = 0; e < r.Effects.Count; e++)
                {
                    if (!BossRuleReference.IsLegalEffect(Head(r.Effects[e]))) { v.Add(A26); return; }
                }
            }
        }

        private static void EvalA27(QaScenario s, CriterionVerdict v)
        {
            for (int i = 0; i < s.BossRules.Count; i++)
            {
                QaBossRuleRow r = s.BossRules[i];
                if (r.ForcePhaseTarget <= 0) { continue; }
                int phases = PhaseCountOf(s, r.BossId);
                if (phases > 0 && r.ForcePhaseTarget > phases) { v.Add(A27); return; }
            }
        }

        private static void EvalA28(QaScenario s, CriterionVerdict v)
        {
            var anchors = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < s.Anchors.Count; i++) { anchors.Add(s.Anchors[i].AnchorId); }
            for (int i = 0; i < s.BossRules.Count; i++)
            {
                QaBossRuleRow r = s.BossRules[i];
                for (int a = 0; a < r.ReferencedAnchors.Count; a++)
                {
                    if (!anchors.Contains(r.ReferencedAnchors[a])) { v.Add(A28); return; }
                }
                for (int e = 0; e < r.Effects.Count; e++)
                {
                    string eff = r.Effects[e];
                    if (eff.StartsWith("MovePlayer:", StringComparison.Ordinal))
                    {
                        string[] parts = eff.Split(':');
                        if (parts.Length >= 3 && parts[2].StartsWith("anchor://", StringComparison.Ordinal) && !anchors.Contains(parts[2]))
                        {
                            v.Add(A28); return;
                        }
                    }
                }
            }
            for (int b = 0; b < s.Bosses.Count; b++)
            {
                if (!anchors.Contains(s.Bosses[b].ArenaAnchor)) { v.Add(A28); return; }
            }
        }

        private static void EvalA29(QaScenario s, CriterionVerdict v)
        {
            // A29 连段模拟不产生负资源。
            if (s.ResourceAfterCombo < 0) { v.Add(A29); }
            // 【D-09 裁定】模拟还必须满足两条执行口径，否则"没报红"没有意义：
            //   ① 帧数够长：模拟时长覆盖序列所需时长
            //   ② 跑过顿帧帧：顿帧不暂停 tick，状态机在顿帧期间仍须合法推进
            if (s.ComboSimulationFrames <= 0) { v.Add(A29); }
            if (s.ComboSequence.Count > 0)
            {
                float simulatedSeconds = s.ComboSimulationFrames * s.ComboFrameDeltaSeconds;
                // 每段技能至少 1 帧推进（真实时长远大于此，这里只判"帧数不是明显不足"）
                float minimumSeconds = s.ComboSequence.Count * s.ComboFrameDeltaSeconds;
                if (simulatedSeconds < minimumSeconds) { v.Add(A29); }
            }
            if (s.ComboHitstopFrames <= 0) { v.Add(A29); }
        }

        private static void EvalA30(QaScenario s, CriterionVerdict v)
        {
            for (int i = 0; i < s.Skills.Count; i++)
            {
                for (int h = 0; h < s.Skills[i].HitShapes.Count; h++)
                {
                    float hs = s.Skills[i].HitShapes[h].Hitstop;
                    if (hs < CombatSpec.HitstopMin || hs > CombatSpec.HitstopMax) { v.Add(A30); return; }
                }
            }
        }

        private static void EvalA31(QaScenario s, CriterionVerdict v)
        {
            // 星盘 prereq 图必须无环：拓扑排序（Kahn）后仍有剩余节点即有环。
            var ids = new List<string>();
            var prereq = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            for (int i = 0; i < s.StarNodes.Count; i++)
            {
                ids.Add(s.StarNodes[i].NodeId);
                prereq[s.StarNodes[i].NodeId] = new List<string>(s.StarNodes[i].PrereqIds);
            }
            var indegree = new Dictionary<string, int>(StringComparer.Ordinal);
            var children = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            for (int i = 0; i < ids.Count; i++)
            {
                indegree[ids[i]] = 0;
                children[ids[i]] = new List<string>();
            }
            for (int i = 0; i < ids.Count; i++)
            {
                List<string> pre = prereq[ids[i]];
                for (int p = 0; p < pre.Count; p++)
                {
                    if (!children.ContainsKey(pre[p])) { continue; } // 孤儿外键不在本判据范围
                    children[pre[p]].Add(ids[i]);
                    indegree[ids[i]] = indegree[ids[i]] + 1;
                }
            }
            var queue = new List<string>();
            for (int i = 0; i < ids.Count; i++) { if (indegree[ids[i]] == 0) { queue.Add(ids[i]); } }
            int visited = 0;
            while (queue.Count > 0)
            {
                string cur = queue[0];
                queue.RemoveAt(0);
                visited++;
                List<string> ch = children[cur];
                for (int i = 0; i < ch.Count; i++)
                {
                    indegree[ch[i]] = indegree[ch[i]] - 1;
                    if (indegree[ch[i]] == 0) { queue.Add(ch[i]); }
                }
            }
            if (visited != ids.Count) { v.Add(A31); }
        }

        private static void EvalA33Family(QaScenario s, CriterionVerdict v)
        {
            // A33：【D-03 裁定后的阶位不变量】——**不存在全局固定的 tier → unlock_type 映射**。
            //   ① 每个技能的 5 阶恰好覆盖 5 种 unlock_type 各一次；
            //   ② tier 1 恒为 stat；tier 5 恒为 mechanic；
            //   ③ 中间 3 阶（resource / derivative / window）的顺序由各技能自行决定。
            var bySkill = new Dictionary<string, List<QaDerivativeRow>>(StringComparer.Ordinal);
            for (int i = 0; i < s.Derivatives.Count; i++)
            {
                QaDerivativeRow d = s.Derivatives[i];
                List<QaDerivativeRow> lst;
                if (!bySkill.TryGetValue(d.SkillId, out lst)) { lst = new List<QaDerivativeRow>(); bySkill[d.SkillId] = lst; }
                lst.Add(d);
            }
            foreach (KeyValuePair<string, List<QaDerivativeRow>> kv in bySkill)
            {
                List<QaDerivativeRow> rows = kv.Value;
                if (rows.Count != 5) { v.Add(A33); continue; }
                var types = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < rows.Count; i++)
                {
                    types.Add(rows[i].UnlockType);
                    if (rows[i].Tier < 1 || rows[i].Tier > 5) { v.Add(A33); }
                }
                if (types.Count != 5) { v.Add(A33); }
                foreach (string required in AllUnlockTypes)
                {
                    if (!types.Contains(required)) { v.Add(A33); }
                }
                QaDerivativeRow t1 = FindTier(rows, 1);
                QaDerivativeRow t5 = FindTier(rows, 5);
                if (t1 == null || t1.UnlockType != "stat") { v.Add(A33); }
                if (t5 == null || t5.UnlockType != "mechanic") { v.Add(A33); }
            }

            // A33-1：双向承载（派生条目的 id ⇄ skill.derivative_ids）
            var skillById = new Dictionary<string, QaSkillRow>(StringComparer.Ordinal);
            for (int i = 0; i < s.Skills.Count; i++) { skillById[s.Skills[i].Id] = s.Skills[i]; }
            var declared = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            for (int i = 0; i < s.DerivativeEffects.Count; i++)
            {
                QaDerivativeEffectRow e = s.DerivativeEffects[i];
                List<string> lst;
                if (!declared.TryGetValue(e.SkillId, out lst)) { lst = new List<string>(); declared[e.SkillId] = lst; }
                lst.Add(e.DerivativeId);

                QaSkillRow sk;
                if (skillById.TryGetValue(e.SkillId, out sk) && !sk.DerivativeIds.Contains(e.DerivativeId))
                {
                    v.Add(A331);
                }
            }
            for (int i = 0; i < s.Skills.Count; i++)
            {
                QaSkillRow sk = s.Skills[i];
                List<string> lst;
                declared.TryGetValue(sk.Id, out lst);
                for (int k = 0; k < sk.DerivativeIds.Count; k++)
                {
                    if (lst == null || !lst.Contains(sk.DerivativeIds[k])) { v.Add(A331); }
                }
            }

            // A33-2：每技能至多 2 条派生条目；【D-03 裁定】派生可落在 1 / 2 / 3 / 5 阶
            // （当前 8 条落在 2 / 3 / 5 阶；"派生只能落在 3/5 阶"是错的）。
            var perSkill = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < s.DerivativeEffects.Count; i++)
            {
                QaDerivativeEffectRow e = s.DerivativeEffects[i];
                int cnt;
                perSkill.TryGetValue(e.SkillId, out cnt);
                perSkill[e.SkillId] = cnt + 1;
                if (e.Tier != 1 && e.Tier != 2 && e.Tier != 3 && e.Tier != 5) { v.Add(A332); }
            }
            foreach (KeyValuePair<string, int> kv in perSkill)
            {
                if (kv.Value > 2) { v.Add(A332); }
            }

            // A33-3：裂石 / 回锋 不允许有派生条目
            for (int i = 0; i < s.DerivativeEffects.Count; i++)
            {
                QaDerivativeEffectRow e = s.DerivativeEffects[i];
                if (e.SkillId == "sk_lieshi" || e.SkillId == "sk_huifeng") { v.Add(A333); }
            }

            // A33-4：unlock_type=derivative ∧ visual_tier=new_graphic 的阶位 ⇒ 必须挂了派生条目
            for (int i = 0; i < s.Derivatives.Count; i++)
            {
                QaDerivativeRow d = s.Derivatives[i];
                if (d.UnlockType != "derivative" || d.VisualTier != "new_graphic") { continue; }
                bool hasEffect = false;
                for (int e = 0; e < s.DerivativeEffects.Count; e++)
                {
                    QaDerivativeEffectRow eff = s.DerivativeEffects[e];
                    if (eff.SkillId == d.SkillId && eff.Tier == d.Tier) { hasEffect = true; break; }
                }
                if (!hasEffect) { v.Add(A334); }
            }

            // A33-5：new_graphic 派生条目数 == 派生清单中标 new_graphic 的条数
            int newGraphicRows = 0;
            for (int i = 0; i < s.DerivativeEffects.Count; i++)
            {
                if (s.DerivativeEffects[i].VisualTier == "new_graphic") { newGraphicRows++; }
            }
            if (newGraphicRows != CombatSpec.DerivativeListNewGraphicCount) { v.Add(A335); }
        }

        private static void EvalA35(QaScenario s, CriterionVerdict v)
        {
            for (int i = 0; i < s.StarNodes.Count; i++)
            {
                if (!MechanicModCatalog.IsLegal(s.StarNodes[i].MechanicMod)) { v.Add(A35); return; }
            }
            // 络脉节点同样只允许合法机制项。
            // 【为什么络脉更适合当负向夹具】络脉的数值列恒空，误写成自由字符串没有别的列可退化，
            // 因此错误必然以"机制字段非法"的形式暴露出来。
            for (int i = 0; i < s.Meridians.Count; i++)
            {
                if (!MechanicModCatalog.IsLegal(s.Meridians[i].MechanicMod)) { v.Add(A35); return; }
            }
            for (int i = 0; i < s.Derivatives.Count; i++)
            {
                for (int m = 0; m < s.Derivatives[i].MechanicTargets.Count; m++)
                {
                    string t = s.Derivatives[i].MechanicTargets[m];
                    if (t.StartsWith("loop_l", StringComparison.Ordinal)) { continue; }
                    v.Add(A35); return;
                }
            }
        }

        private static void EvalA39(QaScenario s, CriterionVerdict v)
        {
            // A39-1：上限类多来源**取最大**。
            // A39-2：其余数值类**取最小、不叠加**；离散类**只生效一次**。
            // 【口径】判的是"合并规则"，不是"某次实现的结果"：
            // 夹具同时给出各来源的原始值与最终合并值，判据据此验证规则。
            for (int i = 0; i < s.EffectMerges.Count; i++)
            {
                QaEffectMergeRow m = s.EffectMerges[i];
                if (m.SourceValues.Count == 0) { continue; }

                if (m.MergeClass == EffectMergeClass.CapUpper)
                {
                    float max = m.SourceValues[0];
                    for (int k = 1; k < m.SourceValues.Count; k++)
                    {
                        if (m.SourceValues[k] > max) { max = m.SourceValues[k]; }
                    }
                    if (Math.Abs(m.MergedValue - max) > 1e-4f) { v.Add(A391); }
                    continue;
                }

                if (m.MergeClass == EffectMergeClass.Numeric)
                {
                    float min = m.SourceValues[0];
                    float sum = 0f;
                    for (int k = 0; k < m.SourceValues.Count; k++)
                    {
                        sum += m.SourceValues[k];
                        if (m.SourceValues[k] < min) { min = m.SourceValues[k]; }
                    }
                    // 取最小 ⇒ 不得叠加（叠加值只有在单来源时才会与 min 相等）
                    if (m.SourceValues.Count > 1 && Math.Abs(m.MergedValue - sum) < 1e-4f
                        && Math.Abs(sum - min) > 1e-4f)
                    {
                        v.Add(A392);
                    }
                    else if (Math.Abs(m.MergedValue - min) > 1e-4f)
                    {
                        v.Add(A392);
                    }
                    continue;
                }

                // 离散类：只生效一次 ⇒ 合并值必须等于某一个来源值，且不得是多个来源的叠加。
                bool matchesOne = false;
                for (int k = 0; k < m.SourceValues.Count; k++)
                {
                    if (Math.Abs(m.MergedValue - m.SourceValues[k]) < 1e-4f) { matchesOne = true; break; }
                }
                if (!matchesOne) { v.Add(A392); }
            }
        }

        private static void EvalA40(QaScenario s, CriterionVerdict v)
        {
            for (int i = 0; i < s.DropPity.Count; i++)
            {
                QaDropPityRow p = s.DropPity[i];
                if (p.Counter < p.Threshold) { continue; }
                bool lastDropped = p.RecentDrops.Count > 0 && p.RecentDrops[p.RecentDrops.Count - 1];
                if (!lastDropped) { v.Add(A40); return; }
            }
        }

        private static void EvalA43(QaScenario s, CriterionVerdict v)
        {
            // 【拆分模型】new_graphic 计数取**派生条目**（§4.8.5 清单里的那 6 条），
            // 不是阶位行 —— 阶位行只是"这一阶挂了 new_graphic 派生"。
            int newGraphic = 0;
            for (int i = 0; i < s.DerivativeEffects.Count; i++)
            {
                if (s.DerivativeEffects[i].VisualTier == "new_graphic") { newGraphic++; }
            }
            int generic = s.GlobalTuning.VfxGenericBudget;
            int budget = s.GlobalTuning.VfxGraphicBudget;
            if (newGraphic + generic > budget) { v.Add(A43); }
        }

        // ─────────────────────────────────────────────────────────────────
        // 打击感 / 可读性
        // ─────────────────────────────────────────────────────────────────

        private static void EvalFeel(QaScenario s, CriterionVerdict v)
        {
            // F-HOLD：【D-04 裁定后的口径】
            //   clip_W = anim_clip_length × 60 ÷ anim_fps   （C 轴先换算到 W 轴）
            //   hold   = hitstun_frames − clip_W − hitstop_frames × (1 − hitstop_time_scale)
            // 判定三件事：① 换算后 hold >= 0（负数报错，**不取 0**）
            //           ② hold ∈ [2,4]（含端点）
            //           ③ 若声明了 hold（派生列，禁手填），必须与重算值相等
            for (int i = 0; i < s.Knockbacks.Count; i++)
            {
                QaKnockbackRow k = s.Knockbacks[i];
                float computed = k.ComputedHold();
                if (computed < 0f) { v.Add(FHold); continue; }
                if (computed < CombatSpec.HoldDurationMin || computed > CombatSpec.HoldDurationMax) { v.Add(FHold); continue; }
                if (Math.Abs(k.HoldDuration - computed) > 0.05f) { v.Add(FHold); }
            }

            // F-HOLD-REACHABLE：【D-14 第三条路】判据区间的上下限必须**永远可测**。
            // 做法：在**合法构造空间**内枚举 hold 的可达值，确认同时存在
            //   ① 低于下限的可达值（负向用例 NC-A15 的着力点）
            //   ② 高于上限的可达值（负向用例 S-14 的着力点）
            // 只要有人收窄了构造空间（例如把 hitstun 限死），这两条负向用例就会失效，
            // 本断言先红并提醒更新 —— 防止出现"永远无法被证伪的用例"。
            {
                bool belowReachable = false;
                bool aboveReachable = false;
                for (int hitstun = CombatSpec.HoldProbeHitstunMin; hitstun <= CombatSpec.HoldProbeHitstunMax && !(belowReachable && aboveReachable); hitstun++)
                {
                    for (int clip = CombatSpec.HoldProbeClipMinC; clip <= CombatSpec.HoldProbeClipMaxC && !(belowReachable && aboveReachable); clip++)
                    {
                        for (int hitstop = 2; hitstop <= 6; hitstop += 2)
                        {
                            float hold = hitstun - clip - hitstop * (1f - CombatSpec.HitstopTimeScale);
                            if (hold < CombatSpec.HoldDurationMin) { belowReachable = true; }
                            if (hold > CombatSpec.HoldDurationMax) { aboveReachable = true; }
                            if (belowReachable && aboveReachable) { break; }
                        }
                    }
                }
                if (!belowReachable || !aboveReachable) { v.Add(FHoldReachable); }
            }

            // F-HITSTOP-CAUSAL：0.15 × hitstop_frames ≤ 1（因果式，不是"≤6"）
            for (int i = 0; i < s.Knockbacks.Count; i++)
            {
                if (s.Knockbacks[i].HitstopTimeScale * s.Knockbacks[i].HitstopFrames > 1f) { v.Add(FHitstopCausal); }
            }

            // F-RECOVERY-GAP：T(位移归零) + 2 ≤ T(收身起始)
            //   T(归零) = hitstop_frames + 后段窗口帧数(6)
            //   T(收身) = 收身起始进度 + 0.85 × hitstop_frames
            for (int i = 0; i < s.Knockbacks.Count; i++)
            {
                QaKnockbackRow k = s.Knockbacks[i];
                // 【D-05 裁定】后段窗口帧数来自击退曲线的派生量，收身起始是参数；
                // 两者都不是写死的常数（默认值 6 / 9 只是当前规格）。
                float tZero = k.HitstopFrames + k.RearWindowFrames;
                float tRec = k.RecoveryStartProgress + 0.85f * k.HitstopFrames;
                if (tZero + CombatSpec.RecoveryGapMin > tRec) { v.Add(FRecoveryGap); }
            }

            // F-KB-TIER：击退距离必须落在档位区间内（含端点）
            for (int i = 0; i < s.Knockbacks.Count; i++)
            {
                QaKnockbackRow k = s.Knockbacks[i];
                float min = TierDistMin(k.Tier);
                float max = TierDistMax(k.Tier);
                if (k.DistMin < min - 1e-4f || k.DistMax > max + 1e-4f) { v.Add(FKbTier); }
            }

            // F-KB-PEAK：峰值速度 ≤ 18 m/s，且最坏档(30fps)逐帧 ≤ 0.5 m/帧，
            //            且**实际运行帧率**下的逐帧位移同样 ≤ 0.5 m/帧。
            //            （不与"30fps 推导值"比较——那会把 60fps 实现误杀，见 B-03）
            for (int i = 0; i < s.Knockbacks.Count; i++)
            {
                QaKnockbackRow k = s.Knockbacks[i];
                if (k.PeakSpeed > CombatSpec.KnockbackPeakSpeedMax) { v.Add(FKbPeak); continue; }
                float worstFrame = k.PeakSpeed / CombatSpec.WorstCaseFps;
                if (worstFrame > CombatSpec.KnockbackPerFrameMaxAtWorstFps + 1e-4f) { v.Add(FKbPeak); continue; }
                float fps = s.RuntimeFps > 1f ? s.RuntimeFps : CombatSpec.WorstCaseFps;
                float runtimeFrame = k.PeakSpeed / fps;
                if (runtimeFrame > CombatSpec.KnockbackPerFrameMaxAtWorstFps + 1e-4f) { v.Add(FKbPeak); }
            }

            // F-DODGE-INVULN：闪避必须有无敌窗口，且长度 1..14 帧，且不越出位移段
            QaDodgeSpec d = s.Dodge;
            int span = d.InvulnEnd - d.InvulnStart;
            if (span < 1 || span > CombatSpec.DodgeInvulnMaxSpanFrames) { v.Add(FDodgeInvuln); }
            if (d.InvulnStart < 1) { v.Add(FDodgeInvuln); }
            if (d.InvulnEnd > d.MoveFrames) { v.Add(FDodgeInvuln); }

            // F-VFX-STRUCTURE：霸体 / 无敌必须引用不同 vfx，且 outline_style 必须不同
            //                （px 差 ≥2px 只记录，不参与判定 —— 见 B 类）
            var assets = new Dictionary<string, QaVfxAssetRow>(StringComparer.Ordinal);
            for (int i = 0; i < s.VfxAssets.Count; i++) { assets[s.VfxAssets[i].VfxId] = s.VfxAssets[i]; }
            for (int i = 0; i < s.VfxSpecs.Count; i++)
            {
                QaVfxSpecRow sp = s.VfxSpecs[i];
                if (sp.ArmorVfxId == sp.InvulnVfxId) { v.Add(FVfxStructure); continue; }
                QaVfxAssetRow armor, invuln;
                if (!assets.TryGetValue(sp.ArmorVfxId, out armor) || !assets.TryGetValue(sp.InvulnVfxId, out invuln))
                {
                    v.Add(FVfxStructure); continue;
                }
                if (armor.OutlineStyle == invuln.OutlineStyle) { v.Add(FVfxStructure); }
            }

            // F-PULSE-HZ：脉冲频率 ≤ 3Hz
            for (int i = 0; i < s.VfxAssets.Count; i++)
            {
                float hz = s.VfxAssets[i].PulseHz;
                if (hz < 0f || hz > CombatSpec.PulseHzMax) { v.Add(FPulseHz); }
            }

            // F-PULSE-WINDOW：窗口必须容得下一个脉冲周期（1000 ÷ (2 × hz) ms）
            //                 推导式，不是写死的 250ms（见 B-02 / D-缺陷登记）
            for (int i = 0; i < s.VfxAssets.Count; i++)
            {
                QaVfxAssetRow a = s.VfxAssets[i];
                if (a.PulseHz <= 0f) { continue; }
                double windowMs = a.WindowFrames * (1000.0 / CombatSpec.LogicFps);
                double needMs = 1000.0 / (2.0 * a.PulseHz);
                if (windowMs + 1e-6 < needMs) { v.Add(FPulseWindow); }
            }

            // F-BLADE-OCCLUSION：遮挡比 ≤ 30%（量的是遮挡比，不是占屏比 —— 见 B-01）
            if (s.BladeLightOcclusionRatio > CombatSpec.BladeLightOcclusionMax + 1e-6f) { v.Add(FBladeOcclusion); }

            // F-E3-LATENCY：输入延迟 ≤ 50ms
            if (s.MeasuredInputLatencyMs > CombatSpec.InputLatencyMaxMs + 1e-6f) { v.Add(FE3Latency); }

            // F-E3-SAME-SOURCE：两条时间戳必须非同源（否则测出来恒为 0 → 假通过）
            if (!s.InputTimestampPipelinesIndependent) { v.Add(FE3SameSource); }

            // F-D5-CONTROL：对照组必须同时消除颜色 / 描边形态 / 入窗时机三个通道
            if (!s.D5ControlEliminatedColor || !s.D5ControlEliminatedOutlineForm || !s.D5ControlEliminatedEntryTiming)
            {
                v.Add(FD5Control);
            }

            // F-DODGE-PUSH：闪避前 2 帧蹬地窗口的位移占比 ≤5%
            if (s.DodgePushWindowRatio < 0f || s.DodgePushWindowRatio > CombatSpec.DodgePushWindowRatioMax + 1e-6f)
            {
                v.Add(FDodgePush);
            }

            // F-HIT-FEEDBACK：命中 → 受击起始 ≤3 帧
            if (s.HitFeedbackFrames < 0 || s.HitFeedbackFrames > CombatSpec.HitFeedbackMaxFrames)
            {
                v.Add(FHitFeedback);
            }

            // F-KB-DIRECTION：击退方向必须与伤害来源方向相反（容差 = 最近 45° 扇区）
            {
                float expected = (s.DamageSourceDirectionDeg + 180f) % 360f;
                float delta = MathfAbs(s.KnockbackDirectionDeg - expected);
                if (delta > 180f) { delta = 360f - delta; }
                if (delta > CombatSpec.KnockbackDirectionToleranceDeg + 1e-3f) { v.Add(FKbDirection); }
            }

            // F-KB-WALL：不得推入墙内 / 推落平台（优先级高于方向与距离）
            if (s.KnockbackPushesIntoWall || s.KnockbackFallsOffPlatform) { v.Add(FKbWall); }

            // F-BOSS-TELEGRAPH：预警必须提前 ≥30 帧
            if (s.BossTelegraphFrames < CombatSpec.BossRangeReadMaxFrames) { v.Add(FBossTelegraph); }

            // F-TELEGRAPH-CHANNELS：地面圈 + 形状 + 音效三通道齐备
            if (!s.TelegraphHasGroundCircle || !s.TelegraphHasShape || !s.TelegraphHasAudio)
            {
                v.Add(FTelegraphChannels);
            }

            // F-INK-CONTRAST：墨线与背景亮度差 ≥60%（取"更亮的一端"，不是固定墨黑）
            if (s.InkContrastRatio < CombatSpec.InkContrastMin - 1e-6f) { v.Add(FInkContrast); }
            if (s.InkOutlinePx != CombatSpec.InkOutlinePx) { v.Add(FInkContrast); }

            // F-VFX-CHANNEL：一渲染器一材质槽；禁 AdditionalMaterial；Clear Coat 全局关闭
            if (s.MaterialSlotCount != 1 || s.HasAdditionalMaterial || s.ClearCoatEnabled) { v.Add(FVfxChannel); }

            // F-ELITE-HITBOX：精英的受击盒缩放必须与渲染体缩放一致
            if (Math.Abs(s.EliteColliderScale - s.EliteVisualScale) > 1e-3f) { v.Add(FEliteHitbox); }

            // F-HITCLIP-VARIANTS：4 变体齐备；反向变体接触点离地 >3cm 且持续 ≥2 帧
            if (s.HitClipVariantCount < CombatSpec.HitClipVariantCountMin) { v.Add(FHitClipVariants); }
            if (s.HitClipReverseContactHeightCm <= CombatSpec.HitClipContactHeightMinCm) { v.Add(FHitClipVariants); }
            if (s.HitClipReverseContactFrames < CombatSpec.HitClipContactFramesMin) { v.Add(FHitClipVariants); }
        }

        private static float MathfAbs(float v)
        {
            return v < 0f ? -v : v;
        }

        private static void EvalEdgeLevels(QaScenario s, CriterionVerdict v)
        {
            // 门槛：严格递增 + 每档 ≥1.3× 上一档 + ≤ 该档预期总战力
            var list = new List<QaDungeonRow>(s.Dungeons);
            list.Sort(delegate (QaDungeonRow a, QaDungeonRow b) { return a.CpThreshold.CompareTo(b.CpThreshold); });
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].CpThreshold > list[i].ExpectedTotalPower) { v.Add(FEdgeLevels); }
                if (i == 0) { continue; }
                float prev = list[i - 1].CpThreshold;
                if (list[i].CpThreshold <= prev) { v.Add(FEdgeLevels); continue; }
                if (list[i].CpThreshold < prev * CombatSpec.CpThresholdMinRatio - 1e-4f) { v.Add(FEdgeLevels); }
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // 辅助
        // ─────────────────────────────────────────────────────────────────

        private static int TotalFramesOfTag(QaScenario s, string tag)
        {
            for (int i = 0; i < s.Skills.Count; i++)
            {
                if (s.Skills[i].EntryTags.Contains(tag)) { return s.Skills[i].TotalFrames; }
            }
            return 0;
        }

        private static int PhaseCountOf(QaScenario s, string bossId)
        {
            for (int i = 0; i < s.Bosses.Count; i++)
            {
                if (s.Bosses[i].BossId == bossId) { return s.Bosses[i].PhaseCount; }
            }
            return 0;
        }

        private static string Head(string effectOrCondition)
        {
            if (string.IsNullOrEmpty(effectOrCondition)) { return effectOrCondition; }
            int idx = effectOrCondition.IndexOf(':');
            return idx < 0 ? effectOrCondition : effectOrCondition.Substring(0, idx);
        }

        /// <summary>5 种阶位类型（D-03 裁定的阶位不变量用）。</summary>
        public static readonly string[] AllUnlockTypes = { "stat", "resource", "derivative", "window", "mechanic" };

        private static QaDerivativeRow FindTier(List<QaDerivativeRow> rows, int tier)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Tier == tier) { return rows[i]; }
            }
            return null;
        }

        private static float TierDistMin(KnockbackTier tier)
        {
            switch (tier)
            {
                case KnockbackTier.Light: return CombatSpec.KnockbackDistMinLight;
                case KnockbackTier.Medium: return CombatSpec.KnockbackDistMinMedium;
                default: return CombatSpec.KnockbackDistMinHeavy;
            }
        }

        private static float TierDistMax(KnockbackTier tier)
        {
            switch (tier)
            {
                case KnockbackTier.Light: return CombatSpec.KnockbackDistMaxLight;
                case KnockbackTier.Medium: return CombatSpec.KnockbackDistMaxMedium;
                default: return CombatSpec.KnockbackDistMaxHeavy;
            }
        }
    }
}
