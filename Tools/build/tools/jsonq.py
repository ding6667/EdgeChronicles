#!/usr/bin/env python3
"""极简 JSON 取值器（点号路径）。

用途：让 Tools/build 的 shell 脚本在「有 jq 用 jq、没 jq 用 python3」的两种环境下
都能读取 Tools/build/baselines/*.json，避免把 JSON 解析塞进 shell（脆弱）。

用法：
    python3 jsonq.py <file.json> <dotted.path>
    python3 jsonq.py --validate <file.json>
    python3 jsonq.py --flatten <file.json>

路径语法：a.b.c  取对象字段；a.0.b 取数组第 0 项；a.b.c 用方括号取值时
          a["b"]["c"] 同样支持。

--flatten：把所有标量叶子摊平成「点号路径=值」逐行输出，供 shell 侧把
          机型基线交给 Unity 构建入口（避免在两处语言里各写一份 JSON 结构）。

退出码：
    0  取值成功 / 校验通过
    2  文件不存在或不是合法 JSON
    3  路径不存在
    4  用法错误
"""

import json
import re
import sys

_INDEX = re.compile(r"^\d+$")


def parse_path(path):
    """把 'a.b[0].c' / 'a.b.0.c' 解析成 ['a','b','0','c']。"""
    parts = []
    for chunk in path.split("."):
        if not chunk:
            continue
        while True:
            m = re.match(r"^([^\[\]]+)((?:\[[^\[\]]+\])*)$", chunk)
            if not m:
                raise ValueError("路径片段无法解析: %r" % chunk)
            name, brackets = m.group(1), m.group(2)
            if name:
                parts.append(name)
            for b in re.findall(r"\[([^\[\]]+)\]", brackets):
                parts.append(b.strip("'\""))
            break
    return parts


def walk(node, parts):
    for part in parts:
        if isinstance(node, list):
            if not _INDEX.match(part):
                raise KeyError("数组需要数字下标，收到 %r" % part)
            idx = int(part)
            if idx >= len(node):
                raise KeyError("下标越界: %d" % idx)
            node = node[idx]
        elif isinstance(node, dict):
            if part not in node:
                raise KeyError("字段不存在: %r" % part)
            node = node[part]
        else:
            raise KeyError("无法在标量上继续取路径: %r" % part)
    return node


def flatten(node, prefix, out):
    if isinstance(node, dict):
        if not node:
            return
        for key in sorted(node.keys()):
            flatten(node[key], "%s.%s" % (prefix, key) if prefix else str(key), out)
    elif isinstance(node, list):
        if not node:
            return
        for idx, item in enumerate(node):
            flatten(item, "%s.%d" % (prefix, idx), out)
    else:
        if node is None:
            return
        if isinstance(node, bool):
            out.append("%s=%s" % (prefix, "true" if node else "false"))
        else:
            out.append("%s=%s" % (prefix, node))


def main(argv):
    if len(argv) < 2:
        sys.stderr.write("用法: jsonq.py <file.json> <path> | --validate <file.json> | --flatten <file.json>\n")
        return 4

    if argv[1] == "--validate":
        if len(argv) != 3:
            sys.stderr.write("用法: jsonq.py --validate <file.json>\n")
            return 4
        path = argv[2]
        try:
            with open(path, "r", encoding="utf-8") as fh:
                json.load(fh)
        except OSError as exc:
            sys.stderr.write("读取失败 %s: %s\n" % (path, exc))
            return 2
        except ValueError as exc:
            sys.stderr.write("JSON 非法 %s: %s\n" % (path, exc))
            return 2
        return 0

    if argv[1] == "--flatten":
        if len(argv) != 3:
            sys.stderr.write("用法: jsonq.py --flatten <file.json>\n")
            return 4
        path = argv[2]
        try:
            with open(path, "r", encoding="utf-8") as fh:
                doc = json.load(fh)
        except OSError as exc:
            sys.stderr.write("读取失败 %s: %s\n" % (path, exc))
            return 2
        except ValueError as exc:
            sys.stderr.write("JSON 非法 %s: %s\n" % (path, exc))
            return 2
        lines = []
        flatten(doc, "", lines)
        for line in lines:
            print(line)
        return 0

    if len(argv) != 3:
        sys.stderr.write("用法: jsonq.py <file.json> <dotted.path>\n")
        return 4

    path, dotted = argv[1], argv[2]
    try:
        with open(path, "r", encoding="utf-8") as fh:
            doc = json.load(fh)
    except OSError as exc:
        sys.stderr.write("读取失败 %s: %s\n" % (path, exc))
        return 2
    except ValueError as exc:
        sys.stderr.write("JSON 非法 %s: %s\n" % (path, exc))
        return 2

    try:
        value = walk(doc, parse_path(dotted))
    except (KeyError, ValueError) as exc:
        sys.stderr.write("取值失败 %s: %s\n" % (dotted, exc))
        return 3

    if isinstance(value, bool):
        print("true" if value else "false")
    elif value is None:
        print("")
    elif isinstance(value, (dict, list)):
        print(json.dumps(value, ensure_ascii=False, sort_keys=True))
    else:
        print(value)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
