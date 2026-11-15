using System;
using System.Collections.Generic;

namespace EdgeChronicles.Data.Progression
{
    /// <summary>
    /// 技传派生条目清单（8 条）。
    /// <para>
    /// 唯一来源：IMPL-v3.0.md §4.8.5（数据层 id 与设计语义）。本类只做<b>代码侧的同一份拷贝</b>，
    /// 用途是 A33-1 / A33-2 / A33-5 的判定与 UI 的派生名映射——<b>不新增、不改写任何一条效果</b>。
    /// </para>
    /// <para>
    /// <b>8 条派生 ≠ 8 个新视觉</b>：只有落在美术白名单里的才允许标 <c>new_graphic</c>，
    /// 其余必须是 <c>param</c> / <c>generic</c>（不占格）。
    /// </para>
    /// </summary>
    public static class DerivativeCatalog
    {
        /// <summary>一条派生条目。</summary>
        public readonly struct Entry
        {
            /// <summary>派生 id（<c>dv_*</c>）。</summary>
            public readonly string Id;

            /// <summary>派生名（中文）。</summary>
            public readonly string Name;

            /// <summary>所属技能 id（<c>sk_*</c>）。</summary>
            public readonly string SkillId;

            /// <summary>所在阶位。</summary>
            public readonly int Tier;

            /// <summary>视觉分级。</summary>
            public readonly string VisualTier;

            public Entry(string id, string name, string skillId, int tier, string visualTier)
            {
                Id = id;
                Name = name;
                SkillId = skillId;
                Tier = tier;
                VisualTier = visualTier;
            }
        }

        private static readonly Entry[] s_entries =
        {
            new Entry("dv_shifeng_last_jump_double", "末跳翻倍", "sk_shifeng", 3, "new_graphic"),
            new Entry("dv_shifeng_second_domain", "双层锋域", "sk_shifeng", 5, "param"),
            new Entry("dv_xunfeng_echo", "回响", "sk_xunfeng", 3, "new_graphic"),
            new Entry("dv_shufeng_poise_break", "破势回锋", "sk_fufeng", 2, "new_graphic"),
            new Entry("dv_baoyuan_shield_break", "破罩冲击", "sk_baoyuan", 5, "new_graphic"),
            new Entry("dv_jufeng_cluster", "聚锋簇", "sk_jufeng", 5, "new_graphic"),
            new Entry("dv_pofeng_chain_burst", "引爆再展域", "sk_pofeng", 5, "new_graphic"),
            new Entry("dv_huifeng_pull_turn", "拉近转向", "sk_huifeng", 5, "generic")
        };

        private static readonly HashSet<string> s_ids = BuildIds();

        /// <summary>全部派生条目。</summary>
        public static IReadOnlyList<Entry> All => s_entries;

        /// <summary>派生条目总数（8）。</summary>
        public static int Count => s_entries.Length;

        /// <summary>其中标 <c>new_graphic</c> 的条数（6 = 美术白名单条数）。</summary>
        public static int NewGraphicCount
        {
            get
            {
                int count = 0;
                foreach (var entry in s_entries)
                {
                    if (string.Equals(entry.VisualTier, "new_graphic", StringComparison.Ordinal)) count++;
                }
                return count;
            }
        }

        /// <summary>id 是否在清单内。</summary>
        public static bool Contains(string derivativeId)
        {
            return derivativeId != null && s_ids.Contains(derivativeId);
        }

        /// <summary>按 id 取条目（不存在返回 false）。</summary>
        public static bool TryGet(string derivativeId, out Entry entry)
        {
            entry = default;
            if (derivativeId == null) return false;
            foreach (var candidate in s_entries)
            {
                if (string.Equals(candidate.Id, derivativeId, StringComparison.Ordinal))
                {
                    entry = candidate;
                    return true;
                }
            }
            return false;
        }

        private static HashSet<string> BuildIds()
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in s_entries) set.Add(entry.Id);
            return set;
        }
    }
}
