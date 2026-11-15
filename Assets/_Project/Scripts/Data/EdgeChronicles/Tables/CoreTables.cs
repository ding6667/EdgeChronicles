using System;

namespace EdgeChronicles.Data.Tables
{
    // ==========================================================================
    // 既有 13 张表（IMPL-v2.0 §5 契约）+ EL-03 §1.8/§1.9 的增量字段。
    // 字段名一律 snake_case（EL-03 §1 导言：同一字段出现两种写法就是两个真值源）。
    // 所有字段为 public 字段：落表 JSON 的键名与字段名逐字相同，不存在转换层。
    // ==========================================================================

    /// <summary>全局调参（key / value / min / max）。</summary>
    public sealed class GlobalTuningRow
    {
        /// <summary>调参键（如 <c>vfx_graphic_budget</c>）。</summary>
        public string key;

        /// <summary>取值。</summary>
        public float value;

        /// <summary>允许下界。</summary>
        public float min;

        /// <summary>允许上界。</summary>
        public float max;

        /// <summary>说明（工程口径备注）。</summary>
        public string note;
    }

    /// <summary>技能相（定长三段）。</summary>
    public sealed class SkillPhases
    {
        /// <summary>前摇帧数（C 轴帧，≥1）。</summary>
        public int startup;

        /// <summary>判定帧数（C 轴帧，≥1）。</summary>
        public int active;

        /// <summary>后摇帧数（C 轴帧，≥1）。</summary>
        public int recovery;

        /// <summary>三段合计帧数（派生）。</summary>
        public int total => startup + active + recovery;
    }

    /// <summary>资源收支。</summary>
    public sealed class ResourceDelta
    {
        /// <summary>释放时（≤0）。</summary>
        public int on_cast;

        /// <summary>命中时。</summary>
        public int on_hit;

        /// <summary>击杀时。</summary>
        public int on_kill;
    }

    /// <summary>核心联动（与 core_link 表对应）。</summary>
    public sealed class CoreLinkSpec
    {
        /// <summary>需要的前置核心状态 id（引用 <c>core_link.state_id</c>）。</summary>
        public string requires_core_state;

        /// <summary>释放后写入的核心状态 id（引用 <c>core_link.state_id</c>）。</summary>
        public string writes_core_state;

        /// <summary>消耗的锋芒层数。</summary>
        public int consume_stacks;
    }

    /// <summary>多段技能的一段（多段技能用；与 <see cref="SkillRow.phases"/> 互斥）。</summary>
    public sealed class ComboSegment
    {
        /// <summary>前摇帧数（C 轴帧）。</summary>
        public int startup;

        /// <summary>判定帧数（C 轴帧）。</summary>
        public int active;

        /// <summary>后摇帧数（C 轴帧）。</summary>
        public int recovery;

        /// <summary>本段判定形状（引用 <c>hit_shape.shape_id</c>）。</summary>
        public string hit_shape_ref;

        /// <summary>本段刀光变体。</summary>
        public string vfx_variant;
    }

    /// <summary>技能行。</summary>
    public sealed class SkillRow
    {
        /// <summary>技能 id（<c>sk_*</c>）。</summary>
        public string skill_id;

        /// <summary>入口标签（<c>light</c> / <c>heavy</c> / <c>dash</c> / <c>control</c> / <c>guard</c> / <c>channel</c> / <c>finisher</c> / <c>core</c>）。</summary>
        public string[] entry_tags;

        /// <summary>动画剪辑资源键。</summary>
        public string anim_clip;

        /// <summary>定长三段（单段技能用；多段技能为 null，改用 <see cref="combo_segments"/>）。</summary>
        public SkillPhases phases;

        /// <summary>本技能的判定形状 id 列表（引用 <c>hit_shape.shape_id</c>）。</summary>
        public string[] hit_shape_ids;

        /// <summary>资源收支。</summary>
        public ResourceDelta resource_delta;

        /// <summary>核心联动。</summary>
        public CoreLinkSpec core_link;

        /// <summary>位移曲线 id（引用 <c>movement_curve.curve_id</c>）。</summary>
        public string movement_curve;

        /// <summary>特效 id。</summary>
        public string vfx_id;

        /// <summary>音效 id。</summary>
        public string audio_id;

        /// <summary>冷却帧数（C 轴帧）。</summary>
        public int cooldown_frames;

        /// <summary>多段技能的分段（单段技能为空数组）。</summary>
        public ComboSegment[] combo_segments;

        /// <summary>派生 id 清单（引用技传派生条目；与 tech_skill 互为完整性校验）。</summary>
        public string[] derivative_ids;

        /// <summary>本技能已修的技传阶位（派生，禁手填）。</summary>
        public int[] tech_tiers;

        /// <summary>总帧数（派生，禁手填）。</summary>
        public int total_frames;

        /// <summary>段数（派生，禁手填；1–4）。</summary>
        public int segment_count;
    }

    /// <summary>取消窗口行（主键 = source_tag + allow_tag + from_frame）。</summary>
    public sealed class CancelWindowRow
    {
        /// <summary>来源标签（发起方技能的 entry_tag）。</summary>
        public string source_tag;

        /// <summary>允许接入的标签。</summary>
        public string allow_tag;

        /// <summary>起始帧（C 轴帧，含）。</summary>
        public int from_frame;

        /// <summary>结束帧（C 轴帧，不含）。</summary>
        public int to_frame;

        /// <summary>接入消耗的资源量。</summary>
        public int resource_cost;

        /// <summary>窗口长度（派生，禁手填）。</summary>
        public int window_length;
    }

    /// <summary>命中形状行。</summary>
    public sealed class HitShapeRow
    {
        /// <summary>形状 id（全局唯一）。</summary>
        public string shape_id;

        /// <summary>所属技能（引用 <c>skill.skill_id</c>）。</summary>
        public string skill_id;

        /// <summary>形状类型（<c>capsule</c> / <c>sector</c> / <c>rect</c>）。</summary>
        public string shape;

        /// <summary>半径（米）。</summary>
        public float radius;

        /// <summary>扇形张角（度；非扇形为 0）。</summary>
        public float angle;

        /// <summary>高度偏移（米）。</summary>
        public float offset_y;

        /// <summary>命中帧（相对本技能起手，C 轴帧；必须 &lt; 判定帧数）。</summary>
        public int frame;

        /// <summary>伤害值。</summary>
        public int damage;

        /// <summary>击退档位（<c>light</c> / <c>medium</c> / <c>heavy</c>）。</summary>
        public string knockback;

        /// <summary>顿帧期时间缩放系数（无量纲；0.15 = 15% 速度；合法区间 [0.05, 0.25]）。</summary>
        public float hitstop;

        /// <summary>多段命中次数（0 = 单次）。</summary>
        public int multi_hit;

        /// <summary>弧长（米，派生，禁手填）。</summary>
        public float arc_length;

        /// <summary>期望 DPS（派生，禁手填）。</summary>
        public float expected_dps;
    }

    /// <summary>位移曲线上的一个关键点。</summary>
    public sealed class CurveKey
    {
        /// <summary>关键点所在帧（C 轴帧）。</summary>
        public int frame;

        /// <summary>该点的归一化速度（0–1）。</summary>
        public float value;
    }

    /// <summary>位移曲线行。</summary>
    public sealed class MovementCurveRow
    {
        /// <summary>曲线 id（<c>mc_*</c>）。</summary>
        public string curve_id;

        /// <summary>关键点序列（按 frame 升序）。</summary>
        public CurveKey[] keys;

        /// <summary>声明的位移距离（米）。</summary>
        public float distance;

        /// <summary>夹取模式（<c>none</c> / <c>clamp</c> / <c>loop</c>）。</summary>
        public string clamp_mode;

        /// <summary>曲线积分距离（米，派生，禁手填）。</summary>
        public float integral_distance;
    }

    /// <summary>资源行。</summary>
    public sealed class ResourceRow
    {
        /// <summary>资源 id（<c>res_*</c>）。</summary>
        public string id;

        /// <summary>上限（层）。</summary>
        public int max;

        /// <summary>每逻辑帧回复量（层 / 帧）。</summary>
        public float regen_per_tick;

        /// <summary>每逻辑帧衰减量（层 / 帧；负值表示衰减）。</summary>
        public float decay_per_tick;

        /// <summary>衰减延迟（帧）：多久没获得新层后开始衰减。</summary>
        public int decay_delay_frames;

        /// <summary>衰减间隔（帧）：每隔多少帧掉 1 层。</summary>
        public int decay_interval_frames;
    }

    /// <summary>核心状态行。</summary>
    public sealed class CoreLinkRow
    {
        /// <summary>状态 id（全局唯一）。</summary>
        public string state_id;

        /// <summary>关联技能（引用 <c>skill.skill_id</c>）。</summary>
        public string skill_id;

        /// <summary>buff id。</summary>
        public string buff_id;

        /// <summary>持续帧数（C 轴帧）。</summary>
        public int duration_frames;

        /// <summary>生效条件标签（空数组 = 无条件）。</summary>
        public string[] conditions;
    }

    /// <summary>敌人行。</summary>
    public sealed class EnemyRow
    {
        /// <summary>敌人 id。</summary>
        public string enemy_id;

        /// <summary>生命值。</summary>
        public int hp;

        /// <summary>韧性值。</summary>
        public int poise;

        /// <summary>导航图 id。</summary>
        public string ai_graph;

        /// <summary>掉落表 id。</summary>
        public string drop_table;

        /// <summary>护盾层数（0 = 无护盾）。</summary>
        public int shield_layers;

        /// <summary>护盾层数上限。</summary>
        public int shield_max;
    }

    /// <summary>Boss 行。</summary>
    public sealed class BossRow
    {
        /// <summary>Boss id。</summary>
        public string boss_id;

        /// <summary>生命值。</summary>
        public int hp;

        /// <summary>阶段数。</summary>
        public int phase_count;

        /// <summary>竞技场机关 id（引用 <c>anchor.anchor_id</c>）。</summary>
        public string arena_anchor;

        /// <summary>规则集引用。</summary>
        public string brain_ref;

        /// <summary>总血量（派生，禁手填）。</summary>
        public int total_hp;
    }

    /// <summary>Boss 规则中的一个条件。</summary>
    public sealed class BossConditionSpec
    {
        /// <summary>条件类型。</summary>
        public string type;

        /// <summary>参数（键值对）。</summary>
        public StringArg[] args;
    }

    /// <summary>Boss 规则中的一个效果。</summary>
    public sealed class BossEffectSpec
    {
        /// <summary>效果类型。</summary>
        public string type;

        /// <summary>参数（键值对）。</summary>
        public StringArg[] args;
    }

    /// <summary>字符串键值参数。</summary>
    public sealed class StringArg
    {
        /// <summary>参数名。</summary>
        public string key;

        /// <summary>参数值。</summary>
        public string value;
    }

    /// <summary>Boss 规则行。</summary>
    public sealed class BossRuleRow
    {
        /// <summary>规则 id。</summary>
        public string rule_id;

        /// <summary>Boss id（引用 <c>boss.boss_id</c>）。</summary>
        public string boss_id;

        /// <summary>生效阶段（1 起）。</summary>
        public int phase;

        /// <summary>触发源。</summary>
        public string trigger;

        /// <summary>条件列表。</summary>
        public BossConditionSpec[] conditions;

        /// <summary>效果列表。</summary>
        public BossEffectSpec[] effects;

        /// <summary>规则指纹（派生，禁手填）。</summary>
        public string rule_hash;
    }

    /// <summary>机关行。</summary>
    public sealed class AnchorRow
    {
        /// <summary>机关 id。</summary>
        public string anchor_id;

        /// <summary>标签（用于按标签查询）。</summary>
        public string tag;

        /// <summary>初始状态。</summary>
        public string init_state;

        /// <summary>是否可交互。</summary>
        public bool interactable;
    }

    /// <summary>副本行。</summary>
    public sealed class DungeonRow
    {
        /// <summary>副本 id。</summary>
        public string dungeon_id;

        /// <summary>波次（小怪波数）。</summary>
        public int[] waves;

        /// <summary>机关 id 列表（引用 <c>anchor.anchor_id</c>）。</summary>
        public string[] anchors;

        /// <summary>Boss id（引用 <c>boss.boss_id</c>）。</summary>
        public string boss_id;

        /// <summary>战力门槛。</summary>
        public int power_threshold;

        /// <summary>小怪总数（派生，禁手填）。</summary>
        public int enemy_count;
    }

    /// <summary>修为线的一级。</summary>
    public sealed class ProgressionTier
    {
        /// <summary>等级（0 起）。</summary>
        public int level;

        /// <summary>升到本级所需 EU。</summary>
        public int cost_eu;

        /// <summary>本级累计原值。</summary>
        public int raw_value;

        /// <summary>效果键（<c>eff_*</c>）。</summary>
        public string effect_key;

        /// <summary>效果系数。</summary>
        public float effect_value;
    }

    /// <summary>养成行（6 修为线 + 名望 5 档 + 重置 3 条同表承载）。</summary>
    public sealed class ProgressionRow
    {
        /// <summary>行 id（<c>line_*</c> / <c>fame_*</c> / <c>reset_*</c>）。</summary>
        public string line_id;

        /// <summary>行种。</summary>
        public string kind;

        /// <summary>战力权重（GDD §4.2；修为线行有效）。</summary>
        public float weight;

        /// <summary>原值（修为线行的当前基准原值）。</summary>
        public int raw_value;

        /// <summary>等级阶梯（修为线行）。</summary>
        public ProgressionTier[] tiers;

        /// <summary>词条 id 列表（修为线行）。</summary>
        public string[] affixes;

        /// <summary>被重置的系统（kind = reset 时必填）。</summary>
        public string target_system;

        /// <summary>重置策略（kind = reset 时必填；枚举只有 free）。</summary>
        public string reset_policy;

        /// <summary>重置消耗（kind = reset 时必填；当前全为 0 = 免费）。</summary>
        public int reset_cost;

        /// <summary>名望档位编号 1–5（kind = fame 时必填）。</summary>
        public int fame_unlock_tier;

        /// <summary>该档对应的名望线 Lv 下界（派生，禁手填）。</summary>
        public int fame_lv_min;

        /// <summary>该档对应的名望线 Lv 上界（派生，禁手填）。</summary>
        public int fame_lv_max;

        /// <summary>名望来源（kind = fame 时必填）。</summary>
        public string fame_source;

        /// <summary>本行对总战力的贡献（派生，禁手填；整数 EU）。</summary>
        public int expected_power;

        /// <summary>该行对应的养成材料（修为线行 = 该线专属材料；名望行 / 重置行 = 空串）。</summary>
        public string material_key;

        /// <summary>0 级 → 1 级的单级升级价（派生，禁手填；修为线行 = tiers[0].cost_eu，其余行 = 0）。</summary>
        public int cost_eu;
    }
}
