#!/usr/bin/env bash
# PlayMode 联动自证：战斗与养成的联动断言矩阵
set -uo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
. "$ROOT/Tools/ci/unity.sh"
reject_if_locked "$ROOT" || exit 3
UNITY="$(resolve_unity)" || exit 2
LOG="$ROOT/Tools/ci/check-tests-playmode.log"
XML="$ROOT/Tools/ci/results-playmode.xml"
rm -f "$XML"
echo "[CHECK] playmode tests 开始"
"$UNITY" -batchmode -nographics -quit -projectPath "$ROOT" \
  -runTests -testPlatform PlayMode -testResults "$XML" -logFile "$LOG"
code=$?
if [ ! -f "$XML" ]; then echo "[FAIL] 未生成测试报告"; tail -40 "$LOG"; exit 1; fi
passed=$(grep -o 'result="Passed"' "$XML" | wc -l | tr -d ' ')
failed=$(grep -o 'result="Failed"' "$XML" | wc -l | tr -d ' ')
echo "[CHECK] PlayMode 通过 $passed / 失败 $failed"
if [ "$failed" -ne 0 ] || [ "$code" -ne 0 ]; then exit 1; fi
echo "[OK] PlayMode 全绿"
