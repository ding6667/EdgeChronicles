using EdgeChronicles.Tests.Qa;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace EdgeChronicles.Tests.Contracts
{
    /// <summary>
    /// 生产侧契约检查（8 项冒烟测试的"生产版"）。
    ///
    /// 【状态说明】工程侧尚未实现下列契约时，这 8 条**必然为红**。
    /// 这是刻意的：**假通过比假失败危险**。
    /// 把"契约缺失"处理成 Skip（跳过）或绿灯，等于给未实现的系统发通行证。
    /// 报错信息里已写明"要实现什么"，实现方按契约落地即可转绿。
    ///
    /// 【与参考实现的关系】EditMode 侧的同编号测试（SmokeXxx）验证的是**判据本身可执行**；
    /// 本文件验证的是**生产实现符合判据**。两者都绿，才算这一项冒烟通过。
    /// </summary>
    public class ProductionContractTests
    {
        /// <summary>
        /// SC-01 · 生产事件总线：万次发布零分配。
        ///
        /// 【为什么直接绑定而不是走反射】
        /// 反射调用会为每次 Invoke 分配一个 object[]，并且把结构体事件装箱——
        /// 那恰好会把"发布路径装箱"这个要抓的缺陷**藏起来**。要测零分配，只能直接调用。
        /// 因此本条是全套测试里唯一直接引用生产类型的地方（Core 已冻结，破坏面可控）。
        /// </summary>
        [Test]
        public void SC01_事件总线_万次发布零分配()
        {
            var bus = new EdgeChronicles.Core.EventBus<int>();
            int received = 0;
            Action<int> handler = delegate (int _) { received++; };
            bus.Subscribe(handler);

            int evt = 1;
            bus.Publish(in evt); // 预热：JIT 与首次派发都在测量窗口之外

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++)
            {
                bus.Publish(in evt);
            }
            long after = GC.GetAllocatedBytesForCurrentThread();

            long allocated = after - before;
            Assert.AreEqual(0L, allocated,
                "生产事件总线在 10000 次发布中分配了 " + allocated + " 字节。发布路径不允许装箱、"
                + "不允许在派发中扩容订阅容器（IMPL-v2.0 §6：事件总线 10k 事件 GC.Alloc == 0）。");
            Assert.AreEqual(10001, received, "生产事件总线没有把事件送达订阅者。");
        }

        [Test]
        public void SC02_导表断言_零错误零警告()
        {
            Type t = ProductionContracts.Require(ProductionContracts.TableValidatorType, ProductionContracts.TableValidatorContractText);
            object validator = Activator.CreateInstance(t);
            MethodInfo run = ProductionContracts.RequireMethod(t, ProductionContracts.TableValidatorRun, 0, ProductionContracts.TableValidatorContractText);
            object summary = run.Invoke(validator, null);
            Assert.IsNotNull(summary, "导表断言执行器返回了 null。");

            int errors = ReadInt(summary, "ErrorCount");
            int warnings = ReadInt(summary, "WarningCount");
            Assert.AreEqual(0, errors, "导表断言存在 " + errors + " 条 error。");
            Assert.AreEqual(0, warnings, "导表断言存在 " + warnings + " 条 warning。");
        }

        [Test]
        public void SC03_技能运行时_可按冻结签名驱动()
        {
            Type t = ProductionContracts.Require(ProductionContracts.SkillRuntimeType, ProductionContracts.SkillRuntimeContractText);
            Assert.IsNotNull(Activator.CreateInstance(t), "技能运行时无法实例化。");

            MethodInfo tryCast = FindMethodByName(t, "TryCast");
            MethodInfo phase = FindPropertyOrMethod(t, "Phase");
            MethodInfo phaseFrame = FindPropertyOrMethod(t, "PhaseFrame");
            Assert.IsNotNull(tryCast, "技能运行时缺少 TryCast。");
            Assert.IsNotNull(phase, "技能运行时缺少 Phase。");
            Assert.IsNotNull(phaseFrame, "技能运行时缺少 PhaseFrame。");
        }

        [Test]
        public void SC04_位移曲线_积分与声明可取()
        {
            Type t = ProductionContracts.Require(ProductionContracts.MovementCurveEvaluatorType, ProductionContracts.MovementCurveContractText);
            object evaluator = Activator.CreateInstance(t);
            MethodInfo integrate = ProductionContracts.RequireMethod(t, "IntegrateDistance", 1, ProductionContracts.MovementCurveContractText);
            MethodInfo declared = ProductionContracts.RequireMethod(t, "GetDeclaredDistance", 1, ProductionContracts.MovementCurveContractText);

            object integral = integrate.Invoke(evaluator, new object[] { "mc_xunfeng" });
            object decl = declared.Invoke(evaluator, new object[] { "mc_xunfeng" });
            double d = Convert.ToDouble(integral);
            double c = Convert.ToDouble(decl);
            Assert.LessOrEqual(Math.Abs(d - c) / c, CombatSpec.CurveIntegralTolerance,
                "曲线 mc_xunfeng 的积分距离 " + d + "m 与声明 " + c + "m 偏差超过 5%。");
        }

        [Test]
        public void SC05_命中几何_与参考实现逐例一致()
        {
            Type t = ProductionContracts.Require(ProductionContracts.HitGeometryType, ProductionContracts.HitGeometryContractText);
            MethodInfo contains = ProductionContracts.RequireMethod(t, "Contains", 4, ProductionContracts.HitGeometryContractText);

            var mismatches = new List<string>();
            CheckSector(contains, mismatches);
            CheckCapsule(contains, mismatches);
            CheckRect(contains, mismatches);
            Assert.IsEmpty(mismatches, "生产命中几何与参考实现不一致：\n" + string.Join("\n", mismatches.ToArray()));
        }

        [Test]
        public void SC06_受击调参_三档与顿帧缩放一致()
        {
            Type t = ProductionContracts.Require(ProductionContracts.CombatTuningType, ProductionContracts.CombatTuningContractText);
            object tuning = Activator.CreateInstance(t);
            object scale = ProductionContracts.RequireMember(t, "HitstopTimeScale", ProductionContracts.CombatTuningContractText);
            Assert.AreEqual(CombatSpec.HitstopTimeScale, Convert.ToSingle(scale), 1e-4f,
                "生产 hitstop_time_scale 与规格卡不一致。");

            MethodInfo spec = ProductionContracts.RequireMethod(t, "GetKnockbackSpec", 1, ProductionContracts.CombatTuningContractText);
            CheckTier(spec, tuning, "light", CombatSpec.HitstunFramesLight, CombatSpec.HitstopFramesLight,
                CombatSpec.KnockbackDistMinLight, CombatSpec.KnockbackDistMaxLight);
            CheckTier(spec, tuning, "medium", CombatSpec.HitstunFramesMedium, CombatSpec.HitstopFramesMedium,
                CombatSpec.KnockbackDistMinMedium, CombatSpec.KnockbackDistMaxMedium);
            CheckTier(spec, tuning, "heavy", CombatSpec.HitstunFramesHeavy, CombatSpec.HitstopFramesHeavy,
                CombatSpec.KnockbackDistMinHeavy, CombatSpec.KnockbackDistMaxHeavy);
        }

        [Test]
        public void SC07_Boss规则_冷启动序列hash稳定()
        {
            Type t = ProductionContracts.Require(ProductionContracts.BossRuleEvaluatorType, ProductionContracts.BossRuleEvaluatorContractText);
            MethodInfo evaluate = ProductionContracts.RequireMethod(t, "Evaluate", 1, ProductionContracts.BossRuleEvaluatorContractText);

            string[] bossIds = { "boss_01_shifo", "boss_02_gumu", "boss_03_tieyao" };
            for (int b = 0; b < bossIds.Length; b++)
            {
                string first = null;
                for (int run = 0; run < 3; run++)
                {
                    object evaluator = Activator.CreateInstance(t);   // 冷启动：全新实例
                    string[] seq = evaluate.Invoke(evaluator, new object[] { bossIds[b] }) as string[];
                    Assert.IsNotNull(seq, "Boss 规则求值返回 null。");
                    string hash = BossRuleReference.SequenceHash(new List<string>(seq));
                    if (first == null) { first = hash; continue; }
                    Assert.AreEqual(first, hash, "Boss " + bossIds[b] + " 的冷启动序列 hash 不稳定。");
                }
            }
        }

        [Test]
        public void SC08_存档_篡改一字节必须失败()
        {
            Type t = ProductionContracts.Require(ProductionContracts.SaveCodecType, ProductionContracts.SaveCodecContractText);
            object codec = Activator.CreateInstance(t);
            MethodInfo write = ProductionContracts.RequireMethod(t, "Write", 1, ProductionContracts.SaveCodecContractText);
            MethodInfo tryRead = ProductionContracts.RequireMethod(t, "TryRead", 2, ProductionContracts.SaveCodecContractText);
            MethodInfo crc = ProductionContracts.RequireMethod(t, "ComputeCrc", 1, ProductionContracts.SaveCodecContractText);

            byte[] body = System.Text.Encoding.UTF8.GetBytes("{\"header\":{\"save_version\":3},\"core\":{\"hero\":{\"hp\":100}}}");
            byte[] blob = write.Invoke(codec, new object[] { body }) as byte[];
            Assert.IsNotNull(blob, "存档写入返回 null。");

            byte[] readBack;
            object[] args = new object[] { blob, null };
            Assert.IsTrue((bool)tryRead.Invoke(codec, args), "合法存档回读失败。");
            readBack = args[1] as byte[];
            Assert.IsNotNull(readBack, "回读没有返回本体。");

            uint expected = Crc32Reference.Compute(body);
            Assert.AreEqual(expected, Convert.ToUInt32(crc.Invoke(codec, new object[] { body })),
                "生产 CRC 与参考实现不一致。");

            var notDetected = new List<string>();
            int[] offsets = ScenarioFactory.TamperOffsets;
            for (int i = 0; i < offsets.Length; i++)
            {
                byte[] copy = (byte[])blob.Clone();
                int idx = offsets[i] < copy.Length ? offsets[i] : copy.Length - 1;
                copy[idx] = (byte)(copy[idx] ^ 0xFF);
                object[] targs = new object[] { copy, null };
                if ((bool)tryRead.Invoke(codec, targs))
                {
                    notDetected.Add("偏移 " + offsets[i]);
                }
            }
            Assert.IsEmpty(notDetected,
                "以下篡改没有被生产存档检出：\n" + string.Join("\n", notDetected.ToArray()));
        }

        // ── 辅助 ─────────────────────────────────────────────────────────

        private static int ReadInt(object summary, string memberName)
        {
            PropertyInfo p = summary.GetType().GetProperty(memberName, BindingFlags.Public | BindingFlags.Instance);
            if (p != null) { return Convert.ToInt32(p.GetValue(summary, null)); }
            FieldInfo f = summary.GetType().GetField(memberName, BindingFlags.Public | BindingFlags.Instance);
            if (f != null) { return Convert.ToInt32(f.GetValue(summary)); }
            Assert.Fail("校验结果对象上找不到 " + memberName + "（契约要求 ErrorCount / WarningCount）。");
            return 0;
        }

        private static MethodInfo FindMethodByName(Type t, string name)
        {
            MethodInfo[] all = t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
            for (int i = 0; i < all.Length; i++) { if (all[i].Name == name) { return all[i]; } }
            return null;
        }

        private static MethodInfo FindPropertyOrMethod(Type t, string name)
        {
            PropertyInfo p = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
            if (p != null) { return p.GetGetMethod(); }
            return FindMethodByName(t, name);
        }

        private static void CheckTier(MethodInfo spec, object tuning, string tier, int hitstun, int hitstop, float distMin, float distMax)
        {
            object[] arr = spec.Invoke(tuning, new object[] { tier }) as float[];
            Assert.IsNotNull(arr, "GetKnockbackSpec(\"" + tier + "\") 返回 null。");
            Assert.AreEqual(5, arr.Length, "GetKnockbackSpec 应返回 { hitstun, hold, hitstop, distMin, distMax } 共 5 个值。");
            Assert.AreEqual(hitstun, (int)arr[0], tier + " 档硬直帧数不符。");
            Assert.GreaterOrEqual(arr[1], CombatSpec.HoldDurationMin, tier + " 档 hold 低于下界。");
            Assert.LessOrEqual(arr[1], CombatSpec.HoldDurationMax, tier + " 档 hold 高于上界。");
            Assert.AreEqual(hitstop, (int)arr[2], tier + " 档顿帧帧数不符。");
            Assert.AreEqual(distMin, arr[3], 1e-4f, tier + " 档击退下界不符。");
            Assert.AreEqual(distMax, arr[4], 1e-4f, tier + " 档击退上界不符。");
        }

        private static void CheckSector(MethodInfo contains, List<string> mismatches)
        {
            List<ScenarioFactory.HitGeometryCase> cases = ScenarioFactory.SectorCases();
            for (int i = 0; i < cases.Count; i++)
            {
                ScenarioFactory.HitGeometryCase c = cases[i];
                float[] p = { c.Radius, c.AngleDeg, c.Origin.X, c.Origin.Y, 0f, 0f, 0f, 0f, 0f, 0f, 0f };
                object[] args = new object[] { "sector", p, c.Point.X, c.Point.Y };
                object target = contains.IsStatic ? null : Activator.CreateInstance(contains.DeclaringType);
                bool actual = (bool)contains.Invoke(target, args);
                if (actual != c.Expected)
                {
                    mismatches.Add("sector / " + c.Note + "：期望 " + c.Expected + "，生产 " + actual);
                }
            }
        }

        private static void CheckCapsule(MethodInfo contains, List<string> mismatches)
        {
            List<ScenarioFactory.HitGeometryCase> cases = ScenarioFactory.CapsuleCases();
            for (int i = 0; i < cases.Count; i++)
            {
                ScenarioFactory.HitGeometryCase c = cases[i];
                float[] p = { c.CapRadius, 0f, 0f, 0f, c.A.X, c.A.Y, c.B.X, c.B.Y, 0f, 0f, 0f };
                object[] args = new object[] { "capsule", p, c.Point.X, c.Point.Y };
                object target = contains.IsStatic ? null : Activator.CreateInstance(contains.DeclaringType);
                bool actual = (bool)contains.Invoke(target, args);
                if (actual != c.Expected)
                {
                    mismatches.Add("capsule / " + c.Note + "：期望 " + c.Expected + "，生产 " + actual);
                }
            }
        }

        private static void CheckRect(MethodInfo contains, List<string> mismatches)
        {
            List<ScenarioFactory.HitGeometryCase> cases = ScenarioFactory.RectCases();
            for (int i = 0; i < cases.Count; i++)
            {
                ScenarioFactory.HitGeometryCase c = cases[i];
                float[] p = { 0f, 0f, c.Center.X, c.Center.Y, 0f, 0f, 0f, 0f, c.HalfX, c.HalfY, c.YawDeg };
                object[] args = new object[] { "rect", p, c.Point.X, c.Point.Y };
                object target = contains.IsStatic ? null : Activator.CreateInstance(contains.DeclaringType);
                bool actual = (bool)contains.Invoke(target, args);
                if (actual != c.Expected)
                {
                    mismatches.Add("rect / " + c.Note + "：期望 " + c.Expected + "，生产 " + actual);
                }
            }
        }
    }
}
