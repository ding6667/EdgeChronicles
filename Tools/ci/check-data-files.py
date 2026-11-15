#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""配表源数据的**文件级预检**（不需要 Unity，秒级）。

定位：它是 `Tools/ci/check-data.sh`（Unity 批处理导表 + A19–A43 全断言）的**前置快筛**，
不是断言的替代品——真正的判定逻辑只有一份，在 C# 侧（EdgeChronicles.Data.Validation.Assertions）。
这里只做「文件级 / 结构级」的检查，目的是让明显的问题在跑慢速 Unity 管线之前就暴露。

检查项：
  1. 22 张表文件齐全、可解析、行数 > 0
  2. **派生列禁手填**：源数据里不得出现派生列（派生值只由导表器算）
  3. 外键完整性（孤儿引用）
  4. 主键唯一
  5. 枚举列取值合法（mechanic_mod 的 28 项等）
  6. 技传阶位：连续无缺口 + 5 种 unlock_type 各一次 + tier1=stat / tier5=mechanic
  7. 派生条目 8 条：id / 技能 / 阶位 / 视觉分级 逐条一致
  8. 节点消耗：值域、同系递增、同分支递增、按推导式复算（禁手填）
  9. 名望 1–5 档齐全、重置行完整
  10. 图集格位配额（专属 + 通用 ≤ 预算）

退出码：0 = 全过；1 = 有失败项（逐条打印）。
"""

import json
import os
import sys
from collections import defaultdict

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
DATA_DIR = os.path.join(ROOT, "Assets", "_Project", "Data")

SPEC_TABLES = [
    "global_tuning", "skill", "cancel_window", "hit_shape", "movement_curve",
    "resource", "core_link", "enemy", "boss", "boss_rule", "anchor", "dungeon",
    "progression", "star_node", "star_link", "tech_tier", "specialty", "meridian",
    "socket_item", "refine_stage",
]
AUX_TABLES = ["affix_pool", "equipment_base"]

# 派生列（禁手填）——必须与 C# 侧的 TableCatalog 一致
DERIVED_COLUMNS = {
    "global_tuning": [],
    "skill": ["tech_tiers", "total_frames", "segment_count"],
    "cancel_window": ["window_length"],
    "hit_shape": ["arc_length", "expected_dps"],
    "movement_curve": ["integral_distance"],
    "resource": [],
    "core_link": [],
    "enemy": [],
    "boss": ["total_hp"],
    "boss_rule": ["rule_hash"],
    "anchor": [],
    "dungeon": ["enemy_count"],
    "progression": ["fame_lv_min", "fame_lv_max", "expected_power", "cost_eu"],
    "star_node": ["prereq_closure", "link_partners"],
    "star_link": ["active_state", "series"],
    "tech_tier": ["cum_cost_eu", "effective_mods"],
    "specialty": [],
    "meridian": ["depth"],
    "socket_item": ["compose_chain", "is_max_level"],
    "refine_stage": ["pity_counter"],
    "affix_pool": [],
    "equipment_base": [],
}

# 外键：列 → (目标表, 目标列)
FOREIGN_KEYS = {
    "skill": [("movement_curve", "movement_curve", "curve_id"), ("hit_shape_ids", "hit_shape", "shape_id")],
    "hit_shape": [("skill_id", "skill", "skill_id")],
    "core_link": [("skill_id", "skill", "skill_id")],
    "boss": [("arena_anchor", "anchor", "anchor_id")],
    "boss_rule": [("boss_id", "boss", "boss_id")],
    "dungeon": [("boss_id", "boss", "boss_id"), ("anchors", "anchor", "anchor_id")],
    "star_node": [("prereq_ids", "star_node", "node_id")],
    "star_link": [("node_a", "star_node", "node_id"), ("node_b", "star_node", "node_id"),
                  ("trigger_node", "star_node", "node_id")],
    "meridian": [("prereq_ids", "meridian", "node_id")],
    "tech_tier": [("skill_id", "skill", "skill_id"), ("pending_rule_key", "core_link", "state_id")],
    "socket_item": [("compose_from", "socket_item", "socket_id")],
    "refine_stage": [("pity_target", "affix_pool", "affix_id")],
    "affix_pool": [("skill_tag", "skill", "skill_id")],
}

PRIMARY_KEYS = {
    "global_tuning": ["key"], "skill": ["skill_id"],
    "cancel_window": ["source_tag", "allow_tag", "from_frame"],
    "hit_shape": ["shape_id"], "movement_curve": ["curve_id"], "resource": ["id"],
    "core_link": ["state_id"], "enemy": ["enemy_id"], "boss": ["boss_id"],
    "boss_rule": ["rule_id"], "anchor": ["anchor_id"], "dungeon": ["dungeon_id"],
    "progression": ["line_id"], "star_node": ["node_id"], "star_link": ["link_id"],
    "tech_tier": ["skill_id", "tier"], "specialty": ["spec_id"], "meridian": ["node_id"],
    "socket_item": ["socket_id"], "refine_stage": ["stage_id"],
    "affix_pool": ["affix_id"], "equipment_base": ["item_id"],
}

MECHANIC_MODS = set("""MM_ATK_NO_INTERRUPT_IN_DOMAIN MM_FINISH_COST_MINUS1 MM_PERFECT_DODGE_REFUND1
MM_RESOURCE_CAP_PLUS1 MM_CORE_CD_MINUS_FRAMES MM_DOMAIN_LAST_JUMP_DOUBLE MM_CHANNEL_INTERVAL_HALF
MM_DASH_IFRAME_PLUS_FRAMES MM_BIND_TRIGGER_VARIANT MM_FINISH_EXTRA_DOMAIN MM_DOMAIN_HIT_GAIN1
MM_GUARD_REFUND_IN_DOMAIN MM_HEAVY_GAIN_EXTRA MM_CANCEL_WINDOW_WIDEN MM_DODGE_CD_MINUS_FRAMES
MM_CRIT_IN_DOMAIN MM_STAR_BREAKSTATE MM_STAR_SHIELD_BREAK_FIRST MM_STAR_LIFESTEAL_CONTINUE
MM_STAR_CHANNEL_MOVE MM_STAR_OVERCAP_TO_DAMAGE MM_STAR_CANCEL_ALL_AFTER_N MM_MER_CONDUCE_SKILL
MM_MER_CONDUCE_RANGE MM_MER_CONDUCE_STACK MM_MER_STACK_REFUND MM_MER_CONDUCE_FINISH
MM_MER_FINISH_CHAIN""".split())

UNLOCK_TYPES = {"stat", "resource", "derivative", "window", "mechanic"}
VISUAL_TIERS = {"none", "param", "generic", "new_graphic"}

# 派生条目清单（IMPL-v3.0.md §4.8.5）：id → (skill_id, tier, visual_tier)
DERIVATIVES = {
    "dv_shifeng_last_jump_double": ("sk_shifeng", 3, "new_graphic"),
    "dv_shifeng_second_domain": ("sk_shifeng", 5, "param"),
    "dv_xunfeng_echo": ("sk_xunfeng", 3, "new_graphic"),
    "dv_shufeng_poise_break": ("sk_fufeng", 2, "new_graphic"),
    "dv_baoyuan_shield_break": ("sk_baoyuan", 5, "new_graphic"),
    "dv_jufeng_cluster": ("sk_jufeng", 5, "new_graphic"),
    "dv_pofeng_chain_burst": ("sk_pofeng", 5, "new_graphic"),
    "dv_huifeng_pull_turn": ("sk_huifeng", 5, "generic"),
}

STAR_TYPE_FACTOR_HINT = ("次数类", ("extra_settle", "stack_per_settle"))


class Report:
    def __init__(self):
        self.failures = []
        self.checks = 0

    def check(self, ok, code, message):
        self.checks += 1
        if not ok:
            self.failures.append("[%s] %s" % (code, message))

    def fail(self, code, message):
        self.check(False, code, message)


def load(report):
    tables = {}
    for name in SPEC_TABLES + AUX_TABLES:
        path = os.path.join(DATA_DIR, name + ".json")
        if not os.path.exists(path):
            report.fail("MISSING", "缺源数据文件：%s" % path)
            continue
        try:
            with open(path, "r", encoding="utf-8") as f:
                obj = json.load(f)
        except Exception as exc:
            report.fail("PARSE", "%s.json 解析失败：%s" % (name, exc))
            continue
        rows = obj.get("rows")
        if not isinstance(rows, list) or not rows:
            report.fail("ROWS", "%s.json 的 rows 为空或不是数组" % name)
            continue
        tables[name] = rows
    return tables


def check_derived_columns(report, tables):
    for name, rows in tables.items():
        derived = set(DERIVED_COLUMNS.get(name, []))
        for i, row in enumerate(rows):
            for col in derived:
                if col in row:
                    report.fail("DERIVED", "%s.json 第 %d 行出现派生列 %s（禁手填）" % (name, i + 1, col))


def check_primary_keys(report, tables):
    for name, rows in tables.items():
        cols = PRIMARY_KEYS.get(name)
        if not cols:
            continue
        seen = set()
        for i, row in enumerate(rows):
            key = "|".join(str(row.get(c, "")) for c in cols)
            if key in seen:
                report.fail("PK", "%s.json 主键重复：%s" % (name, key))
            seen.add(key)


def check_foreign_keys(report, tables):
    for name, rules in FOREIGN_KEYS.items():
        rows = tables.get(name)
        if not rows:
            continue
        for col, target_table, target_col in rules:
            target_rows = tables.get(target_table)
            if target_rows is None:
                continue
            valid = set()
            for trow in target_rows:
                valid.add(str(trow.get(target_col, "")))
            for i, row in enumerate(rows):
                value = row.get(col)
                if value is None:
                    continue
                items = value if isinstance(value, list) else [value]
                for item in items:
                    if item in (None, ""):
                        continue
                    if str(item) not in valid:
                        report.fail("FK", "%s.json 第 %d 行的 %s 引用了不存在的 %s.%s = %s"
                                    % (name, i + 1, col, target_table, target_col, item))


def check_mechanic_mods(report, tables):
    for name in ("star_node", "meridian", "affix_pool"):
        for i, row in enumerate(tables.get(name, [])):
            mod = row.get("mechanic_mod", "")
            if not mod:
                continue
            if mod not in MECHANIC_MODS:
                report.fail("A35", "%s.json 第 %d 行的 mechanic_mod「%s」不在 28 项枚举内"
                            % (name, i + 1, mod))


def check_tech_tier(report, tables):
    rows = tables.get("tech_tier", [])
    by_skill = defaultdict(list)
    for row in rows:
        by_skill[row["skill_id"]].append(row)

    for skill_id, list_ in by_skill.items():
        tiers = sorted(r["tier"] for r in list_)
        if tiers != list(range(1, len(tiers) + 1)):
            report.fail("A33", "%s 的阶位不连续：%s" % (skill_id, tiers))
        types = [r["unlock_type"] for r in sorted(list_, key=lambda r: r["tier"])]
        if len(set(types)) != 5:
            report.fail("A33", "%s 的 5 阶没有覆盖 5 种 unlock_type 各一次：%s" % (skill_id, types))
        if types and types[0] != "stat":
            report.fail("A33", "%s 的 1 阶不是 stat：%s" % (skill_id, types[0]))
        if types and types[-1] != "mechanic":
            report.fail("A33", "%s 的 5 阶不是 mechanic：%s" % (skill_id, types[-1]))

        # A33-1 / A33-2：派生条目双向一致 + 逐条对齐清单
        used = [r for r in list_ if r.get("unlock_derivative_id")]
        ids = [r["unlock_derivative_id"] for r in used]
        if len(ids) != len(set(ids)):
            report.fail("A33-1", "%s 的派生 id 重复：%s" % (skill_id, ids))
        for r in used:
            dv = r["unlock_derivative_id"]
            if dv not in DERIVATIVES:
                report.fail("A33-2", "%s 的派生 id「%s」不在 8 条清单内" % (skill_id, dv))
                continue
            exp_skill, exp_tier, exp_visual = DERIVATIVES[dv]
            if r["skill_id"] != exp_skill:
                report.fail("A33-2", "%s 的派生「%s」归属应为 %s" % (skill_id, dv, exp_skill))
            if r["tier"] != exp_tier:
                report.fail("A33-2", "%s 的派生「%s」阶位应为 %d，实际 %d"
                            % (skill_id, dv, exp_tier, r["tier"]))
            if r["visual_tier"] != exp_visual:
                report.fail("A33-2", "%s 的派生「%s」视觉分级应为 %s，实际 %s"
                            % (skill_id, dv, exp_visual, r["visual_tier"]))

        # A41：5 阶机制改写只准改写一条核心循环
        for r in list_:
            targets = r.get("mechanic_targets") or []
            if r["unlock_type"] == "mechanic":
                if r["tier"] == 5 and len(targets) > 1:
                    report.fail("A41", "%s 5 阶改了 %d 条核心循环" % (skill_id, len(targets)))
                if not targets:
                    report.fail("A41", "%s %d 阶是 mechanic 但 mechanic_targets 为空"
                                % (skill_id, r["tier"]))
            elif targets:
                report.fail("A41", "%s %d 阶不是 mechanic 却有 mechanic_targets"
                            % (skill_id, r["tier"]))

    # A33-5：new_graphic 行数 == 清单里 new_graphic 条数
    new_graphic = sum(1 for r in rows if r.get("visual_tier") == "new_graphic")
    expected = sum(1 for v in DERIVATIVES.values() if v[2] == "new_graphic")
    report.check(new_graphic == expected, "A33-5",
                 "new_graphic 行数 %d != 清单条数 %d" % (new_graphic, expected))

    # A33-3：裂石全部 5 阶 + 回锋 1–4 阶不得有派生 id
    for r in rows:
        sid = r["skill_id"]
        if "lieshi" in sid or ("huifeng" in sid and r["tier"] <= 4):
            if r.get("unlock_derivative_id"):
                report.fail("A33-3", "%s %d 阶不应有派生 id" % (sid, r["tier"]))


def check_skill_derivative_ids(report, tables):
    declared = defaultdict(set)
    for row in tables.get("skill", []):
        declared[row["skill_id"]] = set(row.get("derivative_ids") or [])
    used = defaultdict(set)
    for row in tables.get("tech_tier", []):
        if row.get("unlock_derivative_id"):
            used[row["skill_id"]].add(row["unlock_derivative_id"])

    for skill_id, ids in used.items():
        for dv in ids:
            if dv not in declared.get(skill_id, set()):
                report.fail("A33-1", "%s 的派生「%s」未出现在 skill.derivative_ids" % (skill_id, dv))
    for skill_id, ids in declared.items():
        for dv in ids:
            if dv not in used.get(skill_id, set()):
                report.fail("A33-1", "%s.derivative_ids 里的「%s」没有任何阶位引用" % (skill_id, dv))


def check_costs(report, tables):
    # C1 / C2 值域
    for row in tables.get("star_node", []):
        report.check(1 <= row["cost_eu"] <= 60, "C1", "star_node %s 的 cost_eu %d 超出 1–60"
                    % (row["node_id"], row["cost_eu"]))
    for row in tables.get("meridian", []):
        report.check(1 <= row["cost_eu"] <= 50, "C2", "meridian %s 的 cost_eu %d 超出 1–50"
                    % (row["node_id"], row["cost_eu"]))

    # C3 星盘同系严格递增
    by_series = defaultdict(list)
    for row in tables.get("star_node", []):
        by_series[row["series"]].append(row)
    for series, list_ in by_series.items():
        list_ = sorted(list_, key=lambda r: r["unlock_tier"])
        for i in range(1, len(list_)):
            report.check(list_[i]["cost_eu"] > list_[i - 1]["cost_eu"], "C3",
                        "星盘 %s 系消耗未严格递增：%s(%d) → %s(%d)"
                        % (series, list_[i - 1]["node_id"], list_[i - 1]["cost_eu"],
                           list_[i]["node_id"], list_[i]["cost_eu"]))

    # C6 星盘按推导式复算：2 × 层级系数(1/3/9) × 类型系数(离散/帧=1，次数/机制=2)
    for series, list_ in by_series.items():
        list_ = sorted(list_, key=lambda r: r["unlock_tier"])
        for i, row in enumerate(list_):
            level_factor = [1, 3, 9][i] if i < 3 else 9
            node_type = row.get("node_type")
            type_factor = 2 if node_type in ("count", "mechanic") else 1
            expected = 2 * level_factor * type_factor
            report.check(row["cost_eu"] == expected, "C6",
                        "star_node %s 按推导式重算 = 2×%d×%d = %d，表中 %d"
                        % (row["node_id"], level_factor, type_factor, expected, row["cost_eu"]))

    # C4 络脉同分支递增 + C6 络脉复算：3 × 角色系数(分支 1/叶 3/枢纽 9) × 叶序系数
    mer = {r["node_id"]: r for r in tables.get("meridian", [])}

    for row in tables.get("meridian", []):
        pres = row.get("prereq_ids") or []
        role_factor = {"hub": 9, "leaf": 3, "branch": 1}.get(row.get("node_role"))
        if role_factor is None:
            report.fail("C6", "meridian %s 的 node_role 必须落值（branch / leaf / hub）" % row["node_id"])
            continue
        leaf_factor = row.get("leaf_order", 1) or 1
        expected = 3 * role_factor * leaf_factor
        report.check(row["cost_eu"] == expected, "C6",
                    "meridian %s 按推导式重算 = 3×%d×%d = %d，表中 %d"
                    % (row["node_id"], role_factor, leaf_factor, expected, row["cost_eu"]))
        for pre in pres:
            if pre in mer:
                report.check(row["cost_eu"] > mer[pre]["cost_eu"], "C4",
                            "meridian %s(%d) 未高于其前置 %s(%d)"
                            % (row["node_id"], row["cost_eu"], pre, mer[pre]["cost_eu"]))


def check_progression(report, tables):
    fame_tiers = set()
    reset_rows = 0
    for row in tables.get("progression", []):
        kind = row.get("kind")
        if kind == "fame":
            tier = row.get("fame_unlock_tier")
            report.check(tier in (1, 2, 3, 4, 5), "A42", "名望档位 %s 越界" % tier)
            fame_tiers.add(tier)
        elif kind == "reset":
            reset_rows += 1
            report.check(row.get("reset_policy") == "free", "A42", "重置策略必须是 free")
            report.check(row.get("reset_cost", 0) >= 0, "A42", "重置消耗禁负")
            report.check(bool(row.get("target_system")), "A42", "重置行必须有 target_system")
    for tier in range(1, 6):
        report.check(tier in fame_tiers, "A42", "名望档 %d 缺行" % tier)
    report.check(reset_rows == 3, "A42", "重置行应有 3 条，实际 %d" % reset_rows)


def check_affixes(report, tables):
    affixes = tables.get("affix_pool", [])
    skill_ids = {r["skill_id"] for r in tables.get("skill", [])}
    tagged = [r for r in affixes if r.get("skill_tag")]
    report.check(len(tagged) >= 6, "A37", "带 skill_tag 的词条应 ≥ 6，实际 %d" % len(tagged))
    for row in tagged:
        report.check(row["skill_tag"] in skill_ids, "A37",
                    "词条 %s 挂了不存在的技能 %s" % (row["affix_id"], row["skill_tag"]))


def check_vfx_budget(report, tables):
    tuning = {r["key"]: r["value"] for r in tables.get("global_tuning", [])}
    graphic_budget = tuning.get("vfx_graphic_budget")
    generic_budget = tuning.get("vfx_generic_budget")
    report.check(graphic_budget is not None, "A43", "global_tuning 缺 vfx_graphic_budget")
    report.check(generic_budget is not None, "A43", "global_tuning 缺 vfx_generic_budget")
    if graphic_budget is None or generic_budget is None:
        return
    new_graphic = sum(1 for r in tables.get("tech_tier", []) if r.get("visual_tier") == "new_graphic")
    used = new_graphic + generic_budget
    report.check(used <= graphic_budget, "A43",
                 "占格 %d + 通用件 %d = %d 超过预算 %d" % (new_graphic, generic_budget, used, graphic_budget))


def main():
    report = Report()
    tables = load(report)

    check_derived_columns(report, tables)
    check_primary_keys(report, tables)
    check_foreign_keys(report, tables)
    check_mechanic_mods(report, tables)
    check_tech_tier(report, tables)
    check_skill_derivative_ids(report, tables)
    check_costs(report, tables)
    check_progression(report, tables)
    check_affixes(report, tables)
    check_vfx_budget(report, tables)

    spec_loaded = sum(1 for t in SPEC_TABLES if t in tables)
    aux_loaded = sum(1 for t in AUX_TABLES if t in tables)
    print("[CHECK] data-files: specTables=%d/%d auxTables=%d/%d checks=%d"
          % (spec_loaded, len(SPEC_TABLES), aux_loaded, len(AUX_TABLES), report.checks))

    if report.failures:
        for line in report.failures:
            print("[CHECK] data-file-fail: " + line)
        print("[CHECK] data-file-fail: %d 项失败" % len(report.failures))
        return 1

    print("[CHECK] data-file-ok")
    return 0


if __name__ == "__main__":
    sys.exit(main())
