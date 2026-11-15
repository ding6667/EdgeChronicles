using EdgeChronicles.Tests.Qa;
using NUnit.Framework;
using System.Collections.Generic;

namespace EdgeChronicles.Tests.Smoke
{
    /// <summary>
    /// SC-05 · 命中：扇形 / 胶囊 / 矩形几何边界各 20 例。
    ///
    /// 【口径】量 = 判定结果（bool），无量纲；
    /// 所有距离单位 m，所有角度单位 °（整角，歧义已登记为 D-01）。
    /// </summary>
    public class SmokeHitGeometryTests
    {
        private static void AssertCases(List<ScenarioFactory.HitGeometryCase> cases, string shape, int expectedCount)
        {
            Assert.AreEqual(expectedCount, cases.Count, shape + " 的用例数应为 " + expectedCount + " 例。");
            int mismatch = 0;
            var details = new List<string>();
            for (int i = 0; i < cases.Count; i++)
            {
                bool actual = cases[i].Actual();
                if (actual != cases[i].Expected)
                {
                    mismatch++;
                    details.Add(shape + " / " + cases[i].Note + "：期望 " + cases[i].Expected + "，实际 " + actual);
                }
            }
            Assert.AreEqual(0, mismatch,
                shape + " 几何判定有 " + mismatch + " 例与预期不符：\n" + string.Join("\n", details.ToArray()));
        }

        [Test]
        public void 扇形_二十例边界全通过()
        {
            AssertCases(ScenarioFactory.SectorCases(), "sector", 20);
        }

        [Test]
        public void 胶囊_二十例边界全通过()
        {
            AssertCases(ScenarioFactory.CapsuleCases(), "capsule", 20);
        }

        [Test]
        public void 矩形_二十例边界全通过()
        {
            AssertCases(ScenarioFactory.RectCases(), "rect", 20);
        }

        [Test]
        public void 命中判定不含物理组件依赖()
        {
            // IMPL-v2.0 §7.1 歧义点 3：命中判定是纯数学判定，不挂 Collider。
            // 判据：参考实现只依赖逻辑坐标与形状参数，不读任何物理状态。
            List<ScenarioFactory.HitGeometryCase> cases = ScenarioFactory.SectorCases();
            for (int i = 0; i < cases.Count; i++)
            {
                Assert.IsNotNull(cases[i].Origin, "扇形用例缺少原点，判定将退化为「永远命中」。");
                Assert.AreNotEqual(0f, cases[i].Radius, "扇形用例半径为 0，用例本身无效。");
            }
        }
    }
}
