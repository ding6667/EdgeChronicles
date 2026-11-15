using EdgeChronicles.Tests.Qa;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace EdgeChronicles.Tests.Criteria
{
    /// <summary>
    /// B 类负向用例（特异性 / 防误杀）：6 条。
    ///
    /// 【为什么这一类比 A 类更重要】
    /// 项目历史上三次翻车（刀光面积阈值、脉冲频率常量、帧率依赖单位）注入的**都不是错误，是合法设计**——
    /// 是判据**量错了对象**。A 类永远抓不到这类。
    /// 所以 B 类是本项目的核心防线：每条都写清"哪条合法设计会被误杀"，
    /// 并且在注入合法变体后断言判据**必须通过**。
    ///
    /// 【红线的反向】B 类失败 = 判据误杀了一个正确实现 → 正确动作是**重写判据**，
    /// 不是把合法设计改坏来迁就判据。
    /// </summary>
    public class NegativeCaseClassBTests
    {
        private static void Inject(string caseId, QaScenario s)
        {
            switch (caseId)
            {
                case "NC-B01": // 合法的竖直刀光：遮挡比 12%，投影占屏 0.5%
                    s.BladeLightOcclusionRatio = 0.12f;
                    s.BladeLightScreenCoverageRatio = 0.005f;
                    return;

                case "NC-B02": // 合法的低频脉冲：1Hz / 窗口 600ms；以及默认 0Hz
                    s.VfxAssets[0].PulseHz = 1f;
                    s.VfxAssets[0].WindowFrames = 36;   // 36 帧 @60Hz = 600ms ≥ 1000÷(2×1) = 500ms
                    s.VfxAssets[1].PulseHz = 0f;
                    s.VfxAssets[1].WindowFrames = 5;
                    return;

                case "NC-B03": // 合法的 60fps 实现
                    s.RuntimeFps = 60f;
                    for (int i = 0; i < s.Knockbacks.Count; i++)
                    {
                        if (s.Knockbacks[i].Tier == KnockbackTier.Heavy) { s.Knockbacks[i].PeakSpeed = 13.2f; }
                    }
                    return;

                case "NC-B04": // 刻意的难度断崖：铁砧窟 900 → 700（7.0×）
                    for (int i = 0; i < s.Dungeons.Count; i++)
                    {
                        if (s.Dungeons[i].DungeonId == "dungeon_tiekuang") { s.Dungeons[i].CpThreshold = 700; }
                    }
                    return;

                case "NC-B05": // 新增一条 tier=3 / visual_tier=generic 的合法派生（双向引用齐备）
                    {
                        var d = new QaDerivativeRow();
                        d.SkillId = "sk_jufeng";
                        d.Tier = 3;
                        d.UnlockType = "derivative";
                        d.VisualTier = "generic";
                        d.UnlockDerivativeId = "dv_jufeng_extra";
                        d.CostEu = 3;
                        s.Derivatives.Add(d);
                        for (int i = 0; i < s.Skills.Count; i++)
                        {
                            if (s.Skills[i].Id == "sk_jufeng") { s.Skills[i].DerivativeIds.Add("dv_jufeng_extra"); }
                        }
                    }
                    return;

                case "NC-B06": // 区间端点：hold = 2.0 / 4.0，重档击退 1.2 / 1.5m
                    for (int i = 0; i < s.Knockbacks.Count; i++)
                    {
                        QaKnockbackRow k = s.Knockbacks[i];
                        if (k.Tier == KnockbackTier.Light) { k.HoldDuration = CombatSpec.HoldDurationMin; }
                        if (k.Tier == KnockbackTier.Medium) { k.HoldDuration = CombatSpec.HoldDurationMax; }
                        if (k.Tier == KnockbackTier.Heavy)
                        {
                            k.DistMin = CombatSpec.KnockbackDistMinHeavy;
                            k.DistMax = CombatSpec.KnockbackDistMaxHeavy;
                        }
                    }
                    return;

                default:
                    Assert.Fail("没有为 " + caseId + " 编写注入动作。新增 B 类用例时必须同时补上注入。");
                    return;
            }
        }

        [Test]
        public void 六条B类用例_注入合法设计后判据必须通过()
        {
            List<NegativeCaseRecord> cases = CriterionCatalog.CasesOfClass(NegativeCaseClass.Specificity);
            Assert.AreEqual(6, cases.Count, "B 类用例数不对。");

            var failures = new List<string>();
            for (int i = 0; i < cases.Count; i++)
            {
                NegativeCaseRecord c = cases[i];
                QaScenario s = ScenarioFactory.Clone(ScenarioFactory.LegalBaseline());
                Inject(c.Id, s);

                CriterionVerdict verdict = CriterionOracle.Evaluate(s);
                if (verdict.IsFired(c.TargetCriterionId))
                {
                    failures.Add(c.Id + "（目标 " + c.TargetCriterionId + "）把一个合法设计判成了违规。"
                        + "被误杀的合法设计：" + c.LegalDesignAtRisk
                        + "。**正确动作是重写判据，不是改坏合法设计。**");
                }
            }
            Assert.IsEmpty(failures, "以下 B 类用例失败（判据误杀了正确实现）：\n" + string.Join("\n", failures.ToArray()));
        }

        [Test]
        public void 六条B类用例_注入后场景整体仍然合法()
        {
            // 合法变体注入后，整张表仍应是合法态：
            // 若顺带触发了别的判据，说明注入本身越界了（一次只改一处）。
            List<NegativeCaseRecord> cases = CriterionCatalog.CasesOfClass(NegativeCaseClass.Specificity);
            var failures = new List<string>();
            for (int i = 0; i < cases.Count; i++)
            {
                NegativeCaseRecord c = cases[i];
                QaScenario s = ScenarioFactory.Clone(ScenarioFactory.LegalBaseline());
                Inject(c.Id, s);
                CriterionVerdict verdict = CriterionOracle.Evaluate(s);
                if (verdict.Fired.Count > 0)
                {
                    failures.Add(c.Id + " 注入后报红：" + verdict);
                }
            }
            Assert.IsEmpty(failures, "以下 B 类注入越界（合法变体不该让别的判据报红）：\n"
                + string.Join("\n", failures.ToArray()));
        }
    }
}
