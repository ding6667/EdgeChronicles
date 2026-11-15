using EdgeChronicles.Tests.Qa;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace EdgeChronicles.Tests.Criteria
{
    /// <summary>
    /// 裁定后口径的落地自检。
    ///
    /// 【为什么这三条单独成文件】
    /// 它们都是**裁定直接改出来的**口径，每一条都曾经让既有负向用例失效：
    ///   · D-10：A22 的标签集合扩容 → 原"allowTag=dodge 应报红"的用例不再成立；
    ///   · D-03：A33 从固定映射改为阶位不变量 → 基线夹具必须整体重建；
    ///   · D-02 / D-13：曲线 keys 的量纲两条裁定冲突 → 判据必须能在**两种口径下都判定**。
    /// 这三条测试的作用是：**裁定落地后，口径漂移会立刻红**，而不是等下一次踩坑才发现。
    /// </summary>
    public class RulingComplianceTests
    {
        [Test]
        public void A22_标签全集含门派共有动作标签_dodge与attack必须合法()
        {
            // 【D-10 裁定】allowTags 必须落在：8 技能 entryTags ∪ {dodge, attack}。
            QaScenario s = ScenarioFactory.LegalBaseline();
            s.CancelWindows[0].AllowTag = "dodge";
            CriterionVerdict verdict = CriterionOracle.Evaluate(s);
            Assert.IsFalse(verdict.IsFired(CriterionOracle.A22),
                "「dodge」是门派共有动作标签，A22 不该报红（D-10 裁定后它是合法的）。");

            s = ScenarioFactory.LegalBaseline();
            s.CancelWindows[0].AllowTag = "attack";
            verdict = CriterionOracle.Evaluate(s);
            Assert.IsFalse(verdict.IsFired(CriterionOracle.A22),
                "「attack」是门派共有动作标签，A22 不该报红。");

            // 真正未知的标签仍必须报红
            s = ScenarioFactory.LegalBaseline();
            s.CancelWindows[0].AllowTag = "not_a_real_tag";
            verdict = CriterionOracle.Evaluate(s);
            Assert.IsTrue(verdict.IsFired(CriterionOracle.A22),
                "未知标签必须报红——A22 现在抓不住「引用了不存在的标签」。");
        }

        [Test]
        public void A33_阶位不变量_每技能五阶恰好覆盖五种类型()
        {
            // 【D-03 裁定】不存在全局固定的 tier → unlock_type 映射；
            // 不变量 = ① 5 阶恰好覆盖 5 种类型各一次 ② tier1 恒 stat ③ tier5 恒 mechanic。
            QaScenario s = ScenarioFactory.LegalBaseline();
            var bySkill = new Dictionary<string, List<QaDerivativeRow>>(StringComparer.Ordinal);
            for (int i = 0; i < s.Derivatives.Count; i++)
            {
                List<QaDerivativeRow> lst;
                if (!bySkill.TryGetValue(s.Derivatives[i].SkillId, out lst)) { lst = new List<QaDerivativeRow>(); bySkill[s.Derivatives[i].SkillId] = lst; }
                lst.Add(s.Derivatives[i]);
            }
            Assert.AreEqual(8, bySkill.Count, "基线应覆盖 8 个技能的阶位。");

            foreach (KeyValuePair<string, List<QaDerivativeRow>> kv in bySkill)
            {
                List<QaDerivativeRow> rows = kv.Value;
                Assert.AreEqual(5, rows.Count, kv.Key + " 应有恰好 5 阶（D-03 裁定的不变量 ①）。");
                var types = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < rows.Count; i++) { types.Add(rows[i].UnlockType); }
                Assert.AreEqual(5, types.Count, kv.Key + " 的 5 阶必须覆盖 5 种 unlock_type 各一次。");
                for (int i = 0; i < rows.Count; i++)
                {
                    if (rows[i].Tier == 1) { Assert.AreEqual("stat", rows[i].UnlockType, kv.Key + " 的 tier 1 必须恒为 stat。"); }
                    if (rows[i].Tier == 5) { Assert.AreEqual("mechanic", rows[i].UnlockType, kv.Key + " 的 tier 5 必须恒为 mechanic。"); }
                }
            }
        }

        [Test]
        public void A24_曲线基线在终裁口径下合法()
        {
            // 【D-02 / D-13 终裁】只保留逐帧速度（m/s）一种口径 ——
            // 两套长期并存就是第三个真值源；归一化路径已删除。
            // 保留 `CurveSemantics` 这个开关本身，是为了将来美术若改了 MP_HERO_* 的单位语义，
            // 一个开关就能切回去重新评估 —— 那是一个回退点，不是两套判据。
            QaScenario s = ScenarioFactory.LegalBaseline();
            CriterionVerdict verdict = CriterionOracle.Evaluate(s);
            Assert.IsEmpty(verdict.Fired, "终裁口径（逐帧速度 m/s）下基线不合法：" + verdict);
            Assert.AreEqual(CurveKeySemantics.SpeedMetersPerSecond, CurveSemantics.Current,
                "曲线口径必须是终裁的逐帧速度（m/s）。");
        }

        [Test]
        public void A29_连段模拟跑过顿帧帧且帧数够长()
        {
            // 【D-09 裁定】固定的标准连段序列 + 固定时间步长（60Hz，600 帧）+ 顿帧帧也要跑。
            // A29 的目的是验「状态机不会产生非法状态」，不是验「某一条连段能跑通」。
            QaScenario s = ScenarioFactory.LegalBaseline();
            Assert.AreEqual(600, s.ComboSimulationFrames, "标准连段模拟帧数应为 600。");
            Assert.AreEqual(1f / CombatSpec.LogicFps, s.ComboFrameDeltaSeconds, 1e-6f, "步长应为逻辑 60Hz。");
            Assert.Greater(s.ComboHitstopFrames, 0, "连段模拟必须跑过顿帧帧（顿帧不暂停 tick）。");
            Assert.AreEqual(8, s.ComboSequence.Count, "主序列（熟练链）应覆盖 8 个技能。");
        }

        [Test]
        public void D04_hold_换算为负必须报错而不是取零()
        {
            // 【D-04 裁定】hold = hitstun − clip_W − hitstop×(1−time_scale)；换算后 hold < 0 → 报错，**不取 0**。
            QaScenario s = ScenarioFactory.LegalBaseline();
            for (int i = 0; i < s.Knockbacks.Count; i++)
            {
                if (s.Knockbacks[i].Tier == KnockbackTier.Light)
                {
                    s.Knockbacks[i].AnimClipLengthC = 17;          // clip_W = 17
                    s.Knockbacks[i].HoldDuration = s.Knockbacks[i].ComputedHold(); // = −0.7
                }
            }
            Assert.Less(s.Knockbacks[0].HoldDuration, 0f, "本用例的前提没建立：hold 应当为负。");
            CriterionVerdict verdict = CriterionOracle.Evaluate(s);
            Assert.IsTrue(verdict.IsFired(CriterionOracle.FHold),
                "hold 换算为负却没报红——实现若按「取 0」处理，硬直会短于剪辑本身。");
        }

        [Test]
        public void D14_hold区间端点在time_scale015下不可达_已登记为待裁定()
        {
            // D-14：hold = 整数 − 整数 − hitstop×0.85，而 0.85×{2,4,6} = {1.7,3.4,5.1}，
            // 故 hold 的小数部分恒为 .3/.6/.7，**永远不等于 2.0 或 4.0**。
            // 这条测试把该事实固定下来：哪天 time_scale 或区间改了，端点变得可达，它会红。
            bool anyEndpointReachable = false;
            for (int hitstun = 18; hitstun <= 22 && !anyEndpointReachable; hitstun++)
            {
                for (int hitstop = 2; hitstop <= 6; hitstop += 2)
                {
                    for (int clip = 1; clip <= 30; clip++)
                    {
                        float hold = hitstun - clip - hitstop * (1f - CombatSpec.HitstopTimeScale);
                        if (Math.Abs(hold - CombatSpec.HoldDurationMin) < 1e-4f
                            || Math.Abs(hold - CombatSpec.HoldDurationMax) < 1e-4f)
                        {
                            anyEndpointReachable = true;
                            break;
                        }
                    }
                }
            }
            Assert.IsFalse(anyEndpointReachable,
                "hold 的区间端点已变得可达——D-14 的前提变了，请更新 D-14 的裁定并把 NC-B06 改回用真实端点。");
            Assert.IsNotNull(KnownSpecConflicts.Find("D-14"), "D-14 必须登记在案。");
        }
    }
}
