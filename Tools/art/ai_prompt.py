#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
ai_prompt.py — 《EdgeChronicles》AI 生成提示词管线

归属：美术（art-director）· AI 生成管线
真值源：IMPL-v3.0-AD-03 §5（提示词模板 / 必含词 / 必排词 / 受控偏离）
零外部依赖：只用 Python 标准库（csv / json / argparse / os / re / sys / datetime）。

用法
----
    # 1) 渲染单条并落盘
    python Tools/art/ai_prompt.py --template ui_icon --subject "拾锋簇" \
        --tags "锋簇,聚拢,悬停" --out Assets/_Project/Art/Source/AI/BATCH_01

    # 2) 批量（CSV 或 JSON 清单）
    python Tools/art/ai_prompt.py --manifest Tools/art/manifests/ui_icons.csv \
        --out Assets/_Project/Art/Source/AI/BATCH_01

    # 3) 只校验模板与清单，不落盘
    python Tools/art/ai_prompt.py --manifest ... --check

    # 4) 自检
    python Tools/art/ai_prompt.py --self-test

退出码（**非零即失败**）
----
    0  全部提示词通过必含/必排校验
    1  存在不合规提示词（必含词缺失 / 必排词越界 / 清单字段非法）
    2  用法错误 / 内部错误

模板版本
--------
    ui_icon   —— UI 图标版（AD-03 §5 定稿）。**含一处受控偏离**：
                 图标本体透明、底色由面板提供；其余词项继承 v2.0 不变。
    generic   —— 通用物件版（v2.0 词项全继承）。带宣纸米白底。

受控偏离的准确表述（AD-03 §5 原文口径）
----------------------------------------
    v2.0：「宣纸米白底」
    ui_icon：「底色由面板提供、图标本体透明」
    偏离原因：图标带死底色、落到不同底板上会出现色块边。
    注意：受控偏离**只改底色一条**。宣纸米白仍作为**对比基准**出现在提示词里
    （64px 专项约束要对 #E8DFC9 底板保证 ≥4.5:1），不得因此被误删。

清单格式
--------
    CSV（首行必须含表头）：
        id,system,subject,tags,primary,accent,gen_size,master_size,display_size,template
    JSON：对象数组，每项字段同上（tags 可为数组或逗号分隔字符串）

    字段说明
        id           资产 ID，同时是输出文件名（不含扩展名）
        system       所属系统（SKL / STAR / VEIN / EQUIP / MAT / HUD …）
        subject      主体描述（写实具体的物件/动作，不是抽象词）
        tags         语义标签，逗号分隔；标签必须来自受控词表
        primary      主色，必须落在 6 色语义表内
        accent       强调色，必须落在 6 色语义表内
        gen_size     生成图边长（默认 1024）
        master_size  母版边长（默认 128；线宽/留白的像素基准）
        display_size 显示边长（默认 64；64px 专项约束的判读尺寸）
        template     模板名（ui_icon / generic）

输出目录规范
------------
    <out>/<批次ID>/
        prompts/<ID>.txt     每条一个提示词文件（UTF-8）
        prompts.csv          批量表：id + 完整提示词
        manifest.json        批次元数据（批次 ID、模板版本、条目数、生成时间、条目索引）
"""

import argparse
import csv
import json
import os
import re
import sys
from datetime import datetime

PIPELINE_VERSION = "1.0.0"

# ---------------------------------------------------------------------------
# 6 色语义表（唯一色相来源；与 Tools/art/shaders/EC_Palette.hlsl 同源）
# ---------------------------------------------------------------------------
PALETTE = {
    "朱砂": "#C0392B",
    "竹青": "#7C9A5B",
    "石青": "#3E6B78",
    "赭石": "#8C5A3B",
    "墨黑": "#1E1B18",
    "宣纸米白": "#E8DFC9",
}
PALETTE_HEX = {v.upper(): k for k, v in PALETTE.items()}

# ---------------------------------------------------------------------------
# 必含词（AD-03 §5 原词项，一个字都不能少）
# ---------------------------------------------------------------------------
REQUIRED_TERMS = [
    "水墨留白",
    "木版硬边",
    "低多边形",
    "宣纸米白",
    "色块 ≤8",
    "单色高对比",
    "硬边平涂",
    "纯墨线轮廓",
    "留白 ≥20%",
    "透明背景",
]

# ---------------------------------------------------------------------------
# 必排词（AD-03 §5 原词项）
#   校验口径：这些词**只允许**出现在【禁止】段；正文任何一段出现即判失败。
# ---------------------------------------------------------------------------
BANNED_TERMS = [
    "写实", "PBR", "毛发", "渐变", "景深", "模糊", "噪点", "阴影", "反射",
    "透视", "文字", "数字", "水印", "签名", "多主体", "复杂背景", "贴图",
    "圆角", "光晕", "体积光",
]

# 受控语义标签词表（避免提示词漂到风格外）
SEMANTIC_TAGS = [
    # 技能 / 战斗动作
    "锋域", "锋芒", "层数", "刀光", "斩", "横斩", "上挑", "突刺", "落阵",
    "锋簇", "三叉", "双环", "反震环", "规则符", "护壳", "碎裂",
    "锁定", "定身", "护盾", "霸体", "无敌", "蓄力", "爆发", "引导", "收招",
    "引爆", "扩散", "凝聚", "悬停", "拾取", "吸收", "回旋", "突进", "位移",
    "中空", "重击", "轻击", "错位", "拖尾", "残影", "双层", "外扩", "上抽",
    # 图形元素
    "刃器", "碎块", "液滴", "羽条", "纸片", "堆锥", "圆片", "护具", "履具",
    "枢纽", "分支", "叶节点", "结扣", "铰点", "断口", "方框", "圆点",
    "圆环", "三角", "菱形", "六边方", "同心环", "扇形", "刀身", "环扣",
    # 界面构件
    "底板", "分区底", "槽位底", "页签条", "分隔线", "徽记", "刻度", "底框",
    "外框", "内槽", "图标底板", "角标",
    # 属性 / 修为线
    "体质", "内功", "敏捷", "攻击", "悟性", "声望", "暴击", "减伤", "移速",
    "冷却", "拾取范围", "掉落率", "战力", "折扣", "保底",
    # 状态
    "未解锁", "已解锁", "已点亮", "满级", "不可用", "选中", "悬停态",
    "增益", "减益", "异常", "入窗亮起",
    # 装备 / 材料
    "锋刃", "护衣", "行履", "矿物", "液体", "植物羽", "凭证", "粉末", "消耗",
]

# ---------------------------------------------------------------------------
# 模板
# ---------------------------------------------------------------------------
TEMPLATE_UI_ICON = """【主体】{subject}，{tags}，单一主体
【形态语言】墨线低模·硬边水墨；木版硬边；低多边形；硬边平涂；纯墨线轮廓 3px 等宽；单色高对比；水墨留白
【色彩】{primary_name} {primary} + {accent_name} {accent}，色块 ≤8；透明背景；底色由面板提供（宣纸米白 {base_color} 仅作对比基准，不烘进图像）
【构图】{gen_size}×{gen_size}；主体居中占 60–72%；四周留白 ≥20%（母版 {master_size}px 上 ≥{master_whitespace_px}px）；最多 1 层内部结构
【线与面】轮廓封闭；最小线宽 ≥6px（128 母版基准；本图母版 {master_size}px 时等价 ≥{line_width_px}px）；最小负空间 ≥8px；结构线 ≤12 段
【禁止】{banned}
【{display_size}px 附加约束】单色高对比；无渐变；灰度后轮廓仍可辨（灰阶 ≤2 级）；与底板 {base_color} 最差对比 ≥4.5:1；同族仅靠形状可区分；不得以颜色作唯一区分；留白 ≥20%；最小线宽在 {master_size} 母版 ≥{line_width_px}px（等价于 128 母版 ≥6px）
【交付】透明 PNG {gen_size}×{gen_size}，四周透明 ≥28%；附 {display_size}px 缩略图 + 灰度剪影"""

TEMPLATE_GENERIC = """【主体】{subject}，{tags}，单一主体
【形态语言】墨线低模·硬边水墨；木版硬边；低多边形；硬边平涂；纯墨线轮廓 3px 等宽；单色高对比；水墨留白
【色彩】{primary_name} {primary} + {accent_name} {accent}，色块 ≤8；透明背景
【构图】{gen_size}×{gen_size}；主体居中占 60–72%；四周留白 ≥20%（母版 {master_size}px 上 ≥{master_whitespace_px}px）；最多 1 层内部结构
【线与面】轮廓封闭；最小线宽 ≥6px（128 母版基准；本图母版 {master_size}px 时等价 ≥{line_width_px}px）；最小负空间 ≥8px；结构线 ≤12 段
【禁止】{banned}
【{display_size}px 附加约束】单色高对比；无渐变；灰度后轮廓仍可辨（灰阶 ≤2 级）；与底板 {base_color} 最差对比 ≥4.5:1；同族仅靠形状可区分；不得以颜色作唯一区分；留白 ≥20%；最小线宽在 {master_size} 母版 ≥{line_width_px}px（等价于 128 母版 ≥6px）
【交付】透明 PNG {gen_size}×{gen_size}，四周透明 ≥28%；附 {display_size}px 缩略图 + 灰度剪影"""

TEMPLATES = {
    "ui_icon": TEMPLATE_UI_ICON,
    "generic": TEMPLATE_GENERIC,
}

# 受控偏离登记（用于校验与报告）
CONTROLLED_DEVIATION = {
    "template": "ui_icon",
    "from": "宣纸米白底",
    "to": "底色由面板提供、图标本体透明",
    "reason": "图标带死底色、落到不同底板上会出现色块边",
    "unchanged": "其余词项继承 v2.0 不变；宣纸米白仍作为对比基准出现在提示词里",
}


# ---------------------------------------------------------------------------
# 渲染
# ---------------------------------------------------------------------------

def _hex_to_name(hex_value):
    """十六进制 -> 语义色名。未知色返回空串（由校验判失败）。"""
    return PALETTE_HEX.get(str(hex_value).strip().upper(), "")


def _tags_to_text(tags):
    if isinstance(tags, (list, tuple)):
        return "、".join(str(t).strip() for t in tags if str(t).strip())
    return "、".join([t.strip() for t in str(tags).split(",") if t.strip()])


def render_prompt(spec):
    """把一条清单记录渲染成完整提示词。缺字段抛 ValueError。"""
    template_name = str(spec.get("template", "ui_icon")).strip()
    if template_name not in TEMPLATES:
        raise ValueError("未知模板 %r（可选：%s）"
                         % (template_name, "、".join(sorted(TEMPLATES))))

    subject = str(spec.get("subject", "")).strip()
    if not subject:
        raise ValueError("subject 不能为空")

    tags_text = _tags_to_text(spec.get("tags", ""))
    if not tags_text:
        raise ValueError("tags 不能为空")

    primary = str(spec.get("primary", PALETTE["墨黑"])).strip().upper()
    accent = str(spec.get("accent", PALETTE["朱砂"])).strip().upper()
    base_color = str(spec.get("base_color", PALETTE["宣纸米白"])).strip().upper()

    gen_size = int(spec.get("gen_size", 1024))
    master_size = int(spec.get("master_size", 128))
    display_size = int(spec.get("display_size", 64))

    # 母版上的留白像素：≥20% 边长
    master_whitespace_px = int(round(master_size * 0.20))

    # 最小线宽：AD-03 的锚点是「128 母版 ≥6px」。
    # 母版不是 128 时按同一相对线重换算（6/128 = 4.7%），保证线重一致。
    line_width_px = int(round(6.0 * master_size / 128.0))
    line_width_px = max(1, line_width_px)

    return TEMPLATES[template_name].format(
        subject=subject,
        tags=tags_text,
        primary=primary,
        accent=accent,
        primary_name=_hex_to_name(primary) or "墨黑",
        accent_name=_hex_to_name(accent) or "朱砂",
        base_color=base_color,
        gen_size=gen_size,
        master_size=master_size,
        display_size=display_size,
        master_whitespace_px=master_whitespace_px,
        line_width_px=line_width_px,
        banned=" / ".join(BANNED_TERMS),
    )


# ---------------------------------------------------------------------------
# 校验
# ---------------------------------------------------------------------------

# 否定前缀：这些词紧挨在前面时，该出现属于"显式禁止"的写法，不算违规
#   例：「无渐变」「不透明阴影」「禁止渐变」都合法；「带渐变」不合法。
NEGATION_MARKERS = ("无", "不", "非", "免", "禁", "去", "杜绝", "拒绝", "不含", "不带")


def find_banned_terms(body):
    """返回 body 里出现的必排词（排除被否定前缀修饰的出现）。

    例：正文写「无渐变」不违规；正文写「柔和渐变」违规。
    """
    hits = []
    for term in BANNED_TERMS:
        start = 0
        while True:
            pos = body.find(term, start)
            if pos < 0:
                break
            prefix = body[max(0, pos - 3):pos]
            if not any(marker in prefix for marker in NEGATION_MARKERS):
                hits.append(term)
                break
            start = pos + len(term)
    return hits


def split_sections(prompt):
    """把提示词拆成 {段名: 正文}。"""
    sections = {}
    for chunk in prompt.split("【"):
        if not chunk.strip():
            continue
        name, _, body = chunk.partition("】")
        sections[name.strip()] = body.strip()
    return sections


def validate_prompt(prompt, template_name="ui_icon"):
    """返回 (是否通过, 问题列表)。"""
    problems = []
    sections = split_sections(prompt)

    # 1) 必含词
    for term in REQUIRED_TERMS:
        if term not in prompt:
            problems.append({
                "code": "AI-REQ",
                "message": "缺少必含词「%s」" % term,
                "fix": "把该词项补回模板（AD-03 §5 原文词项，不得删改）",
            })

    # 2) 必排词：只允许出现在【禁止】段
    for name, body in sections.items():
        if name == "禁止":
            continue
        for term in find_banned_terms(body):
            problems.append({
                "code": "AI-BAN",
                "message": "必排词「%s」出现在【%s】段" % (term, name),
                "fix": "从该段移除；它只允许出现在【禁止】段，"
                       "或写成「无X / 不带X」的显式禁止式",
            })

    # 3) 受控偏离：ui_icon 必须"透明 + 底色由面板提供"，且不得要求填死底
    if template_name == "ui_icon":
        if "底色由面板提供" not in prompt:
            problems.append({
                "code": "AI-DEV",
                "message": "ui_icon 模板缺少受控偏离项「底色由面板提供」",
                "fix": "按 AD-03 §5 补回受控偏离表述",
            })
        if re.search(r"宣纸米白底", prompt):
            problems.append({
                "code": "AI-DEV",
                "message": "ui_icon 模板出现 v2.0 的「宣纸米白底」",
                "fix": "改为「底色由面板提供、图标本体透明」",
            })
        if "透明背景" not in prompt:
            problems.append({
                "code": "AI-DEV",
                "message": "ui_icon 模板缺少「透明背景」",
                "fix": "按 AD-03 §5 补回",
            })

    # 4) 64px 专项约束必须齐全
    icon_section = None
    for name, body in sections.items():
        if name.endswith("px 附加约束"):
            icon_section = body
            break
    if icon_section is None:
        problems.append({
            "code": "AI-SIZE",
            "message": "缺少 64px 专项约束段",
            "fix": "补回【64px 附加约束】段",
        })
    else:
        required_64 = ["单色高对比", "无渐变", "灰度后轮廓仍可辨",
                       "≥4.5:1", "不得以颜色作唯一区分", "留白 ≥20%"]
        for term in required_64:
            if term not in icon_section:
                problems.append({
                    "code": "AI-SIZE",
                    "message": "64px 专项约束缺少「%s」" % term,
                    "fix": "按 AD-03 §5 补回该约束项",
                })

    return (len(problems) == 0), problems


def validate_spec(spec):
    """清单条目本身的字段校验（与提示词内容校验分开）。"""
    problems = []
    asset_id = str(spec.get("id", "")).strip()
    if not asset_id:
        problems.append({"code": "AI-ID", "message": "id 不能为空",
                         "fix": "填资产 ID（同时是输出文件名）"})
    elif not re.match(r"^[A-Za-z0-9][A-Za-z0-9_]*$", asset_id):
        problems.append({"code": "AI-ID",
                         "message": "id %r 含非法字符" % asset_id,
                         "fix": "只用字母、数字、下划线"})

    for field in ("primary", "accent"):
        value = str(spec.get(field, "")).strip().upper()
        if value and value not in PALETTE_HEX:
            problems.append({
                "code": "AI-COLOR",
                "message": "%s=%s 不在 6 色语义表内" % (field, value),
                "fix": "只能取 %s" % "、".join("%s %s" % (k, v)
                                             for k, v in PALETTE.items()),
            })

    for field in ("gen_size", "master_size", "display_size"):
        if field in spec and spec[field] in (None, ""):
            problems.append({"code": "AI-SIZE", "message": "%s 不能为空" % field,
                             "fix": "填整数边长"})
        else:
            try:
                value = int(spec.get(field, 0))
            except (TypeError, ValueError):
                problems.append({"code": "AI-SIZE",
                                 "message": "%s 不是整数" % field,
                                 "fix": "填整数边长"})
                continue
            if value <= 0:
                problems.append({"code": "AI-SIZE",
                                 "message": "%s 必须为正" % field,
                                 "fix": "填正整数边长"})

    # 标签必须来自受控词表
    tag_text = _tags_to_text(spec.get("tags", ""))
    for tag in [t for t in tag_text.split("、") if t]:
        if tag not in SEMANTIC_TAGS:
            problems.append({
                "code": "AI-TAG",
                "message": "语义标签「%s」不在受控词表内" % tag,
                "fix": "改用词表内的标签（词表见本文件 SEMANTIC_TAGS）",
            })

    return problems


# ---------------------------------------------------------------------------
# 清单读取
# ---------------------------------------------------------------------------

CSV_FIELDS = ["id", "system", "subject", "tags", "primary", "accent",
              "gen_size", "master_size", "display_size", "template"]


def load_manifest(path):
    """读 CSV 或 JSON 清单，返回 (记录列表, 问题列表)。"""
    problems = []
    records = []

    if not os.path.isfile(path):
        return [], [{"code": "AI-MANIFEST",
                     "message": "清单文件不存在：%s" % path,
                     "fix": "检查路径"}]

    ext = os.path.splitext(path)[1].lower()
    if ext == ".json":
        try:
            with open(path, "r", encoding="utf-8") as fh:
                data = json.load(fh)
        except (OSError, ValueError) as exc:
            return [], [{"code": "AI-MANIFEST",
                         "message": "JSON 清单读取失败：%r" % exc,
                         "fix": "修正 JSON 语法"}]
        if isinstance(data, dict):
            data = data.get("items", [])
        if not isinstance(data, list):
            return [], [{"code": "AI-MANIFEST",
                         "message": "JSON 清单必须是对象数组或含 items 数组",
                         "fix": "改成数组"}]
        for item in data:
            if not isinstance(item, dict):
                problems.append({"code": "AI-MANIFEST",
                                 "message": "JSON 清单里有非对象条目",
                                 "fix": "每条必须是一个对象"})
                continue
            records.append(item)
    elif ext == ".csv":
        try:
            with open(path, "r", encoding="utf-8-sig", newline="") as fh:
                reader = csv.DictReader(fh)
                if reader.fieldnames is None:
                    return [], [{"code": "AI-MANIFEST", "message": "CSV 没有表头",
                                 "fix": "首行写 %s" % ",".join(CSV_FIELDS)}]
                missing = [f for f in ("id", "subject", "tags") if f not in reader.fieldnames]
                if missing:
                    return [], [{"code": "AI-MANIFEST",
                                 "message": "CSV 缺少表头列：%s" % "、".join(missing),
                                 "fix": "首行写 %s" % ",".join(CSV_FIELDS)}]
                for row in reader:
                    records.append({k: (v if v is not None else "") for k, v in row.items()})
        except OSError as exc:
            return [], [{"code": "AI-MANIFEST",
                         "message": "CSV 读取失败：%r" % exc, "fix": "检查文件"}]
    else:
        return [], [{"code": "AI-MANIFEST",
                     "message": "清单格式不支持：%s（只认 .csv / .json）" % ext,
                     "fix": "改成 CSV 或 JSON"}]

    return records, problems


# ---------------------------------------------------------------------------
# 批量
# ---------------------------------------------------------------------------

def run_batch(manifest_path, out_dir, batch_id=None, check_only=False):
    """渲染 + 校验 + 落盘。返回 (是否通过, 报告)。"""
    records, problems = load_manifest(manifest_path)
    report = {
        "pipeline_version": PIPELINE_VERSION,
        "manifest": manifest_path,
        "batch_id": batch_id or os.path.splitext(os.path.basename(manifest_path))[0],
        "generated_at": datetime.now().strftime("%Y-%m-%d %H:%M:%S"),
        "items": [],
        "failures": list(problems),
        "controlled_deviation": CONTROLLED_DEVIATION,
    }

    for idx, spec in enumerate(records):
        item = {
            "index": idx,
            "id": str(spec.get("id", "")).strip(),
            "template": str(spec.get("template", "ui_icon")).strip(),
            "problems": [],
            "prompt": "",
        }

        item["problems"].extend(validate_spec(spec))

        try:
            prompt = render_prompt(spec)
        except ValueError as exc:
            item["problems"].append({
                "code": "AI-RENDER", "message": str(exc), "fix": "补齐清单字段"})
            report["items"].append(item)
            continue

        ok, prompt_problems = validate_prompt(prompt, item["template"])
        item["problems"].extend(prompt_problems)
        item["prompt"] = prompt
        report["items"].append(item)

    report["failed"] = sum(1 for i in report["items"] if i["problems"])
    report["passed"] = len(report["items"]) - report["failed"]

    if check_only or report["failed"] > 0:
        if not check_only:
            report["not_written_because_failed"] = True
        report["output_dir"] = None
        return report["failed"] == 0 and report["failed"] == 0, report

    # 落盘
    batch_dir = os.path.join(out_dir, report["batch_id"])
    prompts_dir = os.path.join(batch_dir, "prompts")
    if not os.path.isdir(prompts_dir):
        os.makedirs(prompts_dir)

    for item in report["items"]:
        txt_path = os.path.join(prompts_dir, "%s.txt" % item["id"])
        with open(txt_path, "w", encoding="utf-8") as fh:
            fh.write(item["prompt"])
            fh.write("\n")

    csv_path = os.path.join(batch_dir, "prompts.csv")
    with open(csv_path, "w", encoding="utf-8-sig", newline="") as fh:
        writer = csv.writer(fh)
        writer.writerow(["id", "template", "prompt"])
        for item in report["items"]:
            writer.writerow([item["id"], item["template"], item["prompt"]])

    manifest_path = os.path.join(batch_dir, "manifest.json")
    index = [{
        "id": i["id"],
        "template": i["template"],
        "prompt_file": "prompts/%s.txt" % i["id"],
    } for i in report["items"]]
    with open(manifest_path, "w", encoding="utf-8") as fh:
        json.dump({
            "pipeline_version": PIPELINE_VERSION,
            "batch_id": report["batch_id"],
            "generated_at": report["generated_at"],
            "source_manifest": os.path.basename(manifest_path)
            if False else os.path.basename(manifest_path),
            "item_count": len(report["items"]),
            "controlled_deviation": CONTROLLED_DEVIATION,
            "items": index,
        }, fh, ensure_ascii=False, indent=2)

    report["output_dir"] = batch_dir
    report["prompt_files"] = len(report["items"])
    return True, report


# ---------------------------------------------------------------------------
# 输出
# ---------------------------------------------------------------------------

def print_report(report):
    print("=" * 72)
    print("AI 提示词管线  v%s" % PIPELINE_VERSION)
    print("清单：%s" % report["manifest"])
    print("批次：%s" % report["batch_id"])
    print("受控偏离：%s" % CONTROLLED_DEVIATION["to"])
    print("-" * 72)
    for item in report["items"]:
        tag = "PASS" if not item["problems"] else "FAIL"
        print("[%s] #%d %s  (%s)" % (tag, item["index"], item["id"], item["template"]))
        for p in item["problems"]:
            print("        %-10s %s" % (p["code"], p["message"]))
            if p.get("fix"):
                print("                   修法：%s" % p["fix"])
    for p in report.get("failures", []):
        print("[FAIL] 清单 %-10s %s" % (p["code"], p["message"]))
    print("-" * 72)
    print("条目 %d：通过 %d，失败 %d"
          % (len(report["items"]), report["passed"], report["failed"]))
    if report.get("output_dir"):
        print("输出目录：%s" % report["output_dir"])
    print("=" * 72)


# ---------------------------------------------------------------------------
# 自检
# ---------------------------------------------------------------------------

def _self_test():
    problems = []

    # 1) 合规条目必须通过
    good = {
        "id": "UI_ICON_SKL_Pickup",
        "subject": "拾锋簇凝聚成的三叉锋簇",
        "tags": "锋簇,凝聚,悬停",
        "primary": "#1E1B18",
        "accent": "#C0392B",
        "template": "ui_icon",
    }
    prompt = render_prompt(good)
    ok, probs = validate_prompt(prompt, "ui_icon")
    if not ok:
        problems.append("合规条目被判失败：%s" % probs)

    # 2) 受控偏离必须体现在模板里
    if "底色由面板提供" not in prompt:
        problems.append("受控偏离「底色由面板提供」未落到模板")
    if "宣纸米白底" in prompt:
        problems.append("ui_icon 模板里出现了 v2.0 的「宣纸米白底」")
    if "宣纸米白" not in prompt:
        problems.append("宣纸米白（对比基准）被误删")

    # 3) 必含词齐全
    for term in REQUIRED_TERMS:
        if term not in prompt:
            problems.append("模板缺少必含词「%s」" % term)

    # 4) 必排词只允许在【禁止】段
    sections = split_sections(prompt)
    for name, body in sections.items():
        if name == "禁止":
            continue
        for term in find_banned_terms(body):
            problems.append("必排词「%s」泄漏到【%s】段" % (term, name))

    # 5) 越界词必须被抓到
    bad_prompt = prompt.replace("【禁止】", "【形态语言】渐变\n【禁止】", 1)
    ok, probs = validate_prompt(bad_prompt, "ui_icon")
    if ok:
        problems.append("越界的必排词没有被抓到（校验失效）")

    # 6) 非法颜色必须被抓到
    probs = validate_spec({"id": "UI_ICON_X", "subject": "s", "tags": "锋域",
                           "primary": "#123456"})
    if not any(p["code"] == "AI-COLOR" for p in probs):
        problems.append("非法颜色没有被抓到")

    # 7) 非法标签必须被抓到
    probs = validate_spec({"id": "UI_ICON_X", "subject": "s", "tags": "外星人"})
    if not any(p["code"] == "AI-TAG" for p in probs):
        problems.append("非法语义标签没有被抓到")

    if problems:
        for p in problems:
            sys.stderr.write("[self-test] FAIL %s\n" % p)
        return 1

    print("self-test OK：模板合规、受控偏离生效、必含/必排词校验有效、"
          "颜色与标签越界可被抓出")
    return 0


# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------

def main(argv=None):
    parser = argparse.ArgumentParser(
        description="EdgeChronicles AI 生成提示词管线（失败即非零退出码）")
    parser.add_argument("--manifest", help="清单路径（.csv / .json）")
    parser.add_argument("--out", help="输出根目录（默认 Assets/_Project/Art/Source/AI）")
    parser.add_argument("--batch-id", help="批次 ID（默认取清单文件名）")
    parser.add_argument("--template", default="ui_icon", help="模板名（ui_icon / generic）")
    parser.add_argument("--subject", help="主体描述")
    parser.add_argument("--tags", help="语义标签，逗号分隔")
    parser.add_argument("--primary", default=PALETTE["墨黑"], help="主色")
    parser.add_argument("--accent", default=PALETTE["朱砂"], help="强调色")
    parser.add_argument("--gen-size", type=int, default=1024, help="生成图边长")
    parser.add_argument("--master-size", type=int, default=128, help="母版边长")
    parser.add_argument("--display-size", type=int, default=64, help="显示边长")
    parser.add_argument("--id", dest="asset_id", help="资产 ID（单条模式）")
    parser.add_argument("--check", action="store_true", help="只校验不落盘")
    parser.add_argument("--json", action="store_true", help="以 JSON 输出报告")
    parser.add_argument("--self-test", action="store_true", help="跑内置自检")
    args = parser.parse_args(argv)

    if args.self_test:
        return _self_test()

    try:
        if args.manifest:
            out_dir = args.out or os.path.join("Assets", "_Project", "Art", "Source", "AI")
            ok, report = run_batch(args.manifest, out_dir,
                                   batch_id=args.batch_id,
                                   check_only=args.check)
            if args.json:
                print(json.dumps(report, ensure_ascii=False, indent=2))
            else:
                print_report(report)
            return 0 if ok else 1

        if args.subject and args.tags:
            spec = {
                "id": args.asset_id or "UI_ICON_SINGLE",
                "subject": args.subject,
                "tags": args.tags,
                "primary": args.primary,
                "accent": args.accent,
                "gen_size": args.gen_size,
                "master_size": args.master_size,
                "display_size": args.display_size,
                "template": args.template,
            }
            problems = validate_spec(spec)
            try:
                prompt = render_prompt(spec)
            except ValueError as exc:
                problems.append({"code": "AI-RENDER", "message": str(exc),
                                 "fix": "补齐参数"})
                prompt = ""
            ok, prompt_problems = validate_prompt(prompt, args.template) if prompt else (False, [])
            problems.extend(prompt_problems)

            report = {
                "pipeline_version": PIPELINE_VERSION,
                "items": [{"index": 0, "id": spec["id"], "template": args.template,
                           "problems": problems, "prompt": prompt}],
                "failures": [],
                "batch_id": args.batch_id or "SINGLE",
                "manifest": None,
                "passed": 0 if problems else 1,
                "failed": 1 if problems else 0,
                "controlled_deviation": CONTROLLED_DEVIATION,
                "output_dir": None,
            }

            if problems and args.check:
                if args.json:
                    print(json.dumps(report, ensure_ascii=False, indent=2))
                else:
                    print_report(report)
                return 1

            out_dir = args.out or os.path.join("Assets", "_Project", "Art", "Source", "AI")
            batch_dir = os.path.join(out_dir, report["batch_id"])
            prompts_dir = os.path.join(batch_dir, "prompts")
            if not os.path.isdir(prompts_dir):
                os.makedirs(prompts_dir)
            with open(os.path.join(prompts_dir, "%s.txt" % spec["id"]),
                      "w", encoding="utf-8") as fh:
                fh.write(prompt)
                fh.write("\n")
            report["output_dir"] = batch_dir

            if args.json:
                print(json.dumps(report, ensure_ascii=False, indent=2))
            else:
                print_report(report)
                print(prompt)
            return 0 if not problems else 1

        parser.print_help()
        sys.stderr.write("\n[ai_prompt] 必须给 --manifest，或同时给 --subject 与 --tags\n")
        return 2

    except Exception as exc:                      # noqa: BLE001 - 顶层兜底，必须非零退出
        sys.stderr.write("[ai_prompt] 内部错误：%r\n" % exc)
        return 2


if __name__ == "__main__":
    sys.exit(main())
