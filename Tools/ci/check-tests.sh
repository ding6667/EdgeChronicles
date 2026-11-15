#!/usr/bin/env bash
# 测试自证：EditMode 测试全绿
set -uo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
. "$ROOT/Tools/ci/unity.sh"
reject_if_locked "$ROOT" || exit 3
UNITY="$(resolve_unity)" || exit 2
LOG="$ROOT/Tools/ci/check-tests.log"
XML="$ROOT/Tools/ci/results.xml"
rm -f "$XML"
echo "[CHECK] tests 开始"
"$UNITY" -batchmode -nographics -projectPath "$ROOT" \
  -runTests -testPlatform EditMode -testResults "$XML" -logFile "$LOG"
code=$?
if [ ! -f "$XML" ]; then echo "[FAIL] 未生成测试报告"; tail -40 "$LOG"; exit 1; fi
passed=$(grep -o 'result="Passed"' "$XML" | wc -l | tr -d ' ')
failed=$(grep -o 'result="Failed"' "$XML" | wc -l | tr -d ' ')
echo "[CHECK] 测试结果 通过 $passed / 失败 $failed"
if [ "$failed" -ne 0 ] || [ "$code" -ne 0 ]; then
  grep -o 'name="[^"]*"[^>]*result="Failed"' "$XML" | head -20
  exit 1
fi
echo "[OK] 测试全绿"
