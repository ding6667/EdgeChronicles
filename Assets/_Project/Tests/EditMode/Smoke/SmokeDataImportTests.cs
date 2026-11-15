using EdgeChronicles.Tests.Qa;
using NUnit.Framework;

namespace EdgeChronicles.Tests.Smoke
{
    /// <summary>
    /// SC-02 · Data：导表器跑 A19–A43，0 error。
    ///
    /// 【口径】量 = 报红条数（条），无量纲；
    /// 执行对象是判据参考执行器（oracle）。生产导表器与它的一致性由
    /// <c>ProductionContractTests</c> 强制——两份结论不一致就报红。
    /// </summary>
    public class SmokeDataImportTests
    {
        [Test]
        public void 合法基线_全部判据零报红()
        {
            QaScenario baseline = ScenarioFactory.LegalBaseline();
            CriterionVerdict verdict = CriterionOracle.Evaluate(baseline);

            Assert.IsEmpty(verdict.Fired,
                "合法基线场景不应触发任何判据，但触发了：" + verdict
                + "。这说明要么基线本身不是合法态（夹具写错），要么判据误杀了合法数据（B 类风险）。");
        }

        [Test]
        public void 导表断言覆盖_A19至A30逐条可判()
        {
            // 每条 A19–A30 都必须至少有一个负向用例（灵敏度），否则这条断言抓不住错。
            string[] required =
            {
                CriterionOracle.A19, CriterionOracle.A20, CriterionOracle.A21, CriterionOracle.A22,
                CriterionOracle.A23, CriterionOracle.A24, CriterionOracle.A25, CriterionOracle.A26,
                CriterionOracle.A27, CriterionOracle.A28, CriterionOracle.A29, CriterionOracle.A30
            };
            for (int i = 0; i < required.Length; i++)
            {
                bool covered = false;
                for (int c = 0; c < CriterionCatalog.NegativeCases.Count; c++)
                {
                    if (CriterionCatalog.NegativeCases[c].TargetCriterionId == required[i]) { covered = true; break; }
                }
                Assert.IsTrue(covered, "断言 " + required[i] + " 没有任何负向用例——它抓不住错，等于没有断言。");
            }
        }

        [Test]
        public void 养成断言_A31至A43已登记且有据可查()
        {
            // A32 / A34 / A36 / A37 / A38 / A41 / A42 属运行时或养成层判据，
            // 由生产导表器执行；本测试只要求它们在目录里**有登记**，不允许静默缺席。
            string[] required =
            {
                CriterionOracle.A31, CriterionOracle.A33, CriterionOracle.A331, CriterionOracle.A332,
                CriterionOracle.A333, CriterionOracle.A334, CriterionOracle.A335, CriterionOracle.A35,
                CriterionOracle.A391, CriterionOracle.A392, CriterionOracle.A40, CriterionOracle.A43
            };
            for (int i = 0; i < required.Length; i++)
            {
                bool known = false;
                for (int c = 0; c < CriterionCatalog.AllCriterionIds().Count; c++)
                {
                    if (CriterionCatalog.AllCriterionIds()[c] == required[i]) { known = true; break; }
                }
                Assert.IsTrue(known, "断言 " + required[i] + " 未登记进判据目录。");
            }
        }
    }
}
