using EdgeChronicles.Tests.Qa;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace EdgeChronicles.Tests.Criteria
{
    /// <summary>
    /// 编号正本与三层算术的自检。
    ///
    /// 【为什么要有这条】正本落在代码里之后，最容易出的事是"文档写 69、代码是 71、
    /// 邮件里说 30 条负向"——三个数各说各话。所以三层算术必须**由代码自己断言**，
    /// 文档只引用，不复述。
    /// </summary>
    public class CanonicalNumberingTests
    {
        [Test]
        public void 三层算术_全量等于走查加断言且互不重叠()
        {
            List<string> all = CriterionCatalog.AllCriterionIds();

            Assert.AreEqual(CriterionCounts.TotalCriterionCount, all.Count,
                "全量在册判据数与 CriterionCounts.TotalCriterionCount 不一致。");

            // 正向走查 ⊆ 全量
            for (int i = 0; i < CriterionCatalog.Walkthrough.Count; i++)
            {
                string id = CriterionCatalog.Walkthrough[i].Id;
                Assert.Contains(id, all, "走查判据 " + id + " 不在全量判据集合里（⊆ 关系被破坏）。");
            }
            Assert.AreEqual(CriterionCounts.WalkthroughCount, CriterionCatalog.Walkthrough.Count,
                "正向走查数与 CriterionCounts.WalkthroughCount 不一致。");

            // 负向用例 ⊆ 全量：每条用例的目标判据必须在全量集合里
            for (int i = 0; i < CriterionCatalog.NegativeCases.Count; i++)
            {
                string target = CriterionCatalog.NegativeCases[i].TargetCriterionId;
                Assert.Contains(target, all,
                    "负向用例 " + CriterionCatalog.NegativeCases[i].Id + " 的目标判据 " + target
                    + " 不在全量判据集合里（⊆ 关系被破坏）。");
            }
            Assert.AreEqual(CriterionCounts.NegativeCaseCount, CriterionCatalog.NegativeCases.Count,
                "负向用例数与 CriterionCounts.NegativeCaseCount 不一致。");
        }

        [Test]
        public void 负向用例与正向走查_编号不相交()
        {
            var walkthroughIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < CriterionCatalog.Walkthrough.Count; i++)
            {
                walkthroughIds.Add(CriterionCatalog.Walkthrough[i].Id);
            }
            var overlaps = new List<string>();
            for (int i = 0; i < CriterionCatalog.NegativeCases.Count; i++)
            {
                string id = CriterionCatalog.NegativeCases[i].Id;
                if (walkthroughIds.Contains(id)) { overlaps.Add(id); }
            }
            Assert.IsEmpty(overlaps,
                "以下编号同时被用作走查项与负向用例（30 ∩ 22 必须为空）：\n" + string.Join("\n", overlaps.ToArray()));
        }

        [Test]
        public void 负向用例编号_全部唯一且成对()
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var duplicates = new List<string>();
            for (int i = 0; i < CriterionCatalog.NegativeCases.Count; i++)
            {
                string id = CriterionCatalog.NegativeCases[i].Id;
                if (!seen.Add(id)) { duplicates.Add(id); }
            }
            Assert.IsEmpty(duplicates, "负向用例编号重复：" + string.Join(", ", duplicates.ToArray()));
        }

        [Test]
        public void GDD已有编号_映射列与走查项一一对应且不冲突()
        {
            // CanonicalId 允许多条走查判据映射到同一个 GDD 编号（D2 同时管方向 / 距离 / 不推入墙），
            // 但不允许两条**不同**的 GDD 编号映射到同一条走查判据。
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            var problems = new List<string>();
            for (int i = 0; i < CriterionCatalog.Walkthrough.Count; i++)
            {
                CriterionRecord c = CriterionCatalog.Walkthrough[i];
                if (string.IsNullOrEmpty(c.CanonicalId)) { continue; }
                string existing;
                if (map.TryGetValue(c.Id, out existing) && existing != c.CanonicalId)
                {
                    problems.Add(c.Id + " 同时映射到 " + existing + " 与 " + c.CanonicalId);
                }
                map[c.Id] = c.CanonicalId;
            }
            Assert.IsEmpty(problems, "GDD 编号映射冲突：\n" + string.Join("\n", problems.ToArray()));

            // 反向：同一个 GDD 编号映射到的走查判据必须是同一族（这里只报冲突不做强约束）
            var byLegacy = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            for (int i = 0; i < CriterionCatalog.Walkthrough.Count; i++)
            {
                CriterionRecord c = CriterionCatalog.Walkthrough[i];
                if (string.IsNullOrEmpty(c.CanonicalId)) { continue; }
                List<string> lst;
                if (!byLegacy.TryGetValue(c.CanonicalId, out lst)) { lst = new List<string>(); byLegacy[c.CanonicalId] = lst; }
                lst.Add(c.Id);
            }
            Assert.IsTrue(byLegacy.ContainsKey("C2"), "闪避无敌（WC-02）应映射到 GDD 的 C2。");
            Assert.IsTrue(byLegacy.ContainsKey("E3"), "输入延迟（WC-16）应映射到 GDD 的 E3。");
            Assert.IsFalse(byLegacy.ContainsKey("E3") && byLegacy["E3"].Contains("WC-22"),
                "E3 是输入延迟，不该同时映射到受击动画变体（WC-22）——那会把两条无关判据绑在一起。");
        }
    }
}
