#!/usr/bin/env bash
# EdgeChronicles · 发布构建公共函数库
#
# 本文件由 Tools/build/*.sh 引用，不单独执行。
# 约定：
#   - 所有失败路径必须返回非零退出码，不允许静默通过。
#   - 退出码在 EXIT_* 常量里集中定义，跨脚本含义一致。

set -uo pipefail

# ── 退出码表（跨脚本统一）────────────────────────────────────────────
readonly EXIT_OK=0            # 成功
readonly EXIT_FAIL=1          # 未分类失败
readonly EXIT_USAGE=2         # 参数 / 用法错误
readonly EXIT_ENV=3           # 环境不可用：Unity 缺失、版本不符、工程被占用
readonly EXIT_BASELINE=4      # 机型基线配置非法或字段缺失
readonly EXIT_BUILD=5         # 构建失败：Unity 报错、产物缺失、场景缺失
readonly EXIT_VERIFY=6        # 产物校验失败：与 build-manifest.json 不符
readonly EXIT_GATE=7          # 资产 Gate / 门控未通过
readonly EXIT_DEPENDENCY=8    # 缺少依赖（jq / python3 / sha256sum）
readonly EXIT_ARCHIVE=9       # 归档或回滚步骤失败
readonly EXIT_SOURCE=10      # 版本源不可信：工作树非干净 / 版本文件缺失或非法

# ── 日志 ────────────────────────────────────────────────────────────
if [ -t 1 ] && [ -z "${NO_COLOR:-}" ]; then
  _C_RESET=$'\033[0m'; _C_INFO=$'\033[36m'; _C_WARN=$'\033[33m'
  _C_ERR=$'\033[31m';  _C_OK=$'\033[32m';   _C_STEP=$'\033[1;35m'
else
  _C_RESET=""; _C_INFO=""; _C_WARN=""; _C_ERR=""; _C_OK=""; _C_STEP=""
fi

log_info()  { printf '%s[INFO ]%s %s\n' "$_C_INFO" "$_C_RESET" "$*"; }
log_ok()    { printf '%s[ OK  ]%s %s\n' "$_C_OK"   "$_C_RESET" "$*"; }
log_warn()  { printf '%s[WARN ]%s %s\n' "$_C_WARN" "$_C_RESET" "$*" >&2; }
log_error() { printf '%s[FAIL ]%s %s\n' "$_C_ERR"  "$_C_RESET" "$*" >&2; }
log_step()  { printf '\n%s== %s ==%s\n' "$_C_STEP" "$*" "$_C_RESET"; }

# die <退出码> <消息...>
die() {
  local code="$1"; shift
  log_error "$*"
  exit "$code"
}

# ── JSON 读取 ───────────────────────────────────────────────────────
# JSONQ_SH 是 JSON 工具调度器（jq → python3 → python → powershell 依次退让）。
# 自托管 Windows runner 上只有 Git Bash 与 PowerShell 5.1，所以最后一条退路是必需的。
JSONQ_SH="${JSONQ_SH:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../tools" && pwd)/jsonq.sh}"
JSONQ_TOOL="${JSONQ_TOOL:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../tools" && pwd)/jsonq.py}"

# json_get <文件> <点号路径>
json_get() {
  local file="$1" path="$2"
  "$JSONQ_SH" "$file" "$path"
}

# json_validate <文件>：合法返回 0，非法返回非零并打印原因。
json_validate() {
  local file="$1"
  "$JSONQ_SH" --validate "$file" || { log_error "JSON 非法: $file"; return "$EXIT_BASELINE"; }
  return 0
}

# json_require <文件> <路径...>：逐个校验路径存在且非空，缺一即失败。
json_require() {
  local file="$1"; shift
  local missing=0 p val
  for p in "$@"; do
    val="$(json_get "$file" "$p" 2>/dev/null)" || val=""
    if [ -z "$val" ] || [ "$val" = "null" ]; then
      log_error "机型基线缺少必填字段: $p  ($file)"
      missing=1
    fi
  done
  [ "$missing" -eq 0 ] || return "$EXIT_BASELINE"
  return 0
}

# ── 杂项 ────────────────────────────────────────────────────────────
# json_escape <字符串>：转义为可安全嵌入 JSON 字符串字面量的形式。
json_escape() {
  printf '%s' "$1" | sed -e 's/\\/\\\\/g' -e 's/"/\\"/g' -e 's/\r//g' -e ':a;N;$!ba;s/\n/\\n/g' -e 's/\t/\\t/g'
}

# sha256_of <文件>
sha256_of() {
  local f="$1" out=""
  if command -v sha256sum >/dev/null 2>&1; then
    out="$(sha256sum "$f" 2>/dev/null | head -1 | awk '{print $1}')"
  elif command -v shasum >/dev/null 2>&1; then
    out="$(shasum -a 256 "$f" 2>/dev/null | head -1 | awk '{print $1}')"
  else
    log_error "缺少 sha256 工具（sha256sum / shasum）"
    return "$EXIT_DEPENDENCY"
  fi
  [ -n "$out" ] || return 1
  # GNU coreutils 在文件名含反斜杠时会给整行加反斜杠前缀（Windows 路径必然命中），
  # 不去掉这个前缀会写进 JSON 变成非法转义。
  out="${out#\\}"
  printf '%s\n' "$out"
}

# script_tmp_dir：脚本专用临时目录。
# 不用系统 mktemp 的默认位置：Windows 环境下 TMPDIR 往往是 C:\... 形式的路径，
# bash 工具链处理不了这种混合路径。Build/ 已被 .gitignore 忽略，放这里最干净。
SCRIPT_TMP_DIR=""
script_tmp_dir() {
  if [ -n "$SCRIPT_TMP_DIR" ] && [ -d "$SCRIPT_TMP_DIR" ]; then
    printf '%s\n' "$SCRIPT_TMP_DIR"
    return 0
  fi
  local base="${EDGE_SCRIPT_TMP_BASE:-${ROOT:-.}/Build/.tmp}"
  if ! mkdir -p "$base" 2>/dev/null; then
    base="${TMPDIR:-/tmp}"
    mkdir -p "$base" 2>/dev/null || return 1
  fi
  local d
  d="$(mktemp -d "$base/ec-build-XXXXXX" 2>/dev/null)" || d=""
  if [ -z "$d" ] || [ ! -d "$d" ]; then
    d="$base/ec-build-$$"
    mkdir -p "$d" 2>/dev/null || return 1
  fi
  SCRIPT_TMP_DIR="$d"
  printf '%s\n' "$d"
}

# dir_size_bytes <目录>：目录内所有文件字节数之和。
dir_size_bytes() {
  local dir="$1"
  if command -v du >/dev/null 2>&1; then
    du -sb "$dir" 2>/dev/null | awk '{print $1}'
  else
    find "$dir" -type f -printf '%s\n' 2>/dev/null | awk '{s+=$1} END {print s+0}'
  fi
}

# require_cmd <命令> <说明>
require_cmd() {
  command -v "$1" >/dev/null 2>&1 || die "$EXIT_DEPENDENCY" "缺少依赖命令: $1 （$2）"
}
