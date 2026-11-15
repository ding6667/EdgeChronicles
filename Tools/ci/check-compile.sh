#!/usr/bin/env bash
# 编译自证：Unity 批处理编译 + 日志扫描错误
set -uo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
. "$ROOT/Tools/ci/unity.sh"
UNITY="$(resolve_unity)" || exit 2
LOG="$ROOT/Tools/ci/check-compile.log"
echo "[CHECK] compile 开始"
"$UNITY" -batchmode -nographics -quit -projectPath "$ROOT" \
  -executeMethod BuildCheck.CompileCheck -logFile "$LOG"
code=$?
if grep -qE "error CS[0-9]+" "$LOG" 2>/dev/null; then
  echo "[FAIL] 存在编译错误："
  grep -E "error CS[0-9]+" "$LOG" | sort -u | head -40
  exit 1
fi
if [ "$code" -ne 0 ]; then
  echo "[FAIL] Unity 退出码 $code"
  tail -40 "$LOG"
  exit 1
fi
echo "[OK] 编译通过"
