using System;
using System.Collections.Generic;

namespace EdgeChronicles.Data
{
    /// <summary>
    /// 技传阶位解锁类型（EL-03 §1.3）。
    /// <para>
    /// 注意：<b>阶位 → 类型不存在全局固定映射</b>。IMPL-v3.0 §4.5.1 明确「每个技能的 5 个阶位恰好覆盖 5 种类型各一次，
    /// 1 阶恒为数值强化、5 阶恒为机制改写，中间 3 阶（资源 / 派生 / 窗口）由各技能自行决定」。
    /// 逐行取值以 IMPL-v3.0 §4.5.4 的 40 行为准。
    /// </para>
    /// </summary>
    public enum TechUnlockType
    {
        /// <summary>数值强化。</summary>
        stat = 0,

        /// <summary>资源规则。</summary>
        resource = 1,

        /// <summary>派生效果。</summary>
        derivative = 2,

        /// <summary>窗口变化。</summary>
        window = 3,

        /// <summary>机制改写。</summary>
        mechanic = 4
    }

    /// <summary>视觉分级（EL-03 §1.3 / IMPL-v3.0 §4.5.3）。</summary>
    public enum VisualTier
    {
        /// <summary>纯数值或纯数据层规则，玩家看得见的东西不变（0 格）。</summary>
        none = 0,

        /// <summary>同一形状的变体（0 格）。</summary>
        param = 1,

        /// <summary>改写战斗规则、无专属战场表现（0 格，通用「规则符 + 阶位刻度环」）。</summary>
        generic = 2,

        /// <summary>引入画面中此前不存在的新形状 / 新对象（1 格）。</summary>
        new_graphic = 3
    }

    /// <summary>星盘 4 系（形状即语义）。</summary>
    public enum StarSeries
    {
        /// <summary>锋：输出的条件。</summary>
        blade = 0,

        /// <summary>血：承受与交换。</summary>
        blood = 1,

        /// <summary>速：节奏与先手。</summary>
        speed = 2,

        /// <summary>势：锋芒资源。</summary>
        momentum = 3
    }

    /// <summary>同系连携的成立条件（唯一合法值）。</summary>
    public enum StarLinkCondition
    {
        /// <summary>同系相邻。</summary>
        same_series_adjacent = 0
    }

    /// <summary>连携生效态。</summary>
    public enum StarLinkState
    {
        /// <summary>未生效。</summary>
        inactive = 0,

        /// <summary>生效。</summary>
        active = 1
    }

    /// <summary>手札特技槽位。</summary>
    public enum SpecialtySlot
    {
        /// <summary>槽 1。</summary>
        slot_1 = 0,

        /// <summary>槽 2。</summary>
        slot_2 = 1,

        /// <summary>槽 3。</summary>
        slot_3 = 2
    }

    /// <summary>手札特技触发时机。</summary>
    public enum SpecialtyTrigger
    {
        /// <summary>常驻规则：随养成快照生效，无触发帧（IMPL-v3.0.md §4.8.6 的 sp_resource_cap_1）。</summary>
        always = 0,

        /// <summary>释放时。</summary>
        on_cast = 1,

        /// <summary>命中时。</summary>
        on_hit = 2,

        /// <summary>闪避时。</summary>
        on_dodge = 3,

        /// <summary>完美闪避时。</summary>
        on_perfect_dodge = 4,

        /// <summary>核心状态时。</summary>
        on_core_state = 5
    }

    /// <summary>锋饰类型（决定可插孔位）。</summary>
    public enum SocketItemType
    {
        /// <summary>攻击类。</summary>
        atk = 0,

        /// <summary>防御类。</summary>
        def = 1,

        /// <summary>功能类。</summary>
        util = 2
    }

    /// <summary>洗练档位对应的词条槽。</summary>
    public enum RefineSlot
    {
        /// <summary>词条槽 1。</summary>
        affix_1 = 0,

        /// <summary>词条槽 2。</summary>
        affix_2 = 1,

        /// <summary>词条槽 3。</summary>
        affix_3 = 2
    }

    /// <summary>progression 表的行种（6 修为线 + 名望 + 重置规则同表承载）。</summary>
    public enum ProgressionKind
    {
        /// <summary>修为线。</summary>
        cultivation = 0,

        /// <summary>名望档。</summary>
        fame = 1,

        /// <summary>重置规则。</summary>
        reset = 2
    }

    /// <summary>被重置的系统。</summary>
    public enum TargetSystem
    {
        /// <summary>修为线。</summary>
        cultivation = 0,

        /// <summary>星盘。</summary>
        star = 1,

        /// <summary>络脉。</summary>
        meridian = 2,

        /// <summary>装备。</summary>
        equipment = 3
    }

    /// <summary>重置策略（枚举已精简为单值，禁出现第二个合法值）。</summary>
    public enum ResetPolicy
    {
        /// <summary>免费。</summary>
        free = 0
    }

    /// <summary>名望来源。</summary>
    public enum FameSource
    {
        /// <summary>通关数。</summary>
        clear_count = 0,

        /// <summary>难度档达成。</summary>
        difficulty = 1,

        /// <summary>两者合计。</summary>
        mixed = 2
    }

    /// <summary>核心循环层级（技传 5 阶可改写的目标）。</summary>
    public enum LoopTarget
    {
        /// <summary>L1 普攻 → 层数。</summary>
        loop_l1 = 0,

        /// <summary>L2 层数 → 核心。</summary>
        loop_l2 = 1,

        /// <summary>L3 取消窗口。</summary>
        loop_l3 = 2,

        /// <summary>L4 闪避 → 强化。</summary>
        loop_l4 = 3,

        /// <summary>L5 技能 → 派生。</summary>
        loop_l5 = 4,

        /// <summary>L6 状态联动。</summary>
        loop_l6 = 5
    }

    /// <summary>技能相。</summary>
    public enum SkillPhase
    {
        /// <summary>前摇。</summary>
        startup = 0,

        /// <summary>判定。</summary>
        active = 1,

        /// <summary>后摇。</summary>
        recovery = 2
    }

    /// <summary>命中形状。</summary>
    public enum HitShapeKind
    {
        /// <summary>胶囊。</summary>
        capsule = 0,

        /// <summary>扇形。</summary>
        sector = 1,

        /// <summary>矩形。</summary>
        rect = 2
    }

    /// <summary>击退档位。</summary>
    public enum KnockbackTier
    {
        /// <summary>轻。</summary>
        light = 0,

        /// <summary>中。</summary>
        medium = 1,

        /// <summary>重。</summary>
        heavy = 2
    }

    /// <summary>Boss 规则触发源（IMPL-v2.0 §4）。</summary>
    public enum BossTrigger
    {
        /// <summary>战斗开始。</summary>
        OnFightStart = 0,
        OnTimer = 1,
        OnHpBelow = 2,
        OnPhaseEnter = 3,
        OnCastComplete = 4,
        OnAddSpawned = 5,
        OnPlayerSkillHit = 6,
        OnPlayerEnterZone = 7,
        OnAnchorStateChanged = 8
    }

    /// <summary>Boss 规则条件。</summary>
    public enum BossCondition
    {
        /// <summary>生命低于。</summary>
        HpBelow = 0,
        HpAbove = 1,
        PhaseEquals = 2,
        PlayerInZone = 3,
        AnchorInState = 4,
        CooldownReady = 5,
        StackCountGte = 6,
        RandomPct = 7,
        AggroCountGte = 8
    }

    /// <summary>Boss 规则效果。</summary>
    public enum BossEffect
    {
        /// <summary>生成危险区。</summary>
        SpawnHazard = 0,
        MovePlayer = 1,
        ModifyTerrain = 2,
        ForcePhase = 3,
        ApplyBuff = 4,
        SpawnAdd = 5,
        PlayVfx = 6,
        ShakeCamera = 7,
        LockZone = 8
    }

    /// <summary>机关状态（状态机 Open → Broken → Lock）。</summary>
    public enum AnchorState
    {
        /// <summary>初始。</summary>
        Open = 0,

        /// <summary>已破坏。</summary>
        Broken = 1,

        /// <summary>已锁定。</summary>
        Lock = 2
    }

    /// <summary>
    /// 枚举目录：给导表器提供「合法字符串集合」。
    /// </summary>
    public static class EnumCatalog
    {
        /// <summary>技传阶位解锁类型的合法值。</summary>
        public static readonly string[] TechUnlockTypeValues = { "stat", "resource", "derivative", "window", "mechanic" };

        /// <summary>视觉分级的合法值。</summary>
        public static readonly string[] VisualTierValues = { "none", "param", "generic", "new_graphic" };

        /// <summary>星盘系的合法值。</summary>
        public static readonly string[] StarSeriesValues = { "blade", "blood", "speed", "momentum" };

        /// <summary>连携条件的合法值。</summary>
        public static readonly string[] StarLinkConditionValues = { "same_series_adjacent" };

        /// <summary>连携状态的合法值。</summary>
        public static readonly string[] StarLinkStateValues = { "inactive", "active" };

        /// <summary>特技槽位的合法值。</summary>
        public static readonly string[] SpecialtySlotValues = { "slot_1", "slot_2", "slot_3" };

        /// <summary>特技触发时机的合法值。</summary>
        public static readonly string[] SpecialtyTriggerValues = { "always", "on_cast", "on_hit", "on_dodge", "on_perfect_dodge", "on_core_state" };

        /// <summary>锋饰类型的合法值。</summary>
        public static readonly string[] SocketItemTypeValues = { "atk", "def", "util" };

        /// <summary>洗练槽位的合法值。</summary>
        public static readonly string[] RefineSlotValues = { "affix_1", "affix_2", "affix_3" };

        /// <summary>progression 行种的合法值。</summary>
        public static readonly string[] ProgressionKindValues = { "cultivation", "fame", "reset" };

        /// <summary>被重置系统的合法值。</summary>
        public static readonly string[] TargetSystemValues = { "cultivation", "star", "meridian", "equipment" };

        /// <summary>重置策略的合法值。</summary>
        public static readonly string[] ResetPolicyValues = { "free" };

        /// <summary>名望来源的合法值。</summary>
        public static readonly string[] FameSourceValues = { "clear_count", "difficulty", "mixed" };

        /// <summary>核心循环目标的合法值。</summary>
        public static readonly string[] LoopTargetValues = { "loop_l1", "loop_l2", "loop_l3", "loop_l4", "loop_l5", "loop_l6" };

        /// <summary>技能相的合法值。</summary>
        public static readonly string[] SkillPhaseValues = { "startup", "active", "recovery" };

        /// <summary>命中形状的合法值。</summary>
        public static readonly string[] HitShapeKindValues = { "capsule", "sector", "rect" };

        /// <summary>击退档位的合法值。</summary>
        public static readonly string[] KnockbackTierValues = { "light", "medium", "heavy" };

        /// <summary>Boss 触发源的合法值。</summary>
        public static readonly string[] BossTriggerValues =
        {
            "OnFightStart", "OnTimer", "OnHpBelow", "OnPhaseEnter", "OnCastComplete",
            "OnAddSpawned", "OnPlayerSkillHit", "OnPlayerEnterZone", "OnAnchorStateChanged"
        };

        /// <summary>Boss 条件的合法值。</summary>
        public static readonly string[] BossConditionValues =
        {
            "HpBelow", "HpAbove", "PhaseEquals", "PlayerInZone", "AnchorInState",
            "CooldownReady", "StackCountGte", "RandomPct", "AggroCountGte"
        };

        /// <summary>Boss 效果的合法值。</summary>
        public static readonly string[] BossEffectValues =
        {
            "SpawnHazard", "MovePlayer", "ModifyTerrain", "ForcePhase", "ApplyBuff",
            "SpawnAdd", "PlayVfx", "ShakeCamera", "LockZone"
        };

        /// <summary>机关状态的合法值。</summary>
        public static readonly string[] AnchorStateValues = { "Open", "Broken", "Lock" };

        /// <summary>取技传解锁类型。</summary>
        public static TechUnlockType ParseTechUnlockType(string value)
        {
            return Parse(value, TechUnlockTypeValues) switch
            {
                "stat" => TechUnlockType.stat,
                "resource" => TechUnlockType.resource,
                "derivative" => TechUnlockType.derivative,
                "window" => TechUnlockType.window,
                _ => TechUnlockType.mechanic
            };
        }

        /// <summary>取视觉分级。</summary>
        public static VisualTier ParseVisualTier(string value)
        {
            return Parse(value, VisualTierValues) switch
            {
                "none" => VisualTier.none,
                "param" => VisualTier.param,
                "generic" => VisualTier.generic,
                _ => VisualTier.new_graphic
            };
        }

        private static string Parse(string value, string[] allowed)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            for (int i = 0; i < allowed.Length; i++)
            {
                if (string.Equals(allowed[i], value, StringComparison.Ordinal)) return allowed[i];
            }
            throw new ArgumentException($"非法枚举值「{value}」；合法集合：{string.Join(" / ", allowed)}");
        }
    }
}
