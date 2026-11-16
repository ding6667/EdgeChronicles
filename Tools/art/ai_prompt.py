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
# 必含词
#   FORM_TERMS = 形态语言共用词项（AD-03 §5 原文词项，2D / 3D 共用）
#   TEMPLATE_REQUIRED = 按模板取的必含词全集（**按模板分档，不混用**）
#     为什么必须分档：2D 的「透明背景 / 留白 ≥20% / 64px 附加约束」对 3D 网格无意义；
#     3D 的「水密 / 四边面 / 三通道打包」对图标无意义。用同一份必含词会逼出一句
#     「3D 模型要有透明背景」这种互相打脸的提示词。
# ---------------------------------------------------------------------------
FORM_TERMS = [
    "墨线低模",
    "硬边水墨",
    "木版硬边",
    "低多边形",
    "硬边平涂",
    "纯墨线轮廓",
    "3px 等宽",
    "单色高对比",
]

# 2D（UI 图标 / 通用物件）专属必含词
REQUIRED_TERMS_2D = FORM_TERMS + [
    "水墨留白",
    "宣纸米白",
    "色块 ≤8",
    "留白 ≥20%",
    "透明背景",
]

# 3D 模型专属必含词（水密 / 四边面 / 顶点色 / 三通道打包 是 3D 链路才有的判据）
REQUIRED_TERMS_3D = FORM_TERMS + [
    "色块 ≤8",
    "宣纸米白",
    "水密",
    "四边面",
    "顶点色",
    "三通道打包",
]

# 3D 贴图源图专属必含词（模型贴图固定 R=AO G=Rough B=Emissive、无 alpha）
#   「硬边平涂」在 3D 贴图上不是修辞：块面内取值恒定、块面边界为硬边，
#   因此 Roughness 图是**分块常量 + 硬边界**，AO 图也只沿块面缝隙加深。
#   四张源图与形态语言同源，故形态词项同样适用于 AO / Rough / Emissive。
REQUIRED_TERMS_TEX = FORM_TERMS + [
    "色块 ≤8",
    "宣纸米白",
    "无 alpha",
    "R=AO",
    "G=Rough",
    "B=Emissive",
    "8bit",
]

# 向后兼容别名：本文件早期版本只有一个 REQUIRED_TERMS（= 2D 口径）
REQUIRED_TERMS = REQUIRED_TERMS_2D

REQUIRED_BY_TEMPLATE = {
    "ui_icon": REQUIRED_TERMS_2D,
    "generic": REQUIRED_TERMS_2D,
    "model_3d": REQUIRED_TERMS_3D,
    "tex_src_3d": REQUIRED_TERMS_TEX,
}

# 各模板「附加约束」段内必须出现的专项判据（分段名以「附加约束」结尾）
SECTION_REQUIRED_BY_TEMPLATE = {
    "ui_icon": ["单色高对比", "无渐变", "灰度后轮廓仍可辨",
                "≥4.5:1", "不得以颜色作唯一区分", "留白 ≥20%"],
    "generic": ["单色高对比", "无渐变", "灰度后轮廓仍可辨",
                "≥4.5:1", "不得以颜色作唯一区分", "留白 ≥20%"],
    "model_3d": ["三角面 ≤", "水密", "四边面", "材质槽 1",
                 "无 alpha", "顶点色", "±10%", "折角"],
    "tex_src_3d": ["无 alpha", "8bit", "RGB", "三通道"],
}

# 3D 硬边 / 平滑的折角阈值（可由模板参数覆写）
#   依据：描边通道的内部结构线由**视空间法线的 2px 邻域差**产生，判据在
#   ECOutlineComposite.shader：edge = 1 - dot(nC, nN)，_ECOutlineEdgeThreshold = 0.35。
#   1 - cos(49.46°) = 0.35 -> 折角 ≥49.46° 才开始出现内部墨线。
#   故硬边阈值取 50°（正好落在描边开始画线的位置），平滑阈值取 30°，
#   30°–50° 之间留 20° 的禁区（防止建模误差把折角在阈值两侧来回翻）。
EDGE_HARD_DEG = 50
EDGE_SMOOTH_DEG = 30

# ---------------------------------------------------------------------------
# 3D 分类表（**taxonomy 不许写死**）
# ---------------------------------------------------------------------------
#   历史教训：本文件曾把 taxonomy 默认值写成 "Boss"、把模板里的名词写死成「敌体」，
#   结果主角 CH_HERO_PickBlade 被渲染成「一个 Boss 敌体：…」——**主角按 Boss 的体量与
#   气质进文生 3D**，第一条资产就被污染。所以这里：
#     ① 词条从清单的 category 列读，**缺了直接报错**，不给静默默认值；
#     ② 「该元素是它与另两个敌体的唯一可区分依据」这句话对主角**不成立**
#        （主角没有"另两个敌体"），按分类条件化，见 silhouette_clause。
#   分类词只描述「体量与身份」，不新增色相、不改体型比。
TAXONOMY = {
    "主角": {
        "label": "主角",
        "silhouette_clause": "这是玩家全程看到最多的剪影，必须一眼记住，"
                             "不得靠颜色或光环提醒身份",
    },
    "Boss": {
        "label": "Boss 敌体",
        "silhouette_clause": "该元素是它与另两个 Boss 的唯一可区分依据",
    },
    "精英": {
        "label": "精英敌体",
        "silhouette_clause": "该元素是它与同副本小怪的唯一可区分依据",
    },
    "小怪": {
        "label": "小怪",
        "silhouette_clause": "同族仅靠形状可区分，不得以颜色作唯一区分",
    },
}

# 不接受「敌体」二字的分类（主角是玩家角色，不是敌人）
NON_ENEMY_CATEGORIES = ("主角",)

# ---------------------------------------------------------------------------
# 必排词（AD-03 §5 原词项）
#   校验口径：这些词**只允许**出现在【禁止】段；正文任何一段出现即判失败。
# ---------------------------------------------------------------------------
BANNED_TERMS = [
    "写实", "PBR", "毛发", "渐变", "景深", "模糊", "噪点", "阴影", "反射",
    "透视", "文字", "数字", "水印", "签名", "多主体", "复杂背景", "贴图",
    "圆角", "光晕", "体积光",
]

# 按模板豁免的必排词（**只对新增的 3D 模板生效，2D 模板口径一字不改**）
#   「贴图」：2D 里禁它是为了防止图标被画成贴图质感；到了 3D 链路，
#   「贴图」是**产物类别名**（模型贴图 1024 三通道打包），禁掉会让提示词
#   无法描述交付物。其余必排词在 3D 模板里仍然全部生效。
def _banned_exempt_terms(template_name):
    """3D 模板的必排词豁免集合。2D 模板返回空集（口径不变）。"""
    if template_name in ("model_3d", "tex_src_3d"):
        return frozenset({"贴图"})
    return frozenset()

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
# 3D 受控标签词表（**与 2D 的 SEMANTIC_TAGS 分开**）
#   为什么不并进上面那张表：2D 标签是「图标上画什么」，3D 标签是「模型长什么样」。
#   并表会让 3D 提示词被迫用「面板底 / 页签条 / 满级」这类与几何无关的词，
#   也会让 2D 图标混进「盔甲 / 长臂 / 炉口」这类只在三维里有意义的词。
#   两张表都只描述**6 色语义表之内**的造型，不引入新色相。
# ---------------------------------------------------------------------------
SEMANTIC_TAGS_3D = [
    # 体型与骨架
    "人形", "双足", "直立", "宽肩", "窄腰", "长臂", "单臂刃", "重甲", "轻甲",
    "布衣", "斗笠", "披风", "束带", "护腕", "胫甲",
    # 主体造型
    "胸腔", "炉口", "肩甲", "碑形", "刀身", "刃器", "刀柄", "刀镡",
    "头冠", "面罩", "腰带", "靴", "断口", "裂纹", "刻纹", "铆钉", "钩扣",
    # 姿态与动作（静态造型用）
    "前倾", "后仰", "蹲踞", "开腿站", "举刃", "拖刃", "抱臂", "垂手",
    # 平涂与描边（对齐形态语言）
    "硬边", "平涂", "块面", "墨线", "留白", "剪影", "低模", "四边面",
    # 顶点色承载（副本差异的唯一手段）
    "顶点色", "色块",
]

# 3D 模板受控标签全集 = 2D 表 + 3D 表（3D 模型提示词也允许用 2D 的通用造型词）
SEMANTIC_TAGS_ALL = SEMANTIC_TAGS + SEMANTIC_TAGS_3D

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

# ---------------------------------------------------------------------------
# 3D 模板一：文生 3D 模型（text-to-3D / image-to-3D）
#   与 2D 模板的根本差别：2D 出的是**图像**，3D 出的是**要被重拓扑与绑定的网格**。
#   故本模板的每一段都对应建模管线里的一个真实闸口（见 modeling-pipeline.md §1）：
#     【主体】【形态语言】 -> 造型与风格
#     【独占剪影】          -> 3 Boss 的**唯一可区分依据**（不许各写一份新造型）
#     【体型与缩放】        -> 体型比 1 : 1.4 : 2.2 与 2 套 rig 的复用前提
#     【拓扑】              -> 重拓扑目标（四边面 / 密度 / 水密）
#     【法线与硬边】        -> 描边通道的内部结构线判据（折角阈值）
#     【材质与顶点色】      -> 材质槽 1 与顶点色 ±10%
#     【禁止】              -> 必排词 + 3D 专属禁止（巨型/爬行/多足）
#     【交付】              -> .fbx + sidecar 的实际字段
# ---------------------------------------------------------------------------
TEMPLATE_MODEL_3D = """【主体】一个 {taxonomy_label}：{subject}，{tags}；**独占剪影元素 = {silhouette}**，{silhouette_clause}，缩到 64px 黑白剪影后仍必须一眼可辨
【形态语言】墨线低模·硬边水墨；木版硬边；低多边形；硬边平涂；纯墨线轮廓 3px 等宽；单色高对比；色块 ≤8；只从 6 色语义表取 3 色 —— {primary_name} {primary} / {accent_name} {accent} / 宣纸米白 {base_color}，不引入新色相
【体型与缩放】{scale_note}；体型比 小怪 1 : 精英 1.4 : Boss 2.2（{body_ratio}）；**双足直立人形，禁巨型 / 禁爬行 / 禁多足**
【拓扑】单一水密网格，**四边面**为主（四边形 ≥90%，三角形仅用于无可避免的收口）；关节处预留至少 2 圈环状边用于蒙皮；平涂块面 ≤{face_block_budget} 块
【法线与硬边】折角 ≥{edge_hard_deg}° 一律硬边（法线断开），折角 ≤{edge_smooth_deg}° 一律平滑（法线连续），{edge_smooth_deg}°–{edge_hard_deg}° 之间不新建折角；**不得用平滑着色掩盖造型**（描边通道按法线差画内部结构线，平滑掉折角 = 内部墨线消失）
【材质与顶点色】材质槽 1（唯一），全部表面由**顶点色**承载明暗与块面区分；顶点色与 {primary_name} {primary} / {accent_name} {accent} 的偏差 ≤±10%；贴图走 {tex_size}×{tex_size} 三通道打包（R=AO G=Rough B=Emissive，**无 alpha**），不在本步生成
【禁止】{banned} / 巨型 / 爬行 / 多足 / 触手 / 分层衣裙 / 毛发碎片
【{tri_limit} 三角面附加约束】三角面 ≤{tri_limit}（目标 {tri_target}）；**必须水密**（无非流形边、无浮空碎块、无内部不可见面）；**必须四边面**为主；**材质槽 1**；**无 alpha**（模型贴图无 alpha 通道）；**顶点色存在且偏差 ≤±10%**；**折角 ≥{edge_hard_deg}° 建硬边**
【交付】单一网格 .fbx（Y 轴向上、正面朝 -Z、变换归零、原点在脚底中点）+ 同名 .meta.json（tri_count / material_slots=1 / textures / channels="R=AO;G=Rough;B=Emissive" / origin=ai / generation_record）；**保留生成原图与本次提示词全文**"""

# ---------------------------------------------------------------------------
# 3D 模板二：模型贴图源图（四张源图分别生成，再打包成一张三通道图）
#   为什么必须分四张：AI 生成不出「打包图」——它只会画一张看着好看的 RGB。
#   正确做法是**一张语义一张**：BaseColor（虽然最终不入包，但顶点色/基础色要它定调）、
#   AO、Rough、Emissive，人工核对后用打包脚本按通道拼成
#   1024×1024 RGB 无 alpha 的 TX_*。打包顺序 R=AO, G=Rough, B=Emissive 是硬约定。
# ---------------------------------------------------------------------------
TEMPLATE_TEX_SRC_3D = """【主体】{subject} 的**{tex_channel}**贴图源图：{tex_role}
【形态语言】墨线低模·硬边水墨；木版硬边；低多边形；硬边平涂；纯墨线轮廓 3px 等宽；单色高对比；色块 ≤8
【色彩】{tex_palette}；整图色块 ≤8；仅许宣纸米白 {base_color} 与 {primary_name} {primary} / {accent_name} {accent} 的明度派生，不引入新色相
【构图】{tex_size}×{tex_size} 正方形，**背靠背平铺（UV 布局按 modeling-pipeline.md §2 的固定分块）**；块与块之间留 ≥4px 的纯色隔离带；不画透视、不画光照方向
【线与面】块面内取值恒定（**硬边平涂**），块面边界为硬边（≤2px 过渡）；无模糊过渡、无渐变、无噪点；块面数 ≤{tex_block_budget}
【禁止】{banned} / 高光 / 环境光遮蔽以外的接触暗部 / 方向性受光
【{tex_size} 附加约束】**{tex_bit_depth}bit 位深**；**{tex_channel_count}；无 alpha**；{tex_grayscale_note}；输出格式 PNG（**三通道打包时 R=AO，G=Rough，B=Emissive**，三张源图必须同尺寸、同 UV 布局、逐块对齐）
【交付】单张 {tex_size}×{tex_size} PNG，命名为 TX_{asset_id}_{tex_channel_tag}_SRC.png（源图，存 Source/）；打包后的成品命名为 TX_{asset_id}.png 并附 sidecar，channels = "R=AO;G=Rough;B=Emissive"""

TEMPLATES = {
    "ui_icon": TEMPLATE_UI_ICON,
    "generic": TEMPLATE_GENERIC,
    "model_3d": TEMPLATE_MODEL_3D,
    "tex_src_3d": TEMPLATE_TEX_SRC_3D,
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


def _text_extra_tags_ok():
    """3D 模板的额外受控参数（默认值集中在这里，便于自检与文档对齐）。

    **taxonomy 不在这里给默认值**：它必须由清单的 category 列派生。
    给默认值就等于给了一个静默的错误起点（曾经默认成 "Boss"，把主角写成 Boss 敌体）。
    """
    return {
        # 模型
        "silhouette": "炉口外张的胸腔",
        "scale_note": "网格以 1.0× 基准身高建模",
        "body_ratio": "本件 2.2×，为上限",
        "tri_target": 7500,
        "tri_limit": 15000,
        "edge_hard_deg": EDGE_HARD_DEG,
        "edge_smooth_deg": EDGE_SMOOTH_DEG,
        "face_block_budget": 24,
        # 贴图源图
        "tex_channel": "基础色",
        "tex_role": "块面明度分布，供人工核对与顶点色采样",
        "tex_size": 1024,
        "tex_palette": "单色块面 + 至多 1 个强调块",
        "tex_block_budget": 12,
        "tex_bit_depth": 8,
        "tex_channel_count": "RGB 三通道",
        "tex_grayscale_note": "AO / Rough 若单独生成则用单通道灰度语义，打包时复制到 R / G",
        "asset_id": "CH_BOSS_FurnaceThroat",
        "tex_channel_tag": "Base",
    }


def resolve_taxonomy(spec):
    """从清单的 category 列解出 (label, silhouette_clause)。**缺失即抛错，不给默认值。**

    分类口径见本文件 TAXONOMY；不在表内的分类一律报错，
    不"就近猜一个"——猜错就是把主角按 Boss 送进生成器的那类事故。
    """
    raw = spec.get("taxonomy")
    if raw is None or str(raw).strip() == "":
        raw = spec.get("category")
    if raw is None or str(raw).strip() == "":
        raise ValueError(
            "model_3d 必须声明 category（%s 之一）；"
            "本字段曾经有写死的默认值 'Boss'，导致主角被渲染成 Boss 敌体"
            % "、".join(TAXONOMY))
    key = str(raw).strip()
    if key not in TAXONOMY:
        raise ValueError("未知 category %r（只认 %s）"
                         % (key, "、".join(TAXONOMY)))
    entry = TAXONOMY[key]
    return entry["label"], entry["silhouette_clause"]


def _render_2d(spec, common):
    return TEMPLATES[str(spec.get("template", "ui_icon")).strip()].format(**common)


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

    # 3D 模板的额外字段（2D 模板不会用到，但 format 需要全部键存在）
    extra = dict(_text_extra_tags_ok())
    for key in list(extra.keys()):
        if key in spec and str(spec[key]).strip() != "":
            extra[key] = spec[key]
    # 数值型字段强制转 int（CSV 读进来是字符串）
    for key in ("tri_target", "tri_limit",
                "edge_hard_deg", "edge_smooth_deg", "tex_size",
                "tex_block_budget", "face_block_budget"):
        try:
            extra[key] = int(extra[key])
        except (TypeError, ValueError):
            raise ValueError("%s 必须是整数（当前 %r）" % (key, extra[key]))

    # 分类：**必须从 category 派生，不许写死**（见 resolve_taxonomy 的教训注释）
    if template_name == "model_3d":
        label, clause = resolve_taxonomy(spec)
        extra["taxonomy_label"] = label
        extra["silhouette_clause"] = clause

    fmt = {
        "subject": subject,
        "tags": tags_text,
        "primary": primary,
        "accent": accent,
        "primary_name": _hex_to_name(primary) or "墨黑",
        "accent_name": _hex_to_name(accent) or "朱砂",
        "base_color": base_color,
        "gen_size": gen_size,
        "master_size": master_size,
        "display_size": display_size,
        "master_whitespace_px": master_whitespace_px,
        "line_width_px": line_width_px,
        "banned": " / ".join(_banned_terms_for(template_name)),
    }
    fmt.update(extra)
    return TEMPLATES[template_name].format(**fmt)


def _banned_terms_for(template_name):
    """该模板生效的必排词（剔除豁免项）。"""
    exempt = _banned_exempt_terms(template_name)
    return [t for t in BANNED_TERMS if t not in exempt]


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

    # 1) 必含词（**按模板分档**：3D 模板不要求「透明背景」，2D 模板不要求「水密」）
    required = REQUIRED_BY_TEMPLATE.get(template_name, REQUIRED_TERMS_2D)
    for term in required:
        if term not in prompt:
            problems.append({
                "code": "AI-REQ",
                "message": "缺少必含词「%s」（模板 %s）" % (term, template_name),
                "fix": "把该词项补回模板（原文词项，不得删改）",
            })

    # 2) 必排词：只允许出现在【禁止】段；按模板剔除豁免项
    exempt = _banned_exempt_terms(template_name)
    for name, body in sections.items():
        if name == "禁止":
            continue
        for term in find_banned_terms(body):
            if term in exempt:
                continue
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

    # 4) 附加约束段（段名以「附加约束」结尾）的专项判据必须齐全
    constraint_section = None
    constraint_name = ""
    for name, body in sections.items():
        if name.endswith("附加约束"):
            constraint_section = body
            constraint_name = name
            break
    if constraint_section is None:
        problems.append({
            "code": "AI-SIZE",
            "message": "缺少专项约束段（段名以「附加约束」结尾）",
            "fix": "补回【…附加约束】段",
        })
    else:
        for term in SECTION_REQUIRED_BY_TEMPLATE.get(template_name, []):
            if term not in constraint_section:
                problems.append({
                    "code": "AI-SIZE",
                    "message": "【%s】缺少「%s」" % (constraint_name, term),
                    "fix": "按模板补回该约束项",
                })

    # 5) 3D 模型的独占剪影元素必须**具名写进**提示词（它是 3 Boss 唯一可区分依据）
    if template_name == "model_3d":
        if "独占剪影元素" not in prompt:
            problems.append({
                "code": "AI-SIL",
                "message": "model_3d 模板缺少「独占剪影元素」声明",
                "fix": "补回【主体】段里的独占剪影元素（它是 Boss 唯一的可区分依据）",
            })
        if "禁巨型" not in prompt or "禁爬行" not in prompt or "禁多足" not in prompt:
            problems.append({
                "code": "AI-SIL",
                "message": "model_3d 模板缺少「禁巨型 / 禁爬行 / 禁多足」",
                "fix": "GDD §8.1 剪影铁律：非人形敌人必须单独报价，不占 MVP 预算",
            })
        if "2.2" not in prompt:
            problems.append({
                "code": "AI-SIL",
                "message": "model_3d 模板缺少体型比 2.2 上限",
                "fix": "补回「小怪 1 : 精英 1.4 : Boss 2.2」",
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

    # 尺寸字段：**只判清单里真的声明了的**（3D 清单没有母版/显示尺寸，用模板默认值）
    for field in ("gen_size", "master_size", "display_size"):
        if field not in spec:
            continue
        if spec[field] in (None, ""):
            problems.append({"code": "AI-SIZE", "message": "%s 不能为空" % field,
                             "fix": "填整数边长"})
            continue
        try:
            value = int(spec[field])
        except (TypeError, ValueError):
            problems.append({"code": "AI-SIZE",
                             "message": "%s 不是整数" % field,
                             "fix": "填整数边长"})
            continue
        if value <= 0:
            problems.append({"code": "AI-SIZE",
                             "message": "%s 必须为正" % field,
                             "fix": "填正整数边长"})

    # 标签必须来自受控词表（**按模板选表**：3D 用 2D+3D 的并集，2D 只用 2D 表）
    template_name = str(spec.get("template", "ui_icon")).strip()
    allowed_tags = SEMANTIC_TAGS_ALL if template_name in ("model_3d", "tex_src_3d") \
        else SEMANTIC_TAGS
    tag_text = _tags_to_text(spec.get("tags", ""))
    for tag in [t for t in tag_text.split("、") if t]:
        if tag not in allowed_tags:
            problems.append({
                "code": "AI-TAG",
                "message": "语义标签「%s」不在受控词表内（模板 %s）"
                           % (tag, template_name),
                "fix": "改用词表内的标签（2D 见 SEMANTIC_TAGS，3D 另见 SEMANTIC_TAGS_3D）",
            })

    # 3D 模板的数值字段越界检查
    if template_name == "model_3d":
        # 分类必须可解（写死默认值曾把主角渲染成 Boss 敌体，故这里前置拦截）
        try:
            resolve_taxonomy(spec)
        except ValueError as exc:
            problems.append({
                "code": "AI-TAX",
                "message": str(exc),
                "fix": "在清单的 category 列填 %s 之一" % "、".join(TAXONOMY),
            })

        try:
            tri_limit = int(spec.get("tri_limit", 15000))
            tri_target = int(spec.get("tri_target", 0))
        except (TypeError, ValueError):
            problems.append({"code": "AI-TRI",
                             "message": "tri_limit / tri_target 必须是整数",
                             "fix": "填整数三角面数"})
        else:
            if tri_limit > 15000:
                problems.append({
                    "code": "AI-TRI",
                    "message": "tri_limit=%d 超过角色面数红线 15000" % tri_limit,
                    "fix": "Boss / 主角一律 ≤15000 三角面（GDD §8.1 硬条款 ② / gate.py G1）",
                })
            if tri_target > tri_limit:
                problems.append({
                    "code": "AI-TRI",
                    "message": "tri_target=%d 不能超过 tri_limit=%d"
                               % (tri_target, tri_limit),
                    "fix": "目标值必须小于上限（留出重拓扑误差余量）",
                })
    return problems


# ---------------------------------------------------------------------------
# 清单读取
# ---------------------------------------------------------------------------

CSV_FIELDS = ["id", "system", "subject", "tags", "primary", "accent",
              "gen_size", "master_size", "display_size", "template"]

# 3D 清单的可选列（缺列时用模板默认值；有列时必须合法）
#   category 是**必填**的（taxonomy 由它派生）；taxonomy 若出现则优先采信（别名）
CSV_FIELDS_3D_OPTIONAL = [
    "taxonomy", "silhouette", "scale_note", "body_ratio",
    "tri_target", "tri_limit", "edge_hard_deg", "edge_smooth_deg",
    "rig", "rig_scale", "uv_template", "output", "sidecar",
]


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

    # ------------------------------------------------------------------
    # 8) 3D 模板：合规的 Boss 条目必须通过
    # ------------------------------------------------------------------
    boss = {
        "id": "CH_BOSS_FurnaceThroat",
        "category": "Boss",
        "subject": "铁砧窟守关者：胸腔前壁裂开成一个向外张开的炉口",
        "tags": "人形,双足,直立,重甲,宽肩,胸腔,炉口,块面",
        "primary": "#8C5A3B",
        "accent": "#C0392B",
        "template": "model_3d",
        "silhouette": "炉口外张的胸腔",
        "body_ratio": "本件 2.2×，为上限",
        "tri_target": 7500,
        "tri_limit": 15000,
    }
    p3d = render_prompt(boss)
    ok, probs = validate_prompt(p3d, "model_3d")
    if not ok:
        problems.append("合规 3D 条目被判失败：%s" % probs)

    if "Boss 敌体" not in p3d:
        problems.append("category=Boss 的词条没有渲染成「Boss 敌体」")
    if "另两个 Boss" not in p3d:
        problems.append("category=Boss 未带上「与另两个 Boss 的唯一可区分依据」这句")

    # ------------------------------------------------------------------
    # 8b) **回归断言：主角不得被渲染成 Boss 敌体**
    #     这是曾经的真实 bug（taxonomy 写死成 "Boss"），必须有断言钉住
    # ------------------------------------------------------------------
    hero = dict(boss, id="CH_HERO_PickBlade", category="主角",
                subject="拾锋门刀客：布衣双足直立，单手垂持一把断锋残刀",
                silhouette="单手垂持的断锋残刀")
    phero = render_prompt(hero)
    if "Boss" in phero.split("\n")[0]:
        problems.append("主角提示词首行出现了「Boss」——taxonomy 写死的 bug 回归了")
    if "敌体" in phero:
        problems.append("主角提示词出现了「敌体」——主角不是敌人")
    if "主角" not in phero.split("\n")[0]:
        problems.append("主角提示词首行未体现「主角」分类")
    if "另两个 Boss" in phero:
        problems.append("主角提示词错误地带上了「另两个 Boss」的句子")
    if "玩家全程看到最多的剪影" not in phero:
        problems.append("主角提示词缺少主角专属的剪影约束句")

    # 缺 category 必须报错（不给静默默认值）
    probs = validate_spec({k: v for k, v in boss.items() if k != "category"})
    if not any(p["code"] == "AI-TAX" for p in probs):
        problems.append("缺 category 时没有报 AI-TAX（默认值会把主角写成 Boss）")

    # 3D 必含词齐全，且不要求 2D 专属词（透明背景 / 留白 ≥20%）
    for term in REQUIRED_TERMS_3D:
        if term not in p3d:
            problems.append("model_3d 缺少必含词「%s」" % term)
    for term in ("透明背景", "留白 ≥20%"):
        if term in p3d:
            problems.append("model_3d 混入了 2D 专属词「%s」" % term)

    # 独占剪影元素必须具名写进提示词（3 Boss 的唯一可区分依据）
    if "独占剪影元素" not in p3d or "炉口外张的胸腔" not in p3d:
        problems.append("model_3d 未把独占剪影元素写进提示词")

    # 必排词在 3D 正文档仍须受约束（除「贴图」豁免外）
    for name, body in split_sections(p3d).items():
        if name == "禁止":
            continue
        for term in find_banned_terms(body):
            if term in _banned_exempt_terms("model_3d"):
                continue
            problems.append("3D 必排词「%s」泄漏到【%s】段" % (term, name))

    # 3D 越界必排词必须被抓到
    bad3d = p3d.replace("【拓扑】", "【拓扑】渐变\n", 1)
    ok, _probs = validate_prompt(bad3d, "model_3d")
    if ok:
        problems.append("3D 越界必排词没有被抓到（校验失效）")

    # 面数越界必须被抓到
    probs = validate_spec(dict(boss, tri_limit=30000))
    if not any(p["code"] == "AI-TRI" for p in probs):
        problems.append("3D 面数越界没有被抓到")

    # 3D 标签越界必须被抓到
    probs = validate_spec(dict(boss, tags="外星人"))
    if not any(p["code"] == "AI-TAG" for p in probs):
        problems.append("3D 非法标签没有被抓到")

    # 2D 标签表不得污染 3D：2D 模板仍不认 3D 专属标签（如「炉口」）
    probs = validate_spec({"id": "UI_ICON_X", "subject": "s", "tags": "炉口"})
    if not any(p["code"] == "AI-TAG" for p in probs):
        problems.append("2D 模板错误地接受了 3D 专属标签")

    # ------------------------------------------------------------------
    # 9) 3D 贴图源图模板：合规必须通过
    # ------------------------------------------------------------------
    tex = {
        "id": "TX_Hero_Body_AO",
        "subject": "主角 CH_HERO_PickBlade 的躯干与护腕",
        "tags": "块面,硬边,平涂,高模细节",
        "primary": "#1E1B18",
        "accent": "#3E6B78",
        "template": "tex_src_3d",
        "tex_channel": "AO",
        "tex_role": "块面缝隙与接缝的接触暗部，供打包到 R 通道",
        "tex_size": 1024,
        "asset_id": "Hero_Body",
        "tex_channel_tag": "AO",
    }
    ptex = render_prompt(tex)
    ok, probs = validate_prompt(ptex, "tex_src_3d")
    if not ok:
        problems.append("合规贴图源图条目被判失败：%s" % probs)

    for term in REQUIRED_TERMS_TEX:
        if term not in ptex:
            problems.append("tex_src_3d 缺少必含词「%s」" % term)
    for kv in ("R=AO", "G=Rough", "B=Emissive", "无 alpha"):
        if kv not in ptex:
            problems.append("tex_src_3d 缺少打包口径「%s」" % kv)

    if problems:
        for p in problems:
            sys.stderr.write("[self-test] FAIL %s\n" % p)
        return 1

    print("self-test OK：模板合规、受控偏离生效、必含/必排词校验有效、"
          "颜色与标签越界可被抓出、"
          "3D 模型与贴图源图模板通过（面数红线 / 独占剪影 / 三通道打包口径生效）、"
          "2D 与 3D 的必含词与标签表互不污染")
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
