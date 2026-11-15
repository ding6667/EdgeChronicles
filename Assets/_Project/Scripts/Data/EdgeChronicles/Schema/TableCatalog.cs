using System.Collections.Generic;

namespace EdgeChronicles.Data.Schema
{
    /// <summary>
    /// 全部表的规格目录（20 张规格表 + 2 张被断言引用的扩展表）。
    /// <para>
    /// 规格来源：既有 13 表 = IMPL-v2.0.md §5；养成 7 表 = IMPL-v3.0.md §7 + EL-03 §1；
    /// 扩展表 = EL-03 §1.8（<c>affix_pool</c>）与 IMPL-v3.0.md §4（<c>equipment_base</c>）——
    /// 这两张表被 A35 / A37 / A38 引用，但不在「13 + 7 = 20」的清单里，故标 <c>Auxiliary</c>，
    /// **不计入 CI 判据里的 <c>tables=20</c>**。
    /// </para>
    /// </summary>
    public static class TableCatalog
    {
        /// <summary>规格表数量（13 + 7）。</summary>
        public const int SpecTableCount = 20;

        /// <summary>扩展表数量（affix_pool / equipment_base）。</summary>
        public const int AuxiliaryTableCount = 2;

        /// <summary>全部表规格（规格表在前，扩展表在后）。</summary>
        public static List<TableSchema> All()
        {
            var list = new List<TableSchema>(SpecTableCount + AuxiliaryTableCount);

            // ---------- 既有 13 表 ----------
            list.Add(new TableSchema("global_tuning", typeof(GlobalTuningRow), false, new[] { "key" },
                new ColumnSpec("key", ColumnType.String, true),
                new ColumnSpec("value", ColumnType.Float, true),
                new ColumnSpec("min", ColumnType.Float),
                new ColumnSpec("max", ColumnType.Float),
                new ColumnSpec("note", ColumnType.String)));

            list.Add(new TableSchema("skill", typeof(SkillRow), false, new[] { "skill_id" },
                new ColumnSpec("skill_id", ColumnType.String, true),
                new ColumnSpec("entry_tags", ColumnType.StringArray, true),
                new ColumnSpec("anim_clip", ColumnType.String, true),
                new ColumnSpec("phases", ColumnType.Object),
                new ColumnSpec("hit_shape_ids", ColumnType.StringArray, true),
                new ColumnSpec("resource_delta", ColumnType.Object),
                new ColumnSpec("core_link", ColumnType.Object),
                new ColumnSpec("movement_curve", ColumnType.String, true, foreignTable: "movement_curve", foreignColumn: "curve_id"),
                new ColumnSpec("vfx_id", ColumnType.String, true),
                new ColumnSpec("audio_id", ColumnType.String, true),
                new ColumnSpec("cooldown_frames", ColumnType.Int, false, min: 0),
                new ColumnSpec("combo_segments", ColumnType.ObjectArray),
                new ColumnSpec("derivative_ids", ColumnType.StringArray),
                new ColumnSpec("tech_tiers", ColumnType.IntArray, false, derived: true, note: "禁手填：由 tech_tier 重算"),
                new ColumnSpec("total_frames", ColumnType.Int, false, derived: true, note: "禁手填：相帧合计"),
                new ColumnSpec("segment_count", ColumnType.Int, false, min: 1, max: 4, derived: true, note: "禁手填：段数")));

            list.Add(new TableSchema("cancel_window", typeof(CancelWindowRow), false, new[] { "source_tag", "allow_tag", "from_frame" },
                new ColumnSpec("source_tag", ColumnType.String, true),
                new ColumnSpec("allow_tag", ColumnType.String, true),
                new ColumnSpec("from_frame", ColumnType.Int, true, min: 0),
                new ColumnSpec("to_frame", ColumnType.Int, true, min: 0),
                new ColumnSpec("resource_cost", ColumnType.Int, false, min: 0),
                new ColumnSpec("window_length", ColumnType.Int, false, derived: true, note: "禁手填：to_frame − from_frame")));

            list.Add(new TableSchema("hit_shape", typeof(HitShapeRow), false, new[] { "shape_id" },
                new ColumnSpec("shape_id", ColumnType.String, true),
                new ColumnSpec("skill_id", ColumnType.String, true, foreignTable: "skill", foreignColumn: "skill_id"),
                new ColumnSpec("shape", ColumnType.String, true, allowed: EnumCatalog.HitShapeKindValues),
                new ColumnSpec("radius", ColumnType.Float, true, min: 0),
                new ColumnSpec("angle", ColumnType.Float, true, min: 0, max: 360),
                new ColumnSpec("offset_y", ColumnType.Float, true),
                new ColumnSpec("frame", ColumnType.Int, true, min: 0),
                new ColumnSpec("damage", ColumnType.Int, true, min: 0),
                new ColumnSpec("knockback", ColumnType.String, true, allowed: EnumCatalog.KnockbackTierValues),
                new ColumnSpec("hitstop", ColumnType.Float, true, min: 0.05, max: 0.25, note: "顿帧期时间缩放系数（无量纲）"),
                new ColumnSpec("multi_hit", ColumnType.Int, false, min: 0),
                new ColumnSpec("arc_length", ColumnType.Float, false, derived: true),
                new ColumnSpec("expected_dps", ColumnType.Float, false, derived: true)));

            list.Add(new TableSchema("movement_curve", typeof(MovementCurveRow), false, new[] { "curve_id" },
                new ColumnSpec("curve_id", ColumnType.String, true),
                new ColumnSpec("keys", ColumnType.ObjectArray, true),
                new ColumnSpec("distance", ColumnType.Float, true, min: 0),
                new ColumnSpec("clamp_mode", ColumnType.String, true),
                new ColumnSpec("integral_distance", ColumnType.Float, false, derived: true)));

            list.Add(new TableSchema("resource", typeof(ResourceRow), false, new[] { "id" },
                new ColumnSpec("id", ColumnType.String, true),
                new ColumnSpec("max", ColumnType.Int, true, min: 1),
                new ColumnSpec("regen_per_tick", ColumnType.Float, true),
                new ColumnSpec("decay_per_tick", ColumnType.Float, true),
                new ColumnSpec("decay_delay_frames", ColumnType.Int, false, min: 0),
                new ColumnSpec("decay_interval_frames", ColumnType.Int, false, min: 1)));

            list.Add(new TableSchema("core_link", typeof(CoreLinkRow), false, new[] { "state_id" },
                new ColumnSpec("state_id", ColumnType.String, true),
                new ColumnSpec("skill_id", ColumnType.String, true, foreignTable: "skill", foreignColumn: "skill_id"),
                new ColumnSpec("buff_id", ColumnType.String, true),
                new ColumnSpec("duration_frames", ColumnType.Int, true, min: 1),
                new ColumnSpec("conditions", ColumnType.StringArray, false)));

            list.Add(new TableSchema("enemy", typeof(EnemyRow), false, new[] { "enemy_id" },
                new ColumnSpec("enemy_id", ColumnType.String, true),
                new ColumnSpec("hp", ColumnType.Int, true, min: 1),
                new ColumnSpec("poise", ColumnType.Int, true, min: 0),
                new ColumnSpec("ai_graph", ColumnType.String, true),
                new ColumnSpec("drop_table", ColumnType.String, true),
                new ColumnSpec("shield_layers", ColumnType.Int, false, min: 0, max: 3),
                new ColumnSpec("shield_max", ColumnType.Int, false, min: 1, max: 3)));

            list.Add(new TableSchema("boss", typeof(BossRow), false, new[] { "boss_id" },
                new ColumnSpec("boss_id", ColumnType.String, true),
                new ColumnSpec("hp", ColumnType.Int, true, min: 1),
                new ColumnSpec("phase_count", ColumnType.Int, true, min: 1),
                new ColumnSpec("arena_anchor", ColumnType.String, true, foreignTable: "anchor", foreignColumn: "anchor_id"),
                new ColumnSpec("brain_ref", ColumnType.String, true),
                new ColumnSpec("total_hp", ColumnType.Int, false, derived: true)));

            list.Add(new TableSchema("boss_rule", typeof(BossRuleRow), false, new[] { "rule_id" },
                new ColumnSpec("rule_id", ColumnType.String, true),
                new ColumnSpec("boss_id", ColumnType.String, true, foreignTable: "boss", foreignColumn: "boss_id"),
                new ColumnSpec("phase", ColumnType.Int, true, min: 1),
                new ColumnSpec("trigger", ColumnType.String, true, allowed: EnumCatalog.BossTriggerValues),
                new ColumnSpec("conditions", ColumnType.ObjectArray, false),
                new ColumnSpec("effects", ColumnType.ObjectArray, true),
                new ColumnSpec("rule_hash", ColumnType.String, false, derived: true)));

            list.Add(new TableSchema("anchor", typeof(AnchorRow), false, new[] { "anchor_id" },
                new ColumnSpec("anchor_id", ColumnType.String, true),
                new ColumnSpec("tag", ColumnType.String, true),
                new ColumnSpec("init_state", ColumnType.String, true, allowed: EnumCatalog.AnchorStateValues),
                new ColumnSpec("interactable", ColumnType.Bool, true)));

            list.Add(new TableSchema("dungeon", typeof(DungeonRow), false, new[] { "dungeon_id" },
                new ColumnSpec("dungeon_id", ColumnType.String, true),
                new ColumnSpec("waves", ColumnType.IntArray, true),
                new ColumnSpec("anchors", ColumnType.StringArray, true),
                new ColumnSpec("boss_id", ColumnType.String, true, foreignTable: "boss", foreignColumn: "boss_id"),
                new ColumnSpec("power_threshold", ColumnType.Int, true, min: 0),
                new ColumnSpec("enemy_count", ColumnType.Int, false, derived: true)));

            list.Add(new TableSchema("progression", typeof(ProgressionRow), false, new[] { "line_id" },
                new ColumnSpec("line_id", ColumnType.String, true),
                new ColumnSpec("kind", ColumnType.String, true, allowed: EnumCatalog.ProgressionKindValues),
                new ColumnSpec("weight", ColumnType.Float, true, min: 0),
                new ColumnSpec("raw_value", ColumnType.Int, true, min: 0),
                new ColumnSpec("tiers", ColumnType.ObjectArray),
                new ColumnSpec("affixes", ColumnType.StringArray),
                new ColumnSpec("material_key", ColumnType.String),
                new ColumnSpec("target_system", ColumnType.String, false, allowed: EnumCatalog.TargetSystemValues),
                new ColumnSpec("reset_policy", ColumnType.String, false, allowed: EnumCatalog.ResetPolicyValues),
                new ColumnSpec("reset_cost", ColumnType.Int, false, min: 0, max: 200),
                new ColumnSpec("fame_unlock_tier", ColumnType.Int, false, min: 1, max: 5),
                new ColumnSpec("fame_lv_min", ColumnType.Int, false, min: 1, max: 10, derived: true),
                new ColumnSpec("fame_lv_max", ColumnType.Int, false, min: 1, max: 10, derived: true),
                new ColumnSpec("fame_source", ColumnType.String, false, allowed: EnumCatalog.FameSourceValues),
                new ColumnSpec("expected_power", ColumnType.Int, false, derived: true, note: "禁手填：本行对总战力的贡献"),
                new ColumnSpec("cost_eu", ColumnType.Int, false, min: 0, max: 40, derived: true, note: "禁手填：0 级 → 1 级单级升级价")));

            // ---------- 养成 7 表 ----------
            list.Add(new TableSchema("star_node", typeof(StarNodeRow), false, new[] { "node_id" },
                new ColumnSpec("node_id", ColumnType.String, true),
                new ColumnSpec("series", ColumnType.String, true, allowed: EnumCatalog.StarSeriesValues),
                new ColumnSpec("pos_x", ColumnType.Int, true, min: -12, max: 12),
                new ColumnSpec("pos_y", ColumnType.Int, true, min: -12, max: 12),
                new ColumnSpec("prereq_ids", ColumnType.StringArray, true),
                new ColumnSpec("unlock_tier", ColumnType.Int, true, min: 1, max: 5),
                new ColumnSpec("cost_eu", ColumnType.Int, true, min: 1, max: 60),
                new ColumnSpec("effect_key", ColumnType.String),
                new ColumnSpec("effect_value", ColumnType.Float, false, min: 0.01, max: 1.00),
                new ColumnSpec("mechanic_mod", ColumnType.String),
                new ColumnSpec("mechanic_param", ColumnType.Int, false, min: 0, max: 100),
                new ColumnSpec("prereq_closure", ColumnType.StringArray, false, derived: true),
                new ColumnSpec("link_partners", ColumnType.StringArray, false, derived: true)));

            list.Add(new TableSchema("star_link", typeof(StarLinkRow), false, new[] { "link_id" },
                new ColumnSpec("link_id", ColumnType.String, true),
                new ColumnSpec("node_a", ColumnType.String, true, foreignTable: "star_node", foreignColumn: "node_id"),
                new ColumnSpec("node_b", ColumnType.String, true, foreignTable: "star_node", foreignColumn: "node_id"),
                new ColumnSpec("link_condition", ColumnType.String, true, allowed: EnumCatalog.StarLinkConditionValues),
                new ColumnSpec("trigger_node", ColumnType.String, true, foreignTable: "star_node", foreignColumn: "node_id"),
                new ColumnSpec("effect_key", ColumnType.String, true),
                new ColumnSpec("effect_value", ColumnType.Float, true, min: 0.01, max: 10.00),
                new ColumnSpec("active_state", ColumnType.String, false, allowed: EnumCatalog.StarLinkStateValues, derived: true),
                new ColumnSpec("series", ColumnType.String, false, allowed: EnumCatalog.StarSeriesValues, derived: true)));

            list.Add(new TableSchema("tech_tier", typeof(TechTierRow), false, new[] { "skill_id", "tier" },
                new ColumnSpec("skill_id", ColumnType.String, true, foreignTable: "skill", foreignColumn: "skill_id"),
                new ColumnSpec("tier", ColumnType.Int, true, min: 1, max: 5),
                new ColumnSpec("unlock_type", ColumnType.String, true, allowed: EnumCatalog.TechUnlockTypeValues),
                new ColumnSpec("visual_tier", ColumnType.String, true, allowed: EnumCatalog.VisualTierValues),
                new ColumnSpec("cost_eu", ColumnType.Int, true, min: 1, max: 80),
                new ColumnSpec("material_key", ColumnType.String, true),
                new ColumnSpec("unlock_derivative_id", ColumnType.String),
                new ColumnSpec("pending_rule_key", ColumnType.String, false, foreignTable: "core_link", foreignColumn: "state_id"),
                new ColumnSpec("mechanic_targets", ColumnType.StringArray, false),
                new ColumnSpec("stat_key", ColumnType.String),
                new ColumnSpec("stat_value", ColumnType.Float, false, min: 0.01, max: 1.00),
                new ColumnSpec("cum_cost_eu", ColumnType.Int, false, derived: true),
                new ColumnSpec("effective_mods", ColumnType.KeyFloatArray, false, derived: true)));

            list.Add(new TableSchema("specialty", typeof(SpecialtyRow), false, new[] { "spec_id" },
                new ColumnSpec("spec_id", ColumnType.String, true),
                new ColumnSpec("slot", ColumnType.String, true, allowed: EnumCatalog.SpecialtySlotValues),
                new ColumnSpec("exclude_group", ColumnType.String, true),
                new ColumnSpec("trigger", ColumnType.String, true, allowed: EnumCatalog.SpecialtyTriggerValues),
                new ColumnSpec("condition_tag", ColumnType.String),
                new ColumnSpec("effect_key", ColumnType.String, true),
                new ColumnSpec("effect_value", ColumnType.Float, true, min: 0.01, max: 1.00),
                new ColumnSpec("cooldown_frames", ColumnType.Int, true, min: 0, max: 600),
                new ColumnSpec("cost_eu", ColumnType.Int, true, min: 1, max: 40)));

            list.Add(new TableSchema("meridian", typeof(MeridianRow), false, new[] { "node_id" },
                new ColumnSpec("node_id", ColumnType.String, true),
                new ColumnSpec("pos_x", ColumnType.Int, true, min: -12, max: 12),
                new ColumnSpec("pos_y", ColumnType.Int, true, min: -12, max: 12),
                new ColumnSpec("prereq_ids", ColumnType.StringArray, true),
                new ColumnSpec("mechanic_mod", ColumnType.String),
                new ColumnSpec("mechanic_param", ColumnType.Int, false, min: 0, max: 100),
                new ColumnSpec("stat_key", ColumnType.String),
                new ColumnSpec("stat_value", ColumnType.Float),
                new ColumnSpec("cost_eu", ColumnType.Int, true, min: 1, max: 50),
                new ColumnSpec("depth", ColumnType.Int, false, min: 0, derived: true)));

            list.Add(new TableSchema("socket_item", typeof(SocketItemRow), false, new[] { "socket_id" },
                new ColumnSpec("socket_id", ColumnType.String, true),
                new ColumnSpec("item_type", ColumnType.String, true, allowed: EnumCatalog.SocketItemTypeValues),
                new ColumnSpec("slot_kind", ColumnType.String, true, allowed: EnumCatalog.SpecialtySlotValues),
                new ColumnSpec("level", ColumnType.Int, true, min: 1, max: 10),
                new ColumnSpec("compose_from", ColumnType.StringArray, true),
                new ColumnSpec("effect_key", ColumnType.String, true),
                new ColumnSpec("effect_value", ColumnType.Float, true, min: 0.01, max: 1.00),
                new ColumnSpec("compose_chain", ColumnType.StringArray, false, derived: true),
                new ColumnSpec("is_max_level", ColumnType.Bool, false, derived: true)));

            list.Add(new TableSchema("refine_stage", typeof(RefineStageRow), false, new[] { "stage_id" },
                new ColumnSpec("stage_id", ColumnType.String, true),
                new ColumnSpec("slot_index", ColumnType.String, true, allowed: EnumCatalog.RefineSlotValues),
                new ColumnSpec("keep_count", ColumnType.Int, true, min: 0, max: 2),
                new ColumnSpec("cost_eu", ColumnType.Int, true, min: 1, max: 60),
                new ColumnSpec("pity_threshold", ColumnType.Int, true, min: 5, max: 30),
                new ColumnSpec("pity_target", ColumnType.String, false, foreignTable: "affix_pool", foreignColumn: "affix_id"),
                new ColumnSpec("refund_tier_down", ColumnType.Bool, true),
                new ColumnSpec("refund_ratio", ColumnType.Float, true, min: 0.00, max: 1.00),
                new ColumnSpec("pity_counter", ColumnType.Int, false, min: 0, max: 29, derived: true)));

            // ---------- 扩展表（被断言引用，不计入 20 张规格表）----------
            list.Add(new TableSchema("affix_pool", typeof(AffixPoolRow), true, new[] { "affix_id" },
                new ColumnSpec("affix_id", ColumnType.String, true),
                new ColumnSpec("effect_key", ColumnType.String, true),
                new ColumnSpec("effect_value", ColumnType.Float, true, min: 0.01, max: 1.00),
                new ColumnSpec("weight", ColumnType.Float, true, min: 0),
                new ColumnSpec("skill_tag", ColumnType.String, false, foreignTable: "skill", foreignColumn: "skill_id"),
                new ColumnSpec("mechanic_mod", ColumnType.String),
                new ColumnSpec("mechanic_param", ColumnType.Int, false, min: 0, max: 100)));

            list.Add(new TableSchema("equipment_base", typeof(EquipmentBaseRow), true, new[] { "item_id" },
                new ColumnSpec("item_id", ColumnType.String, true),
                new ColumnSpec("slot", ColumnType.String, true),
                new ColumnSpec("grade", ColumnType.Int, true, min: 0, max: 4),
                new ColumnSpec("base_power", ColumnType.Int, true, min: 0),
                new ColumnSpec("affix_count", ColumnType.Int, true, min: 0, max: 3)));

            return list;
        }
    }
}
