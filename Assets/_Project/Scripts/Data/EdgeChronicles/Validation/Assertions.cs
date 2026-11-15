using System;
using System.Collections.Generic;
using System.Globalization;
using EdgeChronicles.Data.Progression;
using EdgeChronicles.Data.Schema;
using EdgeChronicles.Data.Tables;

namespace EdgeChronicles.Data.Validation
{
    /// <summary>
    /// A19–A43 与 C1–C7 的可执行判定。
    /// <para>
    /// 判据逻辑严格照规格：战斗侧 A19–A30 来自 IMPL-v2.0.md §5；
    /// 养成侧 A31–A43 来自 EL-03 §2（与 IMPL-v3.0.md §7 索引表一一对应）；
    /// 节点消耗 C1–C7 来自 <c>docs/design/progression-node-cost-rules.md</c>。
    /// </para>
    /// <para>
    /// 每条断言独立成一个 <see cref="AssertionResult"/>，任一 error 即阻断、不生成产物。
    /// </para>
    /// </summary>
    public static class Assertions
    {
        /// <summary>跑全部断言。</summary>
        public static void RunAll(TableSet set, ValidationReport report)
        {
            // ---- 战斗侧（IMPL-v2.0 §5）----
            Run("A19", report, () => CheckA19(set, report));
            Run("A20", report, () => CheckA20(set, report));
            Run("A21", report, () => CheckA21(set, report));
            Run("A22", report, () => CheckA22(set, report));
            Run("A23", report, () => CheckA23(set, report));
            Run("A24", report, () => CheckA24(set, report));
            Run("A25", report, () => CheckA25(set, report));
            Run("A26", report, () => CheckA26(set, report));
            Run("A27", report, () => CheckA27(set, report));
            Run("A28", report, () => CheckA28(set, report));
            Run("A29", report, () => CheckA29(set, report));
            Run("A30", report, () => CheckA30(set, report));

            // ---- 养成侧（EL-03 §2）----
            Run("A31", report, () => CheckA31(set, report));
            Run("A32", report, () => CheckA32(set, report));
            Run("A33", report, () => CheckA33(set, report));
            Run("A33-1", report, () => CheckA33_1(set, report));
            Run("A33-2", report, () => CheckA33_2(set, report));
            Run("A33-3", report, () => CheckA33_3(set, report));
            Run("A33-4", report, () => CheckA33_4(set, report));
            Run("A33-5", report, () => CheckA33_5(set, report));
            Run("A34", report, () => CheckA34(set, report));
            Run("A35", report, () => CheckA35(set, report));
            Run("A36", report, () => CheckA36(set, report));
            Run("A37", report, () => CheckA37(set, report));
            Run("A38", report, () => CheckA38(set, report));
            Run("A39-1", report, () => CheckA39Cap(set, report));
            Run("A39-2", report, () => CheckA39Reduce(set, report));
            Run("A39-3", report, () => CheckA39Numeric(set, report));
            Run("A39-4", report, () => CheckA39Discrete(set, report));
            Run("A40", report, () => CheckA40(set, report));
            Run("A41", report, () => CheckA41(set, report));
            Run("A42", report, () => CheckA42(set, report));
            Run("A43", report, () => CheckA43(set, report));

            // ---- 节点消耗（progression-node-cost-rules.md）----
            Run("C1", report, () => CheckCostRange(set, report, "star_node", 1, 60));
            Run("C2", report, () => CheckCostRange(set, report, "meridian", 1, 50));
            Run("C3", report, () => CheckStarCostMonotonic(set, report));
            Run("C4", report, () => CheckMeridianCostMonotonic(set, report));
            Run("C5", report, () => CheckMeridianTopologyOrder(set, report));
            Run("C6", report, () => CheckCostRecompute(set, report));
            Run("C7", report, () => CheckCostNotInPowerFormula(set, report));

            // ---- 跨表（孤儿外键 / 派生列覆盖）----
            Run("FK", report, () => CheckForeignKeys(set, report));
        }

        private static void Run(string id, ValidationReport report, Func<string> check)
        {
            int before = report.ErrorCount;
            string detail;
            try
            {
                detail = check();
            }
            catch (Exception ex)
            {
                report.Error(id, null, null, null, "断言执行异常：" + ex.GetType().Name + " " + ex.Message);
                detail = "异常";
            }

            var result = new AssertionResult
            {
                Id = id,
                Passed = report.ErrorCount == before,
                Detail = detail
            };
            report.AddAssertion(result);
        }

        // =====================================================================
        // 战斗侧 A19–A30（IMPL-v2.0 §5）
        // =====================================================================

        /// <summary>A19：phases 各 ≥1 且 totalFrames ≤ 90。</summary>
        private static string CheckA19(TableSet set, ValidationReport report)
        {
            var table = set.Get("skill");
            if (table == null) return Missing(set, report, "skill");

            int checkedRows = 0;
            foreach (var bound in table.Rows)
            {
                var row = bound.As<SkillRow>();
                if (row == null) continue;
                if (row.phases == null) continue;   // 多段技能走 combo_segments

                checkedRows++;
                if (row.phases.startup < 1 || row.phases.active < 1 || row.phases.recovery < 1)
                {
                    report.Error("A19", "skill", bound.Key, "phases",
                        $"相帧必须 ≥1，实际 {row.phases.startup}/{row.phases.active}/{row.phases.recovery}");
                }
                if (row.phases.total > 90)
                {
                    report.Error("A19", "skill", bound.Key, "phases", $"合计帧数 {row.phases.total} > 90");
                }
            }
            return $"检查 {checkedRows} 个单段技能";
        }

        /// <summary>A20：hitShape.frame &lt; active。</summary>
        private static string CheckA20(TableSet set, ValidationReport report)
        {
            var shapes = set.Get("hit_shape");
            var skills = set.Get("skill");
            if (shapes == null) return Missing(set, report, "hit_shape");
            if (skills == null) return Missing(set, report, "skill");

            int checkedRows = 0;
            foreach (var bound in shapes.Rows)
            {
                var row = bound.As<HitShapeRow>();
                if (row == null) continue;

                var skillBound = skills.Find(row.skill_id);
                var skill = skillBound?.As<SkillRow>();
                if (skill == null)
                {
                    report.Error("A20", "hit_shape", bound.Key, "skill_id", "所属技能不存在：" + row.skill_id);
                    continue;
                }

                int active = ActiveFrames(skill);
                if (active <= 0) continue;   // 多段技能按段判定，单段取 phases.active
                checkedRows++;

                if (row.frame >= active)
                {
                    report.Error("A20", "hit_shape", bound.Key, "frame",
                        $"命中帧 {row.frame} 必须 < 判定帧数 {active}");
                }
            }
            return $"检查 {checkedRows} 条命中形状";
        }

        /// <summary>A21：fromFrame ≤ toFrame &lt; totalFrames。</summary>
        private static string CheckA21(TableSet set, ValidationReport report)
        {
            var windows = set.Get("cancel_window");
            var skills = set.Get("skill");
            if (windows == null) return Missing(set, report, "cancel_window");
            if (skills == null) return Missing(set, report, "skill");

            int checkedRows = 0;
            foreach (var bound in windows.Rows)
            {
                var row = bound.As<CancelWindowRow>();
                if (row == null) continue;

                checkedRows++;
                if (row.from_frame > row.to_frame)
                {
                    report.Error("A21", "cancel_window", bound.Key, "from_frame",
                        $"from_frame({row.from_frame}) > to_frame({row.to_frame})");
                }

                int total = TotalFramesOfTag(skills, row.source_tag);
                if (total > 0 && row.to_frame >= total)
                {
                    report.Error("A21", "cancel_window", bound.Key, "to_frame",
                        $"to_frame({row.to_frame}) 必须 < 源技能总帧数 {total}");
                }
            }
            return $"检查 {checkedRows} 条取消窗口";
        }

        /// <summary>A22：取消窗口的标签必须存在于某 skill.entryTags。</summary>
        private static string CheckA22(TableSet set, ValidationReport report)
        {
            var windows = set.Get("cancel_window");
            var skills = set.Get("skill");
            if (windows == null) return Missing(set, report, "cancel_window");
            if (skills == null) return Missing(set, report, "skill");

            var tags = new HashSet<string>(StringComparer.Ordinal);
            foreach (var bound in skills.Rows)
            {
                var skill = bound.As<SkillRow>();
                if (skill?.entry_tags == null) continue;
                foreach (var tag in skill.entry_tags) tags.Add(tag);
            }

            int checkedRows = 0;
            foreach (var bound in windows.Rows)
            {
                var row = bound.As<CancelWindowRow>();
                if (row == null) continue;
                checkedRows++;

                if (!tags.Contains(row.source_tag))
                {
                    report.Error("A22", "cancel_window", bound.Key, "source_tag",
                        $"来源标签「{row.source_tag}」不在任何 skill.entry_tags 里");
                }
                if (!tags.Contains(row.allow_tag))
                {
                    report.Error("A22", "cancel_window", bound.Key, "allow_tag",
                        $"允许标签「{row.allow_tag}」不在任何 skill.entry_tags 里");
                }
            }
            return $"检查 {checkedRows} 条取消窗口，技能标签 {tags.Count} 个";
        }

        /// <summary>A23：每 skill ≥1 条 hitShape。</summary>
        private static string CheckA23(TableSet set, ValidationReport report)
        {
            var skills = set.Get("skill");
            var shapes = set.Get("hit_shape");
            if (skills == null) return Missing(set, report, "skill");
            if (shapes == null) return Missing(set, report, "hit_shape");

            var bySkill = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var bound in shapes.Rows)
            {
                var row = bound.As<HitShapeRow>();
                if (row == null) continue;
                if (!bySkill.TryGetValue(row.skill_id, out int count)) count = 0;
                bySkill[row.skill_id] = count + 1;
            }

            foreach (var bound in skills.Rows)
            {
                var row = bound.As<SkillRow>();
                if (row == null) continue;

                int count;
                bySkill.TryGetValue(row.skill_id, out count);
                int listed = row.hit_shape_ids != null ? row.hit_shape_ids.Length : 0;

                if (count == 0 && listed == 0)
                {
                    report.Error("A23", "skill", bound.Key, "hit_shape_ids", "技能没有任何命中形状");
                }
            }
            return $"检查 {skills.Rows.Count} 个技能";
        }

        /// <summary>A24：曲线积分距离与声明偏差 ≤5%。</summary>
        private static string CheckA24(TableSet set, ValidationReport report)
        {
            var curves = set.Get("movement_curve");
            if (curves == null) return Missing(set, report, "movement_curve");

            int checkedRows = 0;
            foreach (var bound in curves.Rows)
            {
                var row = bound.As<MovementCurveRow>();
                if (row == null) continue;
                checkedRows++;

                if (row.distance <= 0f)
                {
                    report.Error("A24", "movement_curve", bound.Key, "distance", "声明距离必须 > 0 才能算偏差");
                    continue;
                }

                double deviation = Math.Abs(row.integral_distance - row.distance) / row.distance;
                if (deviation > 0.05)
                {
                    report.Error("A24", "movement_curve", bound.Key, "integral_distance",
                        $"积分距离 {row.integral_distance:F4} 与声明 {row.distance:F4} 偏差 {(deviation * 100d):F2}% > 5%");
                }
            }
            return $"检查 {checkedRows} 条位移曲线";
        }

        /// <summary>A25：coreLink 的 writes 与 consumeStacks 互斥。</summary>
        private static string CheckA25(TableSet set, ValidationReport report)
        {
            var skills = set.Get("skill");
            if (skills == null) return Missing(set, report, "skill");

            foreach (var bound in skills.Rows)
            {
                var row = bound.As<SkillRow>();
                if (row?.core_link == null) continue;

                bool writes = !string.IsNullOrEmpty(row.core_link.writes_core_state);
                int consume = row.core_link.consume_stacks;
                if (writes && consume > 0)
                {
                    report.Error("A25", "skill", bound.Key, "core_link",
                        $"writes_core_state（{row.core_link.writes_core_state}）与 consume_stacks（{consume}）互斥");
                }
            }
            return $"检查 {skills.Rows.Count} 个技能的核心联动";
        }

        /// <summary>A26：trigger / condition / effect 必在枚举内。</summary>
        private static string CheckA26(TableSet set, ValidationReport report)
        {
            var rules = set.Get("boss_rule");
            if (rules == null) return Missing(set, report, "boss_rule");

            var triggers = new HashSet<string>(EnumCatalog.BossTriggerValues, StringComparer.Ordinal);
            var conditions = new HashSet<string>(EnumCatalog.BossConditionValues, StringComparer.Ordinal);
            var effects = new HashSet<string>(EnumCatalog.BossEffectValues, StringComparer.Ordinal);

            foreach (var bound in rules.Rows)
            {
                var row = bound.As<BossRuleRow>();
                if (row == null) continue;

                if (!triggers.Contains(row.trigger))
                {
                    report.Error("A26", "boss_rule", bound.Key, "trigger", "触发源不在枚举内：" + row.trigger);
                }
                if (row.conditions != null)
                {
                    foreach (var condition in row.conditions)
                    {
                        if (!conditions.Contains(condition.type))
                        {
                            report.Error("A26", "boss_rule", bound.Key, "conditions", "条件类型不在枚举内：" + condition.type);
                        }
                    }
                }
                if (row.effects != null)
                {
                    foreach (var effect in row.effects)
                    {
                        if (!effects.Contains(effect.type))
                        {
                            report.Error("A26", "boss_rule", bound.Key, "effects", "效果类型不在枚举内：" + effect.type);
                        }
                    }
                }
            }
            return $"检查 {rules.Rows.Count} 条 Boss 规则";
        }

        /// <summary>A27：ForcePhase 的目标阶段必须存在。</summary>
        private static string CheckA27(TableSet set, ValidationReport report)
        {
            var rules = set.Get("boss_rule");
            var bosses = set.Get("boss");
            if (rules == null) return Missing(set, report, "boss_rule");
            if (bosses == null) return Missing(set, report, "boss");

            foreach (var bound in rules.Rows)
            {
                var row = bound.As<BossRuleRow>();
                if (row?.effects == null) continue;

                var bossBound = bosses.Find(row.boss_id);
                var boss = bossBound?.As<BossRow>();
                if (boss == null) continue;

                foreach (var effect in row.effects)
                {
                    if (!string.Equals(effect.type, "ForcePhase", StringComparison.Ordinal)) continue;
                    if (effect.args == null) continue;

                    foreach (var arg in effect.args)
                    {
                        if (!string.Equals(arg.key, "to", StringComparison.Ordinal)) continue;
                        int target;
                        if (!int.TryParse(arg.value, NumberStyles.Integer, CultureInfo.InvariantCulture, out target))
                        {
                            report.Error("A27", "boss_rule", bound.Key, "effects", "ForcePhase 的 to 不是整数：" + arg.value);
                            continue;
                        }
                        if (target < 1 || target > boss.phase_count)
                        {
                            report.Error("A27", "boss_rule", bound.Key, "effects",
                                $"ForcePhase 目标阶段 {target} 超出 {row.boss_id} 的阶段数 {boss.phase_count}");
                        }
                    }
                }
            }
            return $"检查 {rules.Rows.Count} 条 Boss 规则";
        }

        /// <summary>A28：所有 <c>anchor://</c> 引用可解析。</summary>
        private static string CheckA28(TableSet set, ValidationReport report)
        {
            var rules = set.Get("boss_rule");
            var anchors = set.Get("anchor");
            if (rules == null) return Missing(set, report, "boss_rule");
            if (anchors == null) return Missing(set, report, "anchor");

            int checkedRefs = 0;
            foreach (var bound in rules.Rows)
            {
                var row = bound.As<BossRuleRow>();
                if (row == null) continue;

                checkedRefs += CheckAnchorRefs(report, "boss_rule", bound.Key, row.conditions, anchors);
                checkedRefs += CheckAnchorRefs(report, "boss_rule", bound.Key, row.effects, anchors);
            }
            return $"检查 {checkedRefs} 处 anchor 引用";
        }

        private static int CheckAnchorRefs(
            ValidationReport report,
            string table,
            string key,
            BossConditionSpec[] conditions,
            TableData anchors)
        {
            int count = 0;
            if (conditions == null) return count;
            foreach (var condition in conditions)
            {
                if (condition.args == null) continue;
                foreach (var arg in condition.args)
                {
                    count += CheckAnchorRef(report, table, key, arg, anchors);
                }
            }
            return count;
        }

        private static int CheckAnchorRefs(
            ValidationReport report,
            string table,
            string key,
            BossEffectSpec[] effects,
            TableData anchors)
        {
            int count = 0;
            if (effects == null) return count;
            foreach (var effect in effects)
            {
                if (effect.args == null) continue;
                foreach (var arg in effect.args)
                {
                    count += CheckAnchorRef(report, table, key, arg, anchors);
                }
            }
            return count;
        }

        private static int CheckAnchorRef(ValidationReport report, string table, string key, StringArg arg, TableData anchors)
        {
            const string prefix = "anchor://";
            if (arg.value == null || !arg.value.StartsWith(prefix, StringComparison.Ordinal)) return 0;

            string anchorId = arg.value.Substring(prefix.Length);
            if (!anchors.Has(anchorId))
            {
                report.Error("A28", table, key, arg.key, "anchor 引用无法解析：" + arg.value);
            }
            return 1;
        }

        /// <summary>A29：连段模拟不产生负资源。</summary>
        private static string CheckA29(TableSet set, ValidationReport report)
        {
            var skills = set.Get("skill");
            var resources = set.Get("resource");
            if (skills == null) return Missing(set, report, "skill");
            if (resources == null) return Missing(set, report, "resource");

            int cap = 0;
            foreach (var bound in resources.Rows)
            {
                var row = bound.As<ResourceRow>();
                if (row == null) continue;
                if (row.max > cap) cap = row.max;
            }
            if (cap <= 0) cap = 5;

            int chains = 0;
            foreach (var bound in skills.Rows)
            {
                var skill = bound.As<SkillRow>();
                if (skill == null) continue;

                int value = cap;
                int delta = skill.resource_delta != null ? skill.resource_delta.on_cast : 0;
                value += delta;
                chains++;

                if (value < 0)
                {
                    report.Error("A29", "skill", bound.Key, "resource_delta",
                        $"连段模拟：释放后资源 {value} < 0（上限 {cap}，on_cast {delta}）");
                }
                if (value > cap)
                {
                    report.Error("A29", "skill", bound.Key, "resource_delta",
                        $"连段模拟：释放后资源 {value} > 上限 {cap}");
                }

                // 命中收益同样不得把资源顶出上限
                int gain = skill.resource_delta != null ? skill.resource_delta.on_hit : 0;
                int afterHit = value + gain;
                if (afterHit > cap)
                {
                    report.Error("A29", "skill", bound.Key, "resource_delta",
                        $"连段模拟：命中后资源 {afterHit} > 上限 {cap}（on_hit {gain}）");
                }
            }
            return $"模拟 {chains} 条连段，资源上限 {cap}";
        }

        /// <summary>A30：hitstop ∈ [0.05, 0.25]。</summary>
        private static string CheckA30(TableSet set, ValidationReport report)
        {
            var shapes = set.Get("hit_shape");
            if (shapes == null) return Missing(set, report, "hit_shape");

            foreach (var bound in shapes.Rows)
            {
                var row = bound.As<HitShapeRow>();
                if (row == null) continue;
                if (row.hitstop < 0.05f || row.hitstop > 0.25f)
                {
                    report.Error("A30", "hit_shape", bound.Key, "hitstop",
                        $"顿帧系数 {row.hitstop} 超出 [0.05, 0.25]");
                }
            }
            return $"检查 {shapes.Rows.Count} 条命中形状";
        }

        // =====================================================================
        // 养成侧 A31–A43（EL-03 §2）
        // =====================================================================

        /// <summary>A31：星盘前置存在且无环（Kahn 拓扑排序）。</summary>
        private static string CheckA31(TableSet set, ValidationReport report)
        {
            var nodes = set.Get("star_node");
            if (nodes == null) return Missing(set, report, "star_node");

            foreach (var bound in nodes.Rows)
            {
                var row = bound.As<StarNodeRow>();
                if (row?.prereq_ids == null) continue;

                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var prereq in row.prereq_ids)
                {
                    if (!nodes.Has(prereq))
                    {
                        report.Error("A31", "star_node", bound.Key, "prereq_ids", "前置节点不存在：" + prereq);
                    }
                    if (string.Equals(prereq, row.node_id, StringComparison.Ordinal))
                    {
                        report.Error("A31", "star_node", bound.Key, "prereq_ids", "前置不得指向自身");
                    }
                    if (!seen.Add(prereq))
                    {
                        report.Error("A31", "star_node", bound.Key, "prereq_ids", "前置列表有重复项：" + prereq);
                    }
                }
            }

            int remaining = KahnRemaining(nodes);
            if (remaining > 0)
            {
                var cycle = FindCycleNode(nodes);
                report.Error("A31", "star_node", cycle, "prereq_ids", "前置图存在环（拓扑排序剩余 " + remaining + " 个节点）");
            }
            return $"检查 {nodes.Rows.Count} 个星盘节点，环剩余 {remaining}";
        }

        /// <summary>A32：同系连携只在两节点均已点亮时生效（静态 + 三档动态夹具）。</summary>
        private static string CheckA32(TableSet set, ValidationReport report)
        {
            var links = set.Get("star_link");
            var nodes = set.Get("star_node");
            if (links == null) return Missing(set, report, "star_link");
            if (nodes == null) return Missing(set, report, "star_node");

            foreach (var bound in links.Rows)
            {
                var row = bound.As<StarLinkRow>();
                if (row == null) continue;

                if (!string.Equals(row.link_condition, "same_series_adjacent", StringComparison.Ordinal))
                {
                    report.Error("A32", "star_link", bound.Key, "link_condition",
                        "连携条件必须是 same_series_adjacent，实际 " + row.link_condition);
                }

                string seriesA = SeriesOf(nodes, row.node_a);
                string seriesB = SeriesOf(nodes, row.node_b);
                if (!string.IsNullOrEmpty(seriesA) && !string.IsNullOrEmpty(seriesB) &&
                    !string.Equals(seriesA, seriesB, StringComparison.Ordinal))
                {
                    report.Error("A32", "star_link", bound.Key, "series", $"两端异系：{seriesA} / {seriesB}");
                }
            }

            // 三档动态夹具：① 仅 A 点亮 → inactive ② A、B 同系且均点亮 → active（且多出一份加成）
            // ③ A、B 均点亮但异系（非法夹具）→ inactive
            var fixture = new List<Fixture>
            {
                new Fixture("仅 A 点亮", false, true, true, false, 0f),
                new Fixture("A、B 同系且均点亮", true, true, true, true, 1f),
                new Fixture("A、B 均点亮但异系（非法夹具）", true, true, false, false, 0f)
            };

            int fixtureFailures = 0;
            foreach (var f in fixtures)
            {
                bool active = ResolveStarLinkState(f.nodeAUnlocked, f.nodeBUnlocked, f.sameSeries);
                if (active != f.expectActive)
                {
                    fixtureFailures++;
                    report.Error("A32", "star_link", "fixture:" + f.name, "active_state",
                        $"夹具「{f.name}」期望 active={f.expectActive}，实际 {active}");
                }
                float bonus = active ? f.effectValue : 0f;
                if (Math.Abs(bonus - f.expectBonus) > 1e-6f)
                {
                    fixtureFailures++;
                    report.Error("A32", "star_link", "fixture:" + f.name, "effect_value",
                        $"夹具「{f.name}」期望加成 {f.expectBonus}，实际 {bonus}");
                }
            }
            return $"检查 {links.Rows.Count} 条连携 + {fixtures.Count} 档动态夹具（失败 {fixtureFailures}）";
        }

        private struct Fixture
        {
            public readonly string name;
            public readonly bool nodeAUnlocked;
            public readonly bool nodeBUnlocked;
            public readonly bool sameSeries;
            public readonly bool expectActive;
            public readonly float effectValue;
            public readonly float expectBonus;

            public Fixture(string name, bool a, bool b, bool same, bool expectActive, float effectValue)
            {
                this.name = name;
                nodeAUnlocked = a;
                nodeBUnlocked = b;
                sameSeries = same;
                this.expectActive = expectActive;
                this.effectValue = effectValue;
                expectBonus = expectActive ? effectValue : 0f;
            }
        }

        /// <summary>连携生效态求值：两节点均点亮且同系 → active。</summary>
        public static bool ResolveStarLinkState(bool nodeAUnlocked, bool nodeBUnlocked, bool sameSeries)
        {
            return nodeAUnlocked && nodeBUnlocked && sameSeries;
        }

        /// <summary>A33：阶位连续无缺口 + 阶位不变量。</summary>
        private static string CheckA33(TableSet set, ValidationReport report)
        {
            var tiers = set.Get("tech_tier");
            if (tiers == null) return Missing(set, report, "tech_tier");

            var bySkill = new Dictionary<string, List<TechTierRow>>(StringComparer.Ordinal);
            foreach (var bound in tiers.Rows)
            {
                var row = bound.As<TechTierRow>();
                if (row == null) continue;
                if (!bySkill.TryGetValue(row.skill_id, out var list))
                {
                    list = new List<TechTierRow>();
                    bySkill[row.skill_id] = list;
                }
                list.Add(row);
            }

            foreach (var pair in bySkill)
            {
                var list = pair.Value;
                var seen = new HashSet<int>();
                int max = 0;
                foreach (var row in list)
                {
                    if (!seen.Add(row.tier))
                    {
                        report.Error("A33", "tech_tier", pair.Key + "|" + row.tier, "tier", "阶位重复");
                    }
                    if (row.tier > max) max = row.tier;
                }
                if (max > 5)
                {
                    report.Error("A33", "tech_tier", pair.Key, "tier", $"最大阶位 {max} > 5");
                }
                for (int t = 1; t <= max; t++)
                {
                    if (!seen.Contains(t))
                    {
                        report.Error("A33", "tech_tier", pair.Key, "tier", $"阶位缺口：缺少 {t} 阶");
                    }
                }

                // 阶位不变量：5 种 unlock_type 各一次；tier1 = stat，tier5 = mechanic
                var typeByTier = new Dictionary<int, string>();
                foreach (var row in list)
                {
                    if (typeByTier.ContainsKey(row.tier))
                    {
                        report.Error("A33", "tech_tier", pair.Key + "|" + row.tier, "unlock_type", "同一阶位出现两种解锁类型");
                        continue;
                    }
                    typeByTier[row.tier] = row.unlock_type;
                }

                if (typeByTier.Count == 5)
                {
                    var typeSet = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var kv in typeByTier) typeSet.Add(kv.Value);
                    if (typeSet.Count != 5)
                    {
                        report.Error("A33", "tech_tier", pair.Key, "unlock_type",
                            "5 个阶位必须恰好覆盖 5 种解锁类型各一次，实际覆盖 " + typeSet.Count + " 种");
                    }
                    if (typeByTier.TryGetValue(1, out string t1) && t1 != "stat")
                    {
                        report.Error("A33", "tech_tier", pair.Key + "|1", "unlock_type", "1 阶恒为 stat，实际 " + t1);
                    }
                    if (typeByTier.TryGetValue(5, out string t5) && t5 != "mechanic")
                    {
                        report.Error("A33", "tech_tier", pair.Key + "|5", "unlock_type", "5 阶恒为 mechanic，实际 " + t5);
                    }
                }
            }
            return $"检查 {bySkill.Count} 个技能的阶位";
        }

        /// <summary>A33-1：派生 id 双向引用一致。</summary>
        private static string CheckA33_1(TableSet set, ValidationReport report)
        {
            var tiers = set.Get("tech_tier");
            var skills = set.Get("skill");
            if (tiers == null) return Missing(set, report, "tech_tier");
            if (skills == null) return Missing(set, report, "skill");

            var derivativeIds = new List<string>();
            foreach (var bound in tiers.Rows)
            {
                var row = bound.As<TechTierRow>();
                if (row == null || string.IsNullOrEmpty(row.unlock_derivative_id)) continue;
                derivativeIds.Add(row.unlock_derivative_id);
            }

            var unique = new HashSet<string>(derivativeIds, StringComparer.Ordinal);
            if (unique.Count != derivativeIds.Count)
            {
                report.Error("A33-1", "tech_tier", null, "unlock_derivative_id", "派生 id 出现重复");
            }

            var declared = new HashSet<string>(StringComparer.Ordinal);
            foreach (var bound in skills.Rows)
            {
                var row = bound.As<SkillRow>();
                if (row?.derivative_ids == null) continue;
                foreach (var id in row.derivative_ids) declared.Add(id);
            }

            foreach (var id in unique)
            {
                if (!declared.Contains(id))
                {
                    report.Error("A33-1", "tech_tier", null, "unlock_derivative_id",
                        "派生 id「" + id + "」未出现在 skill.derivative_ids 里");
                }
            }

            foreach (var id in declared)
            {
                if (!unique.Contains(id))
                {
                    report.Error("A33-1", "skill", null, "derivative_ids",
                        "skill.derivative_ids 里的「" + id + "」没有任何阶位引用");
                }
            }
            return $"检查 {unique.Count} 个派生 id（skill 侧声明 {declared.Count} 个）";
        }

        /// <summary>A33-2：派生 id 必须在清单内、每技能 ≤2 条、tier = 该技能派生类型所在阶位。</summary>
        private static string CheckA33_2(TableSet set, ValidationReport report)
        {
            var tiers = set.Get("tech_tier");
            if (tiers == null) return Missing(set, report, "tech_tier");

            var catalog = DerivativeCatalog.All;
            var perSkill = new Dictionary<string, int>(StringComparer.Ordinal);
            var derivativeTier = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var bound in tiers.Rows)
            {
                var row = bound.As<TechTierRow>();
                if (row == null) continue;

                if (string.Equals(row.unlock_type, "derivative", StringComparison.Ordinal))
                {
                    derivativeTier[row.skill_id] = row.tier;
                }
            }

            foreach (var bound in tiers.Rows)
            {
                var row = bound.As<TechTierRow>();
                if (row == null || string.IsNullOrEmpty(row.unlock_derivative_id)) continue;

                if (!catalog.Contains(row.unlock_derivative_id))
                {
                    report.Error("A33-2", "tech_tier", bound.Key, "unlock_derivative_id",
                        "派生 id 不在 IMPL-v3.0.md §4.8.5 的 8 条清单内：" + row.unlock_derivative_id);
                }

                int count;
                perSkill.TryGetValue(row.skill_id, out count);
                count++;
                perSkill[row.skill_id] = count;
                if (count > 2)
                {
                    report.Error("A33-2", "tech_tier", bound.Key, "unlock_derivative_id",
                        $"技能 {row.skill_id} 的派生条目超过 2 条");
                }

                int expected;
                if (derivativeTier.TryGetValue(row.skill_id, out expected) && expected != row.tier)
                {
                    report.Error("A33-2", "tech_tier", bound.Key, "tier",
                        $"派生条目所在阶位必须等于该技能派生类型所在阶位（期望 {expected}，实际 {row.tier}）");
                }
            }
            return $"检查 {perSkill.Count} 个技能的派生条目";
        }

        /// <summary>A33-3：裂石全部 5 阶 + 回锋 1–4 阶的派生 id 必须为空。</summary>
        private static string CheckA33_3(TableSet set, ValidationReport report)
        {
            var tiers = set.Get("tech_tier");
            if (tiers == null) return Missing(set, report, "tech_tier");

            int checkedRows = 0;
            foreach (var bound in tiers.Rows)
            {
                var row = bound.As<TechTierRow>();
                if (row == null) continue;

                bool lieshi = row.skill_id.IndexOf("lieshi", StringComparison.Ordinal) >= 0;
                bool huifeng = row.skill_id.IndexOf("huifeng", StringComparison.Ordinal) >= 0;

                if (lieshi || (huifeng && row.tier <= 4))
                {
                    checkedRows++;
                    if (!string.IsNullOrEmpty(row.unlock_derivative_id))
                    {
                        report.Error("A33-3", "tech_tier", bound.Key, "unlock_derivative_id",
                            $"{row.skill_id} {row.tier} 阶的派生 id 必须为空，实际 {row.unlock_derivative_id}");
                    }
                }
            }
            return $"检查 {checkedRows} 个应无派生的阶位";
        }

        /// <summary>A33-4：派生类型 + new_graphic 必须有非空派生 id。</summary>
        private static string CheckA33_4(TableSet set, ValidationReport report)
        {
            var tiers = set.Get("tech_tier");
            if (tiers == null) return Missing(set, report, "tech_tier");

            foreach (var bound in tiers.Rows)
            {
                var row = bound.As<TechTierRow>();
                if (row == null) continue;
                if (!string.Equals(row.unlock_type, "derivative", StringComparison.Ordinal)) continue;
                if (!string.Equals(row.visual_tier, "new_graphic", StringComparison.Ordinal)) continue;

                if (string.IsNullOrEmpty(row.unlock_derivative_id))
                {
                    report.Error("A33-4", "tech_tier", bound.Key, "unlock_derivative_id",
                        "派生类型 + new_graphic 的行必须有非空 unlock_derivative_id");
                }
            }
            return "检查派生类型 + new_graphic 的行";
        }

        /// <summary>A33-5：new_graphic 行数 == 派生清单里 new_graphic 的条数。</summary>
        private static string CheckA33_5(TableSet set, ValidationReport report)
        {
            var tiers = set.Get("tech_tier");
            if (tiers == null) return Missing(set, report, "tech_tier");

            int newGraphic = 0;
            foreach (var bound in tiers.Rows)
            {
                var row = bound.As<TechTierRow>();
                if (row == null) continue;
                if (string.Equals(row.visual_tier, "new_graphic", StringComparison.Ordinal)) newGraphic++;
            }

            int expected = DerivativeCatalog.NewGraphicCount;
            if (newGraphic != expected)
            {
                report.Error("A33-5", "tech_tier", null, "visual_tier",
                    $"new_graphic 行数 {newGraphic} != 派生清单里 new_graphic 条数 {expected}");
            }
            return $"new_graphic 行数 {newGraphic}（清单 {expected}）";
        }

        /// <summary>A34：3 个特技槽不得同时装同一互斥组（枚举全组合）。</summary>
        private static string CheckA34(TableSet set, ValidationReport report)
        {
            var specialties = set.Get("specialty");
            if (specialties == null) return Missing(set, report, "specialty");

            var rows = new List<SpecialtyRow>();
            foreach (var bound in specialties.Rows)
            {
                var row = bound.As<SpecialtyRow>();
                if (row != null) rows.Add(row);
            }

            int combos = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                for (int j = i + 1; j < rows.Count; j++)
                {
                    for (int k = j + 1; k < rows.Count; k++)
                    {
                        combos++;
                        var trio = new[] { rows[i], rows[j], rows[k] };
                        for (int a = 0; a < 3; a++)
                        {
                            for (int b = a + 1; b < 3; b++)
                            {
                                if (string.Equals(trio[a].exclude_group, trio[b].exclude_group, StringComparison.Ordinal))
                                {
                                    report.Error("A34", "specialty",
                                        trio[a].spec_id + "+" + trio[b].spec_id, "exclude_group",
                                        $"同一互斥组「{trio[a].exclude_group}」的两条特技不能并存（槽位组合：{trio[0].spec_id}/{trio[1].spec_id}/{trio[2].spec_id}）");
                                }
                            }
                        }
                    }
                }
            }

            // 负向夹具：同组两条必须报错（与枚举结果一致）
            int fixtureChecked = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                for (int j = i + 1; j < rows.Count; j++)
                {
                    if (!string.Equals(rows[i].exclude_group, rows[j].exclude_group, StringComparison.Ordinal)) continue;
                    fixtureChecked++;
                    if (FindConflict(rows, rows[i].spec_id, rows[j].spec_id) == null)
                    {
                        report.Error("A34", "specialty", rows[i].spec_id + "+" + rows[j].spec_id, "exclude_group",
                            "负向夹具：同组两条特技必须被判为冲突");
                    }
                }
            }
            return $"枚举 {combos} 种三槽组合 + {fixtureChecked} 对负向夹具";
        }

        private static string FindConflict(List<SpecialtyRow> rows, string a, string b)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                for (int j = i + 1; j < rows.Count; j++)
                {
                    if (!string.Equals(rows[i].exclude_group, rows[j].exclude_group, StringComparison.Ordinal)) continue;
                    if ((rows[i].spec_id == a && rows[j].spec_id == b) ||
                        (rows[i].spec_id == b && rows[j].spec_id == a))
                    {
                        return rows[i].spec_id + "+" + rows[j].spec_id;
                    }
                }
            }
            return null;
        }

        /// <summary>A35：机制修改项必须落在 28 项枚举内（空串合法）。</summary>
        private static string CheckA35(TableSet set, ValidationReport report)
        {
            int checkedMods = 0;
            checkedMods += CheckModColumn(set, report, "star_node", "mechanic_mod");
            checkedMods += CheckModColumn(set, report, "meridian", "mechanic_mod");
            checkedMods += CheckModColumn(set, report, "affix_pool", "mechanic_mod");

            return $"检查 {checkedMods} 处 mechanic_mod（合法集合 {MechanicModCatalog.Count} 项）";
        }

        private static int CheckModColumn(TableSet set, ValidationReport report, string tableName, string column)
        {
            var table = set.Get(tableName);
            if (table == null)
            {
                if (tableName == "affix_pool")
                {
                    report.Error("A35", tableName, null, column, "缺源数据：affix_pool 表未加载");
                }
                return 0;
            }

            int count = 0;
            foreach (var bound in table.Rows)
            {
                var row = bound.Row;
                if (row == null) continue;

                var field = row.GetType().GetField(column);
                if (field == null) continue;
                var value = field.GetValue(row) as string;
                if (string.IsNullOrEmpty(value)) continue;   // 空串 = 无修改，合法

                count++;
                if (!MechanicModCatalog.TryParse(value, out _))
                {
                    report.Error("A35", tableName, bound.Key, column,
                        $"机制项「{value}」不在 {MechanicModCatalog.Count} 项枚举内；最近似拼写：{MechanicModCatalog.Suggest(value)}");
                }
            }
            return count;
        }

        /// <summary>A36：锋饰合成链无环、最高等级不可被合成、每级至少一个上��级材料。</summary>
        private static string CheckA36(TableSet set, ValidationReport report)
        {
            var items = set.Get("socket_item");
            if (items == null) return Missing(set, report, "socket_item");

            var byId = new Dictionary<string, SocketItemRow>(StringComparer.Ordinal);
            int maxLevel = 0;
            foreach (var bound in items.Rows)
            {
                var row = bound.As<SocketItemRow>();
                if (row == null) continue;
                byId[row.socket_id] = row;
                if (row.level > maxLevel) maxLevel = row.level;
            }

            foreach (var row in byId.Values)
            {
                if (row.level == maxLevel && row.compose_from != null && row.compose_from.Length > 0)
                {
                    report.Error("A36", "socket_item", row.socket_id, "compose_from",
                        $"最高等级（{maxLevel}）的项不得有合成材料");
                }

                if (row.level < maxLevel)
                {
                    bool hasPrevious = false;
                    if (row.compose_from != null)
                    {
                        foreach (var material in row.compose_from)
                        {
                            if (!byId.TryGetValue(material, out var materialRow))
                            {
                                report.Error("A36", "socket_item", row.socket_id, "compose_from", "材料不存在：" + material);
                                continue;
                            }
                            if (materialRow.level == row.level - 1) hasPrevious = true;
                        }
                    }
                    if (!hasPrevious)
                    {
                        report.Error("A36", "socket_item", row.socket_id, "compose_from",
                            $"等级 {row.level} 的项至少要有一个等级 {row.level - 1} 的材料");
                    }
                }
            }

            int remaining = KahnRemainingSocket(items);
            if (remaining > 0)
            {
                report.Error("A36", "socket_item", null, "compose_from", "合成链存在环（拓扑排序剩余 " + remaining + " 项）");
            }
            return $"检查 {byId.Count} 个锋饰，最高等级 {maxLevel}";
        }

        /// <summary>A37：词条挂技能合法性 + 词条池口径 + 技能侧反向检查。</summary>
        private static string CheckA37(TableSet set, ValidationReport report)
        {
            var affixes = set.Get("affix_pool");
            var skills = set.Get("skill");
            if (affixes == null) return Missing(set, report, "affix_pool");
            if (skills == null) return Missing(set, report, "skill");

            var skillIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var bound in skills.Rows)
            {
                var row = bound.As<SkillRow>();
                if (row != null) skillIds.Add(row.skill_id);
            }

            int tagged = 0;
            var hitSkills = new HashSet<string>(StringComparer.Ordinal);
            foreach (var bound in affixes.Rows)
            {
                var row = bound.As<AffixPoolRow>();
                if (row == null) continue;
                if (string.IsNullOrEmpty(row.skill_tag)) continue;

                tagged++;
                if (!skillIds.Contains(row.skill_tag))
                {
                    report.Error("A37", "affix_pool", bound.Key, "skill_tag", "技能不存在：" + row.skill_tag);
                    continue;
                }
                hitSkills.Add(row.skill_tag);
            }

            if (tagged < 6)
            {
                report.Error("A37", "affix_pool", null, "skill_tag",
                    $"带 skill_tag 的词条数 {tagged} < 6（IMPL-v3.0.md §4.1「18 条中至少 6 条带 skill_tag」）");
            }

            // 技能侧反向检查：8 个门派技能（tech_tier 覆盖的技能）中至少 6 个被命中
            var tiers = set.Get("tech_tier");
            var gateSkills = new HashSet<string>(StringComparer.Ordinal);
            if (tiers != null)
            {
                foreach (var bound in tiers.Rows)
                {
                    var row = bound.As<TechTierRow>();
                    if (row != null) gateSkills.Add(row.skill_id);
                }
            }
            else
            {
                gateSkills = skillIds;
            }

            int hit = 0;
            foreach (var id in gateSkills)
            {
                if (hitSkills.Contains(id)) hit++;
            }
            if (gateSkills.Count > 0 && hit < 6)
            {
                report.Error("A37", "affix_pool", null, "skill_tag",
                    $"被词条命中的门派技能数 {hit} < 6（门派技能共 {gateSkills.Count} 个）");
            }
            return $"词条带标签 {tagged} 条，命中技能 {hit}/{gateSkills.Count}";
        }

        /// <summary>A38：expected_power 必须由分项重算得到。</summary>
        private static string CheckA38(TableSet set, ValidationReport report)
        {
            var progression = set.Get("progression");
            var tuning = set.Get("global_tuning");
            if (progression == null) return Missing(set, report, "progression");
            if (tuning == null) return Missing(set, report, "global_tuning");

            int basePower = (int)Math.Round(TuningValue(tuning, "base_power"), MidpointRounding.AwayFromZero);
            if (basePower <= 0)
            {
                report.Error("A38", "global_tuning", null, "base_power", "缺 base_power（战力基数）");
                return "缺 base_power";
            }

            int total = 0;
            foreach (var bound in progression.Rows)
            {
                var row = bound.As<ProgressionRow>();
                if (row == null) continue;
                if (!string.Equals(row.kind, "cultivation", StringComparison.Ordinal)) continue;

                int weightPermille = (int)Math.Round(row.weight * 1000d, MidpointRounding.AwayFromZero);
                int recomputed = (int)Math.Round((double)weightPermille * row.raw_value / 1000d, MidpointRounding.AwayFromZero);
                if (row.expected_power != recomputed)
                {
                    report.Error("A38", "progression", bound.Key, "expected_power",
                        $"手填 {row.expected_power} 与重算 {recomputed} 不等（权重 {row.weight} × 原值 {row.raw_value}）");
                }
                total += recomputed;
            }
            return $"重算基数 {basePower} + 修为线贡献 {total}";
        }

        /// <summary>A39-1：上限类取最大值。</summary>
        private static string CheckA39Cap(TableSet set, ValidationReport report)
        {
            var sources = CollectEffectSources(set);
            if (sources == null) return Missing(set, report, "specialty");

            // 夹具：锋芒上限 5 + 两个来源各 1.2 倍 → 6（不是 5×(1+1.2)，也不是 min）
            var values = new List<float> { 1.2f, 1.2f };
            float merged = EffectMerge.Merge("resource_cap", 5f, values);
            if (Math.Abs(merged - 6f) > 1e-4f)
            {
                report.Error("A39-1", "specialty", null, "effect_key",
                    $"上限类合并夹具失败：max(5, 1.2, 1.2) 应得 6，实际 {merged}");
            }
            if (!EffectMerge.IsCapKey("resource_cap"))
            {
                report.Error("A39-1", null, null, "effect_key", "resource_cap 未登记为上限类");
            }

            int checkedKeys = 0;
            foreach (var pair in sources)
            {
                if (!EffectMerge.IsCapKey(pair.Key)) continue;
                checkedKeys++;
                if (pair.Values.Count < 2) continue;
                float result = EffectMerge.Merge(pair.Key, 0f, pair.Values);
                if (result <= 0f)
                {
                    report.Error("A39-1", null, pair.Key, "effect_key", "上限类合并结果非法：" + result);
                }
            }
            return $"检查 {checkedKeys} 个上限类效果键";
        }

        /// <summary>A39-2：消耗 / 削减类取最小值。</summary>
        private static string CheckA39Reduce(TableSet set, ValidationReport report)
        {
            var sources = CollectEffectSources(set);
            if (sources == null) return Missing(set, report, "specialty");

            var values = new List<float> { 2f, 2f, 2f };
            float merged = EffectMerge.Merge("finish_cost", 3f, values);
            if (Math.Abs(merged - 2f) > 1e-4f)
            {
                report.Error("A39-2", null, null, "effect_key",
                    $"消耗类合并夹具失败：min(3, 2, 2, 2) 应得 2，实际 {merged}");
            }

            int checkedKeys = 0;
            foreach (var pair in sources)
            {
                if (!EffectMerge.IsReduceKey(pair.Key)) continue;
                checkedKeys++;
                float result = EffectMerge.Merge(pair.Key, 0f, pair.Values);
                if (result < 0f)
                {
                    report.Error("A39-2", null, pair.Key, "effect_key", "消耗类合并结果为负：" + result);
                }
            }
            return $"检查 {checkedKeys} 个消耗 / 削减类效果键";
        }

        /// <summary>A39-3：其余数值类取最小值、不叠加。</summary>
        private static string CheckA39Numeric(TableSet set, ValidationReport report)
        {
            var sources = CollectEffectSources(set);
            if (sources == null) return Missing(set, report, "specialty");

            var values = new List<float> { 0.5f, 0.3f };
            float merged = EffectMerge.Merge("some_numeric_effect", 1f, values);
            if (Math.Abs(merged - 0.3f) > 1e-4f)
            {
                report.Error("A39-3", null, null, "effect_key",
                    $"数值类合并夹具失败：min(1, 0.5, 0.3) 应得 0.3，实际 {merged}（不得叠加成 0.8）");
            }

            int checkedKeys = 0;
            foreach (var pair in sources)
            {
                if (EffectMerge.IsCapKey(pair.Key) || EffectMerge.IsReduceKey(pair.Key)) continue;
                checkedKeys++;
                float result = EffectMerge.Merge(pair.Key, 0f, pair.Values);
                if (result < 0f)
                {
                    report.Error("A39-3", null, pair.Key, "effect_key", "数值类合并结果为负：" + result);
                }
            }
            return $"检查 {checkedKeys} 个数值类效果键";
        }

        /// <summary>A39-4：离散类只生效一次。</summary>
        private static string CheckA39Discrete(TableSet set, ValidationReport report)
        {
            var sources = new List<bool> { true, true, true };
            int count = EffectMerge.DiscreteApplicationCount(sources);
            if (count != 1)
            {
                report.Error("A39-4", null, null, "effect_key", $"离散类生效次数 {count} != 1");
            }
            return "检查离散类生效次数";
        }

        /// <summary>A40：掉落保底阈值取自 global_tuning，规则由 DropPity 实现。</summary>
        private static string CheckA40(TableSet set, ValidationReport report)
        {
            var tuning = set.Get("global_tuning");
            if (tuning == null) return Missing(set, report, "global_tuning");

            float threshold = TuningValue(tuning, "drop_pity_threshold");
            if (threshold <= 0f)
            {
                report.Error("A40", "global_tuning", null, "drop_pity_threshold", "缺 drop_pity_threshold（阈值不写死在代码里）");
                return "缺阈值";
            }

            int thresholdInt = (int)Math.Round(threshold, MidpointRounding.AwayFromZero);
            if (!DropPity.ShouldDrop(thresholdInt, thresholdInt))
            {
                report.Error("A40", null, null, "drop_pity", "保底判定夹具失败：计数达到阈值时必须产出");
            }
            if (DropPity.ShouldDrop(thresholdInt - 1, thresholdInt))
            {
                report.Error("A40", null, null, "drop_pity", "保底判定夹具失败：计数未达阈值时不得强制产出");
            }
            if (DropPity.NextCounter(true, 3) != 0)
            {
                report.Error("A40", null, null, "drop_pity", "掉落清零夹具失败");
            }
            if (DropPity.NextCounter(false, 3) != 4)
            {
                report.Error("A40", null, null, "drop_pity", "未掉落计数 +1 夹具失败");
            }
            return $"阈值 {thresholdInt}（取自 global_tuning）";
        }

        /// <summary>A41：技传 5 阶的 mechanic_targets 长度 ≤ 1；非 mechanic 行必须为空。</summary>
        private static string CheckA41(TableSet set, ValidationReport report)
        {
            var tiers = set.Get("tech_tier");
            if (tiers == null) return Missing(set, report, "tech_tier");

            var loops = new HashSet<string>(EnumCatalog.LoopTargetValues, StringComparer.Ordinal);
            foreach (var bound in tiers.Rows)
            {
                var row = bound.As<TechTierRow>();
                if (row == null) continue;

                int count = row.mechanic_targets != null ? row.mechanic_targets.Length : 0;
                bool isMechanic = string.Equals(row.unlock_type, "mechanic", StringComparison.Ordinal);

                if (isMechanic)
                {
                    if (row.tier == 5 && count > 1)
                    {
                        report.Error("A41", "tech_tier", bound.Key, "mechanic_targets",
                            $"5 阶机制改写只能改写一条核心循环，实际 {count} 条");
                    }
                    if (count == 0)
                    {
                        report.Error("A41", "tech_tier", bound.Key, "mechanic_targets", "mechanic 行必须填 mechanic_targets");
                    }
                }
                else if (count > 0)
                {
                    report.Error("A41", "tech_tier", bound.Key, "mechanic_targets",
                        "非 mechanic 行的 mechanic_targets 必须为空数组");
                }

                if (row.mechanic_targets != null)
                {
                    foreach (var target in row.mechanic_targets)
                    {
                        if (!loops.Contains(target))
                        {
                            report.Error("A41", "tech_tier", bound.Key, "mechanic_targets",
                                "核心循环取值不在 loop_l1..loop_l6 内：" + target);
                        }
                    }
                }
            }
            return $"检查 {tiers.Rows.Count} 个技传阶位";
        }

        /// <summary>A42：重置与名望完整性。</summary>
        private static string CheckA42(TableSet set, ValidationReport report)
        {
            var progression = set.Get("progression");
            if (progression == null) return Missing(set, report, "progression");

            var fameTiers = new HashSet<int>();
            foreach (var bound in progression.Rows)
            {
                var row = bound.As<ProgressionRow>();
                if (row == null) continue;

                if (string.Equals(row.kind, "reset", StringComparison.Ordinal))
                {
                    if (row.reset_cost < 0)
                    {
                        report.Error("A42", "progression", bound.Key, "reset_cost", "重置消耗禁负");
                    }
                    if (!string.Equals(row.reset_policy, "free", StringComparison.Ordinal))
                    {
                        report.Error("A42", "progression", bound.Key, "reset_policy",
                            "重置策略必须等于 free，实际 " + row.reset_policy);
                    }
                    if (string.IsNullOrEmpty(row.target_system))
                    {
                        report.Error("A42", "progression", bound.Key, "target_system", "重置行必须填 target_system");
                    }
                }

                if (string.Equals(row.kind, "fame", StringComparison.Ordinal))
                {
                    if (row.fame_unlock_tier < 1 || row.fame_unlock_tier > 5)
                    {
                        report.Error("A42", "progression", bound.Key, "fame_unlock_tier", "名望档位必须 ∈ [1,5]");
                    }
                    else
                    {
                        fameTiers.Add(row.fame_unlock_tier);
                    }

                    int expectedMin = 2 * (row.fame_unlock_tier - 1) + 1;
                    int expectedMax = 2 * row.fame_unlock_tier;
                    if (row.fame_lv_min != expectedMin || row.fame_lv_max != expectedMax)
                    {
                        report.Error("A42", "progression", bound.Key, "fame_lv_min",
                            $"名望档 {row.fame_unlock_tier} 对应 Lv 区间 [{expectedMin}, {expectedMax}]，实际 [{row.fame_lv_min}, {row.fame_lv_max}]");
                    }
                }
            }

            for (int tier = 1; tier <= 5; tier++)
            {
                if (!fameTiers.Contains(tier))
                {
                    report.Error("A42", "progression", null, "fame_unlock_tier", $"名望档 {tier} 缺行（防名望缺档）");
                }
            }
            return $"名望档 {fameTiers.Count} 个（1–5）";
        }

        /// <summary>A43：图集格位配额（两层）。</summary>
        private static string CheckA43(TableSet set, ValidationReport report)
        {
            var tiers = set.Get("tech_tier");
            var tuning = set.Get("global_tuning");
            if (tiers == null) return Missing(set, report, "tech_tier");
            if (tuning == null) return Missing(set, report, "global_tuning");

            float graphicBudget = TuningValue(tuning, "vfx_graphic_budget");
            float genericBudget = TuningValue(tuning, "vfx_generic_budget");
            if (graphicBudget <= 0f)
            {
                report.Error("A43", "global_tuning", null, "vfx_graphic_budget", "缺 vfx_graphic_budget");
                return "缺预算";
            }

            int newGraphic = 0;
            foreach (var bound in tiers.Rows)
            {
                var row = bound.As<TechTierRow>();
                if (row == null) continue;
                if (string.Equals(row.visual_tier, "new_graphic", StringComparison.Ordinal)) newGraphic++;
            }

            double used = newGraphic + genericBudget;
            if (used > graphicBudget + 1e-6)
            {
                report.Error("A43", "tech_tier", null, "visual_tier",
                    $"占格 {newGraphic} + 通用件 {genericBudget} = {used} > 预算 {graphicBudget}");
            }
            return $"占格 {newGraphic} + 通用件 {genericBudget} ≤ 预算 {graphicBudget}";
        }

        // =====================================================================
        // 节点消耗 C1–C7
        // =====================================================================

        /// <summary>C1 / C2：消耗值域。</summary>
        private static string CheckCostRange(TableSet set, ValidationReport report, string tableName, int min, int max)
        {
            var table = set.Get(tableName);
            if (table == null) return Missing(set, report, tableName);

            foreach (var bound in table.Rows)
            {
                var row = bound.Row;
                if (row == null) continue;
                var field = row.GetType().GetField("cost_eu");
                if (field == null) continue;
                int cost = (int)field.GetValue(row);
                if (cost < min || cost > max)
                {
                    report.Error(tableName == "star_node" ? "C1" : "C2", tableName, bound.Key, "cost_eu",
                        $"消耗 {cost} 超出 [{min}, {max}]");
                }
            }
            return $"检查 {table.Rows.Count} 行（值域 {min}–{max}）";
        }

        /// <summary>C3：星盘同系单调递增（N1 &lt; N2 &lt; N3）。</summary>
        private static string CheckStarCostMonotonic(TableSet set, ValidationReport report)
        {
            var nodes = set.Get("star_node");
            if (nodes == null) return Missing(set, report, "star_node");

            var bySeries = new Dictionary<string, List<StarNodeRow>>(StringComparer.Ordinal);
            foreach (var bound in nodes.Rows)
            {
                var row = bound.As<StarNodeRow>();
                if (row == null) continue;
                if (!bySeries.TryGetValue(row.series, out var list))
                {
                    list = new List<StarNodeRow>();
                    bySeries[row.series] = list;
                }
                list.Add(row);
            }

            foreach (var pair in bySeries)
            {
                var list = pair.Value;
                list.Sort((a, b) => a.unlock_tier.CompareTo(b.unlock_tier));
                for (int i = 1; i < list.Count; i++)
                {
                    if (list[i].cost_eu <= list[i - 1].cost_eu)
                    {
                        report.Error("C3", "star_node", list[i].node_id, "cost_eu",
                            $"同系「{pair.Key}」消耗必须严格递增：{list[i - 1].node_id}={list[i - 1].cost_eu} → {list[i].node_id}={list[i].cost_eu}");
                    }
                }
            }
            return $"检查 {bySeries.Count} 个系";
        }

        /// <summary>C4：络脉同分支单调递增（分支 &lt; 叶；破脉·省 &lt; 破脉·连）。</summary>
        private static string CheckMeridianCostMonotonic(TableSet set, ValidationReport report)
        {
            var nodes = set.Get("meridian");
            if (nodes == null) return Missing(set, report, "meridian");

            var byId = new Dictionary<string, MeridianRow>(StringComparer.Ordinal);
            foreach (var bound in nodes.Rows)
            {
                var row = bound.As<MeridianRow>();
                if (row != null) byId[row.node_id] = row;
            }

            foreach (var row in byId.Values)
            {
                if (row.prereq_ids == null) continue;
                foreach (var prereqId in row.prereq_ids)
                {
                    if (!byId.TryGetValue(prereqId, out var prereq)) continue;
                    if (row.cost_eu <= prereq.cost_eu)
                    {
                        report.Error("C4", "meridian", row.node_id, "cost_eu",
                            $"同分支消耗必须严格递增：{prereqId}={prereq.cost_eu} → {row.node_id}={row.cost_eu}");
                    }
                }
            }
            return $"检查 {byId.Count} 个络脉节点";
        }

        /// <summary>C5：络脉拓扑序（枢纽 &gt; 叶 &gt; 分支）。</summary>
        private static string CheckMeridianTopologyOrder(TableSet set, ValidationReport report)
        {
            var nodes = set.Get("meridian");
            if (nodes == null) return Missing(set, report, "meridian");

            foreach (var bound in nodes.Rows)
            {
                var row = bound.As<MeridianRow>();
                if (row?.prereq_ids == null) continue;

                bool isHub = row.prereq_ids.Length >= 3;
                foreach (var prereqId in row.prereq_ids)
                {
                    var prereqBound = nodes.Find(prereqId);
                    var prereq = prereqBound?.As<MeridianRow>();
                    if (prereq == null) continue;

                    bool prereqIsHub = prereq.prereq_ids != null && prereq.prereq_ids.Length >= 3;
                    if (prereqIsHub)
                    {
                        report.Error("C5", "meridian", row.node_id, "prereq_ids",
                            $"枢纽 {prereqId} 不得作为其它节点的前置（拓扑序：分支 → 叶 → 枢纽）");
                    }
                    if (isHub && prereq.prereq_ids != null && prereq.prereq_ids.Length > 0)
                    {
                        report.Error("C5", "meridian", row.node_id, "prereq_ids",
                            $"枢纽的前置只能是分支（{prereqId} 有自己的前置）");
                    }
                }
            }
            return "检查络脉拓扑序";
        }

        /// <summary>C6：按推导式重算 20 行消耗并逐行比对。</summary>
        private static string CheckCostRecompute(TableSet set, ValidationReport report)
        {
            var nodes = set.Get("star_node");
            if (nodes == null) return Missing(set, report, "star_node");

            // 星盘：cost_eu = 基础单价 2 × 层级系数(N1/N2/N3 = 1/3/9) × 类型系数(离散/帧 = 1，次数/机制 = 2)
            var bySeries = new Dictionary<string, List<StarNodeRow>>(StringComparer.Ordinal);
            foreach (var bound in nodes.Rows)
            {
                var row = bound.As<StarNodeRow>();
                if (row == null) continue;
                if (!bySeries.TryGetValue(row.series, out var list))
                {
                    list = new List<StarNodeRow>();
                    bySeries[row.series] = list;
                }
                list.Add(row);
            }

            int checkedRows = 0;
            foreach (var pair in bySeries)
            {
                var list = pair.Value;
                list.Sort((a, b) => a.unlock_tier.CompareTo(b.unlock_tier));
                for (int i = 0; i < list.Count; i++)
                {
                    var row = list[i];
                    int levelFactor = LevelFactor(i + 1);
                    int typeFactor = StarTypeFactor(row);
                    int expected = 2 * levelFactor * typeFactor;
                    checkedRows++;

                    if (row.cost_eu != expected)
                    {
                        report.Error("C6", "star_node", row.node_id, "cost_eu",
                            $"按推导式重算 = 2 × {levelFactor} × {typeFactor} = {expected}，表中值 {row.cost_eu}（不等即 error）");
                    }
                }
            }

            // 络脉：cost_eu = 基础单价 3 × 拓扑角色系数(分支 1 / 叶 3 / 枢纽 9) × 叶序系数
            var meridians = set.Get("meridian");
            if (meridians == null) return Missing(set, report, "meridian");

            var leafOrder = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var bound in meridians.Rows)
            {
                var row = bound.As<MeridianRow>();
                if (row?.prereq_ids == null) continue;
                foreach (var prereqId in row.prereq_ids)
                {
                    int order = leafOrder.TryGetValue(prereqId, out int existing) ? existing + 1 : 1;
                    leafOrder[prereqId] = order;
                }
            }

            foreach (var bound in meridians.Rows)
            {
                var row = bound.As<MeridianRow>();
                if (row == null) continue;
                checkedRows++;

                int roleFactor = row.prereq_ids != null && row.prereq_ids.Length >= 3 ? 9
                    : (row.prereq_ids != null && row.prereq_ids.Length == 1 ? 3 : 1);
                int leafFactor = 1;
                foreach (var prereqId in row.prereq_ids ?? new string[0])
                {
                    if (leafOrder.TryGetValue(prereqId, out int order) && order > leafFactor) leafFactor = order;
                }
                int expected = 3 * roleFactor * leafFactor;

                if (row.cost_eu != expected)
                {
                    report.Error("C6", "meridian", row.node_id, "cost_eu",
                        $"按推导式重算 = 3 × {roleFactor} × {leafFactor} = {expected}，表中值 {row.cost_eu}（不等即 error）");
                }
            }
            return $"复算 {checkedRows} 行";
        }

        private static int LevelFactor(int position)
        {
            if (position <= 1) return 1;
            if (position == 2) return 3;
            return 9;
        }

        private static int StarTypeFactor(StarNodeRow row)
        {
            if (!string.IsNullOrEmpty(row.mechanic_mod)) return 2;   // 机制类
            // 次数类与机制类系数 2；离散 / 帧类系数 1。
            // 次数类由效果键承载（eff_channel_extra_settle / eff_channel_stack_per_settle）。
            if (row.effect_key != null &&
                (row.effect_key.IndexOf("extra_settle", StringComparison.Ordinal) >= 0 ||
                 row.effect_key.IndexOf("stack_per_settle", StringComparison.Ordinal) >= 0))
            {
                return 2;
            }
            return 1;
        }

        /// <summary>C7：星盘 / 络脉的 cost_eu 不进战力公式。</summary>
        private static string CheckCostNotInPowerFormula(TableSet set, ValidationReport report)
        {
            var progression = set.Get("progression");
            if (progression == null) return Missing(set, report, "progression");

            foreach (var bound in progression.Rows)
            {
                var row = bound.As<ProgressionRow>();
                if (row == null) continue;
                if (!string.Equals(row.kind, "cultivation", StringComparison.Ordinal)) continue;

                // 修为线行的战力贡献只由 weight × raw_value 决定；cost_eu 不参与。
                int weightPermille = (int)Math.Round(row.weight * 1000d, MidpointRounding.AwayFromZero);
                int recomputed = (int)Math.Round((double)weightPermille * row.raw_value / 1000d, MidpointRounding.AwayFromZero);
                if (row.expected_power != recomputed)
                {
                    report.Error("C7", "progression", bound.Key, "expected_power",
                        "战力贡献与 weight × raw_value 不符（疑似把 cost_eu 计入了战力）");
                }
            }
            return "检查战力公式未混入节点消耗";
        }

        // =====================================================================
        // 跨表：孤儿外键
        // =====================================================================

        private static string CheckForeignKeys(TableSet set, ValidationReport report)
        {
            int orphans = 0;
            foreach (var table in set.All)
            {
                foreach (var column in table.Schema.Columns)
                {
                    if (string.IsNullOrEmpty(column.ForeignTable)) continue;

                    var target = set.Get(column.ForeignTable);
                    if (target == null)
                    {
                        report.Error("FK", table.Name, null, column.Name, "外键目标表未加载：" + column.ForeignTable);
                        continue;
                    }

                    foreach (var bound in table.Rows)
                    {
                        var field = table.Schema.RowType.GetField(column.Name);
                        if (field == null) continue;
                        var value = field.GetValue(bound.Row);

                        if (value is string text)
                        {
                            if (string.IsNullOrEmpty(text)) continue;
                            if (!target.Has(text))
                            {
                                orphans++;
                                report.Error("FK", table.Name, bound.Key, column.Name,
                                    $"孤儿外键：{text} 不在 {column.ForeignTable}.{column.ForeignColumn} 里");
                            }
                        }
                        else if (value is string[] texts)
                        {
                            foreach (var item in texts)
                            {
                                if (string.IsNullOrEmpty(item)) continue;
                                if (!target.Has(item))
                                {
                                    orphans++;
                                    report.Error("FK", table.Name, bound.Key, column.Name,
                                        $"孤儿外键：{item} 不在 {column.ForeignTable}.{column.ForeignColumn} 里");
                                }
                            }
                        }
                    }
                }
            }
            return $"孤儿外键 {orphans} 个";
        }

        // =====================================================================
        // 工具
        // =====================================================================

        private static string Missing(TableSet set, ValidationReport report, string tableName)
        {
            report.Error("MISSING", tableName, null, null, "缺源数据：" + tableName + " 表未加载，该断言无法执行");
            return "缺源数据（" + tableName + "）";
        }

        private static string SeriesOf(TableData nodes, string nodeId)
        {
            if (nodes == null) return null;
            var bound = nodes.Find(nodeId);
            return bound?.As<StarNodeRow>()?.series;
        }

        private static int ActiveFrames(SkillRow skill)
        {
            if (skill.phases != null) return skill.phases.active;
            if (skill.combo_segments != null && skill.combo_segments.Length > 0)
            {
                int max = 0;
                foreach (var segment in skill.combo_segments)
                {
                    if (segment.active > max) max = segment.active;
                }
                return max;
            }
            return 0;
        }

        private static int TotalFramesOfTag(TableData skills, string tag)
        {
            int total = 0;
            foreach (var bound in skills.Rows)
            {
                var skill = bound.As<SkillRow>();
                if (skill?.entry_tags == null) continue;
                bool matched = false;
                foreach (var entryTag in skill.entry_tags)
                {
                    if (string.Equals(entryTag, tag, StringComparison.Ordinal)) { matched = true; break; }
                }
                if (!matched) continue;
                if (skill.total_frames > total) total = skill.total_frames;
            }
            return total;
        }

        private static float TuningValue(TableData tuning, string key)
        {
            var bound = tuning.Find(key);
            var row = bound?.As<GlobalTuningRow>();
            return row?.value ?? 0f;
        }

        private static int KahnRemaining(TableData nodes)
        {
            var inDegree = new Dictionary<string, int>(StringComparer.Ordinal);
            var adjacency = new Dictionary<string, List<string>>(StringComparer.Ordinal);

            foreach (var bound in nodes.Rows)
            {
                var row = bound.As<StarNodeRow>();
                if (row == null) continue;
                inDegree[row.node_id] = 0;
                if (!adjacency.ContainsKey(row.node_id)) adjacency[row.node_id] = new List<string>();
            }

            foreach (var bound in nodes.Rows)
            {
                var row = bound.As<StarNodeRow>();
                if (row?.prereq_ids == null) continue;
                foreach (var prereq in row.prereq_ids)
                {
                    if (!adjacency.ContainsKey(prereq)) continue;
                    adjacency[prereq].Add(row.node_id);
                    if (!inDegree.ContainsKey(row.node_id)) inDegree[row.node_id] = 0;
                    inDegree[row.node_id] = inDegree[row.node_id] + 1;
                }
            }

            var queue = new Queue<string>();
            foreach (var pair in inDegree)
            {
                if (pair.Value == 0) queue.Enqueue(pair.Key);
            }

            int visited = 0;
            while (queue.Count > 0)
            {
                string current = queue.Dequeue();
                visited++;
                foreach (var next in adjacency[current])
                {
                    inDegree[next] = inDegree[next] - 1;
                    if (inDegree[next] == 0) queue.Enqueue(next);
                }
            }
            return nodes.Rows.Count - visited;
        }

        private static string FindCycleNode(TableData nodes)
        {
            foreach (var bound in nodes.Rows)
            {
                var row = bound.As<StarNodeRow>();
                if (row?.prereq_ids == null) continue;
                foreach (var prereq in row.prereq_ids)
                {
                    if (!nodes.Has(prereq)) return bound.Key;
                }
            }
            return null;
        }

        private static int KahnRemainingSocket(TableData items)
        {
            var inDegree = new Dictionary<string, int>(StringComparer.Ordinal);
            var adjacency = new Dictionary<string, List<string>>(StringComparer.Ordinal);

            foreach (var bound in items.Rows)
            {
                var row = bound.As<SocketItemRow>();
                if (row == null) continue;
                inDegree[row.socket_id] = 0;
                if (!adjacency.ContainsKey(row.socket_id)) adjacency[row.socket_id] = new List<string>();
            }

            foreach (var bound in items.Rows)
            {
                var row = bound.As<SocketItemRow>();
                if (row?.compose_from == null) continue;
                foreach (var material in row.compose_from)
                {
                    if (!adjacency.ContainsKey(material)) continue;
                    adjacency[material].Add(row.socket_id);
                    inDegree[row.socket_id] = inDegree[row.socket_id] + 1;
                }
            }

            var queue = new Queue<string>();
            foreach (var pair in inDegree)
            {
                if (pair.Value == 0) queue.Enqueue(pair.Key);
            }

            int visited = 0;
            while (queue.Count > 0)
            {
                string current = queue.Dequeue();
                visited++;
                foreach (var next in adjacency[current])
                {
                    inDegree[next] = inDegree[next] - 1;
                    if (inDegree[next] == 0) queue.Enqueue(next);
                }
            }
            return items.Rows.Count - visited;
        }

        private sealed class EffectSources
        {
            public readonly List<KeyValuePair<string, List<float>>> Entries = new List<KeyValuePair<string, List<float>>>();
        }

        /// <summary>收集 5 个来源（词条 / 络脉 / 星盘 / 技传 / 手札）提供的同名效果值。</summary>
        private static EffectSources CollectEffectSources(TableSet set)
        {
            var sources = new EffectSources();
            var map = new Dictionary<string, List<float>>(StringComparer.Ordinal);

            void Add(string key, float value)
            {
                if (string.IsNullOrEmpty(key)) return;
                if (!map.TryGetValue(key, out var list))
                {
                    list = new List<float>();
                    map[key] = list;
                }
                list.Add(value);
            }

            var affixes = set.Get("affix_pool");
            if (affixes != null)
            {
                foreach (var bound in affixes.Rows)
                {
                    var row = bound.As<AffixPoolRow>();
                    if (row != null) Add(row.effect_key, row.effect_value);
                }
            }

            var specialties = set.Get("specialty");
            if (specialties != null)
            {
                foreach (var bound in specialties.Rows)
                {
                    var row = bound.As<SpecialtyRow>();
                    if (row != null) Add(row.effect_key, row.effect_value);
                }
            }

            var starNodes = set.Get("star_node");
            if (starNodes != null)
            {
                foreach (var bound in starNodes.Rows)
                {
                    var row = bound.As<StarNodeRow>();
                    if (row != null) Add(row.effect_key, row.effect_value);
                }
            }

            var tiers = set.Get("tech_tier");
            if (tiers != null)
            {
                foreach (var bound in tiers.Rows)
                {
                    var row = bound.As<TechTierRow>();
                    if (row != null) Add(row.stat_key, row.stat_value);
                }
            }

            foreach (var pair in map) sources.Entries.Add(new KeyValuePair<string, List<float>>(pair.Key, pair.Value));
            return sources;
        }
    }
}
