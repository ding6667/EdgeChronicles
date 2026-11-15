using System;
using System.Collections.Generic;
using System.Reflection;

namespace EdgeChronicles.Tests.Qa
{
    /// <summary>生产侧契约缺失。抛出即测试失败——不跳过、不忽略。</summary>
    public sealed class ContractMissingException : Exception
    {
        public ContractMissingException(string message) : base(message) { }
    }

    /// <summary>
    /// 测试所需的**生产侧契约**。
    ///
    /// 【为什么要这份清单】
    /// 测试不能引用尚未实现的生产类型（会编译失败 → 整个测试程序集连同判据目录一起挂掉），
    /// 也不该用"跳过"来掩盖缺失（假通过比假失败危险）。
    /// 因此生产侧的类型名 / 成员签名在此**写死成契约**，由反射绑定：
    ///   · 契约未实现 → 抛 <see cref="ContractMissingException"/>，测试红，并附上要实现什么。
    ///   · 契约已实现 → 直接调用，测的是生产行为。
    ///
    /// 【给 engineering-lead 的落名请求】
    /// 下列类型名与签名是 QA 侧提出的**请求**，不是设计决定；
    /// 若工程侧已有更合适的命名，改本文件一处即可（契约集中在此，便于对齐）。
    /// </summary>
    public static class ProductionContracts
    {
        // ── SC-01 事件总线 ────────────────────────────────────────────────
        public const string EventBusType = "EdgeChronicles.Core.EventBus`1";
        public const string EventBusSubscribe = "Subscribe";
        public const string EventBusPublish = "Publish";

        // ── SC-02 导表断言执行器 ───────────────────────────────────────────
        public const string TableValidatorType = "EdgeChronicles.Data.Validation.TableValidator";
        public const string TableValidatorRun = "ValidateAll";

        // ── SC-03 技能运行时（冻结签名 IMPL-v2.0 §1）─────────────────────────
        public const string SkillRuntimeType = "EdgeChronicles.Kernel.SkillRuntime";

        // ── SC-04 位移曲线求值 ─────────────────────────────────────────────
        public const string MovementCurveEvaluatorType = "EdgeChronicles.Kernel.MovementCurveEvaluator";

        // ── SC-05 命中几何 ────────────────────────────────────────────────
        public const string HitGeometryType = "EdgeChronicles.Kernel.HitGeometry";

        // ── SC-06 受击 / 击退运行时 ────────────────────────────────────────
        public const string CombatTuningType = "EdgeChronicles.Kernel.CombatTuning";

        // ── SC-07 Boss 规则求值 ───────────────────────────────────────────
        public const string BossRuleEvaluatorType = "EdgeChronicles.Kernel.BossRuleEvaluator";

        // ── SC-08 存档编解码 ─────────────────────────────────────────────
        public const string SaveCodecType = "EdgeChronicles.Flow.SaveCodec";

        private static readonly Dictionary<string, Type> _typeCache = new Dictionary<string, Type>(StringComparer.Ordinal);

        /// <summary>按全名解析类型；找不到返回 null。</summary>
        public static Type TryResolve(string fullTypeName)
        {
            Type cached;
            if (_typeCache.TryGetValue(fullTypeName, out cached)) { return cached; }
            Type found = null;
            Assembly[] asms = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < asms.Length; i++)
            {
                Type t = asms[i].GetType(fullTypeName, false);
                if (t != null) { found = t; break; }
            }
            _typeCache[fullTypeName] = found;
            return found;
        }

        /// <summary>按全名解析类型；找不到直接抛异常（带"要实现什么"的说明）。</summary>
        public static Type Require(string fullTypeName, string contractText)
        {
            Type t = TryResolve(fullTypeName);
            if (t == null)
            {
                throw new ContractMissingException(
                    "契约未实现：找不到类型 " + fullTypeName + "。\n本测试需要的契约：\n" + contractText);
            }
            return t;
        }

        /// <summary>按名称找方法（优先参数个数匹配，其次无参重载）。</summary>
        public static MethodInfo RequireMethod(Type type, string methodName, int paramCount, string contractText)
        {
            MethodInfo[] all = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
            MethodInfo fallback = null;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].Name != methodName) { continue; }
                if (all[i].GetParameters().Length == paramCount) { return all[i]; }
                if (fallback == null) { fallback = all[i]; }
            }
            throw new ContractMissingException(
                "契约未实现：类型 " + type.FullName + " 上找不到方法 " + methodName + "(" + paramCount + " 个参数)。\n"
                + "实际可用方法：" + MethodNames(all) + "\n本测试需要的契约：\n" + contractText);
        }

        /// <summary>按名称找属性（含字段回退）。</summary>
        public static object RequireMember(Type type, string memberName, string contractText)
        {
            PropertyInfo p = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
            if (p != null)
            {
                object v = p.GetValue(null, null);
                if (v == null) { throw new ContractMissingException("契约未实现：属性 " + type.FullName + "." + memberName + " 返回 null。"); }
                return v;
            }
            FieldInfo f = type.GetField(memberName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
            if (f != null)
            {
                object v = f.GetValue(null);
                if (v == null) { throw new ContractMissingException("契约未实现：字段 " + type.FullName + "." + memberName + " 为 null。"); }
                return v;
            }
            throw new ContractMissingException(
                "契约未实现：类型 " + type.FullName + " 上找不到成员 " + memberName + "。\n本测试需要的契约：\n" + contractText);
        }

        private static string MethodNames(MethodInfo[] all)
        {
            var names = new List<string>();
            for (int i = 0; i < all.Length; i++)
            {
                ParameterInfo[] ps = all[i].GetParameters();
                string sig = all[i].Name + "(";
                for (int p = 0; p < ps.Length; p++)
                {
                    if (p > 0) { sig += ", "; }
                    sig += ps[p].ParameterType.Name;
                }
                sig += ")";
                names.Add(sig);
            }
            names.Sort(StringComparer.Ordinal);
            return names.Count == 0 ? "（无 public 方法）" : string.Join(", ", names.ToArray());
        }

        /// <summary>契约文本：事件总线。</summary>
        public static string EventBusContractText
        {
            get
            {
                return string.Join("\n", new string[]
                {
                    "// 生产侧已实现（直接绑定，无反射）：",
                    "namespace EdgeChronicles.Core {",
                    "  public sealed class EventBus<TEvent> {",
                    "    public EventBus(int initialCapacity = 8);",
                    "    public void Subscribe(Action<TEvent> handler);",
                    "    public bool Unsubscribe(Action<TEvent> handler);",
                    "    public void Publish(in TEvent evt);",
                    "    public void Clear();",
                    "    public int SubscriberCount { get; }",
                    "  }",
                    "}",
                    "约束：派发零分配（不装箱、不产生闭包 / 枚举器、派发中不扩容）。"
                });
            }
        }

        /// <summary>契约文本：导表断言执行器。</summary>
        public static string TableValidatorContractText
        {
            get
            {
                return string.Join("\n", new string[]
                {
                    "namespace EdgeChronicles.Data.Validation {",
                    "  public sealed class TableValidator {",
                    "    public ValidationSummary ValidateAll();",
                    "  }",
                    "  public struct ValidationSummary {",
                    "    public int ErrorCount;",
                    "    public int WarningCount;",
                    "    public string[] FailedAssertionIds;",
                    "  }",
                    "}"
                });
            }
        }

        /// <summary>契约文本：技能运行时。</summary>
        public static string SkillRuntimeContractText
        {
            get
            {
                return string.Join("\n", new string[]
                {
                    "namespace EdgeChronicles.Kernel {",
                    "  public interface ISkillRuntime {",
                    "    bool TryCast(SkillId id);",
                    "    SkillPhase Phase { get; }",
                    "    int PhaseFrame { get; }",
                    "  }",
                    "  public sealed class SkillRuntime : ISkillRuntime { ... }",
                    "}",
                    "取消回放需要：能注入一帧的输入并推进 tick（本测试用反射驱动，成员名见测试文件注释）。"
                });
            }
        }

        /// <summary>契约文本：位移曲线求值。</summary>
        public static string MovementCurveContractText
        {
            get
            {
                return string.Join("\n", new string[]
                {
                    "namespace EdgeChronicles.Kernel {",
                    "  public sealed class MovementCurveEvaluator {",
                    "    public double IntegrateDistance(string curveId);   // 积分距离（m）",
                    "    public double GetDeclaredDistance(string curveId); // 声明距离（m）",
                    "  }",
                    "}"
                });
            }
        }

        /// <summary>契约文本：命中几何。</summary>
        public static string HitGeometryContractText
        {
            get
            {
                return string.Join("\n", new string[]
                {
                    "namespace EdgeChronicles.Kernel {",
                    "  public sealed class HitGeometry {",
                    "    // shapeParams 顺序固定：",
                    "    //   0 radius(m) 1 angleDeg(整角) 2 originX 3 originY",
                    "    //   4 ax 5 ay 6 bx 7 by 8 halfX 9 halfY 10 yawDeg",
                    "    public static bool Contains(string shape, float[] shapeParams, float pointX, float pointY);",
                    "  }",
                    "}"
                });
            }
        }

        /// <summary>契约文本：受击 / 击退调参。</summary>
        public static string CombatTuningContractText
        {
            get
            {
                return string.Join("\n", new string[]
                {
                    "namespace EdgeChronicles.Kernel {",
                    "  public sealed class CombatTuning {",
                    "    public float HitstopTimeScale { get; }        // 0.15",
                    "    // 返回 { hitstunFrames, holdDuration, hitstopFrames, distMin, distMax }",
                    "    public float[] GetKnockbackSpec(string tier);  // tier: light / medium / heavy",
                    "  }",
                    "}"
                });
            }
        }

        /// <summary>契约文本：Boss 规则求值。</summary>
        public static string BossRuleEvaluatorContractText
        {
            get
            {
                return string.Join("\n", new string[]
                {
                    "namespace EdgeChronicles.Kernel {",
                    "  public sealed class BossRuleEvaluator {",
                    "    // 冷启动求值：返回 Effect 触发序列（顺序稳定）",
                    "    public string[] Evaluate(string bossId);",
                    "  }",
                    "}"
                });
            }
        }

        /// <summary>契约文本：存档编解码。</summary>
        public static string SaveCodecContractText
        {
            get
            {
                return string.Join("\n", new string[]
                {
                    "namespace EdgeChronicles.Flow {",
                    "  public sealed class SaveCodec {",
                    "    public byte[] Write(byte[] body);          // 写头 + CRC + 本体",
                    "    public bool TryRead(byte[] blob, out byte[] body); // 校验失败必须返回 false",
                    "    public uint ComputeCrc(byte[] data);",
                    "  }",
                    "}"
                });
            }
        }
    }
}
