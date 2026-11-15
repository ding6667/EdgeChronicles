# -*- coding: utf-8 -*-
"""人眼项录屏协议 —— 由代码目录**同源生成**。

用法：python docs/qa/gen_protocol_doc.py [工程根目录]
改判据请改 `Assets/_Project/Tests/Shared/Qa/CriterionCatalog.cs`，然后重跑本脚本；
不要手改生成的 .md —— 手改的那份会在下一次生成时被覆盖，而且从此与代码不同源。
"""
import io, os, re, sys
ROOT = sys.argv[1] if len(sys.argv) > 1 else os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
PRO = os.path.join(ROOT, "Assets/_Project/Tests/Shared/Qa/HumanEyeProtocols.cs")
pro = io.open(PRO, encoding="utf-8").read()

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
def sv(v):
    if v is None: return ""
    v = v.strip()
    return v[1:-1] if (v.startswith('"') and v.endswith('"')) else v

hp = []
for blk in pro.split("new HumanEyeProtocol")[1:]:
    blk = blk.split("\n            },")[0]
    d = fields(blk)
    if "Id" in d: hp.append(d)

out = []
w = out.append
w("# QA-v1.0 · 人眼项录屏判定协议")
w("")
w("| 项 | 内容 |")
w("|---|---|")
w("| 文档状态 | v1.0 · 与 `Assets/_Project/Tests/Shared/Qa/HumanEyeProtocols.cs` **同源生成** |")
w("| 适用范围 | 6 条必须人眼判据 + 7 条混合判据的人眼部分 |")
w("| 红线 | **禁止把\"人工确认\"写成通过判据。** 人眼项必须给出机器可辅助的复现条件 |")
w("")
w("## 0. 协议要解决什么")
w("")
w("「**换个人来判结论也要一样**」是硬要求。只写\"第三人称\"\"看起来偏重\"\"稍微有点卡\"这类描述，换一个人判出来的结论必然不同——那它就不是 Gate，只是一个意见。")
w("")
w("因此每条协议必须写死五件事：")
w("")
w("| 要素 | 作用 | 不写会怎样 |")
w("|---|---|---|")
w("| 机位 | 相机相对被测物的方位 / 高度 / 俯角 | 同一个动作从不同角度看，观感不同 |")
w("| 距离（m） | 被测物到相机的水平距离 | 透视强度随距离变，近看很重远看很轻 |")
w("| FOV（°） | 视场角 | FOV 越大同一动作显得越小越弱 |")
w("| 慢放倍数 | 录屏回放的降速倍率 | 60fps 原速看不清瞬时反馈 |")
w("| 判定动作 | 测试者被要求做的**唯一**动作，且可二值化 | 收集到的是意见不是结论 |")
w("")
w("外加**通过标准**（多少样本、什么比例算过）与**记录字段**（每次判定必须记什么，供事后复核）。")
w("")
w("### 自检（已实现为自动化测试）")
w("")
w("`Assets/_Project/Tests/EditMode/Criteria/HumanEyeProtocolTests.cs` 强制：")
w("")
w("1. 每条人眼 / 混合判据都必须指向一个存在的协议编号（HP-xx）。")
w("2. 每条协议的机位 / 距离 / FOV / 慢放 / 判定动作 / 通过标准 / 记录字段**一个都不能空**。")
w("3. 距离 / FOV / 慢放 / 判定动作 / 通过标准里必须出现**具体数值**。")
w("4. 协议文本中不得出现模糊措辞：`第三人称`、`看起来`、`明显`、`主观`、`适当`、`合理`、`大概`、`差不多`、`稍微`、`尽量`、`酌情`。")
w("5. 协议编号不得重复。")
w("6. 观察项 / 定位通道必须显式声明豁免负向用例的理由（WC-05 / WC-12 / WC-14）。")
w("")
w("---")
w("")
w("## 1. 协议清单")
w("")
w("| 编号 | 对应判据 | 标题 | 样本量 |")
w("|---|---|---|---|")
for d in hp:
    w("| `%s` | `%s` | %s | %s |" % (sv(d["Id"]), sv(d.get("CriterionId","")), sv(d.get("Title","")), sv(d.get("SampleSize",""))))
w("")
w("---")
w("")
w("## 2. 逐条协议")
w("")
for d in hp:
    w("### `%s` · %s" % (sv(d["Id"]), sv(d.get("Title",""))))
    w("")
    w("| 字段 | 内容 |")
    w("|---|---|")
    w("| 对应判据 | `%s` |" % sv(d.get("CriterionId","")))
    w("| 机位 | %s |" % sv(d.get("Camera","")))
    w("| 距离 | %s |" % sv(d.get("Distance","")))
    w("| FOV | %s |" % sv(d.get("Fov","")))
    w("| 慢放倍数 | %s |" % sv(d.get("SlowMotion","")))
    w("| 判定动作 | %s |" % sv(d.get("JudgementAction","")))
    w("| 通过标准 | %s |" % sv(d.get("PassCriteria","")))
    w("| 记录字段 | %s |" % sv(d.get("RecordFields","")))
    w("| 样本量 | %s |" % sv(d.get("SampleSize","")))
    w("| 备注 | %s |" % sv(d.get("Note","")))
    w("")
    w("**空白记录表**（复制到测试记录用）：")
    w("")
    w("| 次序 | 记录字段 | 判定 | 备注 |")
    w("|---|---|---|---|")
    for i in range(1, 4):
        w("| %d |  |  |  |" % i)
    w("")
w("---")
w("")
w("## 3. 录屏规范（所有协议通用）")
w("")
w("| 项 | 规定 |")
w("|---|---|")
w("| 分辨率 | 1920×1080（与 UI 五面板规格一致），不得缩放播放 |")
w("| 帧率 | 录制 60fps；回放慢放由各协议的「慢放倍数」栏规定 |")
w("| 命名 | `HP-xx_技能或场景_第N次_日期.mp4`，编号与协议一致 |")
w("| 完整性 | 每次判定必须保留**从预警出现到判定动作完成**的完整片段，不许剪掉等待段 |")
w("| 环境 | 记录显示器型号与分辨率；换机器复测时必须注明 |")
w("| 禁止 | 不许在录屏上叠加调试信息、HUD 数值或调试网格 |")
w("")
w("## 4. 与自动化判据的分工")
w("")
w("| 判据 | 脚本部分 | 人眼部分 |")
w("|---|---|---|")
w("| WC-01 闪避曲线 | SC-01 前 2 帧位移占比 | HP-01 末帧-待机姿势差 |")
w("| WC-02 闪避无敌 | SC-02 窗口长度与位置 | HP-02 10/10 纵斩 |")
w("| WC-04 受击轻重 | SC-04 三档距离落区间 | HP-03 盲测轻重 |")
w("| WC-05 方向可读 | — | HP-04（观察项） |")
w("| WC-10 顿帧语义 | SC-09 time_scale + PhaseFrame | HP-05 卡住且被顶开 |")
w("| WC-11 形态可辨 | SC-10 结构差异（数据层） | HP-07 B2 关键组 |")
w("| WC-12 色差组 | — | HP-06（定位通道） |")
w("| WC-13 形态组 | SC-14 对照组合规性 | HP-07 B2 关键组 |")
w("| WC-14 相机跟随 | — | HP-08（观察项） |")
w("| WC-15 Boss 范围 | SC-11 预警帧数与形状 | HP-09 30 帧内可读 |")
w("| WC-17 刀光 | SC-13 遮挡比 | HP-10 竖直面与离地 |")
w("| WC-18 预警编码 | — | HP-11 三通道 |")
w("| WC-19 墨线 | SC-14 亮度差推导 | HP-12 实际观感 |")
w("")
w("**两条都要过**才算该判据通过；只有脚本部分通过 = 画面上仍然可能出问题（这正是 D5「永远报绿」的形态）。")
w("")
w("*文档结束 · QA-v1.0 · 人眼项录屏判定协议 · 与代码目录同源*")

io.open(os.path.join(ROOT, "docs/qa/QA-v1.0-人眼项录屏协议.md"), "w", encoding="utf-8", newline="\n").write("\n".join(out) + "\n")
print("HP doc generated:", len(hp), "protocols")
