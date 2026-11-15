#!/usr/bin/env bash
# 配表自证：生成数据的存在性与结构校验
set -uo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
. "$ROOT/Tools/ci/unity.sh"
UNITY="$(resolve_unity)" || exit 2
LOG="$ROOT/Tools/ci/check-data.log"
echo "[CHECK] data 开始"
"$UNITY" -batchmode -nographics -quit -projectPath "$ROOT" \
  -executeMethod BuildCheck.DataCheck -logFile "$LOG"
code=$?
if grep -q "\[CHECK\] data-fail" "$LOG" 2>/dev/null; then
  grep "\[CHECK\]" "$LOG"; exit 1
fi
if [ "$code" -ne 0 ]; then echo "[FAIL] 退出码 $code"; tail -40 "$LOG"; exit 1; fi
echo "[OK] 配表校验通过"
