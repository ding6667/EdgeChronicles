using System;
using System.Collections.Generic;

namespace EdgeChronicles.Data
{
    /// <summary>
    /// 机制修改项（<c>MM_*</c>）—— <b>共 28 项，唯一合法集合</b>。
    /// <para>
    /// 唯一真值源：<c>IMPL-v3.0-EL-03-progression.md §2.1</c>。
    /// 枚举成员名与数据里的 id <b>逐字相同</b>，因此 <c>ToString()</c> 即为落表 id。
    /// </para>
    /// <para>
    /// 治理约束（EL-03 §2.1 末）：新增 <c>MM_*</c> 必须<b>同时</b>改
    /// ① 本枚举 ② <c>EdgeChronicles.Data</c> 的枚举定义 ③ UI 图标映射表——三处不同步，断言 A35 立刻拦住。
    /// 本文件即 ②；① 与本文档同源；③ 由 UI 侧按 <see cref="MechanicModCatalog"/> 的参数单位映射。
    /// </para>
    /// <para>
    /// 位序：0–27。<c>MechanicModSet</c> 用 32 位掩码表示「是否生效」，参数另存参数表。
    /// </para>
    /// </summary>
    public enum MechanicMod
    {
        // ---- 一、通用机制项（16 项：词条 / 络脉 / 技传 / 手札）----
        MM_ATK_NO_INTERRUPT_IN_DOMAIN = 0,
        MM_FINISH_COST_MINUS1 = 1,
        MM_PERFECT_DODGE_REFUND1 = 2,
        MM_RESOURCE_CAP_PLUS1 = 3,
        MM_CORE_CD_MINUS_FRAMES = 4,
        MM_DOMAIN_LAST_JUMP_DOUBLE = 5,
        MM_CHANNEL_INTERVAL_HALF = 6,
        MM_DASH_IFRAME_PLUS_FRAMES = 7,
        MM_BIND_TRIGGER_VARIANT = 8,
        MM_FINISH_EXTRA_DOMAIN = 9,
        MM_DOMAIN_HIT_GAIN1 = 10,
        MM_GUARD_REFUND_IN_DOMAIN = 11,
        MM_HEAVY_GAIN_EXTRA = 12,
        MM_CANCEL_WINDOW_WIDEN = 13,
        MM_DODGE_CD_MINUS_FRAMES = 14,
        MM_CRIT_IN_DOMAIN = 15,

        // ---- 二、星盘机制项（6 项：star_node.mechanic_mod）----
        MM_STAR_BREAKSTATE = 16,
        MM_STAR_SHIELD_BREAK_FIRST = 17,
        MM_STAR_LIFESTEAL_CONTINUE = 18,
        MM_STAR_CHANNEL_MOVE = 19,
        MM_STAR_OVERCAP_TO_DAMAGE = 20,
        MM_STAR_CANCEL_ALL_AFTER_N = 21,

        // ---- 三、络脉机制项（6 项：meridian.mechanic_mod；另 2 项复用通用枚举）----
        MM_MER_CONDUCE_SKILL = 22,
        MM_MER_CONDUCE_RANGE = 23,
        MM_MER_CONDUCE_STACK = 24,
        MM_MER_STACK_REFUND = 25,
        MM_MER_CONDUCE_FINISH = 26,
        MM_MER_FINISH_CHAIN = 27
    }

    /// <summary>
    /// <c>mechanic_param</c> 的单位（EL-03 §2.1 末列）。
    /// </summary>
    public enum MechanicParamUnit
    {
        /// <summary>忽略参数（开关型）。</summary>
        None = 0,

        /// <summary>层数（锋芒层 / 护盾层）。</summary>
        Layers = 1,

        /// <summary>次数（每场 / 每次）。</summary>
        Count = 2,

        /// <summary>帧数。</summary>
        Frames = 3,

        /// <summary>百分比（0–100）。</summary>
        Percent = 4,

        /// <summary>厘米（cm）。</summary>
        Centimeters = 5,

        /// <summary>倍率（200 = ×2.00）。</summary>
        Multiplier = 6
    }

    /// <summary>一个机制项的元数据（id、位序、参数单位、含义）。</summary>
    public readonly struct MechanicModSpec
    {
        /// <summary>枚举值。</summary>
        public readonly MechanicMod Mod;

        /// <summary>落表 id（与枚举名逐字相同）。</summary>
        public readonly string Id;

        /// <summary>位序（0–27）。</summary>
        public readonly int BitIndex;

        /// <summary>参数单位。</summary>
        public readonly MechanicParamUnit ParamUnit;

        /// <summary>中文含义（工具用；正式文案走本地化表）。</summary>
        public readonly string Meaning;

        public MechanicModSpec(MechanicMod mod, string id, int bitIndex, MechanicParamUnit paramUnit, string meaning)
        {
            Mod = mod;
            Id = id;
            BitIndex = bitIndex;
            ParamUnit = paramUnit;
            Meaning = meaning;
        }
    }

    /// <summary>
    /// 机制项目录：28 项的唯一清单 + 参数单位 + 近似拼写建议。
    /// </summary>
    public static class MechanicModCatalog
    {
        private static readonly MechanicModSpec[] s_specs = BuildSpecs();
        private static readonly Dictionary<string, MechanicMod> s_byId = BuildIndex(s_specs);

        /// <summary>全部 28 项（顺序 = 位序）。</summary>
        public static IReadOnlyList<MechanicModSpec> All => s_specs;

        /// <summary>合法项总数（28）。</summary>
        public static int Count => s_specs.Length;

        /// <summary>取一项的元数据。</summary>
        public static MechanicModSpec Get(MechanicMod mod)
        {
            int index = (int)mod;
            if (index < 0 || index >= s_specs.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(mod), mod, "机制项不在 28 项清单内");
            }
            return s_specs[index];
        }

        /// <summary>
        /// 取某机制项的参数，并按其单位换算成 UI 展示用的量。
        /// <para>
        /// 参数单位随 id 定（EL-03 §2.1 末列）：层 / 次 / 帧 / % / <b>cm</b> / 倍率。
        /// 半径类是<b>厘米</b>，换算成米时走 <see cref="UnitBridge"/>——换算系数只在 Core 定义一次。
        /// </para>
        /// </summary>
        public static float ParameterInDisplayUnits(MechanicMod mod, int parameter)
        {
            var spec = Get(mod);
            switch (spec.ParamUnit)
            {
                case MechanicParamUnit.Centimeters:
                    return UnitBridge.CentimetersToMeters(parameter);
                case MechanicParamUnit.Multiplier:
                    return parameter / 100f;   // 200 = ×2.00 → 显示 2.00
                case MechanicParamUnit.Percent:
                    return parameter / 100f;   // 3 = 3% → 显示 0.03
                default:
                    return parameter;
            }
        }

        /// <summary>按 id 取枚举；不存在返回 false。</summary>
        public static bool TryParse(string id, out MechanicMod mod)
        {
            mod = default;
            if (string.IsNullOrEmpty(id)) return false;
            return s_byId.TryGetValue(id, out mod);
        }

        /// <summary>按 id 取枚举；不存在抛异常。</summary>
        public static MechanicMod Parse(string id)
        {
            if (!TryParse(id, out var mod))
            {
                throw new ArgumentException(
                    $"未知机制项 id「{id}」。合法集合共 {s_specs.Length} 项；最近似拼写：{Suggest(id)}", nameof(id));
            }
            return mod;
        }

        /// <summary>
        /// 最近似拼写建议（编辑距离最小者）。A35 要求「打印最近似拼写」。
        /// </summary>
        public static string Suggest(string unknownId)
        {
            if (string.IsNullOrEmpty(unknownId)) return string.Empty;

            string best = string.Empty;
            int bestDistance = int.MaxValue;
            foreach (var spec in s_specs)
            {
                int distance = Levenshtein(unknownId, spec.Id);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = spec.Id;
                }
            }
            return best;
        }

        /// <summary>编辑距离（Levenshtein）。</summary>
        public static int Levenshtein(string a, string b)
        {
            if (string.IsNullOrEmpty(a)) return string.IsNullOrEmpty(b) ? 0 : b.Length;
            if (string.IsNullOrEmpty(b)) return a.Length;

            var previous = new int[b.Length + 1];
            var current = new int[b.Length + 1];
            for (int j = 0; j <= b.Length; j++) previous[j] = j;

            for (int i = 1; i <= a.Length; i++)
            {
                current[0] = i;
                for (int j = 1; j <= b.Length; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    int deletion = previous[j] + 1;
                    int insertion = current[j - 1] + 1;
                    int substitution = previous[j - 1] + cost;
                    current[j] = Math.Min(Math.Min(deletion, insertion), substitution);
                }

                var swap = previous;
                previous = current;
                current = swap;
            }

            return previous[b.Length];
        }

        private static MechanicModSpec[] BuildSpecs()
        {
            return new[]
            {
                new MechanicModSpec(MechanicMod.MM_ATK_NO_INTERRUPT_IN_DOMAIN, "MM_ATK_NO_INTERRUPT_IN_DOMAIN", 0, MechanicParamUnit.None, "锋域内普攻不中断"),
                new MechanicModSpec(MechanicMod.MM_FINISH_COST_MINUS1, "MM_FINISH_COST_MINUS1", 1, MechanicParamUnit.Layers, "破锋消耗 −1 层"),
                new MechanicModSpec(MechanicMod.MM_PERFECT_DODGE_REFUND1, "MM_PERFECT_DODGE_REFUND1", 2, MechanicParamUnit.Layers, "完美闪避额外回 1 层"),
                new MechanicModSpec(MechanicMod.MM_RESOURCE_CAP_PLUS1, "MM_RESOURCE_CAP_PLUS1", 3, MechanicParamUnit.Layers, "锋芒上限 +1"),
                new MechanicModSpec(MechanicMod.MM_CORE_CD_MINUS_FRAMES, "MM_CORE_CD_MINUS_FRAMES", 4, MechanicParamUnit.Frames, "拾锋式 CD −3s"),
                new MechanicModSpec(MechanicMod.MM_DOMAIN_LAST_JUMP_DOUBLE, "MM_DOMAIN_LAST_JUMP_DOUBLE", 5, MechanicParamUnit.Multiplier, "锋域结束前最后一跳翻倍"),
                new MechanicModSpec(MechanicMod.MM_CHANNEL_INTERVAL_HALF, "MM_CHANNEL_INTERVAL_HALF", 6, MechanicParamUnit.Percent, "聚锋结算间隔 ×0.5"),
                new MechanicModSpec(MechanicMod.MM_DASH_IFRAME_PLUS_FRAMES, "MM_DASH_IFRAME_PLUS_FRAMES", 7, MechanicParamUnit.Frames, "回锋无敌帧 +3"),
                new MechanicModSpec(MechanicMod.MM_BIND_TRIGGER_VARIANT, "MM_BIND_TRIGGER_VARIANT", 8, MechanicParamUnit.None, "缚锋命中被缚目标触发派生"),
                new MechanicModSpec(MechanicMod.MM_FINISH_EXTRA_DOMAIN, "MM_FINISH_EXTRA_DOMAIN", 9, MechanicParamUnit.Frames, "破锋引爆附带 1 次锋域"),
                new MechanicModSpec(MechanicMod.MM_DOMAIN_HIT_GAIN1, "MM_DOMAIN_HIT_GAIN1", 10, MechanicParamUnit.Layers, "锋域内命中 +1 层"),
                new MechanicModSpec(MechanicMod.MM_GUARD_REFUND_IN_DOMAIN, "MM_GUARD_REFUND_IN_DOMAIN", 11, MechanicParamUnit.Multiplier, "抱元被击回层锋域内 ×2"),
                new MechanicModSpec(MechanicMod.MM_HEAVY_GAIN_EXTRA, "MM_HEAVY_GAIN_EXTRA", 12, MechanicParamUnit.Layers, "裂石在锋域内额外 +1 层"),
                new MechanicModSpec(MechanicMod.MM_CANCEL_WINDOW_WIDEN, "MM_CANCEL_WINDOW_WIDEN", 13, MechanicParamUnit.Frames, "取消窗口延长"),
                new MechanicModSpec(MechanicMod.MM_DODGE_CD_MINUS_FRAMES, "MM_DODGE_CD_MINUS_FRAMES", 14, MechanicParamUnit.Frames, "闪避 CD −N"),
                new MechanicModSpec(MechanicMod.MM_CRIT_IN_DOMAIN, "MM_CRIT_IN_DOMAIN", 15, MechanicParamUnit.Percent, "锋域内暴击率 +N%"),
                new MechanicModSpec(MechanicMod.MM_STAR_BREAKSTATE, "MM_STAR_BREAKSTATE", 16, MechanicParamUnit.Layers, "削韧已满的目标被命中时直接进入破势，破势期间命中额外 +1 层"),
                new MechanicModSpec(MechanicMod.MM_STAR_SHIELD_BREAK_FIRST, "MM_STAR_SHIELD_BREAK_FIRST", 17, MechanicParamUnit.Layers, "对有护盾的目标命中时先击碎 N 层护盾再结算伤害"),
                new MechanicModSpec(MechanicMod.MM_STAR_LIFESTEAL_CONTINUE, "MM_STAR_LIFESTEAL_CONTINUE", 18, MechanicParamUnit.Count, "生命首次归零时改为消耗全部锋芒换 1 次无敌帧续战"),
                new MechanicModSpec(MechanicMod.MM_STAR_CHANNEL_MOVE, "MM_STAR_CHANNEL_MOVE", 19, MechanicParamUnit.None, "聚锋引导期间可移动，移动不打断引导"),
                new MechanicModSpec(MechanicMod.MM_STAR_OVERCAP_TO_DAMAGE, "MM_STAR_OVERCAP_TO_DAMAGE", 20, MechanicParamUnit.Percent, "锋芒达上限时多出的层数转为拾锋式伤害加成"),
                new MechanicModSpec(MechanicMod.MM_STAR_CANCEL_ALL_AFTER_N, "MM_STAR_CANCEL_ALL_AFTER_N", 21, MechanicParamUnit.Count, "同一技能连续释放时第 N 次起取消窗口全开"),
                new MechanicModSpec(MechanicMod.MM_MER_CONDUCE_SKILL, "MM_MER_CONDUCE_SKILL", 22, MechanicParamUnit.Centimeters, "锋域在场时服务技能的增益可传导给锋域边缘 R 内的敌人"),
                new MechanicModSpec(MechanicMod.MM_MER_CONDUCE_RANGE, "MM_MER_CONDUCE_RANGE", 23, MechanicParamUnit.Centimeters, "传导范围扩大"),
                new MechanicModSpec(MechanicMod.MM_MER_CONDUCE_STACK, "MM_MER_CONDUCE_STACK", 24, MechanicParamUnit.None, "传导对象由增益改为层数"),
                new MechanicModSpec(MechanicMod.MM_MER_STACK_REFUND, "MM_MER_STACK_REFUND", 25, MechanicParamUnit.Layers, "被传导层数的敌人死亡时返还 1 层锋芒"),
                new MechanicModSpec(MechanicMod.MM_MER_CONDUCE_FINISH, "MM_MER_CONDUCE_FINISH", 26, MechanicParamUnit.Centimeters, "破锋的引爆可传导给锋域边缘的敌人"),
                new MechanicModSpec(MechanicMod.MM_MER_FINISH_CHAIN, "MM_MER_FINISH_CHAIN", 27, MechanicParamUnit.Count, "引爆可连锁传导给第二个目标")
            };
        }

        private static Dictionary<string, MechanicMod> BuildIndex(MechanicModSpec[] specs)
        {
            var map = new Dictionary<string, MechanicMod>(specs.Length, StringComparer.Ordinal);
            foreach (var spec in specs) map[spec.Id] = spec.Mod;
            return map;
        }
    }
}
