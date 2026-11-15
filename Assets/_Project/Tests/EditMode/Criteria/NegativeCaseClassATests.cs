using EdgeChronicles.Tests.Qa;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace EdgeChronicles.Tests.Criteria
{
    /// <summary>
    /// A 类负向用例（灵敏度）：24 条。
    ///
    /// 【铁律】**漏挂即废判据，不是调阈值。**
    /// 每条用例只注入**一处**错误，然后断言对应判据必须报红。
    /// 一旦某条用例不再报红，正确动作是**重写判据**，不是放宽阈值——
    /// 本测试文件只做"报红 / 不报红"的二元判定，不提供任何"放宽"的开关。
    /// </summary>
    public class NegativeCaseClassATests
    {
        /// <summary>注入表：编号 → 注入动作。注入后必须只有目标判据报红。</summary>
        private static void Inject(string caseId, QaScenario s)
        {
            switch (caseId)
            {
                case "NC-A01": // 闪避无敌窗置零
                    s.Dodge.InvulnStart = 0;
                    s.Dodge.InvulnEnd = 0;
                    return;

                case "NC-A02": // 命中 → 受击起始 5 帧
                    s.HitFeedbackFrames = 5;
                    return;

                case "NC-A03": // 击退方向与伤害来源同向
                    s.DamageSourceDirectionDeg = 0f;
                    s.KnockbackDirectionDeg = 0f;
                    return;

                case "NC-A04": // 轻档击退越界
                    for (int i = 0; i < s.Knockbacks.Count; i++)
                    {
                        if (s.Knockbacks[i].Tier == KnockbackTier.Light) { s.Knockbacks[i].DistMax = 0.8f; }
                    }
                    return;

                case "NC-A05": // 推入墙内
                    s.KnockbackPushesIntoWall = true;
                    return;

                case "NC-A06": // 预警只有 18 帧
                    s.BossTelegraphFrames = 18;
                    return;

                case "NC-A07": // 预警缺音效通道
                    s.TelegraphHasAudio = false;
                    return;

                case "NC-A08": // 墨线与背景亮度差不足
                    s.InkContrastRatio = 0.45f;
                    return;

                case "NC-A09": // 加了 AdditionalMaterial
                    s.HasAdditionalMaterial = true;
                    s.MaterialSlotCount = 2;
                    return;

                case "NC-A10": // 精英只缩渲染体
                    s.EliteColliderScale = 1.0f;
                    s.EliteVisualScale = 1.4f;
                    return;

                case "NC-A11": // 受击变体不齐 + 反向变体几何不稳
                    s.HitClipVariantCount = 3;
                    s.HitClipReverseContactHeightCm = 2.0f;
                    s.HitClipReverseContactFrames = 1;
                    return;

                case "NC-A12": // 对照组只消除了颜色通道
                    s.D5ControlEliminatedColor = true;
                    s.D5ControlEliminatedOutlineForm = false;
                    s.D5ControlEliminatedEntryTiming = false;
                    return;

                case "NC-A13": // 注入 10 帧输入延迟
                    s.InjectedInputLatencyFrames = 10;
                    s.MeasuredInputLatencyMs = 166.7f;
                    s.InputTimestampPipelinesIndependent = true;
                    return;

                case "NC-A14": // 两个时间戳同源，实测恒 0
                    s.InjectedInputLatencyFrames = 10;
                    s.MeasuredInputLatencyMs = 0f;
                    s.InputTimestampPipelinesIndependent = false;
                    return;

                case "NC-A15": // hold 过短
                    for (int i = 0; i < s.Knockbacks.Count; i++)
                    {
                        if (s.Knockbacks[i].Tier == KnockbackTier.Light) { s.Knockbacks[i].HoldDuration = 1.5f; }
                    }
                    return;

                case "NC-A16": // 重档顿帧 8 帧（因果上限）
                    for (int i = 0; i < s.Knockbacks.Count; i++)
                    {
                        if (s.Knockbacks[i].Tier == KnockbackTier.Heavy)
                        {
                            s.Knockbacks[i].HitstopFrames = 8;
                            s.Knockbacks[i].SplitFrame = 8;
                            s.Knockbacks[i].CurveFrames = 8 + CombatSpec.KnockbackRearWindowFrames;
                        }
                    }
                    return;

                case "NC-A17": // 无敌描边改成与霸体同结构
                    for (int i = 0; i < s.VfxAssets.Count; i++)
                    {
                        if (s.VfxAssets[i].VfxId == "vfx_invuln_pofeng")
                        {
                            s.VfxAssets[i].OutlineStyle = CombatSpec.OutlineStyleSingleSolid;
                        }
                    }
                    return;

                case "NC-A18": // 脉冲 4Hz
                    s.VfxAssets[0].PulseHz = 4f;
                    return;

                case "NC-A19": // 脉冲窗口容不下周期
                    s.VfxAssets[0].PulseHz = 2f;
                    s.VfxAssets[0].WindowFrames = 3;
                    return;

                case "NC-A20": // 刀光被剪影遮挡 45%
                    s.BladeLightOcclusionRatio = 0.45f;
                    return;

                case "NC-A21": // 闪避蹬地窗口位移 12%
                    s.DodgePushWindowRatio = 0.12f;
                    return;

                case "NC-A22": // phases.startup = 0
                    s.Skills[0].Startup = 0;
                    return;

                case "NC-A23": // hitShape.frame = active
                    s.Skills[0].HitShapes[0].Frame = s.Skills[0].Active;
                    return;

                case "NC-A24": // 取消窗口引用不存在的标签
                    s.CancelWindows[0].AllowTag = "dodge";
                    return;

                default:
                    Assert.Fail("没有为 " + caseId + " 编写注入动作。新增 A 类用例时必须同时补上注入，且只改一处。");
                    return;
            }
        }

        [Test]
        public void 基线场景_本身零报红()
        {
            QaScenario baseline = ScenarioFactory.LegalBaseline();
            CriterionVerdict verdict = CriterionOracle.Evaluate(baseline);
            Assert.IsEmpty(verdict.Fired, "合法基线不应报红，但报了：" + verdict);
        }

        /// <summary>
        /// 允许的**连带报红**：某些注入只有一个错误，但会同时触发两条判据
        /// （同一个缺陷的两个后果）。这里显式登记，避免把"连带"误判成"注入改多了地方"。
        /// </summary>
        private static string[] Companions(string caseId)
        {
            switch (caseId)
            {
                case "NC-A16":
                    // 顿帧 6 → 8 帧：既越过因果式上限 0.15×h≤1，
                    // 也把"位移归零 → 收身起始"的空档从 2.1 帧压到 1.8 帧。
                    return new string[] { CriterionOracle.FRecoveryGap };
                case "NC-A22":
                    // 前摇 8 → 0 帧：技能总帧数缩短，而取消窗口以绝对帧号声明，
                    // 于是窗口尾帧越界（A21）。这是同一个错误的第二个后果。
                    return new string[] { CriterionOracle.A21 };
                default:
                    return new string[0];
            }
        }

        [Test]
        public void 二十四条A类用例_每条注入后目标判据必须报红()
        {
            List<NegativeCaseRecord> cases = CriterionCatalog.CasesOfClass(NegativeCaseClass.Sensitivity);
            Assert.AreEqual(24, cases.Count, "A 类用例数不对。");

            var failures = new List<string>();
            for (int i = 0; i < cases.Count; i++)
            {
                NegativeCaseRecord c = cases[i];
                QaScenario s = ScenarioFactory.Clone(ScenarioFactory.LegalBaseline());
                Inject(c.Id, s);

                CriterionVerdict verdict = CriterionOracle.Evaluate(s);
                if (!verdict.IsFired(c.TargetCriterionId))
                {
                    failures.Add(c.Id + "（目标 " + c.TargetCriterionId + "）注入后没有报红。"
                        + "实际报红集合：" + verdict
                        + "。没报说明什么：" + c.MeaningIfNotFired
                        + "。**正确动作是重写判据，不是放宽阈值。**");
                }
            }
            Assert.IsEmpty(failures, "以下 A 类用例没挂上（漏挂即废判据）：\n" + string.Join("\n", failures.ToArray()));
        }

        [Test]
        public void 二十四条A类用例_注入只改一处且不牵连其他判据()
        {
            // "一次只改一处"是负向用例的可复现性前提：
            // 注入之后，除目标判据外不应有别的判据报红，否则说明注入顺带改了别的量。
            List<NegativeCaseRecord> cases = CriterionCatalog.CasesOfClass(NegativeCaseClass.Sensitivity);
            var failures = new List<string>();
            for (int i = 0; i < cases.Count; i++)
            {
                NegativeCaseRecord c = cases[i];
                QaScenario s = ScenarioFactory.Clone(ScenarioFactory.LegalBaseline());
                Inject(c.Id, s);
                CriterionVerdict verdict = CriterionOracle.Evaluate(s);

                for (int k = 0; k < verdict.Sorted().Count; k++)
                {
                    string fired = verdict.Sorted()[k];
                    if (fired == c.TargetCriterionId) { continue; }
                    bool allowed = false;
                    string[] companions = Companions(c.Id);
                    for (int m = 0; m < companions.Length; m++)
                    {
                        if (companions[m] == fired) { allowed = true; break; }
                    }
                    if (!allowed)
                    {
                        failures.Add(c.Id + " 额外报红了 " + fired + "（目标只有 " + c.TargetCriterionId + "）");
                    }
                }
            }
            Assert.IsEmpty(failures, "以下注入牵连了其他判据（一次只改一处的纪律被破坏）：\n"
                + string.Join("\n", failures.ToArray()));
        }
    }
}
