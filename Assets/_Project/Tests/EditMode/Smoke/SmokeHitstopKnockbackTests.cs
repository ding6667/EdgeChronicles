using EdgeChronicles.Tests.Qa;
using NUnit.Framework;

namespace EdgeChronicles.Tests.Smoke
{
    /// <summary>
    /// SC-06 · 顿帧：hitstop_time_scale = 0.15、受击三档、硬直 18/20/22。
    ///
    /// 【口径】量 = 帧（逻辑帧）/ 米 / 米每秒；
    /// 顿帧上限用因果式 0.15 × hitstop_frames ≤ 1，不用"≤6 帧"的常数。
    /// </summary>
    public class SmokeHitstopKnockbackTests
    {
        [Test]
        public void 顿帧时间缩放_零点一五()
        {
            Assert.AreEqual(0.15f, CombatSpec.HitstopTimeScale, 1e-6f,
                "hitstop_time_scale 必须是 0.15（0 会立着滑出去，1 没有卡住的手感）。");
        }

        [Test]
        public void 受击三档_硬直顿帧与击退距离逐档核对()
        {
            QaScenario s = ScenarioFactory.LegalBaseline();

            QaKnockbackRow light = Find(s, KnockbackTier.Light);
            QaKnockbackRow medium = Find(s, KnockbackTier.Medium);
            QaKnockbackRow heavy = Find(s, KnockbackTier.Heavy);

            Assert.AreEqual(18, light.HitstunFrames, "轻档硬直应为 18 帧。");
            Assert.AreEqual(20, medium.HitstunFrames, "中档硬直应为 20 帧。");
            Assert.AreEqual(22, heavy.HitstunFrames, "重档硬直应为 22 帧。");

            Assert.AreEqual(2, light.HitstopFrames, "轻档顿帧应为 2 帧。");
            Assert.AreEqual(4, medium.HitstopFrames, "中档顿帧应为 4 帧。");
            Assert.AreEqual(6, heavy.HitstopFrames, "重档顿帧应为 6 帧。");

            Assert.AreEqual(0.3f, light.DistMin, 1e-4f); Assert.AreEqual(0.6f, light.DistMax, 1e-4f);
            Assert.AreEqual(0.8f, medium.DistMin, 1e-4f); Assert.AreEqual(1.2f, medium.DistMax, 1e-4f);
            Assert.AreEqual(1.2f, heavy.DistMin, 1e-4f); Assert.AreEqual(1.5f, heavy.DistMax, 1e-4f);

            // 因果式上限：三档都必须用满边界，且 gap 贴线。
            for (int i = 0; i < s.Knockbacks.Count; i++)
            {
                QaKnockbackRow k = s.Knockbacks[i];
                Assert.LessOrEqual(k.HitstopTimeScale * k.HitstopFrames, 1f,
                    k.Tier + " 档违反因果式上限 0.15 × hitstop_frames ≤ 1。");
                Assert.GreaterOrEqual(k.HoldDuration, CombatSpec.HoldDurationMin);
                Assert.LessOrEqual(k.HoldDuration, CombatSpec.HoldDurationMax);
            }
        }

        [Test]
        public void 顿帧上限写成常数会在time_scale改变时失效()
        {
            // 判据自身的反例：若上限写成"≤6 帧"，把 time_scale 调到 0.10 后
            // 0.10 × 6 = 0.6 仍然成立（更宽松，恒过）；调到 0.20 后 0.20 × 8 = 1.6 越界但常数判据看不见。
            QaScenario s = ScenarioFactory.LegalBaseline();
            for (int i = 0; i < s.Knockbacks.Count; i++) { s.Knockbacks[i].HitstopTimeScale = 0.20f; }
            CriterionVerdict verdict = CriterionOracle.Evaluate(s);
            Assert.IsTrue(verdict.IsFired(CriterionOracle.FHitstopCausal),
                "把 time_scale 调到 0.20 后因果式上限仍未报红——说明上限被写成了常数。");
        }

        private static QaKnockbackRow Find(QaScenario s, KnockbackTier tier)
        {
            for (int i = 0; i < s.Knockbacks.Count; i++)
            {
                if (s.Knockbacks[i].Tier == tier) { return s.Knockbacks[i]; }
            }
            Assert.Fail("场景里缺少 " + tier + " 档受击规格。");
            return null;
        }
    }
}
