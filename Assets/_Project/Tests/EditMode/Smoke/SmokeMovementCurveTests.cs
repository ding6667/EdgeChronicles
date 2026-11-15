using EdgeChronicles.Tests.Qa;
using NUnit.Framework;

namespace EdgeChronicles.Tests.Smoke
{
    /// <summary>
    /// SC-04 · 位移：曲线积分距离与声明偏差 ≤5%。
    ///
    /// 【口径】量 = 相对偏差（%，无量纲），时钟轴 = 逻辑帧；
    /// 积分口径见 CurveIntegratorReference（keys 单位按 m 逐帧位移，歧义已登记为 D-02）。
    /// </summary>
    public class SmokeMovementCurveTests
    {
        [Test]
        public void 合法曲线_积分距离与声明一致()
        {
            QaScenario baseline = ScenarioFactory.LegalBaseline();
            for (int i = 0; i < baseline.MovementCurves.Count; i++)
            {
                QaMovementCurveRow c = baseline.MovementCurves[i];
                float integral = CurveIntegratorReference.IntegrateDistance(c);
                float deviation = CurveIntegratorReference.RelativeDeviation(c);
                Assert.LessOrEqual(deviation, CombatSpec.CurveIntegralTolerance,
                    "曲线 " + c.CurveId + " 的积分距离 " + integral + "m 与声明 " + c.DeclaredDistance
                    + "m 的偏差超过 " + (CombatSpec.CurveIntegralTolerance * 100f) + "%。");
            }
        }

        [Test]
        public void 曲线偏差超限_A24必须报红()
        {
            QaScenario s = ScenarioFactory.LegalBaseline();
            s.MovementCurves[0].DeclaredDistance = 3.0f;
            // 注入一个 8% 偏差：积分 2.76m 对声明 3.0m
            s.MovementCurves[0].Keys = new System.Collections.Generic.List<float> { 0.20f, 0.40f, 0.25f, 0.05f, 0.02f };

            float deviation = CurveIntegratorReference.RelativeDeviation(s.MovementCurves[0]);
            Assert.Greater(deviation, CombatSpec.CurveIntegralTolerance, "注入的偏差没有超过 5%，用例本身失效。");

            CriterionVerdict verdict = CriterionOracle.Evaluate(s);
            Assert.IsTrue(verdict.IsFired(CriterionOracle.A24),
                "A24 未报红。偏差 " + (deviation * 100f) + "% 超过阈值却没被抓住——这条断言已经废了。");
        }
    }
}
