#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
gate.py — 《EdgeChronicles》资产入库 Gate

归属：美术（art-director）· 技术美术
零外部依赖：只用 Python 标准库（json / re / struct / shutil / argparse / os / sys）。

用法
----
    python Tools/art/gate.py --root Assets/_Project/Art --json
    python Tools/art/gate.py --root Assets/_Project/Art --dry-run
    python Tools/art/gate.py --self-test

退出码（**非零即失败**，不存在静默通过）
----
    0  全部通过
    1  存在未通过项（已退回 _WIP/ 并写 _fail.json）
    2  用法错误 / 内部错误 / 根目录不存在

机器判定的 13 条硬约束
-----------------------
    G1 面数：角色 ≤15000 三角面；场景模块/地标/地面 ≤3000 三角面
    G2 贴图尺寸：主角 ≤2048；敌人/Boss ≤1024；场景 ≤512；UI 母版 ≤1024、切片 ≤128
    G3 贴图位深与通道：**按 G9 的分档执行**（8bit；packed 档必须 RGB 三通道且无
             alpha / 无 tRNS，channels 字段 = "R=AO;G=Rough;B=Emissive"；
             rgba 档必须带 alpha，channels 字段 = "RGBA"）
    G4 材质槽数 = 1（OBJ 的 usemtl 分组数 / sidecar 的 material_slots）
    G5 命名合规（按目录分类的正则）
    G6 UV：全部落在 [0,1] 象限；每个 UV 岛距贴图边界 ≥4px；UV 岛之间无重叠
             （**镜像除外**：包围盒相同且缠绕方向相反视为镜像，判为合法）
    G7 授权：sidecar 的 origin != "self" 时，license 字段必须非空；
             origin == "ai" 时，generation_record 必须非空
    G8 引用完整性：sidecar 声明的贴图必须真实存在且通过 G2/G3
    G9 贴图格式分档：**模型贴图 = RGB 三通道、无 alpha**（R=AO G=Rough B=Emissive）；
             **UI / VFX 贴图 = 必须有 alpha**（RGBA），UI 图标另需四周透明 ≥28%
    G10 图集格位占用：图集 A 60 / B 63 / C 18（合计 141），技能图集 58/64
    G11 资源预算：贴图与网格内存不得超过**最低机型档**（默认 low）的
             `Tools/build/baselines/<tier>.json` 的 `memory` 段
    G12 描边 Shader 收录：3 个 ECOutline* Shader 必须在 Graphics Settings 的
             Always Included Shaders 里（漏收录 -> Shader.Find 返回 null ->
             **画面无描边且不报错**，现有任何门控都抓不到）
    G13 描边管线挂载：GraphicsSettings 与**全部画质档**都必须挂 URP 资产
             （任一档为空 -> 切到那档回到 Built-in，描边消失）

贴图尺寸口径（不写第二套）
------------------------
    **出图上限用美术规格**（主角 2048 / 敌人 1024 / Boss 1024 / 场景 512 /
    UI 母版 1024 / 切片 128）；**机型档的 maxTextureSize 是运行时上限**
    （低档 masterTextureLimit=1 → 2048 出图在低档运行时降到 1024），
    因此**不用机型档的 maxTextureSize 判出图**，只用它的 memory 段判资源预算。
    机型档参数的唯一来源 = Tools/build/baselines/*.json，本脚本不复制任何数值。

失败处置
--------
    失败文件（含其 sidecar）自动移动到 <root>/_WIP/<原相对路径>/ ，
    并在同目录写 <文件名>_fail.json（列出全部失败项与修法提示）。
    **不进入人工评审队列** —— 直接由美术侧修好后重新提交。

工程侧约定
----------
    * 模型几何源用 .obj（可直接解析）；.fbx 等二进制模型必须附带同名
      sidecar：<模型文件名>.meta.json
    * sidecar 字段（缺字段 = 判定失败，不允许"缺省通过"）：
        tri_count        int      三角面数
        material_slots   int      材质槽数（必须为 1）
        textures         list[str] 贴图相对路径（相对 sidecar 所在目录）
        channels         str      固定 "R=AO;G=Rough;B=Emissive"
        origin           str      self | purchased | ai | cc0 | licensed
        license          str      非空（origin != self 时必填）
        generation_record str    非空（origin == ai 时必填）
"""

import argparse
import csv
import json
import os
import re
import shutil
import struct
import sys
from collections import Counter

GATE_VERSION = "1.0.0"

# ---------------------------------------------------------------------------
# 分类规则：按目录决定 名称正则 / 面数上限 / 贴图上限 / 类型
# ---------------------------------------------------------------------------

TRI_LIMIT_CHARACTER = 15000
TRI_LIMIT_SCENE = 3000

# 通用贴图命名：固定前缀 TX_ + 可变 ID（大小写字母、数字、下划线）
TEX_NAME = r"^TX_[A-Za-z0-9][A-Za-z0-9_]*$"

# 分类规则：按目录决定 名称正则 / 面数上限 / 贴图上限
#   (目录前缀, 模型名正则, 贴图名正则, 面数上限, 贴图边长上限)
RULES = [
    ("Character/Hero", r"^CH_HERO_[A-Za-z0-9][A-Za-z0-9_]*$", TEX_NAME,
     TRI_LIMIT_CHARACTER, 2048),
    ("Character/Enemy", r"^CH_ENEMY_[A-Za-z0-9][A-Za-z0-9_]*$", TEX_NAME,
     TRI_LIMIT_CHARACTER, 1024),
    ("Boss", r"^CH_BOSS_[A-Za-z0-9][A-Za-z0-9_]*$", TEX_NAME,
     TRI_LIMIT_CHARACTER, 1024),
    ("Scene/Module", r"^SC_MODULE_[A-Za-z0-9][A-Za-z0-9_]*$", TEX_NAME,
     TRI_LIMIT_SCENE, 512),
    ("Scene/Landmark", r"^SC_LANDMARK_[A-Za-z0-9][A-Za-z0-9_]*$", TEX_NAME,
     TRI_LIMIT_SCENE, 512),
    ("Scene/Ground", r"^SC_GROUND_[A-Za-z0-9][A-Za-z0-9_]*$", TEX_NAME,
     TRI_LIMIT_SCENE, 512),
    ("UI", None, r"^UI_(ICON|PANEL)_[A-Za-z0-9][A-Za-z0-9_]*$", None, 1024),
    ("VFX", None, r"^VFX_[A-Za-z0-9][A-Za-z0-9_]*$", None, 2048),
]

# Source/ 下的母版兜底分类（只有贴图）
FALLBACK_CATEGORY = ("Source", None, TEX_NAME, None, 2048)

MODEL_EXT = {".obj", ".fbx"}
TEXTURE_EXT = {".png", ".tga"}
SIDECAR_EXT = ".meta.json"
WIP_DIR = "_WIP"
UNITY_META_EXT = ".meta"

# 只有这些扩展名的文件会参与判定**并且**允许被退回 _WIP。
# 其余一律只报 warning、永不移动 —— gate 不得破坏工程结构。
GATED_EXT = MODEL_EXT | TEXTURE_EXT

REQUIRED_CHANNELS = "R=AO;G=Rough;B=Emissive"
REQUIRED_CHANNELS_RGBA = "RGBA"
PADDING_PX = 4
UV_EPSILON = 1e-4
UI_TRANSPARENT_BORDER = 0.28

# 贴图格式分档：模型贴图打包三通道无 alpha；UI / VFX 贴图必须有 alpha
TEXTURE_KIND_PACKED = "packed"
TEXTURE_KIND_RGBA = "rgba"

# 目录 -> 贴图格式（sidecar 的 texture_kind 可覆写）
PACKED_CATEGORIES = ("Character/Hero", "Character/Enemy", "Boss",
                     "Scene/Module", "Scene/Landmark", "Scene/Ground")

# 图集格位占用（AD-03 §1.1–§1.3 / §4.2 的分配与缓冲）
ATLAS_OCCUPANCY = {"A": 60, "B": 63, "C": 18}
ATLAS_CELLS = 64
VFX_ATLAS_OCCUPIED = 58
VFX_ATLAS_CELLS = 64

# 机型档（唯一来源 Tools/build/baselines/*.json，本脚本不复制其中的数值）
BASELINE_DIR_REL = os.path.join("Tools", "build", "baselines")
DEFAULT_BASELINE_TIER = "low"
DEFAULT_ATLAS_MANIFEST_REL = os.path.join("Tools", "art", "manifests",
                                           "ui_atlas_141.csv")


# ---------------------------------------------------------------------------
# 数据结构
# ---------------------------------------------------------------------------

class Failure(object):
    __slots__ = ("code", "message", "fix")

    def __init__(self, code, message, fix=""):
        self.code = code
        self.message = message
        self.fix = fix

    def to_dict(self):
        return {"code": self.code, "message": self.message, "fix": self.fix}


class Record(object):
    """单个资产的判定结果。"""

    def __init__(self, path, kind, category):
        self.path = path
        self.kind = kind          # model / texture / sidecar / unknown
        self.category = category  # 命中的分类名
        self.failures = []
        self.warnings = []
        self.sidecar = None
        self.sidecar_path = None
        self.texture_kind = None
        self.texture_bytes = 0
        self.islands = 0
        self.triangles = None
        self.textures = []

    @property
    def passed(self):
        return len(self.failures) == 0

    def fail(self, code, message, fix=""):
        self.failures.append(Failure(code, message, fix))

    def warn(self, code, message):
        self.warnings.append(Failure(code, message))

    def to_dict(self):
        return {
            "path": self.path,
            "kind": self.kind,
            "category": self.category,
            "texture_kind": self.texture_kind,
            "passed": self.passed,
            "triangles": self.triangles,
            "uv_islands": self.islands,
            "textures": self.textures,
            "failures": [f.to_dict() for f in self.failures],
            "warnings": [w.to_dict() for w in self.warnings],
        }


# ---------------------------------------------------------------------------
# 机型档（唯一来源：Tools/build/baselines/*.json）
# ---------------------------------------------------------------------------

def repo_root_from_script():
    """Tools/art/gate.py -> 仓库根目录。"""
    return os.path.dirname(os.path.dirname(os.path.dirname(
        os.path.abspath(__file__))))


def load_baseline(tier, root=None):
    """读 Tools/build/baselines/<tier>.json。

    返回 (data, problems)。找不到文件时返回 (None, [problem])，
    **不静默通过**：预算判不了就报出来。
    """
    problems = []
    base = root or repo_root_from_script()
    path = os.path.join(base, BASELINE_DIR_REL, "%s.json" % tier)
    if not os.path.isfile(path):
        problems.append({
            "code": "G11-NOBASE",
            "message": "找不到机型档 %s" % path,
            "fix": "先让发布侧提供 Tools/build/baselines/%s.json" % tier,
        })
        return None, problems
    try:
        with open(path, "r", encoding="utf-8") as fh:
            data = json.load(fh)
    except (OSError, ValueError) as exc:
        problems.append({
            "code": "G11-BADBASE",
            "message": "机型档读取失败：%r" % exc,
            "fix": "修正 %s" % path,
        })
        return None, problems
    return data, problems


# ---------------------------------------------------------------------------
# PNG 头解析（只需要 IHDR + tRNS，不需要解压像素）
# ---------------------------------------------------------------------------

PNG_SIGNATURE = b"\x89PNG\r\n\x1a\n"

PNG_COLOR_CHANNELS = {0: 1, 2: 3, 3: 1, 4: 2, 6: 4}


def read_png_info(path):
    """返回 dict(width, height, bit_depth, color_type, channels, has_alpha) 或 None。"""
    try:
        with open(path, "rb") as fh:
            data = fh.read(64 * 1024)
    except OSError:
        return None

    if not data.startswith(PNG_SIGNATURE):
        return None

    pos = 8
    ihdr = None
    has_trns = False
    while pos + 8 <= len(data):
        (length,) = struct.unpack(">I", data[pos:pos + 4])
        ctype = data[pos + 4:pos + 8]
        chunk = data[pos + 8:pos + 8 + length]
        if ctype == b"IHDR":
            ihdr = struct.unpack(">IIBBBBB", chunk[:13])
        elif ctype == b"tRNS":
            has_trns = True
        elif ctype == b"IDAT":
            break
        pos += 12 + length
        if ihdr is not None and ctype == b"tRNS":
            break

    if ihdr is None:
        return None

    width, height, bit_depth, color_type, _, _, _ = ihdr
    channels = PNG_COLOR_CHANNELS.get(color_type, 0)
    # color type 4 (灰度+alpha) / 6 (RGBA) 自带 alpha；3 (调色板) / 0 (灰度) 另看 tRNS
    has_alpha = color_type in (4, 6) or (color_type in (0, 3) and has_trns)

    return {
        "width": width,
        "height": height,
        "bit_depth": bit_depth,
        "color_type": color_type,
        "channels": channels,
        "has_alpha": has_alpha,
    }


# ---------------------------------------------------------------------------
# PNG 像素解码（只为判定透明边框；只支持 RGBA8 非隔行）
# ---------------------------------------------------------------------------

MAX_DECODE_PIXELS = 512 * 512


def _png_unfilter(raw, width, height, bpp=4):
    """反过滤 PNG 扫描线。返回 bytearray 像素数据或 None（格式不支持）。"""
    stride = width * bpp
    out = bytearray(stride * height)
    pos = 0
    prev = bytearray(stride)
    for y in range(height):
        if pos >= len(raw):
            return None
        ftype = raw[pos]
        pos += 1
        line = bytearray(raw[pos:pos + stride])
        if len(line) < stride:
            return None
        pos += stride
        if ftype == 0:
            pass
        elif ftype == 1:
            for i in range(bpp, stride):
                line[i] = (line[i] + line[i - bpp]) & 0xFF
        elif ftype == 2:
            for i in range(stride):
                line[i] = (line[i] + prev[i]) & 0xFF
        elif ftype == 3:
            for i in range(stride):
                left = line[i - bpp] if i >= bpp else 0
                line[i] = (line[i] + ((left + prev[i]) >> 1)) & 0xFF
        elif ftype == 4:
            for i in range(stride):
                a = line[i - bpp] if i >= bpp else 0
                b = prev[i]
                c = prev[i - bpp] if i >= bpp else 0
                pp = a + b - c
                pa, pb, pc = abs(pp - a), abs(pp - b), abs(pp - c)
                if pa <= pb and pa <= pc:
                    pred = a
                elif pb <= pc:
                    pred = b
                else:
                    pred = c
                line[i] = (line[i] + pred) & 0xFF
        else:
            return None
        out[y * stride:(y + 1) * stride] = line
        prev = line
    return out


def transparent_border_ratio(path):
    """返回 (最小透明边距比例, error)。

    最小透明边距比例 = 四个方向里最小的「非透明内容外沿到画布边」的距离 / 画布边长。
    只支持 RGBA8 非隔行；超出像素预算或格式不支持时返回 (None, 说明)。
    """
    import zlib

    try:
        with open(path, "rb") as fh:
            data = fh.read()
    except OSError as exc:
        return None, "读取失败：%r" % exc

    if not data.startswith(PNG_SIGNATURE):
        return None, "不是 PNG"

    pos = 8
    ihdr = None
    idat = bytearray()
    while pos + 8 <= len(data):
        (length,) = struct.unpack(">I", data[pos:pos + 4])
        ctype = data[pos + 4:pos + 8]
        chunk = data[pos + 8:pos + 8 + length]
        if ctype == b"IHDR":
            ihdr = struct.unpack(">IIBBBBB", chunk[:13])
        elif ctype == b"IDAT":
            idat += chunk
        elif ctype == b"IEND":
            break
        pos += 12 + length

    if ihdr is None:
        return None, "缺少 IHDR"
    width, height, bit_depth, color_type, _, _, interlace = ihdr
    if bit_depth != 8 or color_type != 6 or interlace != 0:
        return None, "只支持 RGBA8 非隔行（当前 bit_depth=%d color_type=%d interlace=%d）" % (
            bit_depth, color_type, interlace)
    if width * height > MAX_DECODE_PIXELS:
        return None, "像素数 %d 超过解码预算 %d" % (width * height, MAX_DECODE_PIXELS)

    try:
        raw = zlib.decompress(bytes(idat))
    except zlib.error as exc:
        return None, "解压失败：%r" % exc

    pixels = _png_unfilter(raw, width, height, bpp=4)
    if pixels is None:
        return None, "扫描线反过滤失败"

    min_x, min_y, max_x, max_y = width, height, -1, -1
    stride = width * 4
    for y in range(height):
        row = pixels[y * stride:(y + 1) * stride]
        for x in range(width):
            if row[x * 4 + 3] != 0:
                if x < min_x:
                    min_x = x
                if x > max_x:
                    max_x = x
                break
        # 逐行找右边界
        if max_x < width - 1:
            for x in range(width - 1, -1, -1):
                if row[x * 4 + 3] != 0:
                    if x > max_x:
                        max_x = x
                    break
    if max_x < 0:
        return 0.0, "整张图全透明"

    margins = (min_x / float(width),
               (width - 1 - max_x) / float(width),
               min_y / float(height),
               (height - 1 - max_y) / float(height))
    return min(margins), None


# ---------------------------------------------------------------------------
# OBJ 解析
# ---------------------------------------------------------------------------

class ObjMesh(object):
    __slots__ = ("positions", "uvs", "triangles", "materials", "uv_vertices")

    def __init__(self):
        self.positions = []      # (x, y, z)
        self.uvs = []            # (u, v)
        self.triangles = []      # [(uv_idx, uv_idx, uv_idx), ...]
        self.materials = []      # 用到的 usemtl 名（保序去重）
        self.uv_vertices = []    # 三角形里出现的 uv 顶点索引列表


def parse_obj(path):
    """解析 .obj。返回 ObjMesh；解析失败返回 None。"""
    mesh = ObjMesh()
    uv_map = {}          # (v_idx, vt_idx) -> uv_vertex_index
    cur_material = None
    materials_seen = []

    try:
        fh = open(path, "r", encoding="utf-8", errors="replace")
    except OSError:
        return None

    with fh:
        for line in fh:
            line = line.strip()
            if not line or line.startswith("#"):
                continue
            parts = line.split()
            key = parts[0]
            if key == "v":
                try:
                    mesh.positions.append((float(parts[1]), float(parts[2]), float(parts[3])))
                except (IndexError, ValueError):
                    return None
            elif key == "vt":
                try:
                    u = float(parts[1])
                    v = float(parts[2])
                except (IndexError, ValueError):
                    return None
                mesh.uvs.append((u, v))
            elif key == "usemtl":
                cur_material = parts[1] if len(parts) > 1 else "<unnamed>"
                if cur_material not in materials_seen:
                    materials_seen.append(cur_material)
            elif key == "f":
                idx = []
                for token in parts[1:]:
                    bits = token.split("/")
                    if len(bits) < 2:
                        continue
                    try:
                        v_idx = int(bits[0])
                        vt_idx = int(bits[1])
                    except ValueError:
                        continue
                    # OBJ 的负索引是相对索引
                    if v_idx < 0:
                        v_idx = len(mesh.positions) + v_idx
                    if vt_idx < 0:
                        vt_idx = len(mesh.uvs) + vt_idx
                    if vt_idx < 1 or vt_idx > len(mesh.uvs):
                        continue
                    key2 = (v_idx, vt_idx)
                    if key2 not in uv_map:
                        uv_map[key2] = len(mesh.uv_vertices)
                        mesh.uv_vertices.append(vt_idx - 1)
                    idx.append(uv_map[key2])
                if len(idx) == 3:
                    mesh.triangles.append((idx[0], idx[1], idx[2]))
                elif len(idx) == 4:
                    # 四边形拆两个三角形
                    mesh.triangles.append((idx[0], idx[1], idx[2]))
                    mesh.triangles.append((idx[0], idx[2], idx[3]))

    mesh.materials = materials_seen
    return mesh


# ---------------------------------------------------------------------------
# UV 岛分析
# ---------------------------------------------------------------------------

class UVIsland(object):
    __slots__ = ("indices", "bbox", "area")

    def __init__(self, indices, bbox, area):
        self.indices = indices
        self.bbox = bbox    # (min_u, min_v, max_u, max_v)
        self.area = area    # 有符号面积（缠绕方向）


def build_uv_islands(mesh):
    """按 UV 顶点连通性切岛，并计算包围盒与有符号面积。"""
    parent = list(range(len(mesh.uv_vertices)))

    def find(a):
        while parent[a] != a:
            parent[a] = parent[parent[a]]
            a = parent[a]
        return a

    def union(a, b):
        ra, rb = find(a), find(b)
        if ra != rb:
            parent[rb] = ra

    for tri in mesh.triangles:
        union(tri[0], tri[1])
        union(tri[1], tri[2])

    groups = {}
    for i in range(len(mesh.uv_vertices)):
        groups.setdefault(find(i), []).append(i)

    islands = []
    for members in groups.values():
        us = []
        vs = []
        for i in members:
            u, v = mesh.uvs[mesh.uv_vertices[i]]
            us.append(u)
            vs.append(v)
        if not us:
            continue
        bbox = (min(us), min(vs), max(us), max(vs))

        area = 0.0
        for tri in mesh.triangles:
            if find(tri[0]) != find(members[0]):
                continue
            pts = [mesh.uvs[mesh.uv_vertices[t]] for t in tri]
            area += 0.5 * ((pts[1][0] - pts[0][0]) * (pts[2][1] - pts[0][1]) -
                           (pts[2][0] - pts[0][0]) * (pts[1][1] - pts[0][1]))
        islands.append(UVIsland(members, bbox, area))

    return islands


def bbox_overlap(a, b, eps=1e-6):
    return (a[0] < b[2] - eps and b[0] < a[2] - eps and
            a[1] < b[3] - eps and b[1] < a[3] - eps)


def bbox_same(a, b, eps=1e-4):
    return (abs(a[0] - b[0]) < eps and abs(a[1] - b[1]) < eps and
            abs(a[2] - b[2]) < eps and abs(a[3] - b[3]) < eps)


# ---------------------------------------------------------------------------
# 分类
# ---------------------------------------------------------------------------

def classify(rel_path, ext):
    """按目录 + 扩展名分类。

    返回 (kind, category, name_regex, tri_limit, tex_limit)；
    kind ∈ model / texture；无法归类时 kind 为 None。
    """
    parts = rel_path.split("/")
    category = None
    for prefix, model_re, tex_re, tri_limit, tex_limit in RULES:
        p = prefix.split("/")
        if len(parts) >= len(p) and parts[:len(p)] == p:
            category = (prefix, model_re, tex_re, tri_limit, tex_limit)
            break
    if category is None:
        if parts[0] == "Source":
            category = FALLBACK_CATEGORY
        else:
            return None, "", None, None, None

    prefix, model_re, tex_re, tri_limit, tex_limit = category
    if ext in MODEL_EXT:
        if model_re is None:
            return None, prefix, None, tri_limit, tex_limit
        return "model", prefix, model_re, tri_limit, tex_limit
    if ext in TEXTURE_EXT:
        return "texture", prefix, tex_re, tri_limit, tex_limit
    return None, prefix, None, tri_limit, tex_limit


# ---------------------------------------------------------------------------
# 单项判定
# ---------------------------------------------------------------------------

def check_texture(path, rel_path, tex_limit, kind, record):
    """G2 / G3 / G5 / G9（贴图部分）。

    kind = "packed"：模型贴图 —— RGB 三通道、无 alpha、R=AO G=Rough B=Emissive
    kind = "rgba"  ：UI / VFX 贴图 —— 必须有 alpha；另需四周透明 ≥28%
    """
    info = read_png_info(path)
    if info is None:
        record.fail("G3-PARSE", "无法解析 PNG 头（文件损坏或不是 PNG）",
                    "导出为标准 PNG 后重新提交")
        return

    if info["bit_depth"] != 8:
        record.fail("G3-DEPTH", "贴图位深必须为 8（当前 %d）" % info["bit_depth"],
                    "导出为 8bit PNG")

    if kind == TEXTURE_KIND_PACKED:
        if info["color_type"] != 2:
            record.fail("G3-CHANNEL",
                        "模型贴图必须为 RGB 三通道（当前 color type %d，通道数 %d）"
                        % (info["color_type"], info["channels"]),
                        "导出为 RGB 8bit PNG（R=AO G=Rough B=Emissive）")
        if info["has_alpha"]:
            record.fail("G3-ALPHA",
                        "模型贴图含 alpha 通道（tRNS / RGBA）；"
                        "模型贴图固定 R=AO G=Rough B=Emissive，**无 alpha**",
                        "导出为 RGB 8bit PNG")
    else:
        if not info["has_alpha"]:
            record.fail("G3-NOALPHA",
                        "UI / VFX 贴图**必须带 alpha**（当前 color type %d，无 alpha）"
                        % info["color_type"],
                        "导出为 RGBA8 PNG；图标本体透明、四周透明 ≥28%")
        else:
            ratio, err = transparent_border_ratio(path)
            if ratio is None:
                record.warn("G9-NOBORDER",
                            "无法判定透明边框（%s）；请人工确认四周透明 >=28%%" % err)
            elif ratio < UI_TRANSPARENT_BORDER - 1e-6:
                record.fail("G9-BORDER",
                            "四周透明边距最小为 %.1f%%，低于 %d%%"
                            % (ratio * 100.0, int(UI_TRANSPARENT_BORDER * 100)),
                            "把内容向内收，四周留白 >=%d%%"
                            % int(UI_TRANSPARENT_BORDER * 100))

    limit = tex_limit if tex_limit else 2048
    if max(info["width"], info["height"]) > limit:
        record.fail("G2-SIZE",
                    "贴图尺寸 %dx%d 超过上限 %d" % (info["width"], info["height"], limit),
                    "降采样到 %d 以内再入库" % limit)


def check_uv(mesh, texture_size, record):
    """G6：UV 象限 / padding / 重叠（镜像除外）。"""
    if not mesh.uvs:
        record.fail("G6-NOUV", "模型没有 UV 坐标", "补 UV 后重新导出")
        return

    out_of_range = 0
    for u, v in mesh.uvs:
        if u < -UV_EPSILON or u > 1.0 + UV_EPSILON or \
           v < -UV_EPSILON or v > 1.0 + UV_EPSILON:
            out_of_range += 1
    if out_of_range:
        record.fail("G6-RANGE",
                    "%d 个 UV 坐标落在 [0,1] 象限之外" % out_of_range,
                    "UV 重排到 [0,1] 象限内（禁止超出后 wrap）")

    islands = build_uv_islands(mesh)
    record.islands = len(islands)

    if not islands:
        return

    # padding：每个岛距贴图四边 >= PADDING_PX
    if texture_size:
        tw, th = texture_size
        for i, island in enumerate(islands):
            min_u, min_v, max_u, max_v = island.bbox
            pad_u = min(min_u * tw, (1.0 - max_u) * tw)
            pad_v = min(min_v * th, (1.0 - max_v) * th)
            if pad_u < PADDING_PX - 1e-6 or pad_v < PADDING_PX - 1e-6:
                record.fail("G6-PADDING",
                            "UV 岛 #%d 距贴图边界 padding %.2fpx（U）/ %.2fpx（V），"
                            "低于 %dpx" % (i, pad_u, pad_v, PADDING_PX),
                            "把 UV 岛向内收 %dpx 后重排" % PADDING_PX)
                break
    else:
        record.warn("G6-NOPAD", "拿不到贴图尺寸，跳过 padding 判定")

    # 重叠（镜像除外）
    for i in range(len(islands)):
        for j in range(i + 1, len(islands)):
            a, b = islands[i], islands[j]
            if not bbox_overlap(a.bbox, b.bbox):
                continue
            mirrored = (bbox_same(a.bbox, b.bbox) and
                        (a.area * b.area) < 0 and
                        abs(abs(a.area) - abs(b.area)) < 1e-6)
            if mirrored:
                continue
            record.fail("G6-OVERLAP",
                        "UV 岛 #%d 与 #%d 重叠（且不是镜像对）" % (i, j),
                        "重排 UV 使各岛不重叠；镜像对请保证包围盒完全一致且缠绕方向相反")
            return


def check_license(sidecar, record):
    """G7：授权与 AI 生成记录。"""
    origin = sidecar.get("origin")
    if not origin:
        record.fail("G7-ORIGIN", "sidecar 缺少 origin 字段",
                    "填 origin：self / purchased / ai / cc0 / licensed")
        return
    if origin == "self":
        return
    if not str(sidecar.get("license", "")).strip():
        record.fail("G7-LICENSE",
                    "origin=%s 时 license 字段不得为空" % origin,
                    "填写真实的授权类型与来源（商店包名 / 许可证 / 委托方）")
    if origin == "ai" and not str(sidecar.get("generation_record", "")).strip():
        record.fail("G7-AIREC",
                    "origin=ai 时 generation_record 不得为空（留存生成记录）",
                    "填模型、提示词、生成日期、授权条款核对结论")


def infer_texture_kind(category, sidecar):
    """贴图格式分档。

    sidecar 的 texture_kind 优先（合法值 packed / rgba）；
    否则按目录推断：模型目录 -> packed，UI / VFX / Source -> rgba。
    """
    if sidecar and sidecar.get("texture_kind"):
        kind = str(sidecar["texture_kind"]).strip().lower()
        if kind in (TEXTURE_KIND_PACKED, TEXTURE_KIND_RGBA):
            return kind
    if category in PACKED_CATEGORIES:
        return TEXTURE_KIND_PACKED
    return TEXTURE_KIND_RGBA


def expected_channels_for(kind):
    return REQUIRED_CHANNELS if kind == TEXTURE_KIND_PACKED else REQUIRED_CHANNELS_RGBA


def check_sidecar_required(sidecar, record, expected_channels=None):
    """sidecar 必填字段。"""
    if "tri_count" not in sidecar:
        record.fail("G1-NOTRI", "sidecar 缺少 tri_count", "填三角面数")
    if "material_slots" not in sidecar:
        record.fail("G4-NOSLOT", "sidecar 缺少 material_slots", "填材质槽数（必须为 1）")
    want = expected_channels or REQUIRED_CHANNELS
    if "channels" not in sidecar:
        record.fail("G3-NOCH", "sidecar 缺少 channels",
                    '固定填 "%s"' % want)
    elif str(sidecar["channels"]).replace(" ", "") != want:
        record.fail("G3-CH",
                    "channels 字段必须为 \"%s\"（当前 \"%s\"）"
                    % (want, sidecar["channels"]),
                    '改成 "%s"' % want)


# ---------------------------------------------------------------------------
# 扫描与判定
# ---------------------------------------------------------------------------

MODEL_SIDECAR_NAMES = {".obj": ".meta.json", ".fbx": ".meta.json"}


def collect_files(root):
    """返回 (相对路径列表, sidecar 路径集合)。"""
    files = []
    sidecars = set()
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = [d for d in dirnames if d != WIP_DIR]
        for name in sorted(filenames):
            full = os.path.join(dirpath, name)
            rel = os.path.relpath(full, root).replace("\\", "/")
            files.append(rel)
            if name.endswith(SIDECAR_EXT):
                sidecars.add(rel)
    return files, sidecars


def load_sidecar(root, rel_path, cache):
    if rel_path in cache:
        return cache[rel_path]
    full = os.path.join(root, rel_path)
    data = None
    try:
        with open(full, "r", encoding="utf-8") as fh:
            data = json.load(fh)
    except (OSError, ValueError):
        data = None
    cache[rel_path] = data
    return data


def find_sidecar_for(rel_path, sidecars):
    base = rel_path.rsplit("/", 1)[-1]
    stem, ext = os.path.splitext(base)
    candidate = stem + SIDECAR_EXT
    for sc in sidecars:
        if sc.rsplit("/", 1)[-1] == candidate:
            return sc
    return None


def run_gate(root, dry_run=False, do_reject=True,
             baseline_tier=DEFAULT_BASELINE_TIER, atlas_manifest=None):
    if not os.path.isdir(root):
        sys.stderr.write("[gate] 根目录不存在：%s\n" % root)
        return 2, None

    base_root = repo_root_from_script()
    if atlas_manifest is None:
        atlas_manifest = os.path.join(base_root, DEFAULT_ATLAS_MANIFEST_REL)

    baseline, baseline_problems = load_baseline(baseline_tier, base_root)

    files, sidecars = collect_files(root)
    cache = {}
    records = []

    for rel in files:
        if rel.endswith(SIDECAR_EXT):
            continue    # sidecar 跟随其宿主资产一起判定
        base = rel.rsplit("/", 1)[-1]
        stem, ext = os.path.splitext(base)
        ext = ext.lower()

        if ext == UNITY_META_EXT:
            continue    # Unity 生成的 .meta：既不判定也不移动

        if ext not in GATED_EXT:
            # 不在受管资产类型内：只登记，不失败、不移动
            rec = Record(rel, "unmanaged", "")
            rec.warn("G5-UNMANAGED",
                     "扩展名 %s 不在受管资产类型内（模型 OBJ/FBX、贴图 PNG/TGA），"
                     "不参与判定、不会被退回" % ext)
            records.append(rec)
            continue

        kind, category, regex, tri_limit, tex_limit = classify(rel, ext)
        if kind is None:
            rec = Record(rel, "unknown", category)
            if category:
                rec.fail("G5-KIND",
                         "目录 %s 只接受 %s"
                         % (category, "模型文件" if tri_limit else "贴图文件"),
                         "换用正确类型的文件")
            else:
                rec.fail("G5-PATH",
                         "目录不在任何已知分类下"
                         "（Character/… / Boss / Scene/… / UI / VFX / Source）",
                         "移动到正确目录，或在 gate.py 的 RULES 中登记新分类")
            records.append(rec)
            continue

        rec = Record(rel, kind, category)

        # G5 命名
        if not re.match(regex, stem):
            rec.fail("G5-NAME",
                     "文件名 %r 不符合 %s 分类的命名规范 %s" % (stem, category, regex),
                     "按 %s 改名" % regex)

        if kind == "texture":
            if ext not in TEXTURE_EXT:
                rec.fail("G2-EXT", "贴图只接受 PNG/TGA（当前 %s）" % ext,
                         "导出为 PNG")
            else:
                # 贴图的授权 / AI 生成记录 + 格式分档：sidecar 存在则必检，不存在则告警
                sc_rel = find_sidecar_for(rel, sidecars)
                sidecar = None
                if sc_rel:
                    rec.sidecar_path = sc_rel
                    sidecar = load_sidecar(root, sc_rel, cache)
                    rec.sidecar = sidecar
                    if sidecar is None:
                        rec.fail("G8-SC", "sidecar %s 无法解析为 JSON" % sc_rel,
                                 "sidecar 必须是合法 UTF-8 JSON 对象")
                    else:
                        check_license(sidecar, rec)
                else:
                    rec.warn("G7-NOSC",
                             "贴图没有 sidecar，无法核验授权与 AI 生成记录"
                             "（%s.meta.json）" % stem)

                kind = infer_texture_kind(category, sidecar)
                rec.texture_kind = kind
                check_texture(os.path.join(root, rel), rel, tex_limit, kind, rec)
                info = read_png_info(os.path.join(root, rel))
                if info:
                    rec.texture_bytes = int(info["width"] * info["height"] *
                                            max(1, info["channels"]) *
                                            MIP_CHAIN_FACTOR)
        elif kind == "model":
            if ext not in MODEL_EXT:
                rec.fail("G1-EXT", "模型只接受 OBJ/FBX（当前 %s）" % ext,
                         "导出为 OBJ 或 FBX")
            else:
                _check_model(root, rel, ext, stem, tri_limit, tex_limit,
                             sidecars, cache, rec)
        else:
            rec.fail("G5-KIND", "未知资产种类", "修正目录分类")

        records.append(rec)

    # sidecar 孤儿检查
    for sc_rel in sorted(sidecars):
        owner = find_owner(sc_rel, files)
        if owner is None:
            rec = Record(sc_rel, "sidecar", "")
            rec.fail("G8-ORPHAN",
                     "sidecar 没有对应的宿主资产文件",
                     "删除该 sidecar，或补上对应资产")
            records.append(rec)

    # 引用完整性（G8）
    _check_references(root, records, cache)

    # 全局判定：G10 图集格位 / G11 资源预算
    for prob in check_atlas_manifest(atlas_manifest):
        rec = Record("(全局) 图集格位", "global", "ATLAS")
        rec.fail(prob["code"], prob["message"], prob.get("fix", ""))
        records.append(rec)

    for prob in baseline_problems:
        rec = Record("(全局) 机型档 %s" % baseline_tier, "global", "BASELINE")
        rec.fail(prob["code"], prob["message"], prob.get("fix", ""))
        records.append(rec)

    for prob in check_budget(records, baseline):
        rec = Record("(全局) 资源预算", "global", "BUDGET")
        rec.fail(prob["code"], prob["message"], prob.get("fix", ""))
        records.append(rec)

    # G12 / G13 描边管线接线（静默失败点）
    pipe_problems, pipe_warnings = check_pipeline_wiring(base_root)
    if pipe_problems or pipe_warnings:
        rec = Record("(全局) 描边管线接线", "global", "PIPELINE")
        for prob in pipe_problems:
            rec.fail(prob["code"], prob["message"], prob.get("fix", ""))
        for warn in pipe_warnings:
            rec.warn(warn["code"], warn["message"])
        records.append(rec)

    # 退回 _WIP
    rejected = []
    if do_reject:
        rejected = reject_failures(root, records, dry_run=dry_run)

    report = {
        "gate_version": GATE_VERSION,
        "root": root,
        "dry_run": dry_run,
        "baseline_tier": baseline_tier,
        "baseline_loaded": baseline is not None,
        "atlas_manifest": atlas_manifest,
        "total": len(records),
        "passed": sum(1 for r in records if r.passed),
        "failed": sum(1 for r in records if not r.passed),
        "rejected": len(rejected),
        "records": [r.to_dict() for r in records],
        "rejected_files": rejected,
    }
    return (0 if report["failed"] == 0 else 1), report


def _check_model(root, rel, ext, stem, tri_limit, tex_limit, sidecars, cache, rec):
    """模型判定：G1 / G4 / G6 / G7 / G8。"""
    full = os.path.join(root, rel)
    sc_rel = find_sidecar_for(rel, sidecars)
    sidecar = None

    if sc_rel:
        rec.sidecar_path = sc_rel
        sidecar = load_sidecar(root, sc_rel, cache)
        rec.sidecar = sidecar
        if sidecar is None:
            rec.fail("G8-SC", "sidecar %s 无法解析为 JSON" % sc_rel,
                     "sidecar 必须是合法 UTF-8 JSON 对象")
        else:
            check_sidecar_required(sidecar, rec,
                                   expected_channels=REQUIRED_CHANNELS)
            check_license(sidecar, rec)
    elif ext == ".obj":
        # .obj 可直接解析几何，但授权信息仍必须有载体
        rec.fail("G7-NOSC",
                 "缺少 sidecar %s%s" % (stem + SIDECAR_EXT,
                                       "（授权/贴图声明的唯一载体）"),
                 "补一份 %s%s" % (stem + SIDECAR_EXT))
    else:
            rec.fail("G8-NOSC",
                     "FBX 必须附带 sidecar %s" % (stem + SIDECAR_EXT),
                     "导出几何统计到 %s" % (stem + SIDECAR_EXT))

    mesh = None
    if ext == ".obj":
        mesh = parse_obj(full)
        if mesh is None:
            rec.fail("G1-PARSE", "OBJ 解析失败", "重新导出 OBJ")

    # G1 面数
    if mesh is not None:
        rec.triangles = len(mesh.triangles)
        tri_count = len(mesh.triangles)
    elif sidecar and "tri_count" in sidecar:
        try:
            tri_count = int(sidecar["tri_count"])
            rec.triangles = tri_count
        except (TypeError, ValueError):
            rec.fail("G1-NOTRI", "sidecar tri_count 不是整数",
                     "填整数三角面数")
            tri_count = None
    else:
        tri_count = None

    if tri_count is not None and tri_limit is not None and tri_count > tri_limit:
        rec.fail("G1-TRIS",
                 "三角面 %d 超过上限 %d" % (tri_count, tri_limit),
                 "减面到 %d 以内（低模 + 硬边，优先改拓扑而不是降模精度）" % tri_limit)

    # G4 材质槽
    if mesh is not None:
        slots = len(mesh.materials)
        if slots != 1:
            rec.fail("G4-SLOTS",
                     "OBJ 里出现 %d 个 usemtl 分组（%s），材质槽数必须为 1"
                     % (slots, ", ".join(mesh.materials) or "无"),
                     "合并到单一 usemtl，贴图全部打进同一张 R=AO G=Rough B=Emissive")
    if sidecar and "material_slots" in sidecar:
        try:
            declared = int(sidecar["material_slots"])
        except (TypeError, ValueError):
            declared = None
            rec.fail("G4-NOSLOT", "sidecar material_slots 不是整数", "填整数")
        if declared is not None and declared != 1:
            rec.fail("G4-SLOT1",
                     "sidecar 声明材质槽数 %d，必须为 1" % declared,
                     "合并到 1 个槽（5 个共享材质 M_Rock/M_Wood/M_Ground/M_Cloth/M_Emissive 之一）")

    # G8 贴图引用 + G2/G3 贴图本体
    texture_size = None
    if sidecar:
        textures = sidecar.get("textures")
        if not textures:
            rec.fail("G8-NOTEX", "sidecar 未声明 textures", "列出贴图相对路径")
        else:
            for tex_rel in textures:
                tex_full = os.path.join(root, os.path.dirname(rel), tex_rel)
                tex_full = os.path.normpath(tex_full)
                if not os.path.isfile(tex_full):
                    rec.fail("G8-MISSING", "sidecar 声明的贴图不存在：%s" % tex_rel,
                             "补齐贴图或修正 sidecar 路径")
                    continue
                rec.textures.append(tex_rel)
                info = read_png_info(tex_full)
                if info is None:
                    rec.fail("G3-PARSE", "贴图 %s 无法解析 PNG 头" % tex_rel,
                             "导出为标准 PNG")
                    continue
                limit = tex_limit if tex_limit else 2048
                if max(info["width"], info["height"]) > limit:
                    rec.fail("G2-SIZE",
                             "贴图 %s 尺寸 %dx%d 超过上限 %d"
                             % (tex_rel, info["width"], info["height"], limit),
                             "降采样到 %d 以内" % limit)
                if info["color_type"] != 2 or info["has_alpha"]:
                    rec.fail("G3-CHANNEL",
                             "贴图 %s 通道不合规（color type %d，含 alpha=%s），"
                             "模型贴图必须 RGB 三通道且无 alpha"
                             % (tex_rel, info["color_type"], info["has_alpha"]),
                             "导出为 RGB 8bit PNG")
                if texture_size is None:
                    texture_size = (info["width"], info["height"])

    # G6 UV
    if mesh is not None:
        check_uv(mesh, texture_size, rec)


def find_owner(sc_rel, files):
    base = sc_rel.rsplit("/", 1)[-1]
    stem = base[:-len(SIDECAR_EXT)]
    for rel in files:
        if rel.endswith(SIDECAR_EXT):
            continue
        rbase = rel.rsplit("/", 1)[-1]
        if os.path.splitext(rbase)[0] == stem:
            return rel
    return None


def _check_references(root, records, cache):
    """孤立贴图检查：Source/ 之外的贴图没有被任何 sidecar 引用 -> 警告。"""
    referenced = set()
    for rec in records:
        if rec.sidecar and "textures" in rec.sidecar:
            base_dir = os.path.dirname(rec.path)
            for tex_rel in rec.sidecar["textures"]:
                referenced.add(os.path.normpath(os.path.join(base_dir, tex_rel)).replace("\\", "/"))

    for rec in records:
        if rec.kind != "texture":
            continue
        if rec.category == "Source":
            continue      # Source/ 是母版目录，允许暂未引用
        if rec.path.replace("\\", "/") not in referenced:
            rec.warn("G8-ORPHAN",
                     "贴图没有被任何 sidecar 引用（孤立资产）")


# ---------------------------------------------------------------------------
# G12 / G13 描边管线接线（静默失败点）
# ---------------------------------------------------------------------------

# 三个描边 Shader：漏进 Always Included Shaders -> Shader.Find 返回 null
# -> Feature 静默跳过 -> 画面无描边且**不报错**。现有任何门控都抓不到。
OUTLINE_SHADER_NAMES = ("EC/OutlineMask", "EC/OutlineDilate", "EC/OutlineComposite")


def _read_text(path):
    try:
        with open(path, "r", encoding="utf-8", errors="replace") as fh:
            return fh.read()
    except OSError:
        return None


def _always_included_guids(graphics_settings_path):
    """解析 ProjectSettings/GraphicsSettings.asset 的 m_AlwaysIncludedShaders。"""
    text = _read_text(graphics_settings_path)
    if text is None:
        return None
    guids = []
    in_block = False
    for line in text.split("\n"):
        stripped = line.strip()
        if stripped.startswith("m_AlwaysIncludedShaders:"):
            in_block = True
            continue
        if in_block:
            if stripped.startswith("- "):
                m = re.search(r"guid:\s*([0-9a-fA-F]{32})", stripped)
                if m:
                    guids.append(m.group(1))
            elif stripped and not stripped.startswith("- "):
                break
    return guids


def _project_render_pipeline_wired(project_root):
    """检查 GraphicsSettings + 全部画质档都挂了 URP 资产。"""
    gs_path = os.path.join(project_root, "ProjectSettings", "GraphicsSettings.asset")
    text = _read_text(gs_path)
    if text is None:
        return ["读不到 %s" % gs_path]

    problems = []
    m = re.search(r"m_CustomRenderPipeline:\s*\{fileID:\s*(\d+)", text)
    if m is None or m.group(1) == "0":
        problems.append("GraphicsSettings.m_CustomRenderPipeline 为空 —— "
                        "工程仍在 Built-in 管线，描边通道整体不工作")

    q_path = os.path.join(project_root, "ProjectSettings", "QualitySettings.asset")
    qtext = _read_text(q_path)
    if qtext is None:
        problems.append("读不到 %s" % q_path)
    else:
        tiers = re.findall(r"customRenderPipeline:\s*\{fileID:\s*(\d+)", qtext)
        if not tiers:
            problems.append("QualitySettings 里没有 customRenderPipeline 字段")
        null_tiers = sum(1 for t in tiers if t == "0")
        if null_tiers:
            problems.append("%d 个画质档的 customRenderPipeline 为空 —— "
                            "切到那些档会回到 Built-in，描边消失"
                            % null_tiers)
    return problems


def _find_shader_guids(project_root):
    """扫描 Assets 下所有 .shader，找出三个描边 Shader 的 guid。"""
    found = {}
    assets_root = os.path.join(project_root, "Assets")
    for dirpath, dirnames, filenames in os.walk(assets_root):
        dirnames[:] = [d for d in dirnames if d not in (".git", "_WIP")]
        for name in filenames:
            if not name.endswith(".shader"):
                continue
            full = os.path.join(dirpath, name)
            head = _read_text(full)
            if head is None:
                continue
            m = re.search(r'Shader\s+"([^"]+)"', head)
            if not m:
                continue
            shader_name = m.group(1)
            if shader_name not in OUTLINE_SHADER_NAMES:
                continue
            meta = _read_text(full + ".meta")
            if meta is None:
                continue
            g = re.search(r"guid:\s*([0-9a-fA-F]{32})", meta)
            if g:
                found[shader_name] = g.group(1)
    return found


def check_pipeline_wiring(project_root):
    """G12 / G13：描边管线的接线完整性。

    返回 (problems, warnings)。
    """
    problems = []
    warnings = []

    for msg in _project_render_pipeline_wired(project_root):
        problems.append({"code": "G13-URP", "message": msg,
                         "fix": "先让工程侧把 URP 资产挂上 GraphicsSettings 与全部画质档"})

    gs_path = os.path.join(project_root, "ProjectSettings", "GraphicsSettings.asset")
    included = _always_included_guids(gs_path)
    if included is None:
        problems.append({"code": "G12-GS", "message": "读不到 %s" % gs_path,
                         "fix": "确认工程路径"})
        return problems, warnings

    found = _find_shader_guids(project_root)
    missing_files = [n for n in OUTLINE_SHADER_NAMES if n not in found]
    if missing_files:
        warnings.append({
            "code": "G12-NOTCOPIED",
            "message": "描边 Shader 尚未拷进工程（缺 %s）——描边通道未启用"
                       % "、".join(missing_files),
            "fix": "把 Tools/art/shaders/* 拷到 Assets/_Project/Art/Shaders/",
        })
        return problems, warnings

    not_included = [n for n in OUTLINE_SHADER_NAMES if found[n] not in included]
    if not_included:
        problems.append({
            "code": "G12-ALWAYS",
            "message": "Always Included Shaders 未收录：%s —— "
                       "Shader.Find 会返回 null，**画面无描边且不报错**"
                       % "、".join(not_included),
            "fix": "Project Settings > Graphics > Always Included Shaders 追加这 3 个 Shader",
        })
    return problems, warnings


# ---------------------------------------------------------------------------
# G10 图集格位占用
# ---------------------------------------------------------------------------

# 各图集的母版 / 显示边长（AD-03 §1.1–§1.3）
ATLAS_TILE = {
    "A": (128, 64),
    "B": (128, 64),
    "C": (64, 32),
}


def check_atlas_manifest(manifest_path):
    """G10：UI 图集格位占用与每图集的母版/显示尺寸。

    格位分配的**唯一来源**就是这份清单，判定不得另写一份。
    """
    problems = []
    if not os.path.isfile(manifest_path):
        return [{
            "code": "G10-NOMANIFEST",
            "message": "找不到图集派工清单：%s" % manifest_path,
            "fix": "先生成 Tools/art/manifests/ui_atlas_141.csv",
        }]

    try:
        with open(manifest_path, "r", encoding="utf-8-sig", newline="") as fh:
            rows = list(csv.DictReader(fh))
    except (OSError, ValueError) as exc:
        return [{
            "code": "G10-BADMANIFEST",
            "message": "图集清单读取失败：%r" % exc,
            "fix": "修正 %s" % manifest_path,
        }]

    counts = Counter(r.get("atlas", "") for r in rows)
    for atlas in sorted(ATLAS_OCCUPANCY):
        want = ATLAS_OCCUPANCY[atlas]
        got = counts.get(atlas, 0)
        if got != want:
            problems.append({
                "code": "G10-COUNT",
                "message": "图集 %s 分配 %d 格，应为 %d 格" % (atlas, got, want),
                "fix": "增删清单行到 %d 格" % want,
            })
        if got > ATLAS_CELLS:
            problems.append({
                "code": "G10-OVER",
                "message": "图集 %s 占用 %d 格，超过格位数 %d" % (atlas, got, ATLAS_CELLS),
                "fix": "压回 %d 格以内" % ATLAS_CELLS,
            })

    total = sum(counts.get(a, 0) for a in ATLAS_OCCUPANCY)
    if total != sum(ATLAS_OCCUPANCY.values()):
        problems.append({
            "code": "G10-TOTAL",
            "message": "图集分配合计 %d 格，应为 %d 格"
                       % (total, sum(ATLAS_OCCUPANCY.values())),
            "fix": "核对三张图集的分配数",
        })

    seen_ids = set()
    seen_cells = set()
    for row in rows:
        aid = (row.get("id") or "").strip()
        atlas = (row.get("atlas") or "").strip()
        cell = (row.get("cell") or "").strip()
        if not aid:
            problems.append({"code": "G10-ID", "message": "清单有行的 id 为空",
                             "fix": "补 id"})
        elif aid in seen_ids:
            problems.append({"code": "G10-DUPID", "message": "清单 id 重复：%s" % aid,
                             "fix": "id 必须唯一"})
        else:
            seen_ids.add(aid)

        key = (atlas, cell)
        if cell and key in seen_cells:
            problems.append({"code": "G10-DUPCELL",
                             "message": "清单格位重复：%s %s" % (atlas, cell),
                             "fix": "一格只放一个资产"})
        elif cell:
            seen_cells.add(key)

        if atlas in ATLAS_TILE:
            want_master, want_display = ATLAS_TILE[atlas]
            try:
                master = int(row.get("master_size", 0))
                display = int(row.get("display_size", 0))
            except (TypeError, ValueError):
                master = display = -1
            if master != want_master or display != want_display:
                problems.append({
                    "code": "G10-SIZE",
                    "message": "图集 %s 的 %s 母版/显示应为 %d/%d，实际 %d/%d"
                               % (atlas, aid, want_master, want_display, master, display),
                    "fix": "改成母版 %dpx / 显示 %dpx" % (want_master, want_display),
                })

    if VFX_ATLAS_OCCUPIED > VFX_ATLAS_CELLS:
        problems.append({
            "code": "G10-VFX",
            "message": "技能 VFX 图集占用 %d 格，超过 %d 格"
                       % (VFX_ATLAS_OCCUPIED, VFX_ATLAS_CELLS),
            "fix": "压回 %d 格以内" % VFX_ATLAS_CELLS,
        })

    return problems


# ---------------------------------------------------------------------------
# G11 资源预算（按最低机型档）
# ---------------------------------------------------------------------------

# 低模硬边典型值：约 1.5 顶点/面 × 36 B 顶点属性 + 6 B 索引 ≈ 60 B/面，取 64 B 保守上取整。
# 该系数使 96 MB 的低档网格预算恰好容纳 120 万三角面（IMPL 的同屏面数红线）。
MESH_BYTES_PER_TRIANGLE = 64
MIP_CHAIN_FACTOR = 4.0 / 3.0     # mip 链总��约为首级的 1.33 倍


def check_budget(records, baseline):
    """G11：贴图与网格内存预算。

    · 贴图只计**随包发布**的切片（路径含 Atlas 段）；Source 下的母版是源文件，不进包。
    · 网格计全部模型（都进包）。
    · 上限取**最低机型档**的 memory 段（把最坏情况锚在最低硬件档）。
    """
    problems = []
    if baseline is None:
        return problems

    memory = baseline.get("memory", {})
    tex_cap_mb = memory.get("textureMemoryMB")
    mesh_cap_mb = memory.get("meshMemoryMB")

    tex_bytes = 0
    mesh_bytes = 0

    for rec in records:
        if rec.kind == "texture" and rec.texture_bytes:
            if "Atlas" in rec.path.split("/"):
                tex_bytes += rec.texture_bytes
        if rec.kind == "model" and rec.triangles:
            mesh_bytes += rec.triangles * MESH_BYTES_PER_TRIANGLE

    if tex_cap_mb:
        cap = int(tex_cap_mb) * 1024 * 1024
        if tex_bytes > cap:
            problems.append({
                "code": "G11-TEXMEM",
                "message": "随包贴图内存 %.1f MB，超过机型档上限 %d MB"
                           % (tex_bytes / 1048576.0, tex_cap_mb),
                "fix": "降贴图尺寸 / 压缩格式，或走机型档的 masterTextureLimit",
            })
    if mesh_cap_mb:
        cap = int(mesh_cap_mb) * 1024 * 1024
        if mesh_bytes > cap:
            problems.append({
                "code": "G11-MESHMEM",
                "message": "网格内存 %.1f MB，超过机型档上限 %d MB"
                           % (mesh_bytes / 1048576.0, mesh_cap_mb),
                "fix": "减面（低模 + 硬边，优先改拓扑而不是降模精度）",
            })

    return problems


# ---------------------------------------------------------------------------
# 退回 _WIP
# ---------------------------------------------------------------------------

def reject_failures(root, records, dry_run=False):
    """把失败资产移动到 <root>/_WIP/<原相对路径>/，并写 <名>_fail.json。

    安全约束（gate 不得破坏工程）：
      · 只移动受管资产类型（OBJ/FBX/PNG/TGA）
      · Unity 的 .meta 随宿主资产一起移动（保住 GUID 与引用）
      · 未受管文件（其它扩展名）永远不移动
    """
    rejected = []
    wip_root = os.path.join(root, WIP_DIR)

    for rec in records:
        if rec.passed:
            continue
        src = os.path.join(root, rec.path)
        if not os.path.isfile(src):
            continue
        base = os.path.basename(rec.path)
        movable = (os.path.splitext(base)[1].lower() in GATED_EXT or
                   rec.path.endswith(SIDECAR_EXT))
        if not movable:
            continue    # 二次保险：只退回受管资产

        rel_dir = os.path.dirname(rec.path)
        dst_dir = os.path.join(wip_root, rel_dir) if rel_dir else wip_root
        dst = os.path.join(dst_dir, base)
        # <文件名去扩展名>_fail.json
        fail_name = os.path.splitext(base)[0] + "_fail.json"
        fail_path = os.path.join(dst_dir, fail_name)

        fail_doc = {
            "gate_version": GATE_VERSION,
            "asset": rec.path,
            "category": rec.category,
            "kind": rec.kind,
            "failed_at": "gate.py",
            "failures": [f.to_dict() for f in rec.failures],
            "warnings": [w.to_dict() for w in rec.warnings],
            "instruction": "本文件不进人工评审队列。修好列出的失败项后，"
                           "把资产移回原目录重新跑 gate。",
        }

        if not dry_run:
            if not os.path.isdir(dst_dir):
                os.makedirs(dst_dir)
            if os.path.exists(dst):
                os.remove(dst)
            shutil.move(src, dst)

            # 连带移动 sidecar
            if rec.sidecar_path:
                sc_src = os.path.join(root, rec.sidecar_path)
                if os.path.isfile(sc_src):
                    sc_dst = os.path.join(dst_dir, os.path.basename(rec.sidecar_path))
                    if os.path.exists(sc_dst):
                        os.remove(sc_dst)
                    shutil.move(sc_src, sc_dst)
                    fail_doc["sidecar_moved"] = rec.sidecar_path

            # 连带移动 Unity .meta（保住 GUID 与既有引用）
            meta_src = src + UNITY_META_EXT
            if os.path.isfile(meta_src):
                meta_dst = os.path.join(dst_dir, base + UNITY_META_EXT)
                if os.path.exists(meta_dst):
                    os.remove(meta_dst)
                shutil.move(meta_src, meta_dst)
                fail_doc["unity_meta_moved"] = os.path.basename(meta_src)

            with open(fail_path, "w", encoding="utf-8") as fh:
                json.dump(fail_doc, fh, ensure_ascii=False, indent=2)

        rejected.append({
            "asset": rec.path,
            "moved_to": os.path.relpath(dst, root).replace("\\", "/"),
            "fail_json": os.path.relpath(fail_path, root).replace("\\", "/"),
            "failures": [f.code for f in rec.failures],
        })
    return rejected


# ---------------------------------------------------------------------------
# 报告输出
# ---------------------------------------------------------------------------

def print_text_report(report):
    print("=" * 72)
    print("EdgeChronicles 资产入库 Gate  v%s" % report["gate_version"])
    print("根目录：%s%s" % (report["root"], "   [DRY-RUN]" if report["dry_run"] else ""))
    print("-" * 72)
    for rec in report["records"]:
        tag = "PASS" if rec["passed"] else "FAIL"
        print("[%s] %s" % (tag, rec["path"]))
        for f in rec["failures"]:
            print("        %-10s %s" % (f["code"], f["message"]))
            if f["fix"]:
                print("                   修法：%s" % f["fix"])
        for w in rec["warnings"]:
            print("        %-10s %s" % (w["code"], w["message"]))
    print("-" * 72)
    print("合计 %d 项：通过 %d，失败 %d，退回 _WIP %d"
          % (report["total"], report["passed"], report["failed"], report["rejected"]))
    print("=" * 72)


# ---------------------------------------------------------------------------
# 自检
# ---------------------------------------------------------------------------

def _self_test():
    """用合成样本验证判定逻辑本身确实会失败（非静默通过）。

    每个场景用**独立的临时子目录**，互不污染（否则前一个场景的"故意失败"
    会漏进下一个场景的"应当通过"断言里）。
    """
    import tempfile

    tmp = tempfile.mkdtemp(prefix="ec_gate_selftest_")
    try:
        def scenario(name):
            d = os.path.join(tmp, name)
            os.makedirs(d)
            return d

        def hero_fixture(root, name="CH_HERO_Test", uv_scale=0.5, uv_min=0.1):
            d = os.path.join(root, "Character", "Hero")
            os.makedirs(d)
            _write_obj(os.path.join(d, "%s.obj" % name), uv_scale=uv_scale, uv_min=uv_min)
            _write_png(os.path.join(d, "TX_Hero.png"), 64, 64)
            with open(os.path.join(d, "%s.meta.json" % name), "w", encoding="utf-8") as fh:
                json.dump({
                    "tri_count": 2,
                    "material_slots": 1,
                    "textures": ["TX_Hero.png"],
                    "channels": REQUIRED_CHANNELS,
                    "origin": "self",
                }, fh)
            return d

        def run(root):
            return run_gate(root, dry_run=True, do_reject=False)

        def codes_of(report):
            return set(f["code"] for r in report["records"] for f in r["failures"])

        # 1) 合规的主角模型必须通过
        s1 = scenario("s1")
        hero_fixture(s1)
        code, report = run(s1)
        assert code == 0, "合规样本应通过，实际 exit=%d\n%s" % (
            code, json.dumps(report, ensure_ascii=False, indent=2))

        # 2) 越界 UV -> 必须失败 G6-RANGE
        s2 = scenario("s2")
        hero_fixture(s2, name="CH_HERO_Bad", uv_scale=1.4)
        code, report = run(s2)
        assert code == 1, "越界 UV 样本必须失败，实际 exit=%d" % code
        assert "G6-RANGE" in codes_of(report), "应命中 G6-RANGE"

        # 3) 命名不合规 + 缺 license -> 必须失败
        s3 = scenario("s3")
        d3 = os.path.join(s3, "Character", "Enemy")
        os.makedirs(d3)
        _write_obj(os.path.join(d3, "bad name.obj"), uv_scale=0.5)
        with open(os.path.join(d3, "bad name.meta.json"), "w", encoding="utf-8") as fh:
            json.dump({
                "tri_count": 2,
                "material_slots": 1,
                "textures": [],
                "channels": REQUIRED_CHANNELS,
                "origin": "purchased",
                "license": "",
            }, fh)
        code, report = run(s3)
        assert code == 1
        got = codes_of(report)
        assert "G5-NAME" in got, "应命中 G5-NAME，实际 %s" % got
        assert "G7-LICENSE" in got, "应命中 G7-LICENSE，实际 %s" % got

        # 4) 合规 UI 图标（RGBA + 透明边 >=28%）必须通过
        s4 = scenario("s4")
        d4 = os.path.join(s4, "UI", "Source")
        os.makedirs(d4)
        _write_png(os.path.join(d4, "UI_ICON_SKL_Core.png"), 64, 64,
                   rgba=True, margin_ratio=0.35)
        code, report = run(s4)
        assert code == 0, "合规 UI 图标应通过，实际 exit=%d\n%s" % (
            code, json.dumps(report, ensure_ascii=False, indent=2))

        # 5) UI 图标没有 alpha -> 必须失败 G3-NOALPHA
        s5 = scenario("s5")
        d5 = os.path.join(s5, "UI", "Source")
        os.makedirs(d5)
        _write_png(os.path.join(d5, "UI_ICON_SKL_Light.png"), 64, 64, rgba=False)
        code, report = run(s5)
        assert code == 1
        assert "G3-NOALPHA" in codes_of(report), "应命中 G3-NOALPHA"

        # 6) UI 图标透明边不足 -> 必须失败 G9-BORDER
        s6 = scenario("s6")
        d6 = os.path.join(s6, "UI", "Source")
        os.makedirs(d6)
        _write_png(os.path.join(d6, "UI_ICON_SKL_Heavy.png"), 64, 64,
                   rgba=True, margin_ratio=0.10)
        code, report = run(s6)
        assert code == 1
        assert "G9-BORDER" in codes_of(report), "应命中 G9-BORDER"

        # 7) 真实退回：确认文件被移进 _WIP 且写出 _fail.json
        s7 = scenario("s7")
        hero_fixture(s7, name="CH_HERO_Fail", uv_scale=1.4)
        code, report = run_gate(s7, dry_run=False, do_reject=True)
        assert code == 1
        wip_files = []
        for dirpath, _dirs, filenames in os.walk(os.path.join(s7, WIP_DIR)):
            wip_files.extend(filenames)
        assert any(f.endswith("_fail.json") for f in wip_files), \
            "应生成 _fail.json，实际 %s" % wip_files

        # 8) G12 静默失败点：Shader 已拷入工程但未收录 Always Included -> 必须失败
        fake = scenario("s8")
        sd = os.path.join(fake, "Assets", "_Project", "Art", "Shaders")
        os.makedirs(sd)
        os.makedirs(os.path.join(fake, "ProjectSettings"))
        repo = repo_root_from_script()
        for fname in ("GraphicsSettings.asset", "QualitySettings.asset"):
            src = os.path.join(repo, "ProjectSettings", fname)
            if os.path.isfile(src):
                shutil.copy(src, os.path.join(fake, "ProjectSettings", fname))
        guids = {}
        for i, shname in enumerate(("ECOutlineMask.shader",
                                    "ECOutlineDilate.shader",
                                    "ECOutlineComposite.shader")):
            src = os.path.join(repo, "Tools", "art", "shaders", shname)
            if not os.path.isfile(src):
                continue
            shutil.copy(src, os.path.join(sd, shname))
            guid = "%032x" % (0xABC00000 + i)
            guids[shname] = guid
            with open(os.path.join(sd, shname + ".meta"), "w",
                      encoding="utf-8") as mf:
                mf.write("fileFormatVersion: 2\nguid: %s\n" % guid)

        pipe_problems, _pipe_warnings = check_pipeline_wiring(fake)
        assert any(p["code"] == "G12-ALWAYS" for p in pipe_problems), \
            "未收录 Always Included 时应报 G12-ALWAYS，实际 %s" % pipe_problems

        gs = os.path.join(fake, "ProjectSettings", "GraphicsSettings.asset")
        if os.path.isfile(gs) and guids:
            with open(gs, "r", encoding="utf-8") as fh:
                gtxt = fh.read()
            marker = "  m_AlwaysIncludedShaders:\n"
            entry = "".join("  - {fileID: 4800000, guid: %s, type: 3\n"
                            % g for g in guids.values())
            if marker in gtxt:
                with open(gs, "w", encoding="utf-8") as fh:
                    fh.write(gtxt.replace(marker, marker + entry, 1))
                pipe_problems, _ = check_pipeline_wiring(fake)
                assert not any(p["code"] == "G12-ALWAYS" for p in pipe_problems), \
                    "已收录后不应再报 G12-ALWAYS"

        print("self-test OK：合规样本通过、违规样本失败、"
              "模型贴图无 alpha / UI 贴图必须有 alpha / 透明边框判定均生效、"
              "图集格位与资源预算判定接入、"
              "Always Included Shaders 静默失败点可被抓出、"
              "失败资产退回 _WIP 并写 _fail.json")
        return 0
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


def _write_obj(path, uv_scale=0.5, uv_min=0.1):
    """写一个 2 三角形的 OBJ，UV 落在 [uv_min, uv_min+uv_scale]。"""
    with open(path, "w", encoding="utf-8") as fh:
        fh.write("# gate self-test fixture\n")
        fh.write("usemtl M_Rock\n")
        fh.write("v 0 0 0\nv 1 0 0\nv 1 1 0\nv 0 1 0\n")
        fh.write("vt %f %f\nvt %f %f\nvt %f %f\nvt %f %f\n"
                 % (uv_min, uv_min,
                    uv_min + uv_scale, uv_min,
                    uv_min + uv_scale, uv_min + uv_scale,
                    uv_min, uv_min + uv_scale))
        fh.write("f 1/1 2/2 3/3\n")
        fh.write("f 1/1 3/3 4/4\n")


def _write_png(path, width, height, rgba=False, margin_ratio=0.3):
    """写一个最小合法 PNG。

    rgba=False -> RGB8（模型贴图，无 alpha）
    rgba=True  -> RGBA8，四周按 margin_ratio 留透明边（UI / VFX 贴图）
    """
    import zlib
    if not rgba:
        raw = b""
        for _y in range(height):
            raw += b"\x00" + bytes([128, 128, 128] * width)
        color_type = 2
        px = 3
    else:
        m = int(round(width * margin_ratio))
        raw = b""
        for y in range(height):
            row = bytearray()
            for x in range(width):
                inside = (m <= x < width - m) and (m <= y < height - m)
                row += bytes([20, 20, 20, 255] if inside else [0, 0, 0, 0])
            raw += b"\x00" + bytes(row)
        color_type = 6
        px = 4

    def chunk(tag, data):
        return (struct.pack(">I", len(data)) + tag + data +
                struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF))

    ihdr = struct.pack(">IIBBBBB", width, height, 8, color_type, 0, 0, 0)
    png = (PNG_SIGNATURE + chunk(b"IHDR", ihdr) +
           chunk(b"IDAT", zlib.compress(raw)) + chunk(b"IEND", b""))
    with open(path, "wb") as fh:
        fh.write(png)


# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------

def main(argv=None):
    parser = argparse.ArgumentParser(
        description="EdgeChronicles 资产入库 Gate（失败即非零退出码）")
    parser.add_argument("--root", default="Assets/_Project/Art",
                        help="扫描根目录（默认 Assets/_Project/Art）")
    parser.add_argument("--json", action="store_true",
                        help="以 JSON 输出报告（stdout）")
    parser.add_argument("--dry-run", action="store_true",
                        help="只判定不退回 _WIP（退出码仍然反映真实结果）")
    parser.add_argument("--no-reject", action="store_true",
                        help="不移动任何文件（与 --dry-run 同效）")
    parser.add_argument("--baseline", default=DEFAULT_BASELINE_TIER,
                        help="机型档（low / medium / high），资源预算按最低档判"
                             "（默认 %s）" % DEFAULT_BASELINE_TIER)
    parser.add_argument("--atlas-manifest", default=None,
                        help="图集派工清单（默认 %s）" % DEFAULT_ATLAS_MANIFEST_REL)
    parser.add_argument("--self-test", action="store_true",
                        help="跑内置自检（用合成样本验证判定逻辑）")
    args = parser.parse_args(argv)

    if args.self_test:
        return _self_test()

    try:
        code, report = run_gate(args.root,
                                 dry_run=args.dry_run or args.no_reject,
                                 do_reject=not (args.dry_run or args.no_reject),
                                 baseline_tier=args.baseline,
                                 atlas_manifest=args.atlas_manifest)
    except Exception as exc:                      # noqa: BLE001 - 顶层兜底，必须非零退出
        sys.stderr.write("[gate] 内部错误：%r\n" % exc)
        return 2

    if args.json:
        print(json.dumps(report, ensure_ascii=False, indent=2))
    else:
        print_text_report(report)

    return code


if __name__ == "__main__":
    sys.exit(main())
