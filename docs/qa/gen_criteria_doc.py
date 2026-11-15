# -*- coding: utf-8 -*-
"""判据目录 —— 由代码目录**同源生成**。

用法：python docs/qa/gen_criteria_doc.py [工程根目录]
改判据请改 `Assets/_Project/Tests/Shared/Qa/CriterionCatalog.cs`，然后重跑本脚本；
不要手改生成的 .md —— 手改的那份会在下一次生成时被覆盖，而且从此与代码不同源。
"""
import io, os, re, sys

ROOT = sys.argv[1] if len(sys.argv) > 1 else os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
CAT = os.path.join(ROOT, "Assets/_Project/Tests/Shared/Qa/CriterionCatalog.cs")
PRO = os.path.join(ROOT, "Assets/_Project/Tests/Shared/Qa/HumanEyeProtocols.cs")

def read(p): return io.open(p, encoding="utf-8").read()
cat = read(CAT); pro = read(PRO)

def fields(block):
    """提取字段赋值。

    【必须支持一行多个赋值】C# 的对象初始化器常把两个字段写在同一行
    （如 `Id = "NC-A01", Class = NegativeCaseClass.Sensitivity, TargetCriterionId = ...`），
    按行解析会漏掉后面的字段 —— 漏一个字段，正文就少一列（甚至整张表空）。
    切分点用 ", 后面紧跟 标识符 =" 作为边界，字符串内部的逗号不会被误切。
    """
    d = {}
    for line in block.split("\n"):
        for m in re.finditer(r'(\w+)\s*=\s*(.*?)(?=,\s*\w+\s*=|$)', line):
            k = m.group(1); v = m.group(2).strip().rstrip(',').strip()
            if k not in d: d[k] = v
    return d

def strval(v):
    if v is None: return ""
    v = v.strip()
    if v.startswith('"') and v.endswith('"'):
        return v[1:-1]
    return v

def idlist(v):
    if not v: return []
    return re.findall(r'CriterionOracle\.(\w+)', v)

# ── 走查判据 ──
wt = []
for blk in cat.split("new CriterionRecord")[1:]:
    blk = blk.split("\n            },")[0]
    d = fields(blk)
    if "Id" not in d: continue
    if not d["Id"].strip('"').startswith("WC-"): continue
    mu = re.search(r'Unit\s*=\s*QaUnit\.(\w+)', blk)
    ma = re.search(r'Axis\s*=\s*QaClockAxis\.(\w+)', blk)
    d["_unit"] = mu.group(1) if mu else "None"
    d["_axis"] = ma.group(1) if ma else "None"
    d["_veto"] = "true" if re.search(r'Veto\s*=\s*true', blk) else "false"
    wt.append(d)

# ── 负向用例 ──
nc = []
for blk in cat.split("new NegativeCaseRecord")[1:]:
    blk = blk.split("\n            },")[0]
    d = fields(blk)
    if "Id" not in d: continue
    nc.append(d)

# ── 协议 ──
hp = []
for blk in pro.split("new HumanEyeProtocol")[1:]:
    blk = blk.split("\n            },")[0]
    d = fields(blk)
    if "Id" not in d: continue
    hp.append(d)

UNIT = {"None":"—","Meter":"m","Centimeter":"cm","MeterPerSecond":"m/s","Frame":"帧","Millisecond":"ms",
        "Second":"s","Degree":"°","Ratio":"比例","Percent":"%","Count":"个数","Pixel":"px"}
AXIS = {"None":"—","WallClock":"W 墙钟","Clip":"C 剪辑","LogicTick":"逻辑 60Hz"}
EXEC = {"AutoScript":"纯脚本","Mixed":"混合","HumanEye":"必须人眼"}

out = []
w = out.append
w("# QA-v1.0 · 判据目录（22 条走查 + 30 条负向用例）")
w("")
w("| 项 | 内容 |")
w("|---|---|")
w("| 文档状态 | v1.0 · 与 `Assets/_Project/Tests/Shared/Qa/CriterionCatalog.cs` **同源生成**（改判据先改代码，本文件由脚本重生成） |")
w("| 唯一真值源 | 判据正文与阈值以代码目录为准；本文件是它的可读投影 |")
w("| 适用 | Gate 1 走查、导表断言验收、负向用例评审 |")
w("")
w("## 0. 怎么读这本目录")
w("")
w("每条判据必须回答 GDD §12.1 的**判据四问**，缺一条不予收录：")
w("")
w("1. **判据量的是什么？** → 必须写清量纲与时钟轴。拿不同量纲的量相减，结论永远是假的。")
w("2. **判据怎么量化？** → 必须能落到\"脚本判定\"或\"写死机位的人眼判定\"。")
w("3. **判据的假阳性是什么？** → **必须能写出自己的误判条件**，写不出就不收。")
w("4. **证伪案例是什么？** → 每条判据必须配负向用例；漏挂即废判据，**不是调阈值**。")
w("")
w("时间量一律用 `T_xxx`（elapsed 时刻，t=0 在命中帧）；帧号只用于标识对象，**禁止参与算术**。")
w("")
w("### 编号说明")
w("")
w("22 条走查表与 30 条负向用例的**权威编号清单在现有文档中不存在**（GDD §10.1 / §13 引用了它们，但无正文定义），因此本目录使用 QA 内部编号：")
w("")
w("| 前缀 | 含义 |")
w("|---|---|")
w("| `WC-xx` | 走查判据（Walkthrough Criterion） |")
w("| `NC-Axx` | A 类负向用例 · 灵敏度：注入一个错误 → 判据**必须报错** |")
w("| `NC-Bxx` | B 类负向用例 · 特异性 / 防误杀：注入一个**合法但不同**的设计 → 判据**必须通过** |")
w("| `A19`–`A43` | 导表断言沿用其既有编号（IMPL-v2.0 §5 / EL-03 §2），与本文不是两份定义 |")
w("")
w("### 三层关系（D-06 裁定：代码即正本）")
w("")
w("> 全量判据 **70** 条（在册）→ **负向用例 30** 条（24 A 类 + 6 B 类）+ **正向走查 22** 条（9 纯脚本 + 7 混合 + 6 必须人眼）。")
w("> 推导关系：**30 与 22 都从属于全量判据，且两者互不相交**。")
w("> 三条数字不是并列的三个总数，而是**同一本目录的三个切面**——由 `CriterionCatalog.DescribeRelations()` 统一给出，防止它们各自漂移。")
w("")
w("### 对位断言（EL-03 §5 CI 判据矩阵已改为引用式）")
w("")
w("EL-03 §5 的 CI 判据矩阵已改为**只引 §2 的断言编号**，判据逻辑的唯一真值源是 **EL-03 §2**。")
w("因此本目录凡与 A19–A43 重叠的部分**只引用编号、不重新定义判据**；没有对应断言的填 `—`。")
w("")
w("**实测重叠只有 3 条**（WC-01→A24、WC-09→A30、WC-10→A30）——这说明两套体系基本正交：")
w("")
w("| 体系 | 管什么 |")
w("|---|---|")
w("| 22 条走查判据 | 手感 / 可读性（打击感、预警、盲测可辨性） |")
w("| A19–A43 | 数据完整性（表结构、外键、枚举、区间、派生一致性） |")
w("")
w("> 另：**A38 的战力权重已改为引用式**（引 GDD §4.2 修为线权重表，不再写死 6 个数字）。")
w("> 测试侧夹具**不构造任何战力权重**——A38 由生产导表器执行，读表而非读常量。")
w("")
w("> 已登记为规格冲突 **D-06**：GDD §13 的「30 条用例（22 正向 + 24 A 类 + 6 B 类）」同句内 22+24+6=52≠30，正文与计数自相矛盾。等待正本确认后做一次编号映射。")
w("")
w("### 编号正本与三层算术")
w("")
w("**正本**：`Assets/_Project/Tests/Shared/Qa/CriterionCatalog.cs` 就是判据编号的唯一真值源。")
w("GDD §10.1 / §12 / §13 与 IMPL §7 一律改为**引用这里的 id**，不再各自编号。")
w("")
w("| 层 | 数量 | 含义 | 关系 |")
w("|---|---|---|---|")
w("| 全量判据 | **70** | 所有可判定项（22 走查 + 48 断言 / 打击感判据） | — |")
w("| 负向用例 | **30** | 24 A 类（灵敏度）+ 6 B 类（特异性） | ⊆ 全量判据 |")
w("| 正向走查 | **22** | 9 纯脚本 + 7 混合 + 6 必须人眼 | ⊆ 全量判据 |")
w("")
w("**负向用例 ∩ 正向走查 = ∅**（30 + 22 = 52 条互不重叠）：正向走查回答「要满足什么」，负向用例回答「注入错误后判据还报不报」。")
w("")
w("> 全量数从 69 变 70 是因为 **A39 拆成 A39-1 / A39-2**（语义变了，判据条数 +1）。")
w("> 数字写在代码里（`CriterionCounts`），文档只引用、不复述——复述即第二个真值源。")
w("")
w("### GDD 已有编号的映射")
w("")
w("设计线裁定「正本编号沿用 GDD 已有 ID」，因此每条走查判据额外保留 `LegacyDocId` 映射列。")
w("GDD 里没有正文定义的编号（**A2 / A4**）暂留空——**宁可空着，不做假映射**。")
w("")
w("| 走查判据 | GDD 旧编号 |")
w("|---|---|")
for d in wt:
    lg = strval(d.get("CanonicalId", ""))
    if lg:
        w("| `%s` | %s |" % (strval(d["Id"]), lg))
w("")
w("### 可执行化分布")
w("")
w("| 等级 | 条数 | 判据编号 |")
w("|---|---|---|")
for lvl in ["AutoScript","Mixed","HumanEye"]:
    ids = [strval(x["Id"]) for x in wt if x.get("Executability","").endswith(lvl)]
    w("| %s | %d | %s |" % (EXEC[lvl], len(ids), "、".join(ids)))
w("")
w("分布为 **9 / 7 / 6**，与 GDD §10.1 W4「9 条纯脚本 / 7 条混合 / 6 条必须人眼」一致，可作为本目录重建合理性的旁证（**不作为编号正确性的证据**）。")
w("")
w("---")
w("")
w("## 1. 22 条走查判据")
w("")
w("| 编号 | 正本编号 | 标题 | 量纲 | 时钟轴 | 可执行化 | 对位断言 | 落点 | 一票否决 |")
w("|---|---|---|---|---|---|---|---|---|")
for d in wt:
    unit = UNIT.get(d.get("_unit","None"), d.get("_unit",""))
    axis = AXIS.get(d.get("_axis","None"), d.get("_axis",""))
    ex = EXEC.get(d.get("Executability","").split(".")[-1], d.get("Executability",""))
    veto = "**是**" if d.get("_veto") == "true" else "否"
    rel = re.findall(r'"([^"]+)"', d.get("RelatedAssertionIds", ""))
    reltxt = "、".join(rel) if rel else "—"
    w("| `%s` | %s | %s | %s | %s | %s | %s | %s | %s |" % (
        strval(d["Id"]), strval(d.get("CanonicalId","—")), strval(d.get("Title","")), unit, axis, ex, reltxt,
        strval(d.get("ExecutableAs","")), veto))
w("")
w("### 逐条明细")
w("")
for d in wt:
    w("#### `%s` %s" % (strval(d["Id"]), strval(d.get("Title",""))))
    w("")
    w("- **判据**：%s" % strval(d.get("Statement","")))
    w("- **量纲 / 时钟轴**：%s / %s" % (UNIT.get(d.get("_unit","None"),""), AXIS.get(d.get("_axis","None"),"")))
    w("- **可执行化**：%s → %s" % (EXEC.get(d.get("Executability","").split(".")[-1],""), strval(d.get("ExecutableAs",""))))
    w("- **自身的误判条件**：%s" % strval(d.get("FalsePositiveCondition","")))
    if d.get("NegativeCaseWaiver"):
        w("- **负向用例豁免理由**：%s" % strval(d["NegativeCaseWaiver"]))
    w("- **出处**：%s" % strval(d.get("SourceRef","")))
    w("")
w("---")
w("")
w("## 2. 30 条负向用例")
w("")
w("### 2.1 A 类 · 灵敏度（24 条）")
w("")
w("> **规则**：注入一个错误 → 判据必须报错。没报错说明什么，必须写出来。")
w("> **铁律**：漏挂即废判据，**不是调阈值**。判据抓不住错时，正确动作是重写判据。")
w("")
w("| 编号 | 目标判据 | 对位断言 | 注入什么 | 期望结果 | 没报说明什么 | 通道 |")
w("|---|---|---|---|---|---|---|")
for d in nc:
    if "Sensitivity" not in d.get("Class",""): continue
    tgt = d.get("TargetCriterionId","").replace("CriterionOracle.","")
    rel = re.findall(r'"([^"]+)"', d.get("RelatedAssertionIds", ""))
    reltxt = "、".join(rel) if rel else "—"
    w("| `%s` | `%s` | %s | %s | %s | %s | %s |" % (
        strval(d["Id"]), tgt, reltxt, strval(d.get("Injection","")), strval(d.get("Expected","")),
        strval(d.get("MeaningIfNotFired","")), "脚本" if "AutoScript" in d.get("Channel","") else "人眼"))
w("")
w("### 2.2 B 类 · 特异性 / 防误杀（6 条）")
w("")
w("> **收录原则**：每条必须写清\"**哪条合法设计会被误杀**\"。写不清楚的，说明这条用例不该存在。")
w("> ")
w("> **为什么这 6 条比 A 类更重要**：项目历史上三次翻车（刀光面积阈值 / 脉冲频率常量 / 帧率依赖单位）注入的**都不是错误，是合法设计**——是判据**量错了对象**。A 类永远抓不到这类。")
w("")
w("| 编号 | 目标判据 | 对位断言 | 注入什么（合法但不同） | 期望结果 | 哪条合法设计会被误杀 |")
w("|---|---|---|---|---|---|")
for d in nc:
    if "Specificity" not in d.get("Class",""): continue
    tgt = d.get("TargetCriterionId","").replace("CriterionOracle.","")
    rel = re.findall(r'"([^"]+)"', d.get("RelatedAssertionIds", ""))
    reltxt = "、".join(rel) if rel else "—"
    w("| `%s` | `%s` | %s | %s | %s | %s |" % (
        strval(d["Id"]), tgt, reltxt, strval(d.get("Injection","")), strval(d.get("Expected","")),
        strval(d.get("LegalDesignAtRisk",""))))
w("")
w("---")
w("")
w("## 3. 执行入口")
w("")
w("| 内容 | 路径 |")
w("|---|---|")
w("| 22 + 30 判据目录（机器可读） | `Assets/_Project/Tests/Shared/Qa/CriterionCatalog.cs` |")
w("| 判据参考执行器（oracle） | `Assets/_Project/Tests/Shared/Qa/CriterionOracle.cs` |")
w("| 规格常量（跨域数字冻结版） | `Assets/_Project/Tests/Shared/Qa/QaPrimitives.cs` → `CombatSpec` |")
w("| A 类负向用例执行 | `Assets/_Project/Tests/EditMode/Criteria/NegativeCaseClassATests.cs` |")
w("| B 类负向用例执行 | `Assets/_Project/Tests/EditMode/Criteria/NegativeCaseClassBTests.cs` |")
w("| 目录完整性自检 | `Assets/_Project/Tests/EditMode/Criteria/CatalogIntegrityTests.cs` |")
w("| 人眼项协议 | `Assets/_Project/Tests/Shared/Qa/HumanEyeProtocols.cs` + `docs/qa/QA-v1.0-人眼项录屏协议.md` |")
w("| **静态自检（不需 Unity）** | `python docs/qa/qa_lint.py` |")
w("| **43 条负向注入离线自检** | `python docs/qa/qa_oracle_mirror.py` |")
w("")
w("## 4. 已登记的判据缺陷与规格冲突")
w("")
w("这些不是 bug，是**判据本身的问题**。逐条列出、逐条登记，不允许它们以\"静默通过\"的形式留在生产代码里。")
w("")
w("| 编号 | 冲突内容 | 出处 A | 出处 B | 影响 | 处置 |")
w("|---|---|---|---|---|---|")

conf = read(os.path.join(ROOT, "Assets/_Project/Tests/Shared/Qa/References.cs"))
for blk in conf.split("new SpecConflict")[1:]:
    blk = blk.split("\n            },")[0]
    d = fields(blk)
    if "Id" not in d: continue
    res = strval(d.get("Resolution",""))
    res = ("**" + res + "**") if res.startswith("Resolved") else res
    w("| `%s` | %s | %s | %s | %s | %s |" % (
        strval(d["Id"]), strval(d.get("Summary","")), strval(d.get("SourceA","")),
        strval(d.get("SourceB","")), strval(d.get("Impact","")), res))
w("")
w("**判据与被测规格冲突时，正确动作是改判据或改规格，不是放宽阈值**；但改哪一边属于规格裁定权，不在 QA。")
w("")
w("处置状态：`Resolved` = 已裁定并落地；`Pending` = 两条裁定冲突、待复核（测试侧双口径就绪，不阻塞）；`Registered` = 已登记、待裁定。")
w("")
w("*文档结束 · QA-v1.0 · 判据目录 · 与代码目录同源*")

io.open(os.path.join(ROOT, "docs/qa/QA-v1.0-判据目录.md"), "w", encoding="utf-8", newline="\n").write("\n".join(out) + "\n")
print("判据目录 generated:", len(out), "lines;", len(wt), "走查;", len(nc), "负向;", len(hp), "协议")
