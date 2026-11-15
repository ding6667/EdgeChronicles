using EdgeChronicles.Tests.Qa;
using NUnit.Framework;
using System.Collections.Generic;

namespace EdgeChronicles.Tests.Smoke
{
    /// <summary>
    /// SC-07 · Boss：3 份 rule 冷启动，Effect 触发序列 hash 相同。
    ///
    /// 【口径】量 = 序列 hash（16 进制字符串，无量纲）；
    /// "冷启动"= 不带任何前置状态的求值（同一条规则不得依赖上一次求值的残留）。
    /// </summary>
    public class SmokeBossRuleTests
    {
        [Test]
        public void 三份规则_三次冷启动序列hash相同()
        {
            QaScenario baseline = ScenarioFactory.LegalBaseline();
            for (int b = 0; b < baseline.Bosses.Count; b++)
            {
                QaBossRow boss = baseline.Bosses[b];
                string first = null;
                for (int run = 0; run < 3; run++)
                {
                    List<string> seq = BossRuleReference.EvaluateSequence(boss, baseline.BossRules, baseline);
                    string hash = BossRuleReference.SequenceHash(seq);
                    if (first == null) { first = hash; continue; }
                    Assert.AreEqual(first, hash,
                        "Boss " + boss.BossId + " 第 " + (run + 1) + " 次冷启动的 Effect 序列 hash 与第一次不同 → "
                        + "规则求值依赖了残留状态或使用了不稳定遍历顺序。");
                }
                Assert.IsNotEmpty(first, "Boss " + boss.BossId + " 的冷启动序列为空。");
            }
        }

        [Test]
        public void 三个Boss_机制维度不同_序列互不相同()
        {
            QaScenario baseline = ScenarioFactory.LegalBaseline();
            var hashes = new List<string>();
            for (int b = 0; b < baseline.Bosses.Count; b++)
            {
                List<string> seq = BossRuleReference.EvaluateSequence(baseline.Bosses[b], baseline.BossRules, baseline);
                hashes.Add(BossRuleReference.SequenceHash(seq));
            }
            for (int i = 0; i < hashes.Count; i++)
            {
                for (int j = i + 1; j < hashes.Count; j++)
                {
                    Assert.AreNotEqual(hashes[i], hashes[j],
                        "两个 Boss 的 Effect 序列完全相同 → 机制维度没有拉开（IMPL-v2.0 §4.1）。");
                }
            }
        }

        [Test]
        public void 规则求值_枚举合法且机关可解析()
        {
            QaScenario baseline = ScenarioFactory.LegalBaseline();
            CriterionVerdict verdict = CriterionOracle.Evaluate(baseline);
            Assert.IsFalse(verdict.IsFired(CriterionOracle.A26), "boss_rule 出现枚举外的 trigger / condition / effect。");
            Assert.IsFalse(verdict.IsFired(CriterionOracle.A27), "ForcePhase 指向不存在的阶段。");
            Assert.IsFalse(verdict.IsFired(CriterionOracle.A28), "存在无法解析的 anchor:// 引用。");
        }
    }
}
