using System;
using System.Collections.Generic;

namespace EdgeChronicles.Data.Progression
{
    /// <summary>多来源合并的类别（A39-1…A39-4）。</summary>
    public enum EffectMergeClass
    {
        /// <summary>上限类：取<b>最大</b>（A39-1）。值语义 = 倍率（系数），与基数相乘。</summary>
        Cap = 0,

        /// <summary>消耗 / 削减类：取<b>最小</b>（A39-2）。值语义 = 绝对值。</summary>
        Reduce = 1,

        /// <summary>其余数值类：取<b>最小</b>、不叠加（A39-3）。值语义 = 绝对值。</summary>
        Numeric = 2,

        /// <summary>离散类：只生效一次（A39-4）。</summary>
        Discrete = 3
    }

    /// <summary>
    /// 多来源合并求值器（EL-03 §2 的 A39-1…A39-4）。
    /// <para>
    /// 合并**只在快照构建时（tick #0）算一次**，热路径不做合并（帧内不得出现 `Dictionary&lt;string,…&gt;` / LINQ / 装箱）。
    /// </para>
    /// <para>
    /// 值语义按类别区分，**这是 A39 最容易出错的地方**：
    /// 上限类给的是<b>倍率</b>（系数），与基数相乘；消耗 / 数值类给的是<b>绝对值</b>，与基数取最小。
    /// 若把两者混为一谈，「锋芒上限 5 → 6」会被算成 5×(1+1)=10 或 min(5,1)=1。
    /// </para>
    /// </summary>
    public static class EffectMerge
    {
        /// <summary>
        /// 归入「上限类」的 effect key 清单。
        /// <para>
        /// 治理：上限类的 key 必须显式登记——<b>漏登记 = 该上限会被当成普通数值类取最小</b>，
        /// 把「锋芒上限 5 → 6」判成 1。新增上限类效果时必须在这里加一行。
        /// </para>
        /// </summary>
        private static readonly HashSet<string> s_capKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            "resource_cap"          // 锋芒上限（上限类；5 → 6 的唯一养成来源是手札特技）
        };

        /// <summary>
        /// 归入「消耗 / 削减类」的 effect key 清单。
        /// <para>
        /// 治理：削减类与上限类同向漏登记风险，但方向相反——**漏登记 = 取最小被跳过**，
        /// 破锋消耗会停在 3 层。新增削减类效果时必须在这里加一行。
        /// </para>
        /// </summary>
        private static readonly HashSet<string> s_reduceKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            "finish_cost"           // 破锋消耗层数
        };

        /// <summary>是否登记过的上限类 key。</summary>
        public static bool IsCapKey(string effectKey)
        {
            return effectKey != null && s_capKeys.Contains(effectKey);
        }

        /// <summary>是否登记过的消耗 / 削减类 key。</summary>
        public static bool IsReduceKey(string effectKey)
        {
            return effectKey != null && s_reduceKeys.Contains(effectKey);
        }

        /// <summary>判定一个 effect key 属于哪一类。</summary>
        public static EffectMergeClass Classify(string effectKey)
        {
            if (IsCapKey(effectKey)) return EffectMergeClass.Cap;
            if (IsReduceKey(effectKey)) return EffectMergeClass.Reduce;
            return EffectMergeClass.Numeric;
        }

        /// <summary>
        /// 合并多个来源的同名效果。
        /// <para>
        /// <b>值语义按类别区分，这是 A39 最容易出错的地方</b>：
        /// <list type="bullet">
        /// <item><b>上限类（Cap）</b>：来源给的是<b>倍率</b>（系数域 0.01–1.00 是硬约束，绝对值 6 存不进去），
        /// 合并取<b>最大倍率</b>，结果 = 基数 × 最大倍率。锋芒上限 5 × 1.2 = 6 ✓。</item>
        /// <item><b>消耗 / 削减类（Reduce）</b>：来源给的是<b>绝对值</b>（层数 / 帧数），
        /// 合并取<b>最小值</b>，且基数参与取小。破锋消耗 min(3, 2) = 2 ✓。</item>
        /// <item><b>其余数值类（Numeric）</b>：来源给的是<b>加成量</b>，
        /// 合并取<b>最小加成</b>（不是求和）。两个来源 0.5 与 0.3 → 取 0.3，不是 0.8 ✓。</item>
        /// <item><b>离散类（Discrete）</b>：只生效一次，值为 0 / 1。</item>
        /// </list>
        /// </para>
        /// </summary>
        /// <param name="effectKey">效果键。</param>
        /// <param name="baseAbsolute">基数（上限类 = 绝对上限；消耗类 = 绝对消耗；数值类 = 基准加成）。</param>
        /// <param name="sourceValues">各来源提供的值。</param>
        /// <returns>合并后的值。</returns>
        public static float Merge(string effectKey, float baseAbsolute, IReadOnlyList<float> sourceValues)
        {
            EffectMergeClass cls = Classify(effectKey);

            if (cls == EffectMergeClass.Discrete)
            {
                return sourceValues != null && sourceValues.Count > 0 ? 1f : 0f;
            }

            if (sourceValues == null || sourceValues.Count == 0) return baseAbsolute;

            switch (cls)
            {
                case EffectMergeClass.Cap:
                {
                    float best = 1f;
                    for (int i = 0; i < sourceValues.Count; i++)
                    {
                        if (sourceValues[i] > best) best = sourceValues[i];
                    }
                    return baseAbsolute * best;
                }
                case EffectMergeClass.Reduce:
                {
                    float result = baseAbsolute;
                    for (int i = 0; i < sourceValues.Count; i++)
                    {
                        if (sourceValues[i] < result) result = sourceValues[i];
                    }
                    return result;
                }
                default:
                {
                    float smallest = sourceValues[0];
                    for (int i = 1; i < sourceValues.Count; i++)
                    {
                        if (sourceValues[i] < smallest) smallest = sourceValues[i];
                    }
                    return baseAbsolute + smallest;
                }
            }
        }

        /// <summary>
        /// 离散类求值：多个来源同时提供时，生效次数仍然只算 1（A39-4）。
        /// </summary>
        public static int DiscreteApplicationCount(IReadOnlyList<bool> sources)
        {
            if (sources == null) return 0;
            for (int i = 0; i < sources.Count; i++)
            {
                if (sources[i]) return 1;
            }
            return 0;
        }
    }

    /// <summary>
    /// 掉落保底（A40）。
    /// <para>
    /// 计数器<b>按 material_key 全局累计</b>，不随副本重置——放 <c>enemy.drop_table</c> 会让换本刷把计数洗掉。
    /// 阈值取 <c>global_tuning.drop_pity_threshold</c>（不写死在代码里）。
    /// </para>
    /// </summary>
    public static class DropPity
    {
        /// <summary>本次结算是否必须产出该材料。</summary>
        public static bool ShouldDrop(int counter, int threshold)
        {
            if (threshold <= 0) return false;
            return counter >= threshold;
        }

        /// <summary>结算后的计数：实际掉落清零，未掉落 +1。</summary>
        public static int NextCounter(bool dropped, int counter)
        {
            return dropped ? 0 : counter + 1;
        }
    }

    /// <summary>
    /// 机制修改项位集（<c>MM_*</c> 32 位掩码 + 参数表）。
    /// <para>
    /// EL-03 §3：热路径只做位测试（O(1)），不做字典查找、不做 LINQ。
    /// </para>
    /// </summary>
    public sealed class MechanicModSet
    {
        private readonly uint _mask;
        private readonly int[] _parameters;

        public MechanicModSet(uint mask, int[] parameters)
        {
            _mask = mask;
            _parameters = parameters ?? new int[0];
        }

        /// <summary>位掩码。</summary>
        public uint Mask => _mask;

        /// <summary>某机制项是否生效。</summary>
        public bool IsActive(MechanicMod mod)
        {
            int bit = (int)mod;
            if (bit < 0 || bit >= 32) return false;
            return (_mask & (1u << bit)) != 0u;
        }

        /// <summary>某机制项的参数（未生效时返回 0）。</summary>
        public int Parameter(MechanicMod mod)
        {
            int bit = (int)mod;
            if (bit < 0 || bit >= _parameters.Length) return 0;
            return _parameters[bit];
        }
    }

    /// <summary>
    /// 技传派生解锁位掩码（按 <c>dv_*</c> 索引）。
    /// </summary>
    public sealed class DerivativeMask
    {
        private readonly ulong _mask;

        public DerivativeMask(ulong mask)
        {
            _mask = mask;
        }

        /// <summary>位掩码。</summary>
        public ulong Mask => _mask;

        /// <summary>某派生是否已解锁。</summary>
        public bool IsUnlocked(int index)
        {
            if (index < 0 || index >= 64) return false;
            return (_mask & (1UL << index)) != 0UL;
        }
    }

    /// <summary>
    /// 词条按技能标签的修正（定长数组，热路径数组直读）。
    /// </summary>
    public sealed class SkillTagMod
    {
        private readonly float[] _modifiers;

        public SkillTagMod(float[] modifiers)
        {
            _modifiers = modifiers ?? new float[0];
        }

        /// <summary>取某技能标签的修正系数（1 = 无修正）。</summary>
        public float Modifier(int skillIndex)
        {
            if (skillIndex < 0 || skillIndex >= _modifiers.Length) return 1f;
            return _modifiers[skillIndex];
        }
    }

    /// <summary>
    /// 属性向量（随快照重建，热路径结构体直读）。
    /// </summary>
    public struct StatBlock
    {
        /// <summary>攻击。</summary>
        public int Attack;
        /// <summary>防御。</summary>
        public int Defense;
        /// <summary>暴击率（百分比）。</summary>
        public int CritPercent;
        /// <summary>生命上限。</summary>
        public int MaxHp;
        /// <summary>锋芒上限（层）。</summary>
        public int ResourceCap;
        /// <summary>移速系数（1 = 基准）。</summary>
        public float MoveSpeedScale;
        /// <summary>总战力（整数 EU，派生值）。</summary>
        public int PowerTotal;
    }

    /// <summary>
    /// 养成态快照（不可变；战斗期只读引用）。
    /// </summary>
    public sealed class ProgressionSnapshot
    {
        /// <summary>属性向量。</summary>
        public StatBlock Stats;

        /// <summary>机制修改位集。</summary>
        public MechanicModSet MechanicMods;

        /// <summary>派生解锁位掩码。</summary>
        public DerivativeMask Derivatives;

        /// <summary>词条按技能标签的修正。</summary>
        public SkillTagMod SkillTagMods;

        /// <summary>各养成系统的已解锁状态（按系统 id 索引的不可变集合）。</summary>
        public readonly Dictionary<string, bool> UnlockedSystems = new Dictionary<string, bool>(StringComparer.Ordinal);
    }
}
