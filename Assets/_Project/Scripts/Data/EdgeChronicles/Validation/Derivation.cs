using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using EdgeChronicles.Data.Schema;
using EdgeChronicles.Data.Serialization;

namespace EdgeChronicles.Data.Validation
{
    /// <summary>
    /// 派生值求值器（<b>禁手填</b>）。
    /// <para>
    /// 规则：① 派生列的值**一律由公式算出来**，源数据里若写了且与重算值不等 → **error**（派生值不得成为第二真值源）；
    /// ② 算出来的值写回行对象，导出时输出的是重算值；
    /// ③ 有些派生列依赖存档态（<c>star_link.active_state</c>），导入期没有存档，按「全部未点亮」落 <c>inactive</c>，
    /// 运行期读档后再重算——这类列在导入期**不参与手填比对**，只做枚举合法性检查。
    /// </para>
    /// </summary>
    public static class Derivation
    {
        /// <summary>对全部表执行派生求值。</summary>
        public static void DeriveAll(TableSet set, ValidationReport report)
        {
            DeriveSkills(set, report);
            DeriveCancelWindows(set, report);
            DeriveHitShapes(set, report);
            DeriveMovementCurves(set, report);
            DeriveBosses(set, report);
            DeriveBossRules(set, report);
            DeriveDungeons(set, report);
            DeriveProgression(set, report);
            DeriveStarNodes(set, report);
            DeriveStarLinks(set, report);
            DeriveTechTiers(set, report);
            DeriveMeridians(set, report);
            DeriveSocketItems(set, report);
            DeriveRefineStages(set, report);
        }

        // ------------------------------------------------------------------ skill

        private static void DeriveSkills(TableSet set, ValidationReport report)
        {
            var table = set.Get("skill");
            if (table == null) return;

            var techTiers = set.Get("tech_tier");
            foreach (var bound in table.Rows)
            {
                var row = bound.As<SkillRow>();
                if (row == null) continue;

                int total;
                if (row.phases != null)
                {
                    total = row.phases.total;
                }
                else if (row.combo_segments != null && row.combo_segments.Length > 0)
                {
                    // 多段技能：单段时长相加只用于「一次连段」的时长口径；
                    // 段数另计（combo_segments.Length）。
                    var first = row.combo_segments[0];
                    total = first.startup + first.active + first.recovery;
                }
                else
                {
                    total = 0;
                }

                Set(table, bound, report, "total_frames", total);

                int segments = row.combo_segments != null && row.combo_segments.Length > 0 ? row.combo_segments.Length : 1;
                Set(table, bound, report, "segment_count", segments);

                var tiers = new List<int>();
                if (techTiers != null)
                {
                    foreach (var tierRow in techTiers.Rows)
                    {
                        var typed = tierRow.As<TechTierRow>();
                        if (typed == null || !string.Equals(typed.skill_id, row.skill_id, StringComparison.Ordinal)) continue;
                        tiers.Add(typed.tier);
                    }
                }
                tiers.Sort();
                Set(table, bound, report, "tech_tiers", tiers.ToArray());
            }
        }

        // ------------------------------------------------------------ cancel_window

        private static void DeriveCancelWindows(TableSet set, ValidationReport report)
        {
            var table = set.Get("cancel_window");
            if (table == null) return;

            foreach (var bound in table.Rows)
            {
                var row = bound.As<CancelWindowRow>();
                if (row == null) continue;
                Set(table, bound, report, "window_length", row.to_frame - row.from_frame);
            }
        }

        // --------------------------------------------------------------- hit_shape

        private static void DeriveHitShapes(TableSet set, ValidationReport report)
        {
            var table = set.Get("hit_shape");
            if (table == null) return;

            var skills = set.Get("skill");
            foreach (var bound in table.Rows)
            {
                var row = bound.As<HitShapeRow>();
                if (row == null) continue;

                double arc = 0d;
                if (row.shape == "sector")
                {
                    arc = 2d * Math.PI * row.radius * (row.angle / 360d);
                }
                Set(table, bound, report, "arc_length", (float)arc);

                int skillFrames = 0;
                if (skills != null)
                {
                    var skillRow = skills.Find(row.skill_id);
                    var typed = skillRow?.As<SkillRow>();
                    if (typed != null) skillFrames = typed.total_frames;
                }

                double hits = row.multi_hit + 1;
                double seconds = skillFrames > 0 ? skillFrames / 60d : 0d;
                double dps = seconds > 0d ? row.damage * hits / seconds : 0d;
                Set(table, bound, report, "expected_dps", (float)dps);
            }
        }

        // ---------------------------------------------------------- movement_curve

        private static void DeriveMovementCurves(TableSet set, ValidationReport report)
        {
            var table = set.Get("movement_curve");
            if (table == null) return;

            foreach (var bound in table.Rows)
            {
                var row = bound.As<MovementCurveRow>();
                if (row == null) continue;

                // 关键点的值 = 该帧的位移（米）；曲线积分距离 = 各帧位移之和。
                double integral = 0d;
                if (row.keys != null)
                {
                    for (int i = 0; i < row.keys.Length; i++) integral += row.keys[i].value;
                }
                Set(table, bound, report, "integral_distance", (float)integral);
            }
        }

        // -------------------------------------------------------------------- boss

        private static void DeriveBosses(TableSet set, ValidationReport report)
        {
            var table = set.Get("boss");
            if (table == null) return;

            foreach (var bound in table.Rows)
            {
                var row = bound.As<BossRow>();
                if (row == null) continue;
                // 分段血量由 Boss 规则分配，表的 total_hp = 本体总血量。
                Set(table, bound, report, "total_hp", row.hp);
            }
        }

        private static void DeriveBossRules(TableSet set, ValidationReport report)
        {
            var table = set.Get("boss_rule");
            if (table == null) return;

            foreach (var bound in table.Rows)
            {
                var row = bound.As<BossRuleRow>();
                if (row == null) continue;

                var sb = new StringBuilder();
                sb.Append(row.trigger);
                if (row.conditions != null)
                {
                    foreach (var condition in row.conditions)
                    {
                        sb.Append('|').Append(condition.type);
                        AppendArgs(sb, condition.args);
                    }
                }
                if (row.effects != null)
                {
                    foreach (var effect in row.effects)
                    {
                        sb.Append('|').Append(effect.type);
                        AppendArgs(sb, effect.args);
                    }
                }

                Set(table, bound, report, "rule_hash", ComputeFnv1A(sb.ToString()));
            }
        }

        private static void AppendArgs(StringBuilder sb, StringArg[] args)
        {
            if (args == null) return;
            foreach (var arg in args)
            {
                sb.Append(':').Append(arg.key).Append('=').Append(arg.value);
            }
        }

        /// <summary>FNV-1a 64 位哈希（十六进制文本）。规则指纹用它，保证同规则同指纹。</summary>
        public static string ComputeFnv1A(string text)
        {
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            ulong hash = offset;
            byte[] bytes = Encoding.UTF8.GetBytes(text ?? string.Empty);
            foreach (byte b in bytes)
            {
                hash ^= b;
                hash *= prime;
            }
            return hash.ToString("x16", CultureInfo.InvariantCulture);
        }

        // ----------------------------------------------------------------- dungeon

        private static void DeriveDungeons(TableSet set, ValidationReport report)
        {
            var table = set.Get("dungeon");
            if (table == null) return;

            foreach (var bound in table.Rows)
            {
                var row = bound.As<DungeonRow>();
                if (row == null) continue;
                int count = 0;
                if (row.waves != null)
                {
                    foreach (int wave in row.waves) count += wave;
                }
                Set(table, bound, report, "enemy_count", count);
            }
        }

        // -------------------------------------------------------------- progression

        private static void DeriveProgression(TableSet set, ValidationReport report)
        {
            var table = set.Get("progression");
            if (table == null) return;

            foreach (var bound in table.Rows)
            {
                var row = bound.As<ProgressionRow>();
                if (row == null) continue;

                bool isCultivation = string.Equals(row.kind, "cultivation", StringComparison.Ordinal);
                bool isFame = string.Equals(row.kind, "fame", StringComparison.Ordinal);

                if (isFame)
                {
                    int tier = row.fame_unlock_tier;
                    Set(table, bound, report, "fame_lv_min", 2 * (tier - 1) + 1);
                    Set(table, bound, report, "fame_lv_max", 2 * tier);
                }

                // 本行对总战力的贡献 = 权重 × 原值（权重以千分比取整，差值必须精确为 0）。
                int contribution = 0;
                if (isCultivation)
                {
                    int weightPermille = (int)Math.Round(row.weight * 1000d, MidpointRounding.AwayFromZero);
                    contribution = (int)Math.Round((double)weightPermille * row.raw_value / 1000d, MidpointRounding.AwayFromZero);
                }
                Set(table, bound, report, "expected_power", contribution);

                int cost = 0;
                if (isCultivation && row.tiers != null && row.tiers.Length > 0)
                {
                    cost = row.tiers[0].cost_eu;
                }
                Set(table, bound, report, "cost_eu", cost);
            }
        }

        // --------------------------------------------------------------- star_node

        private static void DeriveStarNodes(TableSet set, ValidationReport report)
        {
            var table = set.Get("star_node");
            if (table == null) return;

            var links = set.Get("star_link");
            foreach (var bound in table.Rows)
            {
                var row = bound.As<StarNodeRow>();
                if (row == null) continue;

                var closure = new List<string>();
                CollectClosure(table, row.node_id, closure);
                closure.Remove(row.node_id);
                Set(table, bound, report, "prereq_closure", closure.ToArray());

                var partners = new List<string>();
                if (links != null)
                {
                    foreach (var linkRow in links.Rows)
                    {
                        var link = linkRow.As<StarLinkRow>();
                        if (link == null) continue;
                        if (string.Equals(link.node_a, row.node_id, StringComparison.Ordinal)) partners.Add(link.node_b);
                        else if (string.Equals(link.node_b, row.node_id, StringComparison.Ordinal)) partners.Add(link.node_a);
                    }
                }
                partners.Sort(StringComparer.Ordinal);
                Set(table, bound, report, "link_partners", partners.ToArray());
            }
        }

        /// <summary>收集某节点的前置传递闭包（DFS，含去重）。</summary>
        public static void CollectClosure(TableData table, string nodeId, List<string> output)
        {
            var bound = table.Find(nodeId);
            if (bound == null) return;
            var row = bound.As<StarNodeRow>();
            if (row == null || row.prereq_ids == null) return;

            foreach (var prereq in row.prereq_ids)
            {
                if (string.Equals(prereq, nodeId, StringComparison.Ordinal)) continue;
                if (output.Contains(prereq)) continue;
                output.Add(prereq);
                CollectClosure(table, prereq, output);
            }
        }

        // --------------------------------------------------------------- star_link

        private static void DeriveStarLinks(TableSet set, ValidationReport report)
        {
            var table = set.Get("star_link");
            if (table == null) return;

            var nodes = set.Get("star_node");
            foreach (var bound in table.Rows)
            {
                var row = bound.As<StarLinkRow>();
                if (row == null) continue;

                string seriesA = SeriesOf(nodes, row.node_a);
                string seriesB = SeriesOf(nodes, row.node_b);

                if (!string.IsNullOrEmpty(seriesA) && !string.IsNullOrEmpty(seriesB) &&
                    !string.Equals(seriesA, seriesB, StringComparison.Ordinal))
                {
                    report.Error("A32", "star_link", bound.Key, "series",
                        $"连携两端异系：{row.node_a}({seriesA}) / {row.node_b}({seriesB})");
                }

                Set(table, bound, report, "series", seriesA ?? string.Empty);

                // 生效态依赖存档态（节点是否点亮）：导入期无存档 → 全部未点亮。
                // 该列在运行期读档后重算，导入期不与手填值比对。
                var field = table.Schema.RowType.GetField("active_state", BindingFlags.Public | BindingFlags.Instance);
                if (field != null) field.SetValue(row, "inactive");
            }
        }

        private static string SeriesOf(TableData nodes, string nodeId)
        {
            if (nodes == null) return null;
            var bound = nodes.Find(nodeId);
            return bound?.As<StarNodeRow>()?.series;
        }

        // ---------------------------------------------------------------- tech_tier

        private static void DeriveTechTiers(TableSet set, ValidationReport report)
        {
            var table = set.Get("tech_tier");
            if (table == null) return;

            foreach (var bound in table.Rows)
            {
                var row = bound.As<TechTierRow>();
                if (row == null) continue;

                int cumulative = 0;
                var mods = new List<EffectModEntry>();
                foreach (var otherBound in table.Rows)
                {
                    var other = otherBound.As<TechTierRow>();
                    if (other == null) continue;
                    if (!string.Equals(other.skill_id, row.skill_id, StringComparison.Ordinal)) continue;
                    if (other.tier > row.tier) continue;

                    cumulative += other.cost_eu;

                    if (!string.IsNullOrEmpty(other.stat_key))
                    {
                        var existing = mods.Find(m => string.Equals(m.key, other.stat_key, StringComparison.Ordinal));
                        if (existing != null) existing.value += other.stat_value;
                        else mods.Add(new EffectModEntry { key = other.stat_key, value = other.stat_value });
                    }
                }

                Set(table, bound, report, "cum_cost_eu", cumulative);
                Set(table, bound, report, "effective_mods", mods.ToArray());
            }
        }

        // ----------------------------------------------------------------- meridian

        private static void DeriveMeridians(TableSet set, ValidationReport report)
        {
            var table = set.Get("meridian");
            if (table == null) return;

            foreach (var bound in table.Rows)
            {
                var row = bound.As<MeridianRow>();
                if (row == null) continue;

                int depth = 0;
                if (row.prereq_ids != null)
                {
                    foreach (var prereq in row.prereq_ids)
                    {
                        int prereqDepth = DepthOf(table, prereq, new HashSet<string>(StringComparer.Ordinal));
                        if (prereqDepth + 1 > depth) depth = prereqDepth + 1;
                    }
                }
                Set(table, bound, report, "depth", depth);
            }
        }

        private static int DepthOf(TableData table, string nodeId, HashSet<string> visiting)
        {
            if (!visiting.Add(nodeId)) return 0;
            var bound = table.Find(nodeId);
            var row = bound?.As<MeridianRow>();
            if (row == null || row.prereq_ids == null) return 0;

            int depth = 0;
            foreach (var prereq in row.prereq_ids)
            {
                int prereqDepth = DepthOf(table, prereq, visiting);
                if (prereqDepth + 1 > depth) depth = prereqDepth + 1;
            }
            return depth;
        }

        // -------------------------------------------------------------- socket_item

        private static void DeriveSocketItems(TableSet set, ValidationReport report)
        {
            var table = set.Get("socket_item");
            if (table == null) return;

            foreach (var bound in table.Rows)
            {
                var row = bound.As<SocketItemRow>();
                if (row == null) continue;

                var chain = new List<string>();
                CollectComposeChain(table, row.socket_id, chain, new HashSet<string>(StringComparer.Ordinal));
                chain.Remove(row.socket_id);
                Set(table, bound, report, "compose_chain", chain.ToArray());

                bool isMax = true;
                foreach (var otherBound in table.Rows)
                {
                    var other = otherBound.As<SocketItemRow>();
                    if (other == null || other.compose_from == null) continue;
                    foreach (var material in other.compose_from)
                    {
                        if (string.Equals(material, row.socket_id, StringComparison.Ordinal)) { isMax = false; break; }
                    }
                    if (!isMax) break;
                }
                Set(table, bound, report, "is_max_level", isMax);
            }
        }

        private static void CollectComposeChain(TableData table, string socketId, List<string> output, HashSet<string> visiting)
        {
            if (!visiting.Add(socketId)) return;
            var bound = table.Find(socketId);
            var row = bound?.As<SocketItemRow>();
            if (row == null || row.compose_from == null) return;

            foreach (var material in row.compose_from)
            {
                if (output.Contains(material)) continue;
                output.Add(material);
                CollectComposeChain(table, material, output, visiting);
            }
        }

        // ------------------------------------------------------------- refine_stage

        private static void DeriveRefineStages(TableSet set, ValidationReport report)
        {
            var table = set.Get("refine_stage");
            if (table == null) return;

            foreach (var bound in table.Rows)
            {
                // 保底计数是存档内计数（读档时回填），表内恒 0。
                Set(table, bound, report, "pity_counter", 0);
            }
        }

        // ------------------------------------------------------------------ helper

        /// <summary>写派生值；源数据里若有手填值且与重算值不等 → error。</summary>
        private static void Set(TableData table, BoundRow bound, ValidationReport report, string column, object value)
        {
            var field = table.Schema.RowType.GetField(column, BindingFlags.Public | BindingFlags.Instance);
            if (field == null) return;

            field.SetValue(bound.Row, value);

            if (!bound.Source.TryGetValue(column, out var sourceValue) || sourceValue == null || sourceValue.IsNull) return;

            bool equal;
            if (value is int intValue)
            {
                equal = sourceValue.Kind == JsonKind.Number && Math.Abs(sourceValue.NumberValue - intValue) < 1e-9;
            }
            else if (value is float floatValue)
            {
                equal = sourceValue.Kind == JsonKind.Number && Math.Abs(sourceValue.NumberValue - floatValue) < 1e-6;
            }
            else if (value is bool boolValue)
            {
                equal = sourceValue.Kind == JsonKind.Bool && sourceValue.BoolValue == boolValue;
            }
            else if (value is string stringValue)
            {
                equal = sourceValue.Kind == JsonKind.String && string.Equals(sourceValue.StringValue, stringValue, StringComparison.Ordinal);
            }
            else
            {
                // 数组 / 复合类型：源数据里写了派生数组一律判为手填（error）。
                equal = false;
            }

            if (!equal)
            {
                report.Error("DERIVED", table.Name, bound.Key, column,
                    $"派生列禁手填：源数据写了 {Describe(sourceValue)}，重算值 = {DescribeValue(value)}；以重算值为准");
            }
        }

        private static string Describe(JsonValue value)
        {
            switch (value.Kind)
            {
                case JsonKind.Number: return value.NumberValue.ToString("R", CultureInfo.InvariantCulture);
                case JsonKind.String: return "\"" + value.StringValue + "\"";
                case JsonKind.Bool: return value.BoolValue ? "true" : "false";
                default: return value.Kind.ToString();
            }
        }

        private static string DescribeValue(object value)
        {
            if (value == null) return "null";
            if (value is int i) return i.ToString(CultureInfo.InvariantCulture);
            if (value is float f) return f.ToString("R", CultureInfo.InvariantCulture);
            if (value is bool b) return b ? "true" : "false";
            if (value is string s) return "\"" + s + "\"";
            if (value is System.Collections.IEnumerable enumerable && !(value is string))
            {
                var sb = new StringBuilder("[");
                bool first = true;
                foreach (var item in enumerable)
                {
                    if (!first) sb.Append(", ");
                    sb.Append(DescribeValue(item));
                    first = false;
                }
                return sb.Append(']').ToString();
            }
            return value.ToString();
        }
    }
}
