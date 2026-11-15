# -*- coding: utf-8 -*-
"""QA 静态自检（不需要 Unity，1 秒跑完）。

【它挡什么】
本项目在这批文件上踩过两次"整仓编译红"，两次都是**静态可查**的：

  ① 中文引号被写成 ASCII 直引号 → C# 字符串字面量提前结束 → 一连串 CS1003。
     （第一次：154 + 76 + 2 处；第二次：对象初始化器里 `Veto = false` 少一个逗号。）
  ② 判据正文里的全角标点漏进代码位置。

【为什么值得固化成脚本】
这两个 bug 的共同点是：**编译器发现得晚、影响面却极大**（测试程序集编译失败 = 全仓编译失败
= CI 红 = 出包失败）。把它们挡在提交前，比在 CI 里等编译失败便宜得多。

【检查项】
  L1 C# 词法：字符串字面量是否被提前结束（真正的 C# 词法扫，不是数引号个数）
  L2 C# 词法：对象初始化器里成员赋值后是否缺逗号
  L3 C# 词法：代码位置（字符串外）是否混入全角标点
  L4 编码：UTF-8 无 BOM + LF
  L5 纪律：源码里不得出现 TODO / 待定 / 工期 / 成本 / 人日 / 预算 / 排期
  L6 判据自检：调用 qa_oracle_mirror.py 跑 43 条负向注入

用法：python docs/qa/qa_lint.py [工程根目录]
退出码 0 = 通过；1 = 有问题。
"""

import os
import subprocess
import sys

# 全角标点（代码位置出现即错）
FULLWIDTH = "，（）；：？！【】《》"
# 禁止词（项目纪律）
BANNED_WORDS = ["TODO", "待定", "工期", "成本", "人日", "预算", "排期"]


def iter_cs_files(root):
    base = os.path.join(root, "Assets", "_Project", "Tests")
    for dirpath, _dirnames, filenames in os.walk(base):
        for name in filenames:
            if name.endswith(".cs"):
                yield os.path.join(dirpath, name)


def strip_code(line):
    """粗略去掉字符串与行注释，只留代码部分。"""
    out = []
    i = 0
    n = len(line)
    in_str = False
    while i < n:
        c = line[i]
        if in_str:
            if c == "\\":
                i += 2
                continue
            if c == '"':
                in_str = False
            i += 1
            continue
        if c == '"':
            in_str = True
            i += 1
            continue
        if c == "/" and i + 1 < n and line[i + 1] == "/":
            break
        out.append(c)
        i += 1
    return "".join(out)


def check_strings_and_punct(path, problems):
    """L1 + L3：按 C# 词法扫字符串边界，并检查代码位置的全角标点。"""
    with open(path, encoding="utf-8") as f:
        src = f.read()
    i = 0
    n = len(src)
    line = 1
    legal = set(",;)]}:")
    while i < n:
        c = src[i]
        if c == "\n":
            line += 1
            i += 1
            continue
        if c == "/" and i + 1 < n and src[i + 1] == "/":
            while i < n and src[i] != "\n":
                i += 1
            continue
        if c == "/" and i + 1 < n and src[i + 1] == "*":
            i += 2
            while i + 1 < n and not (src[i] == "*" and src[i + 1] == "/"):
                if src[i] == "\n":
                    line += 1
                i += 1
            i += 2
            continue
        if c == "@" and i + 1 < n and src[i + 1] == '"':
            i += 2
            while i < n:
                if src[i] == '"':
                    if i + 1 < n and src[i + 1] == '"':
                        i += 2
                        continue
                    i += 1
                    break
                if src[i] == "\n":
                    line += 1
                i += 1
            continue
        if c == '"':
            start_line = line
            i += 1
            while i < n:
                if src[i] == "\\":
                    i += 2
                    continue
                if src[i] == '"':
                    i += 1
                    break
                if src[i] == "\n":
                    line += 1
                    problems.append("%s:%d 字符串字面量未闭合" % (path, start_line))
                    break
                i += 1
            j = i
            while j < n and src[j] in " \t":
                j += 1
            if j >= n:
                continue
            nxt = src[j]
            if nxt == "\n":
                line += 1
                continue
            if nxt in legal or nxt in "=+|&":
                continue
            if nxt == "/" and j + 1 < n and src[j + 1] in "/*":
                continue
            problems.append(
                "%s:%d 字符串提前结束（下一个字符是 %r）——多半是中文引号被写成了 ASCII 直引号"
                % (path, start_line, nxt))
            continue
        i += 1

    with open(path, encoding="utf-8") as f:
        for idx, ln in enumerate(f.read().split("\n"), 1):
            code = strip_code(ln)
            for ch in FULLWIDTH:
                if ch in code:
                    problems.append("%s:%d 代码位置混入全角标点「%s」" % (path, idx, ch))


def check_commas(path, problems):
    """L2：对象初始化器里成员赋值行若不以逗号结尾、下一行仍是成员赋值 → 缺逗号。"""
    with open(path, encoding="utf-8") as f:
        lines = f.read().split("\n")
    for i, ln in enumerate(lines):
        # 去掉行尾注释与字符串内容（枚举成员 `X = 0,   // 注释` 会把逗号藏在注释后面；
        # 而 `anchor://` 这样的内容里带 `//`，不能用朴素的 split("//")）
        stripped = strip_code(ln).strip()
        if not stripped or stripped.startswith("//"):
            continue
        # 方法链续行（`.Select(...)`）不是成员赋值
        if stripped.startswith("."):
            continue
        if stripped.endswith(",") or stripped.endswith(";"):
            continue
        if "=" not in stripped or stripped.startswith("if ") or stripped.startswith("return"):
            continue
        # 形如 `Name = value`（无分号、无逗号结尾）
        head = stripped.split("=", 1)[0].strip()
        if not head or " " in head:
            continue
        j = i + 1
        while j < len(lines) and lines[j].strip() == "":
            j += 1
        if j >= len(lines):
            continue
        nxt = strip_code(lines[j]).strip()
        if nxt.startswith("//") or nxt.startswith("}") or nxt.startswith("{"):
            continue
        if nxt.startswith("."):
            continue
        nhead = nxt.split("=", 1)[0].strip() if "=" in nxt else ""
        if nhead and " " not in nhead and not nxt.endswith(";") and not nxt.endswith(","):
            problems.append("%s:%d 成员赋值后可能缺逗号（下一行仍是成员赋值）→ %s"
                            % (path, i + 1, stripped[:60]))


def check_encoding(path, problems):
    """L4：UTF-8 无 BOM + LF。"""
    with open(path, "rb") as f:
        raw = f.read()
    if raw.startswith(b"\xef\xbb\xbf"):
        problems.append("%s 含 UTF-8 BOM（Unity 下会出乱码）" % path)
    try:
        raw.decode("utf-8")
    except UnicodeDecodeError as e:
        problems.append("%s 不是合法 UTF-8：%s" % (path, e))
    if b"\r\n" in raw:
        problems.append("%s 含 CRLF（工程统一用 LF）" % path)


def check_banned(path, problems):
    """L5：项目纪律词。"""
    with open(path, encoding="utf-8") as f:
        for idx, ln in enumerate(f.read().split("\n"), 1):
            for w in BANNED_WORDS:
                if w in ln:
                    problems.append("%s:%d 出现禁止词「%s」" % (path, idx, w))


def main():
    root = sys.argv[1] if len(sys.argv) > 1 else os.getcwd()
    problems = []
    files = sorted(iter_cs_files(root))
    for p in files:
        check_strings_and_punct(p, problems)
        check_commas(p, problems)
        check_encoding(p, problems)
        check_banned(p, problems)

    mirror = os.path.join(root, "docs", "qa", "qa_oracle_mirror.py")
    if os.path.exists(mirror):
        r = subprocess.run([sys.executable, mirror], cwd=root,
                           stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
        out = r.stdout.decode("utf-8", "replace").strip()
        if r.returncode != 0:
            problems.append("判据负向注入自检未通过：\\n" + out)
    else:
        problems.append("找不到 %s" % mirror)

    if problems:
        print("[FAIL ] QA 静态自检发现 %d 个问题：" % len(problems))
        for p in problems:
            print("       " + p)
        return 1
    print("[OK   ] %d 个 .cs 通过静态自检（字符串 / 逗号 / 全角标点 / 编码 / 纪律词）；"
          "43 条负向注入自检通过" % len(files))
    return 0


if __name__ == "__main__":
    sys.exit(main())
