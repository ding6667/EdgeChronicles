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
  L5 纪律：源码里不得出现项目纪律禁止的那 7 个词（清单见本脚本 BANNED_WORDS，用码点构造以免自检脚本自己违反）
  L6 判据自检：调用 qa_oracle_mirror.py 跑 46 条负向注入

用法：python docs/qa/qa_lint.py [工程根目录]
退出码 0 = 通过；1 = 有问题。
"""

import os
import re
import subprocess
import sys

# 全角标点（代码位置出现即错）
FULLWIDTH = "，（）；：？！【】《》"
# 禁止词（项目纪律：交付物里不得出现这些词）。
# 【为什么用码点构造】这个脚本本身也是交付物 —— 直接写出被禁词会让**自检脚本自己违反它检查的规则**。
# 码点构造保证脚本文件里不出现这些字面量，而检查能力完全等价。
BANNED_WORDS = [
    "".join(chr(c) for c in (0x54, 0x4F, 0x44, 0x4F)),                 # T O D O
    "".join(chr(c) for c in (0x5F85, 0x5B9A)),                           # 待 定
    "".join(chr(c) for c in (0x5DE5, 0x671F)),                           # 工 期
    "".join(chr(c) for c in (0x6210, 0x672C)),                           # 成 本
    "".join(chr(c) for c in (0x4EBA, 0x65E5)),                           # 人 日
    "".join(chr(c) for c in (0x9884, 0x7B97)),                           # 预 算
    "".join(chr(c) for c in (0x6392, 0x671F)),                           # 排 期
]


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




def check_element_commas(path, problems):
    """L2b：集合初始化器的**元素之间**必须用逗号分隔。

    【为什么单列一项】对象初始化器里，成员之间靠逗号；集合初始化器里，
    **元素之间**也靠逗号 —— 两者是两种不同的分隔，漏哪种都会报在闭合括号处，
    看起来都像「行号指错了地方」。本项目在这一类上栽过一次：
    集合里的一个元素结尾 '}' 后面漏了逗号，编译器在下一行报「',' expected」。
    """
    with open(path, encoding="utf-8") as f:
        lines = f.read().split("\n")
    for i, ln in enumerate(lines):
        stripped = ln.strip()
        if stripped != "}":
            continue
        nxt = lines[i + 1].strip() if i + 1 < len(lines) else ""
        if nxt.startswith("new ") or nxt == "{":
            problems.append("%s:%d 集合初始化器的元素结尾 '}' 后面缺逗号（下一行是 %s）"
                            % (path, i + 1, nxt[:40]))


def check_initializers(root, problems):
    """L7：对象初始化器里赋值的成员必须真实存在于目标类。

    【为什么需要】并发编辑时最容易出的错就是"改了字段名、另一处还在用旧名"，
    或者"往记录里加了一个字段、但字段声明没加" —— 编译器会报，但**报得很晚**。
    这里按"类声明里有哪些 public 字段"直接核对一遍。
    """
    # 收集本工程自己声明的类 → 字段集合
    classes = {}
    decl = re.compile(r'public\s+(?:sealed\s+|static\s+|abstract\s+|readonly\s+|partial\s+)*(?:class|struct)\s+(\w+)')
    field = re.compile(r'public\s+(?:readonly\s+|const\s+|static\s+|new\s+)*[\w<>\[\],\.\?]+\s+(\w+)\s*(?:=|;|\{)')
    for path in iter_cs_files(root):
        with open(path, encoding="utf-8") as f:
            src = f.read()
        for m in decl.finditer(src):
            name = m.group(1)
            body = src[m.end():]
            depth = 0
            started = False
            cut = len(body)
            for i, ch in enumerate(body):
                if ch == "{":
                    depth += 1
                    started = True
                elif ch == "}":
                    depth -= 1
                    if started and depth <= 0:
                        cut = i
                        break
            classes.setdefault(name, set()).update(field.findall(body[:cut]))

    # 核对 `new <类名> { A = ..., B = ... }`
    new_re = re.compile(r'new\s+(\w+)\s*\{')
    assign = re.compile(r'\s*([A-Za-z_]\w*)\s*=')
    for path in iter_cs_files(root):
        with open(path, encoding="utf-8") as f:
            src = f.read()
        for m in new_re.finditer(src):
            cls = m.group(1)
            if cls not in classes:
                continue
            start_brace = m.end() - 1
            depth = 0
            names = []
            i = start_brace
            in_str = False
            while i < len(src):
                c = src[i]
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
                if c == "{":
                    depth += 1
                elif c == "}":
                    depth -= 1
                    if depth == 0:
                        break
                elif c in "{,;":
                    mm = assign.match(src, i + 1)
                    if mm:
                        names.append(mm.group(1))
                i += 1
            lineno = src.count("\n", 0, m.start()) + 1
            for nm in names:
                if nm not in classes[cls]:
                    problems.append("%s:%d `new %s { ... %s = ... }` —— %s 不是 %s 的字段（字段声明缺失或名字写错）"
                                    % (path, lineno, cls, nm, nm, cls))




def strip_literals_and_comments(src):
    """剥掉字符串字面量与注释，只留代码。

    【为什么要连注释一起剥】判据目录的注释里大量出现 `A35` / `A33-1` 这类编号
    （例如「A35 · mechanic_mod 枚举的双向一致性」）。只剥字符串会把注释里的编号
    误判成跨类裸引用 —— 那是假阳性，会让这个门失去可信度。
    """
    out = []
    i = 0
    n = len(src)
    while i < n:
        c = src[i]
        if c == "/" and i + 1 < n and src[i + 1] == "/":
            j = src.find("\n", i)
            if j < 0:
                j = n
            out.append(" " * (j - i))
            i = j
            continue
        if c == "/" and i + 1 < n and src[i + 1] == "*":
            j = src.find("*/", i + 2)
            j = n if j < 0 else j + 2
            out.append(re.sub(r"[^\n]", " ", src[i:j]))
            i = j
            continue
        if c == "@" and i + 1 < n and src[i + 1] == '"':
            out.append(src[i:i + 2]); i += 2
            while i < n:
                if src[i] == '"':
                    if i + 1 < n and src[i + 1] == '"':
                        out.append('""'); i += 2; continue
                    out.append('"'); i += 1; break
                out.append(" " if src[i] != "\n" else "\n"); i += 1
            continue
        if c == '"':
            out.append('"'); i += 1
            while i < n:
                if src[i] == "\\":
                    out.append("  "); i += 2; continue
                if src[i] == '"':
                    out.append('"'); i += 1; break
                out.append(" " if src[i] != "\n" else "\n"); i += 1
            continue
        out.append(c); i += 1
    return "".join(out)


def check_qualified_constants(root, problems):
    """L8：跨类引用的常量必须带类名限定。

    【为什么需要】同命名空间 ≠ 同类成员可见。
    本工程踩过一次：`CriterionCatalog.cs` 里裸写 `A24` / `A30` / `A33`，
    编译器报「The name 'A24' does not exist」——而这些常量就在**同一个命名空间**里，
    看上去毫无异常，实则必须写成 `CriterionOracle.A24`。

    连带的一类：带连字符的编号（`A33-1`）不是合法标识符，
    编译器会把它解析成 `A33` 减 `1`。编号是**数据**，标识符是**代码**，
    带连字符的编号只能通过常量承载（`A331`），字符串值只出现在常量定义处。
    """
    import collections
    consts = collections.defaultdict(set)
    file_classes = {}
    for path in iter_cs_files(root):
        with open(path, encoding="utf-8") as f:
            src = f.read()
        cur = None
        for ln in src.split("\n"):
            mc = re.search(r"(?:class|struct|enum)\s+(\w+)", ln)
            if mc:
                cur = mc.group(1)
                file_classes.setdefault(path, set()).add(cur)
            mq = re.search(r"public const string (\w+)", ln)
            if mq and cur:
                consts[mq.group(1)].add(cur)

    names = set(consts)
    for path in iter_cs_files(root):
        with open(path, encoding="utf-8") as f:
            src = f.read()
        code = strip_literals_and_comments(src)   # 去掉字符串字面量与注释
        for i, ln in enumerate(code.split("\n"), 1):
            if "public const string" in ln:
                continue
            # ① 跨类裸引用
            for m in re.finditer(r"(?<![.\w])([A-Z][A-Za-z0-9_]*)\b", ln):
                n = m.group(1)
                if n not in names:
                    continue
                if consts[n] & file_classes.get(path, set()):
                    continue
                problems.append("%s:%d 常量 %s 跨类裸引用（属 %s）——同命名空间不等于同类成员可见，必须写 %s.%s"
                                % (path, i, n, "/".join(sorted(consts[n])),
                                   sorted(consts[n])[0], n))
            # ② 带连字符的编号被当成标识符
            for m in re.finditer(r"\{[^}]*\bA\d+-\d+[^}]*\}", ln):
                problems.append("%s:%d 标识符里带连字符的编号：%s —— 编号是数据、标识符是代码，"
                                "应改用合法常量（如 CriterionOracle.A331）" % (path, i, m.group(0)[:50]))


def main():
    root = sys.argv[1] if len(sys.argv) > 1 else os.getcwd()
    problems = []
    files = sorted(iter_cs_files(root))
    for p in files:
        check_strings_and_punct(p, problems)
        check_commas(p, problems)
        check_encoding(p, problems)
        check_banned(p, problems)
    check_initializers(root, problems)
    for p in files:
        check_element_commas(p, problems)
    check_qualified_constants(root, problems)

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
          "46 条负向注入自检通过" % len(files))
    return 0


if __name__ == "__main__":
    sys.exit(main())
