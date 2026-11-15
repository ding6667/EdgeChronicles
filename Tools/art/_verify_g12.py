# -*- coding: utf-8 -*-
"""G12 两分支验证：已拷入工程但未收录 -> 应失败；已收录 -> 应通过。"""
import importlib.util
import os
import shutil

spec = importlib.util.spec_from_file_location("gate", "Tools/art/gate.py")
g = importlib.util.module_from_spec(spec)
spec.loader.exec_module(g)

REPO = os.path.abspath(".")
root = os.path.join(os.environ.get("TEMP", "/tmp"), "ec_g12_verify")
shutil.rmtree(root, ignore_errors=True)
sd = os.path.join(root, "Assets", "_Project", "Art", "Shaders")
os.makedirs(sd)
os.makedirs(os.path.join(root, "ProjectSettings"))
for f in ("GraphicsSettings.asset", "QualitySettings.asset"):
    shutil.copy(os.path.join(REPO, "ProjectSettings", f),
                os.path.join(root, "ProjectSettings", f))

guids = {}
for i, sh in enumerate(("ECOutlineMask.shader", "ECOutlineDilate.shader",
                        "ECOutlineComposite.shader")):
    shutil.copy(os.path.join(REPO, "Tools", "art", "shaders", sh),
                os.path.join(sd, sh))
    guid = "%032x" % (0xABC00000 + i)
    guids[sh] = guid
    with open(os.path.join(sd, sh + ".meta"), "w", encoding="utf-8") as fh:
        fh.write("fileFormatVersion: 2\n")
        fh.write("guid: %s\n" % guid)
        fh.write("ShaderImporter:\n  externalObjects: {}\n")

gs = os.path.join(root, "ProjectSettings", "GraphicsSettings.asset")
with open(gs, encoding="utf-8") as fh:
    txt = fh.read()
marker = "  m_AlwaysIncludedShaders:\n"
assert marker in txt, "锚点没找到"
entry = "".join("  - {fileID: 4800000, guid: %s, type: 3}\n" % g for g in guids.values())

probs, warns = g.check_pipeline_wiring(root)
print("场景1（已拷入工程、未收录 Always Included）")
print("  problems:", [(p["code"], p["message"][:70]) for p in probs])
print("  warnings:", [w["code"] for w in warns])
assert any(p["code"] == "G12-ALWAYS" for p in probs), "场景1 应报 G12-ALWAYS"

with open(gs, "w", encoding="utf-8") as fh:
    fh.write(txt.replace(marker, marker + entry, 1))

probs, warns = g.check_pipeline_wiring(root)
print("场景2（已收录 Always Included）")
print("  problems:", [(p["code"], p["message"][:70]) for p in probs])
print("  warnings:", [w["code"] for w in warns])
assert not any(p["code"] == "G12-ALWAYS" for p in probs), "场景2 不应再报 G12-ALWAYS"

shutil.rmtree(root, ignore_errors=True)
print("G12 两分支验证通过")
