# -*- coding: utf-8 -*-
"""生成 docs/qa/STATUS-2026-11-15.md（状态说明）。

【为什么用脚本生成】判据缺陷登记的"逐条结论"必须与代码目录
`Assets/_Project/Tests/Shared/Qa/References.cs` → `KnownSpecConflicts` 同源。
手抄一遍 = 第二个真值源 = 这个项目十几轮一直在杀的那个病。
"""

import io
import os
import re
import sys

ROOT = sys.argv[1] if len(sys.argv) > 1 else os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
REF = os.path.join(ROOT, "Assets/_Project/Tests/Shared/Qa/References.cs")
OUT = os.path.join(ROOT, "docs/qa/STATUS-2026-11-15.md")


def first_sentence(text):
    t = text.strip()
    for sep in ("。", "；"):
        i = t.find(sep)
        if i > 0:
            t = t[:i + 1]
            break
    return t.replace("|", "/")


def main():
    src = io.open(REF, encoding="utf-8").read()
    rows = []
    for blk in src.split("new SpecConflict")[1:]:
        blk = blk.split("\n            },")[0]
        cid = re.search(r'Id = "(D-\d+)"', blk)
        res = re.search(r'Resolution = "([^"]*)"', blk)
        if not (cid and res):
            continue
        r = res.group(1)
        state = "已修" if r.startswith("Resolved") else ("待复核" if r.startswith("Pending") else "未裁")
        concl = first_sentence(r)
        for pre in ("Resolved · ", "Pending · ", "Registered"):
            if concl.startswith(pre):
                concl = concl[len(pre):]
                break
        rows.append((cid.group(1), "| `%s` | %s | %s |" % (cid.group(1), concl, state)))
    rows.sort(key=lambda x: (len(x[0]), x[0]))
    rows = [r[1] for r in rows]

    body = """# EC-S1-QA-01 · 状态说明（2026-11-15）

| 项 | 内容 |
|---|---|
| 负责 | quality-lead（严守真）· 测试策略 / 判据 / 夹具 / 冒烟 / 回归规范 |
| 路径归属 | `Assets/_Project/Tests/**` + `docs/qa/**`（**只由 QA 合并**，工程线不直接改） |
| 提交 | 未提交 / 未推送（由主理人统一提交） |
| 静态自检 | `python docs/qa/qa_lint.py` → 通过（28 个 .cs） |
| 判据自检 | `python docs/qa/qa_oracle_mirror.py` → 通过（30 规范集 + 15 补充集 × 两种曲线口径） |

---

## 1. 今天实际落了什么

### 1.1 文档（`docs/qa/`）

| 文件 | 内容 |
|---|---|
| `QA-v1.0-判据目录.md` | 22 条走查判据 + 30 条负向用例 + 冲突登记；**由代码目录同源生成**（`gen_criteria_doc.py`） |
| `QA-v1.0-人眼项录屏协议.md` | 12 条录屏协议：机位 / 距离 / FOV / 慢放倍数 / 判定动作 / 通过标准 / 记录字段全部写死（`gen_protocol_doc.py` 生成） |
| `QA-v1.0-Bug分级与回归规范.md` | 4 轴分级（S0–S4）+ L0–L3 回归层 + 19 条禁止事项 |
| `QA-v1.0-测试规范.md` | 判据四问 / 量纲与时钟轴 / 漏挂即废判据 / 假通过治理 / 生产契约清单 / 缺口清单 / CI 接入 |
| `STATUS-2026-11-15.md` | 本文件（由 `gen_status_doc.py` 生成） |

### 1.2 代码（`Assets/_Project/Tests/`，28 个 .cs / 3 个 asmdef）

| 目录 | 内容 |
|---|---|
| `Shared/Qa/` | 判据目录（**编号正本**）+ 判据参考执行器（oracle）+ 夹具工厂 + 中立数据模型 + 参考实现 + 生产契约清单 + 冲突登记 |
| `EditMode/Smoke/` | 8 项冒烟（事件总线零分配 / 导表 A19–A43 / 取消窗口回放 / 曲线积分 / 命中几何 60 例 / 顿帧三档 / Boss 冷启动序列 / 存档 CRC） |
| `EditMode/Criteria/` | 目录完整性、24 A 类、6 B 类、15 补充类、裁定落地自检、编号正本三层算术、机制枚举 parity |
| `EditMode/Contracts/` | 8 条生产契约（**契约缺失即红，不 Skip**） |
| `PlayMode/` | tick 8 阶段顺序与 no_writeback（骨架已就位，等生产契约落地后扩展） |

### 1.3 判据在册

| 层 | 数量 | 关系 |
|---|---|---|
| 全量判据 | **71** | 22 走查 + 49 断言 / 打击感判据 |
| 负向用例（规范集） | **30** | 24 A 类（灵敏度）+ 6 B 类（特异性）；属于全量 |
| 正向走查 | **22** | 9 纯脚本 + 7 混合 + 6 必须人眼；属于全量 |
| 补充负向注入 | **15** | 补齐 30 条未覆盖的断言分支（**不计入规范集**） |

> 30 与 22 互不相交（有测试锁住）。三层数字写在代码里（`CriterionCounts`），文档只引用不复述。

### 1.4 两条不依赖 Unity 的自检脚本（可直接挂 lint.yml）

| 脚本 | 作用 |
|---|---|
| `docs/qa/qa_lint.py` | L1 字符串提前结束 · L2 缺逗号 · L3 全角标点混入代码位 · L4 编码 · L5 纪律词 · L6 判据自检 · L7 初始化器成员名必须在类里声明 |
| `docs/qa/qa_oracle_mirror.py` | 45 条负向注入离线自检；判据逻辑的**第二实现** |

**两条都做过反向验证**（故意注入错误确认会红）——没有反向验证的检查脚本只是"看起来很认真的代码"。

---

## 2. 遗留什么、卡在哪、下一步该谁做什么

| # | 遗留 | 卡在哪 | 下一步 | 谁 |
|---|---|---|---|---|
| 1 | **全仓真编译未拿到实据** | Unity Editor 关着；沙箱禁止任何 C# 编译器（未绕过） | 开 Editor 跑 MCP `hasErrors` 探针，或跑工程侧的编译自证脚本 | release-ops-lead |
| 2 | 工程线 6 处编译错误 | 全在工程线自己未写完的导表断言文件里 | 清零后重跑 #1 | engineering-lead |
| 3 | 7 条生产契约未落地 | `TableValidator` / `SkillRuntime` / `MovementCurveEvaluator` / `HitGeometry` / `CombatTuning` / `BossRuleEvaluator` / `SaveCodec` 尚不存在 | 按 `QA-v1.0-测试规范.md` §3 落名（改签名只改 `ProductionContracts.cs` 一处） | engineering-lead |
| 4 | EL-03 断言层 6 处口径 | A33 ② / A33-2 / A33-3 / A33-5 / A39「络脉 +1」/ `tech_skill` 残留 | **已提级为工程线阻塞项**；口径定了之后我再对齐判据描述（**现在不猜着改**） | engineering-lead → design-strategist → QA |
| 5 | 曲线 `value` 的 schema 注释 | D-13 已落「逐帧速度 m/s」 | 按 m/s 改注释与派生列算法 | engineering-lead |
| 6 | `HitShapeRow.frame` 的轴标注 | D-12 已落「墙钟 W」 | C 改 W | engineering-lead |
| 7 | `Vec2` 定点精度 | D-11 已落 1/1000 m | `UnitsPerMeter` 100 改 1000 | engineering-lead |
| 8 | 12 条人眼项的实测结论 | 需要录屏与盲测人力 | Gate 时由 QA 组织，结论以带协议编号的录屏归档 | QA（Gate 时） |
| 9 | A2 / A4 的正本编号 | GDD §10.1 提到但**四份文档里都无正文定义** | 映射表里留空（宁可空着不做假映射）；正本补齐后再填 | design-strategist |
| 10 | D-09 A29 的模拟序列 | 「连段模拟」没定义序列与时间步长 | 给一条标准连段序列，或明确由生产侧指定序列执行 | design-strategist |

---

## 3. 判据缺陷登记：逐条处置结论

> 全表在 `Assets/_Project/Tests/Shared/Qa/References.cs` → `KnownSpecConflicts`（**代码即正本**），本节由脚本生成。

| 编号 | 结论 | 状态 |
|---|---|---|
%s

---

## 4. 明早接手的第一动作

1. 等工程线 6 处编译错误清零 → 跑工程侧编译自证脚本 → 拿到「全仓 0 error」的实据 → **QA 这条门才能关**。
2. 跑 `python docs/qa/qa_lint.py` 与 `python docs/qa/qa_oracle_mirror.py` 确认判据侧仍全绿（这两条不需要 Unity）。
3. 若工程线改了判据语义：**发 QA 一条「改哪条、改成什么」**，由 QA 合并进 `Assets/_Project/Tests/**`（**不要直接改测试目录**——并发写过一次的代价是字段重名，下次可能是语义打架）。

---

*本文件是快照，不复述判据正文——判据正文以 `CriterionCatalog.cs` 为准。*
""" % ("\n".join(rows))

    tmp = OUT + ".tmp"
    with io.open(tmp, "w", encoding="utf-8", newline="") as f:
        f.write(body)
        f.flush()
        os.fsync(f.fileno())
    os.replace(tmp, OUT)
    print("STATUS generated: %d 条缺陷登记" % len(rows))
    return 0


if __name__ == "__main__":
    sys.exit(main())
