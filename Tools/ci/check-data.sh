#!/usr/bin/env bash
# 配表自证：导表器跑全部断言 + 生成物结构校验
#
# 判据（EL-03 §5 ②）：日志含 `ValidationSummary: error=0 warning=0`；退出码 0。
# 入口：EdgeChronicles.Tools.Editor.Ci.ImportAndValidate（导表 → L1 静态断言 → 生成 → L2 跨表审计）
set -uo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
. "$ROOT/Tools/ci/unity.sh"
reject_if_locked "$ROOT" || exit 3
UNITY="$(resolve_unity)" || exit 2
LOG="$ROOT/Tools/ci/check-data.log"
echo "[CHECK] data 开始"
"$UNITY" -batchmode -nographics -quit -projectPath "$ROOT" \
  -executeMethod EdgeChronicles.Tools.Editor.Ci.ImportAndValidate.Run -logFile "$LOG"
code=$?
if grep -q "\[CHECK\] data-fail" "$LOG" 2>/dev/null; then
  grep "\[CHECK\]" "$LOG"; exit 1
fi
if ! grep -q "ValidationSummary: error=0 warning=0" "$LOG" 2>/dev/null; then
  echo "[FAIL] 导表断言未全过（期望 ValidationSummary: error=0 warning=0）"
  grep -E "ValidationSummary|AssertionSummary|\[CHECK\]" "$LOG" 2>/dev/null | tail -30
  tail -40 "$LOG"
  exit 1
fi
if [ "$code" -ne 0 ]; then echo "[FAIL] 退出码 $code"; tail -40 "$LOG"; exit 1; fi
echo "[OK] 配表校验通过"
