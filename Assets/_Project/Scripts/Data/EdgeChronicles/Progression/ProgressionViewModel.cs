using UnityEngine;

namespace EdgeChronicles.Data.Progression
{
    /// <summary>
    /// 养成 UI 的读取契约（EL-03 §7）。
    /// <para>
    /// 三条边界（EL-03 §7 导言）：
    /// ① UI <b>不得直接读 <c>ProgressionSnapshot</c></b>，一律走本 ViewModel；
    /// ② UI <b>每帧最多读一次</b>（沿用 IMPL-v2.0 §2「UI 只读快照，每帧最多一次」）；
    /// ③ 本结构是<b>扁平结构</b>（无字典、无嵌套类），字段名带面板前缀，便于 IL2CPP 与序列化。
    /// </para>
    /// <para>
    /// 字段名用 camelCase（UI 侧 C# 属性名），与数据表的 snake_case 的映射<b>由 Kernel 在快照重建时一次完成</b>——
    /// 映射只在这里发生一次，其它地方不重复写。
    /// </para>
    /// </summary>
    public sealed class ProgressionViewModel
    {
        // ---- 通用 ----
        /// <summary>总战力（派生）。</summary>
        public int power_total;
        /// <summary>投入前后的战力差（派生）。</summary>
        public int power_delta;
        /// <summary>各材料持有量（按 material_key 索引）。</summary>
        public int[] material_count;
        /// <summary>名望档位（1–5）。</summary>
        public int fame_tier;
        /// <summary>名望点数。</summary>
        public int fame_points;
        /// <summary>距下一档还差什么。</summary>
        public string fame_next_requirement;

        // ---- 修为面板 ----
        /// <summary>修为线数量。</summary>
        public int cult_line_count;
        /// <summary>修为线 id。</summary>
        public string[] cult_line_id;
        /// <summary>修为线名。</summary>
        public string[] cult_line_name;
        /// <summary>当前等级。</summary>
        public int[] cult_line_level;
        /// <summary>等级上限。</summary>
        public int[] cult_line_max_level;
        /// <summary>下一级消耗。</summary>
        public int[] cult_line_next_cost;
        /// <summary>下一级效果文案。</summary>
        public string[] cult_line_next_effect;
        /// <summary>刻度进度（0–1）。</summary>
        public float[] cult_line_progress;

        // ---- 装备面板（含词条 / 锋饰 / 洗练 · 解锋）----
        /// <summary>部位数（常量 3）。</summary>
        public int equip_slot_count;
        /// <summary>部位品阶（0–5）。</summary>
        public int[] equip_grade;
        /// <summary>部位词条数。</summary>
        public int[] equip_affix_count;
        /// <summary>部位是否开放锋饰孔。</summary>
        public bool[] equip_socket_open;
        /// <summary>部位战力贡献。</summary>
        public int[] equip_power;
        /// <summary>词条 id。</summary>
        public string[] affix_id;
        /// <summary>词条名。</summary>
        public string[] affix_name;
        /// <summary>词条图标。</summary>
        public Sprite[] affix_icon;
        /// <summary>词条值（系数）。</summary>
        public float[] affix_value;
        /// <summary>「仅对某技能生效」角标（空串 = 无）。</summary>
        public string[] affix_skill_tag;
        /// <summary>是否机制类词条（决定圆点还是通用「机制改写」标记）。</summary>
        public bool[] affix_is_mechanic;
        /// <summary>词条所在槽（洗练用）。</summary>
        public int[] affix_slot;
        /// <summary>3 锋饰槽当前装备 id。</summary>
        public string[] socket_item_id;
        /// <summary>锋饰名。</summary>
        public string[] socket_item_name;
        /// <summary>锋饰等级。</summary>
        public int[] socket_item_level;
        /// <summary>锋饰图标。</summary>
        public Sprite[] socket_item_icon;
        /// <summary>背包某件能否装入当前槽（类型匹配 + 孔位开放）。</summary>
        public bool[] socket_equipable;
        /// <summary>可合成到下一级的目标 id。</summary>
        public string[] socket_next_level_id;
        /// <summary>洗练抽屉的档位列表。</summary>
        public string[] refine_stage_id;
        /// <summary>保留条数（不被重掷）。</summary>
        public int[] refine_keep_count;
        /// <summary>洗练消耗。</summary>
        public int[] refine_cost;
        /// <summary>保底进度 n。</summary>
        public int[] refine_pity;
        /// <summary>保底阈值。</summary>
        public int[] refine_pity_threshold;
        /// <summary>解锋返还比例。</summary>
        public float[] refine_refund_ratio;

        // ---- 星盘面板 ----
        /// <summary>节点数（12）。</summary>
        public int star_node_count;
        /// <summary>节点 id。</summary>
        public string[] star_node_id;
        /// <summary>所属系（blade / blood / speed / momentum）。</summary>
        public string[] star_node_series;
        /// <summary>节点横坐标（锚点布局用，不写死像素）。</summary>
        public int[] star_node_pos_x;
        /// <summary>节点纵坐标。</summary>
        public int[] star_node_pos_y;
        /// <summary>是否已点亮。</summary>
        public bool[] star_node_unlocked;
        /// <summary>是否可点亮（前置齐 + 档位够 + 材料够）。</summary>
        public bool[] star_node_available;
        /// <summary>消耗。</summary>
        public int[] star_node_cost;
        /// <summary>效果预览文案。</summary>
        public string[] star_node_effect_text;
        /// <summary>机制修改项 id（非空 = 机制类节点）；空串 = 离散 / 次数 / 帧类节点。</summary>
        public string[] star_node_mechanic_mod;
        /// <summary>连携三态：潜在 / 未满足 / 生效。</summary>
        public string[] star_link_state;

        // ---- 手札 + 技传面板 ----
        /// <summary>特技槽数（常量 3）。</summary>
        public int spec_slot_count;
        /// <summary>槽位特技 id。</summary>
        public string[] spec_id;
        /// <summary>特技名。</summary>
        public string[] spec_name;
        /// <summary>特技图标。</summary>
        public Sprite[] spec_icon;
        /// <summary>规则文案。</summary>
        public string[] spec_effect_text;
        /// <summary>与另一槽互斥 → 标红。</summary>
        public bool[] spec_conflict;
        /// <summary>技能数（常量 8）。</summary>
        public int tech_skill_count;
        /// <summary>阶位阶梯格数（常量 5）。</summary>
        public int tech_tier_count;
        /// <summary>已达阶位。</summary>
        public int[] tech_current_tier;
        /// <summary>下一阶消耗（谱份数）。</summary>
        public int[] tech_next_cost;
        /// <summary>阶位类型（数值 / 资源 / 派生 / 窗口 / 机制）。</summary>
        public string[] tech_unlock_type;
        /// <summary>阶位效果文案。</summary>
        public string[] tech_effect_text;
        /// <summary>派生名。</summary>
        public string[] tech_derivative_name;
        /// <summary>不出图 / 参数化 / 通用符 / 专属 VFX。</summary>
        public string[] tech_visual_tier;

        // ---- 络脉面板 ----
        /// <summary>节点数（常量 8）。</summary>
        public int meridian_node_count;
        /// <summary>节点 id。</summary>
        public string[] meridian_node_id;
        /// <summary>是否机制类（恒为 true）。</summary>
        public bool[] meridian_node_is_mechanic;
        /// <summary>机制修改项 id。</summary>
        public string[] meridian_mechanic_mod;
        /// <summary>底部机制说明条文案。</summary>
        public string[] meridian_effect_text;
        /// <summary>是否已解锁。</summary>
        public bool[] meridian_unlocked;
        /// <summary>是否可点亮。</summary>
        public bool[] meridian_available;

        // ---- 重置（挂在修为 / 星盘 / 络脉面板内）----
        /// <summary>「重置」按钮列表长度。</summary>
        public int reset_target_count;
        /// <summary>被重置的系统。</summary>
        public string[] reset_target_system;
        /// <summary>消耗（当前全为 0 = 免费）。</summary>
        public int[] reset_cost;
        /// <summary>策略（当前仅 free）。</summary>
        public string[] reset_policy;
    }
}
