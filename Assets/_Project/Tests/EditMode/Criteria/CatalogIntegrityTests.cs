using EdgeChronicles.Tests.Qa;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace EdgeChronicles.Tests.Criteria
{
    /// <summary>
    /// 判据目录自检。
    ///
    /// 【作用】把 GDD §12.1 的判据四问变成会红的断言：
    /// ① 量纲声明 ② 时钟轴声明 ③ **能写出自己的误判条件** ④ 配一条负向用例。
    /// 少一条 → 这里红。防止判据在长期演进中被悄悄"降级"成没有证伪装置的口号。
    /// </summary>
    public class CatalogIntegrityTests
    {
        [Test]
        public void 走查判据_恰好二十二条()
        {
            Assert.AreEqual(22, CriterionCatalog.Walkthrough.Count,
                "走查判据应为 22 条（GDD §10.1 W4）。当前 " + CriterionCatalog.Walkthrough.Count + " 条。");
        }

        [Test]
        public void 负向用例_二十四条A类加六条B类()
        {
            List<NegativeCaseRecord> a = CriterionCatalog.CasesOfClass(NegativeCaseClass.Sensitivity);
            List<NegativeCaseRecord> b = CriterionCatalog.CasesOfClass(NegativeCaseClass.Specificity);
            Assert.AreEqual(24, a.Count, "A 类（灵敏度）负向用例应为 24 条。");
            Assert.AreEqual(6, b.Count, "B 类（特异性）负向用例应为 6 条。");
            Assert.AreEqual(30, CriterionCatalog.NegativeCases.Count, "负向用例总数应为 30 条。");
        }

        [Test]
        public void 每条走查判据_量纲与时钟轴都已声明()
        {
            var problems = new List<string>();
            for (int i = 0; i < CriterionCatalog.Walkthrough.Count; i++)
            {
                CriterionRecord c = CriterionCatalog.Walkthrough[i];
                if (c.Unit == QaUnit.None) { problems.Add(c.Id + " 缺量纲声明"); }
                if (c.Axis == QaClockAxis.None && !c.AxisNotApplicable) { problems.Add(c.Id + " 缺时钟轴声明（且未声明不适用）"); }
                if (string.IsNullOrEmpty(c.Statement)) { problems.Add(c.Id + " 缺判据正文"); }
            }
            Assert.IsEmpty(problems, "以下判据的量纲 / 时钟轴 / 正文不完整：\n" + string.Join("\n", problems.ToArray()));
        }

        [Test]
        public void 每条走查判据_都能写出自己的误判条件()
        {
            var problems = new List<string>();
            for (int i = 0; i < CriterionCatalog.Walkthrough.Count; i++)
            {
                CriterionRecord c = CriterionCatalog.Walkthrough[i];
                if (string.IsNullOrEmpty(c.FalsePositiveCondition) || c.FalsePositiveCondition.Trim().Length < 20)
                {
                    problems.Add(c.Id + " 的误判条件为空或过短");
                }
            }
            Assert.IsEmpty(problems,
                "以下判据写不出自己的误判条件 —— 按 GDD §12.1 判据四问 ③，不予收录：\n"
                + string.Join("\n", problems.ToArray()));
        }

        [Test]
        public void 每条负向用例_都能写出没报说明什么()
        {
            var problems = new List<string>();
            for (int i = 0; i < CriterionCatalog.NegativeCases.Count; i++)
            {
                NegativeCaseRecord c = CriterionCatalog.NegativeCases[i];
                if (string.IsNullOrEmpty(c.MeaningIfNotFired) || c.MeaningIfNotFired.Trim().Length < 10)
                {
                    problems.Add(c.Id + " 缺「没报说明什么」");
                }
                if (string.IsNullOrEmpty(c.Injection) || c.Injection.Trim().Length < 8)
                {
                    problems.Add(c.Id + " 缺注入描述");
                }
                if (string.IsNullOrEmpty(c.Expected)) { problems.Add(c.Id + " 缺期望结果"); }
                if (string.IsNullOrEmpty(c.SourceRef)) { problems.Add(c.Id + " 缺出处"); }
            }
            Assert.IsEmpty(problems, "负向用例字段不完整：\n" + string.Join("\n", problems.ToArray()));
        }

        [Test]
        public void 每条B类用例_都写清了哪条合法设计会被误杀()
        {
            var problems = new List<string>();
            List<NegativeCaseRecord> b = CriterionCatalog.CasesOfClass(NegativeCaseClass.Specificity);
            for (int i = 0; i < b.Count; i++)
            {
                if (string.IsNullOrEmpty(b[i].LegalDesignAtRisk) || b[i].LegalDesignAtRisk.Trim().Length < 20)
                {
                    problems.Add(b[i].Id + " 没写清「哪条合法设计会被误杀」——B 类的收录原则，缺这条就不该存在");
                }
            }
            Assert.IsEmpty(problems, "以下 B 类用例不满足收录原则：\n" + string.Join("\n", problems.ToArray()));
        }

        [Test]
        public void 每条走查判据_都有负向用例或明确的豁免理由()
        {
            var problems = new List<string>();
            var targets = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < CriterionCatalog.NegativeCases.Count; i++)
            {
                targets.Add(CriterionCatalog.NegativeCases[i].TargetCriterionId);
            }
            for (int i = 0; i < CriterionCatalog.Walkthrough.Count; i++)
            {
                CriterionRecord c = CriterionCatalog.Walkthrough[i];
                if (c.LinkedOracleIds.Count == 0)
                {
                    if (string.IsNullOrEmpty(c.NegativeCaseWaiver))
                    {
                        problems.Add(c.Id + " 既没有可脚本判据，也没有写明豁免负向用例的理由");
                    }
                    continue;
                }
                bool covered = false;
                for (int k = 0; k < c.LinkedOracleIds.Count; k++)
                {
                    if (targets.Contains(c.LinkedOracleIds[k])) { covered = true; break; }
                }
                if (!covered)
                {
                    problems.Add(c.Id + " 没有任何负向用例覆盖它对应的判据（"
                        + string.Join(",", c.LinkedOracleIds.ToArray()) + "）");
                }
            }
            Assert.IsEmpty(problems,
                "以下走查判据缺少证伪装置（GDD §12.1 ④）：\n" + string.Join("\n", problems.ToArray()));
        }

        [Test]
        public void 可执行化分布_九脚本七混合六人眼()
        {
            int auto = 0, mixed = 0, human = 0;
            for (int i = 0; i < CriterionCatalog.Walkthrough.Count; i++)
            {
                CriterionExecutability e = CriterionCatalog.Walkthrough[i].Executability;
                if (e == CriterionExecutability.AutoScript) { auto++; }
                else if (e == CriterionExecutability.Mixed) { mixed++; }
                else { human++; }
            }
            Assert.AreEqual(9, auto, "纯脚本判据应为 9 条（GDD §10.1 W4）。");
            Assert.AreEqual(7, mixed, "混合判据应为 7 条（GDD §10.1 W4）。");
            Assert.AreEqual(6, human, "必须人眼判据应为 6 条（GDD §10.1 W4）。");
        }

        [Test]
        public void 每条人眼判据_都指向写死机位与慢放倍数的录屏协议()
        {
            var problems = new List<string>();
            for (int i = 0; i < CriterionCatalog.Walkthrough.Count; i++)
            {
                CriterionRecord c = CriterionCatalog.Walkthrough[i];
                if (c.Executability == CriterionExecutability.HumanEye && !c.ExecutableAs.Contains("HP-"))
                {
                    problems.Add(c.Id + " 是人眼项，但没有指向录屏协议编号（HP-xx）");
                }
                if (c.Executability == CriterionExecutability.Mixed && !c.ExecutableAs.Contains("HP-"))
                {
                    problems.Add(c.Id + " 是混合项，人眼部分必须同样指向录屏协议编号");
                }
            }
            Assert.IsEmpty(problems, "人眼项缺固定复现条件：\n" + string.Join("\n", problems.ToArray()));
        }

        [Test]
        public void 三层关系_负向三十与走查二十二互不相交()
        {
            // D-06 裁定：代码即正本，三条数字必须由同一部目录给出，不能各自漂移。
            string relations = CriterionCatalog.DescribeRelations();
            Assert.IsTrue(relations.Contains("负向用例 30 条"), "负向用例数不是 30：" + relations);
            Assert.IsTrue(relations.Contains("正向走查 22 条"), "正向走查数不是 22：" + relations);

            // 30 与 22 不相交：负向用例的 TargetCriterionId 不应等于任何走查判据的 Id。
            for (int i = 0; i < CriterionCatalog.NegativeCases.Count; i++)
            {
                NegativeCaseRecord c = CriterionCatalog.NegativeCases[i];
                Assert.IsNull(CriterionCatalog.FindCriterion(c.TargetCriterionId),
                    "负向用例 " + c.Id + " 的目标判据 " + c.TargetCriterionId
                    + " 与走查判据同名 —— 两套集合相交，三层关系就不成立了。");
            }
        }

        [Test]
        public void 每条走查判据_都声明了正本编号或明确为空()
        {
            // 正本编号沿用 GDD 既有 ID；没有的填 "—"，**不允许留空字符串**（那等于"忘了填"）。
            var problems = new List<string>();
            for (int i = 0; i < CriterionCatalog.Walkthrough.Count; i++)
            {
                CriterionRecord c = CriterionCatalog.Walkthrough[i];
                if (string.IsNullOrEmpty(c.CanonicalId)) { problems.Add(c.Id + " 的 CanonicalId 为空"); }
            }
            Assert.IsEmpty(problems, "正本编号缺失：\n" + string.Join("\n", problems.ToArray()));
        }

        [Test]
        public void 规格冲突清单_每条都带出处与影响()
        {
            Assert.Greater(KnownSpecConflicts.All.Count, 0, "规格冲突清单为空——判据缺陷必须显式登记，不允许静默通过。");
            var problems = new List<string>();
            for (int i = 0; i < KnownSpecConflicts.All.Count; i++)
            {
                SpecConflict c = KnownSpecConflicts.All[i];
                if (string.IsNullOrEmpty(c.SourceA) || string.IsNullOrEmpty(c.SourceB)) { problems.Add(c.Id + " 缺出处"); }
                if (string.IsNullOrEmpty(c.Impact)) { problems.Add(c.Id + " 缺影响说明"); }
                if (string.IsNullOrEmpty(c.Resolution)) { problems.Add(c.Id + " 缺处置状态"); }
            }
            Assert.IsEmpty(problems, "规格冲突登记不完整：\n" + string.Join("\n", problems.ToArray()));
        }
    }
}
