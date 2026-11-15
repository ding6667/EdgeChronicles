#!/usr/bin/env bash
# EdgeChronicles · JSON 工具调度器
#
# 用法：
#   bash Tools/build/tools/jsonq.sh <file.json> <dotted.path>
#   bash Tools/build/tools/jsonq.sh --validate <file.json>
#   bash Tools/build/tools/jsonq.sh --flatten <file.json>
#
# 按可用性依次退让：
#   1. jq            —— 最快，CI 镜像一般自带
#   2. python3       —— 常规 Linux / 部分 Windows 环境
#   3. python        —— 老环境的 python 2/3 别名
#   4. powershell    —— Windows 自带 PowerShell 5.1（ConvertFrom-Json），
#                       自托管 Windows runner 只有 Git Bash 时靠它兜底
#
# 退出码：0 成功 / 2 文件或 JSON 非法 / 3 路径不存在 / 4 用法错误

set -uo pipefail

SELF_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PY_TOOL="$SELF_DIR/jsonq.py"
PS_TOOL="$SELF_DIR/jsonq.ps1"

if [ $# -lt 1 ]; then
  echo "用法: jsonq.sh <file.json> <path> | --validate <file.json> | --flatten <file.json>" >&2
  exit 4
fi

if command -v jq >/dev/null 2>&1; then
  case "$1" in
    --validate) jq -e . "$2" >/dev/null 2>&1 || { echo "JSON 非法: $2" >&2; exit 2; }; exit 0 ;;
    --flatten)  jq -r 'paths(scalars) as $p | "\($p|map(tostring)|join("."))=\(getpath($p))"' "$2"; exit $? ;;
    *)          jq -r "$2" "$1"; exit $? ;;
  esac
fi

if command -v python3 >/dev/null 2>&1; then
  python3 "$PY_TOOL" "$@"; exit $?
fi

if command -v python >/dev/null 2>&1; then
  python "$PY_TOOL" "$@"; exit $?
fi

if command -v powershell >/dev/null 2>&1; then
  powershell -NoProfile -ExecutionPolicy Bypass -File "$PS_TOOL" "$@"
  exit $?
fi

if command -v pwsh >/dev/null 2>&1; then
  pwsh -NoProfile -File "$PS_TOOL" "$@"
  exit $?
fi

echo "[FAIL] 找不到可用的 JSON 解析器：需要 jq / python3 / python / powershell 之一" >&2
exit 8
