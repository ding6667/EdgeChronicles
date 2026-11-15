#!/usr/bin/env bash
# EdgeChronicles · CI 入口预检（executeMethod 指向的类型必须真实存在）
#
# 为什么需要这一步：
#   Unity 的 -executeMethod 指向的类型/方法不存在时，Unity 要**导入完整个工程之后**才报错，
#   日志里只有一句 "executeMethod method not found"，排查方向很容易跑偏
#   （看起来像代码编译不过，其实是 CI 入口写错了）。
#   养成数据门控就出过这个问题：脚本写的是 ShiFeng.Tools.Editor.Ci.ProgressionAudit，
#   而工程的程序集根命名空间是 EdgeChronicles.Tools.Editor，工程里根本没有 ShiFeng 这棵树。
#
# 本脚本**不需要启动 Unity**，纯文件级检查，几秒跑完：
#   1) 扫出 Tools/ci/*.sh 里所有 -executeMethod <值>；
#   2) 在 Assets/ 里找对应类型的定义（类声明 + 命名空间）；
#   3) 解析不出来 → 明确报错：哪个脚本、期望什么、实际有什么、两种读法各自差在哪。
#
# Unity 的解析规则（照此判断）：
#   -executeMethod <类全名>.<方法名>
#   类全名可带命名空间。例：`BuildCheck.CompileCheck` → 类 BuildCheck、方法 CompileCheck。
#
# 退出码：
#   0  全部入口可解析
#   7  有入口指向不存在的类型 / 命名空间不匹配 / 方法不存在（CI 配置错误，不是被测代码的问题）

set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

# shellcheck source=lib/common.sh
. "$SCRIPT_DIR/lib/common.sh"

CI_DIR="$ROOT/Tools/ci"

[ -d "$CI_DIR" ] || die "$EXIT_FAIL" "找不到 CI 脚本目录: $CI_DIR"

log_step "CI 入口预检 · executeMethod 解析"

# ── 1. 收集脚本里引用的方法 ────────────────────────────────────────
ENTRIES=""
for f in "$CI_DIR"/*.sh; do
  [ -f "$f" ] || continue
  while IFS= read -r full; do
    [ -n "$full" ] || continue
    ENTRIES="$ENTRIES$(basename "$f")|$full"$'\n'
  done < <(grep -oE '^[[:space:]]*-executeMethod[[:space:]]+[A-Za-z0-9_.]+' "$f" 2>/dev/null \
           | sed -E 's/^[[:space:]]*-executeMethod[[:space:]]+//')
done

if [ -z "$ENTRIES" ]; then
  log_warn "没有在任何 CI 脚本里找到 -executeMethod（检查项可能全部走 -runTests）"
  log_ok "CI 入口预检通过（无 executeMethod 引用）"
  exit 0
fi

# ── 2. 建工程类型索引（命名空间.类名 → 声明文件）────────────────────
INDEX_DIR="$ROOT/Build/.tmp"
mkdir -p "$INDEX_DIR" 2>/dev/null || INDEX_DIR="${TMPDIR:-/tmp}"
TYPE_INDEX="$INDEX_DIR/edge-type-index.txt"

: >"$TYPE_INDEX"
while IFS= read -r f; do
  [ -f "$f" ] || continue
  ns="$(grep -oE '^namespace[[:space:]]+[A-Za-z0-9_.]+' "$f" 2>/dev/null \
        | sed -E 's/^namespace[[:space:]]+//' | head -1)"
  grep -oE '\b(class|struct)[[:space:]]+[A-Za-z_][A-Za-z0-9_]*' "$f" 2>/dev/null \
    | sed -E 's/^(class|struct)[[:space:]]+//' | while IFS= read -r tn; do
        [ -n "$tn" ] || continue
        if [ -n "$ns" ]; then printf '%s.%s\t%s\n' "$ns" "$tn" "$f"; else printf '%s\t%s\n' "$tn" "$f"; fi
      done
done < <(find "$ROOT/Assets" -name "*.cs" -type f 2>/dev/null) >"$TYPE_INDEX"

FAIL_COUNT=0
TOTAL=0

# 找类型：给定「类全名」，返回 声明文件（找不到则空）
find_type_file() {
  local full="$1"
  awk -F'\t' -v want="$full" '$1 == want { print $2; exit }' "$TYPE_INDEX"
}

# 按「命名空间 + 类名」找：命名空间可为���
find_type_by_ns() {
  local ns="$1" tn="$2"
  awk -F'\t' -v ns="$ns" -v tn="$tn" '
    { split($1, p, "."); n = length(p); name = p[n];
      if (name == tn) { if (n == 1) { if (ns == "") { print $2; exit } } else { pre = p[1]; for (i = 2; i < n; i++) pre = pre "." p[i]; if (pre == ns) { print $2; exit } } } }' "$TYPE_INDEX"
}

check_entry() {
  local script="$1" value="$2"
  TOTAL=$((TOTAL + 1))

  local method="${value##*.}"          # Unity 规则：最后一段 = 方法名
  local class_full="${value%.*}"        # Unity 规则：其余 = 类全名（可带命名空间）
  local class_ns="${class_full%.*}"
  [ "$class_ns" = "$class_full" ] && class_ns=""
  local class_simple="${class_full##*.}"

  # 可能本意：整个值就是类全名（类=最后一段，命名空间=其余），方法名还差一段
  local intent_ns="${value%.*}"
  local intent_class="${value##*.}"

  local file_a file_b
  file_a="$(find_type_file "$class_full")"
  file_b="$(find_type_file "$value")"

  # 情况 1：Unity 规则下的类存在
  if [ -n "$file_a" ]; then
    if grep -qE "\b${method}[[:space:]]*\(" "$file_a" 2>/dev/null; then
      log_ok "$script → $value  可解析"
      return
    fi
    log_error "$script 引用的方法不存在: $class_full.$method"
    log_error "  类型在 $file_a，但里面没有 $method 方法。"
    FAIL_COUNT=$((FAIL_COUNT + 1))
    return
  fi

  # 情况 2：类全名 = 整个值（作者可能本意），方法名段缺失
  if [ -n "$file_b" ]; then
    log_error "$script 的 -executeMethod 少了一段（方法名）"
    log_error "  工程里确实有类型 $intent_ns.$intent_class（$file_b）。"
    log_error "  但 Unity 的规则是 <类全名>.<方法名>：当前值会被读成"
    log_error "    类全名 = $class_full（= 命名空间 $class_ns + 类 $class_simple），方法 = $method"
    log_error "  请把脚本改成：$value.<方法名>（或让类名与方法名同名）。"
    FAIL_COUNT=$((FAIL_COUNT + 1))
    return
  fi

  # 情况 3：类根本不存在
  log_error "$script 引用的类型不存在: $value"
  log_error "  读法 A（Unity 规则）：类全名 = $class_full，方法 = $method"
  log_error "           即：命名空间 $class_ns 下的类 $class_simple，含静态方法 $method"
  log_error "  读法 B（作者可能本意）：类全名 = $value"
  log_error "           即：命名空间 $intent_ns 下的类 $intent_class（方法名段未写）"
  local near
  near="$(awk -F'\t' -v tn="$class_simple" -v tn2="$intent_class" '{ split($1, p, "."); n = length(p); if (p[n] == tn || p[n] == tn2) print $1 }' "$TYPE_INDEX" | sort -u | head -4 | tr '\n' ' ')"
  if [ -n "$near" ]; then
    log_error "  工程里名字相近的类型: $near"
  else
    log_error "  工程里没有与 $class_simple / $intent_class 同名的类型。"
  fi
  log_error "  最可能的修法（Ci 门面类写法，与脚本注释『入口：<值>』一致）："
  log_error "    namespace $class_ns {"
  log_error "      public static class $class_simple {"
  log_error "        public static void $method() { /* ... */ }"
  log_error "      }"
  log_error "    }"
  log_error "    放在 Assets/_Project/Editor/ 下（Editor 目录才会被编进编辑器程序集）。"
  log_error "  Unity 要导入完整个工程之后才报 'executeMethod method not found'，很容易误判成代码问题。"
  FAIL_COUNT=$((FAIL_COUNT + 1))
}

while IFS='|' read -r script value; do
  [ -n "${script:-}" ] || continue
  check_entry "$script" "$value"
done <<<"$ENTRIES"

rm -f "$TYPE_INDEX" 2>/dev/null || true

echo
if [ "$FAIL_COUNT" -gt 0 ]; then
  log_error "CI 入口预检失败：$FAIL_COUNT 处引用指向不存在的类型或命名空间"
  log_error "这是 CI 入口配置问题，不是被测代码的问题。请先修好入口再判断代码红绿。"
  exit "$EXIT_GATE"
fi

log_ok "CI 入口预检通过（$TOTAL 处引用全部可解析）"
exit 0
