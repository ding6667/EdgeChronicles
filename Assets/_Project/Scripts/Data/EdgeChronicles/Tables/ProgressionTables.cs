using System;

namespace EdgeChronicles.Data.Tables
{
    // ==========================================================================
    // 养成层 7 张新表（EL-03 §1）+ 2 张被断言引用的扩展表。
    // 字段名严格按 EL-03 §1 的 snake_case；派生列一律标 <c>derived</c>（禁手填）。
    // ==========================================================================

    /// <summary>星盘节点行（12 行 = 4 系 × 3 节点）。</summary>
    public sealed class StarNodeRow
    {
        /// <summary>节点 id（<c>sn_*</c>）。</summary>
        public string node_id;

        /// <summary>所属系。</summary>
        public string series;

        /// <summary>布局横坐标（锚点布局用，不是像素）。</summary>
        public int pos_x;

        /// <summary>布局纵坐标（锚点布局用，不是像素）。</summary>
        public int pos_y;

        /// <summary>前置节点 id 列表（0–2 项，去重、无自引用）。</summary>
        public string[] prereq_ids;

        /// <summary>解锁所需名望档位（1–5 = I–V）。</summary>
        public int unlock_tier;

        /// <summary>消耗 EU。</summary>
        public int cost_eu;

        /// <summary>效果键（离散 / 次数 / 帧类节点用；与 mechanic_mod 互斥）。</summary>
        public string effect_key;

        /// <summary>效果系数（0.01–1.00；与 mechanic_mod 互斥）。</summary>
        public float effect_value;

        /// <summary>
        /// 节点类型（离散 / 次数 / 帧 / 机制）——<b>手填</b>。
        /// <para>
        /// 它是 C6 复算消耗的必需输入（类型系数：离散 / 帧 = 1，次数 / 机制 = 2），
        /// 不能靠效果键字符串反推——那是把设计内容藏进命名里，换个键名就静默算错。
        /// </para>
        /// </summary>
        public string node_type;

        /// <summary>机制修改项（机制类节点用；与 effect_key 互斥；空串 = 无）。</summary>
        public string mechanic_mod;

        /// <summary>机制参数（0–100；单位随 mechanic_mod 定）。</summary>
        public int mechanic_param;

        /// <summary>前置传递闭包（派生，禁手填）。</summary>
        public string[] prereq_closure;

        /// <summary>同系连携伙伴节点 id（派生，禁手填；双向）。</summary>
        public string[] link_partners;
    }

    /// <summary>同系连携行（8 行）。</summary>
    public sealed class StarLinkRow
    {
        /// <summary>连携 id（<c>sl_*</c>）。</summary>
        public string link_id;

        /// <summary>链式靠前的节点 id。</summary>
        public string node_a;

        /// <summary>链式靠后的节点 id。</summary>
        public string node_b;

        /// <summary>成立条件（唯一合法值 <c>same_series_adjacent</c>）。</summary>
        public string link_condition;

        /// <summary>触发节点（= 靠后的那个）。</summary>
        public string trigger_node;

        /// <summary>效果键（<c>eff_link_*</c>）。</summary>
        public string effect_key;

        /// <summary>效果值（倍率或层数）。</summary>
        public float effect_value;

        /// <summary>生效态（派生，禁手填）。</summary>
        public string active_state;

        /// <summary>同系（派生，禁手填；取 node_a 与 node_b 的共同 series）。</summary>
        public string series;
    }

    /// <summary>技传阶位行（8 技能 × 5 阶 = 40 行）。表名 = <c>tech_tier</c>（EL-03 §1.3）。</summary>
    public sealed class TechTierRow
    {
        /// <summary>技能 id（引用 <c>skill.skill_id</c>）。</summary>
        public string skill_id;

        /// <summary>阶位（1–5，从 1 连续无缺口）。</summary>
        public int tier;

        /// <summary>解锁类型。</summary>
        public string unlock_type;

        /// <summary>视觉分级。</summary>
        public string visual_tier;

        /// <summary>消耗 EU（谱份数 1/2/3/4/6 对应 1–5 阶）。</summary>
        public int cost_eu;

        /// <summary>材料键（<c>mat_*</c>）。</summary>
        public string material_key;

        /// <summary>解锁的派生条目 id（<c>dv_*</c>；3 阶与 5 阶填）。</summary>
        public string unlock_derivative_id;

        /// <summary>待触发标记键（<c>pr_*</c>，引用 <c>core_link.state_id</c>；可空串）。</summary>
        public string pending_rule_key;

        /// <summary>改写的核心循环（<c>loop_l1</c>..<c>loop_l6</c>；unlock_type = mechanic 时必填且长度 ≤ 1）。</summary>
        public string[] mechanic_targets;

        /// <summary>数值效果键（<c>eff_*</c>）。</summary>
        public string stat_key;

        /// <summary>数值效果系数（0.01–1.00）。</summary>
        public float stat_value;

        /// <summary>累计消耗（派生，禁手填）。</summary>
        public int cum_cost_eu;

        /// <summary>累计生效的数值修正（派生，禁手填）。</summary>
        public EffectModEntry[] effective_mods;
    }

    /// <summary>键 → 值 的一对（用于 <c>effective_mods</c> 与规则参数）。</summary>
    public sealed class EffectModEntry
    {
        /// <summary>键。</summary>
        public string key;

        /// <summary>值。</summary>
        public float value;
    }

    /// <summary>手札特技行（6 条 · 3 槽）。</summary>
    public sealed class SpecialtyRow
    {
        /// <summary>特技 id（<c>sp_*</c>）。</summary>
        public string spec_id;

        /// <summary>槽位。</summary>
        public string slot;

        /// <summary>互斥组 id（如 <c>resource_cap</c> / <c>cooldown</c>）。</summary>
        public string exclude_group;

        /// <summary>触发时机。</summary>
        public string trigger;

        /// <summary>条件标签（<c>cond_*</c>；可空串）。</summary>
        public string condition_tag;

        /// <summary>效果键（<c>eff_*</c>）。</summary>
        public string effect_key;

        /// <summary>效果系数（0.01–1.00）。</summary>
        public float effect_value;

        /// <summary>冷却帧数（C 轴帧，0–600）。</summary>
        public int cooldown_frames;

        /// <summary>消耗 EU。</summary>
        public int cost_eu;
    }

    /// <summary>络脉节点行（8 行 = 1 枢纽 + 3 分支 + 4 叶）。</summary>
    public sealed class MeridianRow
    {
        /// <summary>节点 id（<c>md_*</c>）。</summary>
        public string node_id;

        /// <summary>布局横坐标。</summary>
        public int pos_x;

        /// <summary>布局纵坐标。</summary>
        public int pos_y;

        /// <summary>前置节点 id 列表（0–3 项）。</summary>
        public string[] prereq_ids;

        /// <summary>
        /// 拓扑角色（分支 / 叶 / 枢纽）——<b>手填</b>，是 C6 复算消耗的必需输入（角色系数 1 / 3 / 9）。
        /// </summary>
        public string node_role;

        /// <summary>
        /// 叶序系数（同一分支下的第 k 个叶 = k）——<b>手填</b>。
        /// <para>
        /// 必须是显式数据：按 node_id 排序去推叶序会与设计线定的顺序不一致（破脉·省 = 1、破脉·连 = 2），
        /// 而排序结果一变，消耗就静默错位。
        /// </para>
        /// </summary>
        public int leaf_order;

        /// <summary>机制修改项（空串 = 无）。</summary>
        public string mechanic_mod;

        /// <summary>机制参数（0–100）。</summary>
        public int mechanic_param;

        /// <summary>数值效果键（络脉 8 行恒为空）。</summary>
        public string stat_key;

        /// <summary>数值效果系数（络脉 8 行恒为空）。</summary>
        public float stat_value;

        /// <summary>消耗 EU。</summary>
        public int cost_eu;

        /// <summary>前置闭包最大深度（派生，禁手填）。</summary>
        public int depth;
    }

    /// <summary>锋饰行（3 槽 × 3 类 = 9 类目，多等级）。</summary>
    public sealed class SocketItemRow
    {
        /// <summary>锋饰 id（<c>so_*</c>）。</summary>
        public string socket_id;

        /// <summary>类型（决定可插孔位）。</summary>
        public string item_type;

        /// <summary>孔位类型。</summary>
        public string slot_kind;

        /// <summary>等级（1–10）。</summary>
        public int level;

        /// <summary>合成材料 id 列表（2–3 项；最高级为空数组）。</summary>
        public string[] compose_from;

        /// <summary>效果键（<c>eff_*</c>）。</summary>
        public string effect_key;

        /// <summary>效果系数（0.01–1.00）。</summary>
        public float effect_value;

        /// <summary>合成链闭包（派生，禁手填）。</summary>
        public string[] compose_chain;

        /// <summary>是否最高级（派生，禁手填）。</summary>
        public bool is_max_level;
    }

    /// <summary>洗练档位行（「重磨」+ 解锋）。</summary>
    public sealed class RefineStageRow
    {
        /// <summary>档位 id（<c>rs_*</c>）。</summary>
        public string stage_id;

        /// <summary>词条槽。</summary>
        public string slot_index;

        /// <summary>保留条数（不被重掷）。</summary>
        public int keep_count;

        /// <summary>消耗 EU。</summary>
        public int cost_eu;

        /// <summary>保底计数阈值。</summary>
        public int pity_threshold;

        /// <summary>保底目标词条 id（空串 = 保底随机）。</summary>
        public string pity_target;

        /// <summary>是否解锋（品阶回退返还）。</summary>
        public bool refund_tier_down;

        /// <summary>返还比例（0.00–1.00）。</summary>
        public float refund_ratio;

        /// <summary>保底计数（派生，禁手填；存档内计数，读档时回填）。</summary>
        public int pity_counter;
    }

    /// <summary>词条池行（EL-03 §1.8 扩展表：加 skill_tag / mechanic_mod）。</summary>
    public sealed class AffixPoolRow
    {
        /// <summary>词条 id。</summary>
        public string affix_id;

        /// <summary>效果键（<c>eff_*</c>）。</summary>
        public string effect_key;

        /// <summary>效果系数（只存系数，不存绝对值）。</summary>
        public float effect_value;

        /// <summary>战力权重（参与 A38 的 Σ affix_weighted）。</summary>
        public float weight;

        /// <summary>限定技能（引用 <c>skill.skill_id</c>；空串 = 不挂技能）。</summary>
        public string skill_tag;

        /// <summary>机制修改项（空串 = 无机制修改）。</summary>
        public string mechanic_mod;

        /// <summary>机制参数。</summary>
        public int mechanic_param;
    }

    /// <summary>装备基底行（3 部位 × 5 品阶）。</summary>
    public sealed class EquipmentBaseRow
    {
        /// <summary>基底 id。</summary>
        public string item_id;

        /// <summary>部位（<c>weapon</c> / <c>cloth</c> / <c>pendant</c>）。</summary>
        public string slot;

        /// <summary>品阶（0–4 对应 白 / 绿 / 蓝 / 紫 / 金）。</summary>
        public int grade;

        /// <summary>基础战力（整数 EU）。</summary>
        public int base_power;

        /// <summary>该品阶可带的词条条数上限。</summary>
        public int affix_count;
    }
}
