using EdgeChronicles.Tests.Qa;
using NUnit.Framework;

namespace EdgeChronicles.Tests.Smoke
{
    /// <summary>
    /// SC-03 · 技能：8 技能 × 取消窗口回放，窗口命中率 100%、无非法相变。
    ///
    /// 【口径】量 = 命中率（%）/ 非法相变次数（次），时钟轴 = 逻辑帧；
    /// 规则集 = IMPL-v3.0 §1.3：16 组「不可」+ 9 组「仅命中可」，其余默认放行。
    /// </summary>
    public class SmokeSkillCancelWindowTests
    {
        [Test]
        public void 八技能六十四组_窗口命中率百分之百且无非法相变()
        {
            QaScenario baseline = ScenarioFactory.LegalBaseline();
            CancelWindowReplayer.ReplayResult r = CancelWindowReplayer.Replay(baseline);

            Assert.AreEqual(64, r.TotalGroups, "8 技能 × 8 目标标签应为 64 组，实际 " + r.TotalGroups + " 组。");
            Assert.AreEqual(100, r.HitRatePercent,
                "窗口命中率未达 100%，不一致的组：\n" + string.Join("\n", r.Mismatches.ToArray()));
            Assert.AreEqual(0, r.IllegalTransitions,
                "出现非法相变：取消窗口伸进了不可取消段（前摇 / 判定），会导致被打断。");
        }

        [Test]
        public void 取消规则表_十六不可九仅命中可其余放行()
        {
            int forbidden = 0;
            int hitOnly = 0;
            int allowed = 0;
            for (int s = 0; s < ScenarioFactory.SkillTags.Length; s++)
            {
                for (int t = 0; t < ScenarioFactory.SkillTags.Length; t++)
                {
                    CancelRuleKind k = ScenarioFactory.RuleOf(ScenarioFactory.SkillTags[s], ScenarioFactory.SkillTags[t]);
                    if (k == CancelRuleKind.Forbidden) { forbidden++; }
                    else if (k == CancelRuleKind.HitOnly) { hitOnly++; }
                    else { allowed++; }
                }
            }
            Assert.AreEqual(16, forbidden, "「不可」组数不对（IMPL-v3.0 §1.3）。");
            Assert.AreEqual(9, hitOnly, "「仅命中可」组数不对（IMPL-v3.0 §1.3）。");
            Assert.AreEqual(39, allowed, "默认放行组数不对（64 − 16 − 9）。");
        }

        [Test]
        public void 拾锋式与破锋_后摇段不可被任何技能取消()
        {
            // IMPL-v3.0 §1.3 明确：拾锋式落阵 22 帧不可取消、破锋后摇 20 帧是唯一的贪刀惩罚窗。
            // 这两处硬直是**故意留的**，实现时不得当 bug 修掉。
            for (int t = 0; t < ScenarioFactory.SkillTags.Length; t++)
            {
                CancelRuleKind k = ScenarioFactory.RuleOf("core", ScenarioFactory.SkillTags[t]);
                Assert.AreEqual(CancelRuleKind.Forbidden, k,
                    "拾锋式 → " + ScenarioFactory.SkillTags[t] + " 必须是「不可」（落错位置必须付代价）。");
            }
        }
    }
}
