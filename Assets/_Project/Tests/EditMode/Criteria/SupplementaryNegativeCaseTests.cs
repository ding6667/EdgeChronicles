using EdgeChronicles.Tests.Qa;
using NUnit.Framework;
using System.Collections.Generic;

namespace EdgeChronicles.Tests.Criteria
{
    /// <summary>
    /// 补充负向注入（13 条）——覆盖**未进 30 条规范集**的导表断言分支。
    ///
    /// 【为什么不并进 30 条】
    /// GDD §13 固定收录 30 条（24 A 类 + 6 B 类）。那 30 条是**规范集**，条数固定；
    /// 但它没有覆盖到 A19–A43 的全部断言分支。本文件是**补充集**：
    ///   · 不计入 30 条；
    ///   · 但每一条都遵守同一套纪律（一次只改一处 / 漏挂即废判据 / 没报说明什么）；
    ///   · 存在的意义：**没有负向用例的断言抓不住错，等于没有断言**。
    ///
    /// 【A39 的拆分】按 EL-03 §2：A39-1 = 上限类多来源**取最大**；
    /// A39-2 = 其余数值类**取最小、不叠加**，离散类只生效一次。两条分别造。
    /// </summary>
    public class SupplementaryNegativeCaseTests
    {
        /// <summary>补充注入：编号 → (目标断言, 注入动作)。</summary>
        private static void Inject(string id, QaScenario s)
        {
            switch (id)
            {
                case "S-01": // A21：取消窗口首尾颠倒
                    s.CancelWindows[0].FromFrame = s.CancelWindows[0].ToFrame + 1;
                    return;

                case "S-02": // A21：取消窗口尾帧越过技能总帧数
                    {
                        QaSkillRow sk = s.Skills[0];
                        int total = sk.Startup + sk.Active + sk.Recovery;
                        for (int i = 0; i < s.CancelWindows.Count; i++)
                        {
                            if (s.CancelWindows[i].SourceTag == sk.EntryTags[0]) { s.CancelWindows[i].ToFrame = total; }
                        }
                    }
                    return;

                case "S-03": // A23：某技能没有命中形状
                    s.Skills[0].HitShapes.Clear();
                    return;

                case "S-04": // A24：曲线积分与声明偏差 8%
                    // 【口径无关的注入】按**当前口径**生成一条积分只有声明值 92% 的曲线：
                    // 速度口径下 keys 是 m/s，占比口径下 keys 是 Σ=0.92 的占比 —— 两种口径偏差都是 8%。
                    s.MovementCurves[0] = ScenarioFactory.MakeCurve("mc_xunfeng", 3.0f, 5, 0.92f);
                    return;

                case "S-05": // A25：coreLink 同时写状态又消耗层数
                    s.Skills[0].WritesCoreState = true;
                    s.Skills[0].ConsumeStacks = true;
                    return;

                case "S-06": // A26：枚举外的 trigger
                    s.BossRules[0].Trigger = "OnSomeUnknownTrigger";
                    return;

                case "S-07": // A27：ForcePhase 指向不存在的阶段
                    s.BossRules[3].ForcePhaseTarget = 9;
                    s.BossRules[3].Effects = new List<string> { "LockZone:zone_arena:Close", "ForcePhase:9", "ShakeCamera:0.6:18" };
                    return;

                case "S-08": // A28：无法解析的 anchor://
                    s.BossRules[0].ReferencedAnchors = new List<string> { "anchor://puzzle/pillar_99" };
                    return;

                case "S-09": // A29：连段后资源为负
                    s.ResourceAfterCombo = -1;
                    return;

                case "S-10": // A30：hitstop 超出 [0.05, 0.25]
                    s.Skills[0].HitShapes[0].Hitstop = 0.30f;
                    return;

                case "S-11": // A35：络脉节点的机制字段写成自由字符串
                    // 【为什么用络脉不用星盘】络脉的数值列恒空，误写没有别的列可退化，
                    // 错误必然以"机制字段非法"的形式暴露。
                    s.Meridians[0].MechanicMod = "MM_SomeFreeTextMechanic";
                    return;

                case "S-12": // A39-1：上限类多来源被叠加（应取最大）
                    for (int i = 0; i < s.EffectMerges.Count; i++)
                    {
                        if (s.EffectMerges[i].MergeClass == EffectMergeClass.CapUpper)
                        {
                            s.EffectMerges[i].MergedValue = 2f; // max(1,1)=1，叠加得 2
                        }
                    }
                    return;

                case "S-14": // hold 高于上限（可达值 4.8）
                    // 【D-14 第三条路】与 NC-A15（低于下限）配对：
                    // 两条都必须用**可达**值，不用恰好等于 2.0 / 4.0 的端点。
                    // 重档硬直 22 → 23 帧 → hold = 23 − 13 − 6×0.85 = 4.8 > 4 → 必须报红。
                    for (int i = 0; i < s.Knockbacks.Count; i++)
                    {
                        if (s.Knockbacks[i].Tier == KnockbackTier.Heavy)
                        {
                            s.Knockbacks[i].HitstunFrames = 23;
                            s.Knockbacks[i].HoldDuration = s.Knockbacks[i].ComputedHold();
                        }
                    }
                    return;

                case "S-15": // hold 重算为负（D-04：换算后 hold < 0 必须报错，不取 0）
                    for (int i = 0; i < s.Knockbacks.Count; i++)
                    {
                        if (s.Knockbacks[i].Tier == KnockbackTier.Light)
                        {
                            // 受击剪辑加长到 17 C 帧 @60fps → clip_W = 17
                            // → hold = 18 − 17 − 2×0.85 = −0.7 < 0
                            s.Knockbacks[i].AnimClipLengthC = 17;
                            s.Knockbacks[i].HoldDuration = s.Knockbacks[i].ComputedHold();
                        }
                    }
                    return;

                case "S-16": // A29：连段模拟没跑顿帧帧（顿帧不暂停 tick，必须跑过）
                    // 【D-09 裁定】连段模拟必须跑过顿帧帧，否则验不到"顿帧期间状态机仍在合法推进"。
                    s.ComboHitstopFrames = 0;
                    return;

                case "S-13": // A39-2：数值类多来源被相加（应取最小不叠加）
                    for (int i = 0; i < s.EffectMerges.Count; i++)
                    {
                        if (s.EffectMerges[i].MergeClass == EffectMergeClass.Numeric)
                        {
                            s.EffectMerges[i].MergedValue = 13f; // min(5,8)=5，相加得 13
                        }
                    }
                    return;

                default:
                    Assert.Fail("没有为 " + id + " 编写注入动作。");
                    return;
            }
        }

        private static readonly string[] Targets =
        {
            CriterionOracle.A21, CriterionOracle.A21, CriterionOracle.A23, CriterionOracle.A24,
            CriterionOracle.A25, CriterionOracle.A26, CriterionOracle.A27, CriterionOracle.A28,
            CriterionOracle.A29, CriterionOracle.A30, CriterionOracle.A35,
            CriterionOracle.A391, CriterionOracle.A392,
            CriterionOracle.FHold, CriterionOracle.FHold, CriterionOracle.A29
        };

        private static readonly string[] Ids =
        {
            "S-01", "S-02", "S-03", "S-04", "S-05", "S-06", "S-07", "S-08",
            "S-09", "S-10", "S-11", "S-12", "S-13", "S-14", "S-15", "S-16"
        };

        [Test]
        public void 补充注入_每条注入后目标断言必须报红()
        {
            var failures = new List<string>();
            for (int i = 0; i < Ids.Length; i++)
            {
                QaScenario s = ScenarioFactory.Clone(ScenarioFactory.LegalBaseline());
                Inject(Ids[i], s);
                CriterionVerdict verdict = CriterionOracle.Evaluate(s);
                if (!verdict.IsFired(Targets[i]))
                {
                    failures.Add(Ids[i] + "（目标 " + Targets[i] + "）注入后没有报红。实际报红集合：" + verdict
                        + "。**漏挂即废判据——正确动作是重写判据，不是调阈值。**");
                }
            }
            Assert.IsEmpty(failures, "以下补充注入没挂上：\n" + string.Join("\n", failures.ToArray()));
        }

        [Test]
        public void 补充注入_不牵连其他判据()
        {
            var failures = new List<string>();
            for (int i = 0; i < Ids.Length; i++)
            {
                QaScenario s = ScenarioFactory.Clone(ScenarioFactory.LegalBaseline());
                Inject(Ids[i], s);
                CriterionVerdict verdict = CriterionOracle.Evaluate(s);
                for (int k = 0; k < verdict.Sorted().Count; k++)
                {
                    string fired = verdict.Sorted()[k];
                    if (fired != Targets[i])
                    {
                        failures.Add(Ids[i] + " 额外报红了 " + fired + "（目标 " + Targets[i] + "）");
                    }
                }
            }
            Assert.IsEmpty(failures, "以下补充注入牵连了其他判据：\n" + string.Join("\n", failures.ToArray()));
        }

        [Test]
        public void A39拆分_上限类取最大其余取最小不叠加()
        {
            // 判据自身的可读性检查：把 A39-1 / A39-2 的口径写在测试里，
            // 避免"两条其实在查同一件事"却没人发现。
            QaScenario s = ScenarioFactory.LegalBaseline();
            CriterionVerdict verdict = CriterionOracle.Evaluate(s);
            Assert.IsFalse(verdict.IsFired(CriterionOracle.A391), "合法基线不应触发 A39-1（上限类取最大）。");
            Assert.IsFalse(verdict.IsFired(CriterionOracle.A392), "合法基线不应触发 A39-2（取最小不叠加）。");
        }
    }
}
