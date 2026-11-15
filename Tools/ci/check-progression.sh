#!/usr/bin/env bash
# 养成层数据自证：孤儿外键 / 派生列覆盖 / A31–A43 全部断言
#
# 判据（EL-03 §5 ③）：日志含 `ProgressionAudit: tables=20 orphans=0 derivedOverwritten=100%`；
#                    A31–A43 全部 PASS；mechanic_mod 越界计数 = 0。
# 入口：EdgeChronicles.Tools.Editor.Ci.ProgressionAudit
set -uo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
. "$ROOT/Tools/ci/unity.sh"
reject_if_locked "$ROOT" || exit 3
UNITY="$(resolve_unity)" || exit 2
LOG="$ROOT/Tools/ci/check-progression.log"
echo "[CHECK] progression 开始"
"$UNITY" -batchmode -nographics -quit -projectPath "$ROOT" \
  -executeMethod EdgeChronicles.Tools.Editor.Ci.ProgressionAudit -logFile "$LOG"
code=$?
if grep -q "ProgressionAudit: tables=20 orphans=0 derivedOverwritten=100%" "$LOG" 2>/dev/null; then
  echo "[OK] 养成数据完整性通过"; exit 0
fi
grep -E "ProgressionAudit|AssertionSummary|Assertion .* FAIL" "$LOG" 2>/dev/null | tail -30
echo "[FAIL] 养成数据审计未通过（退出码 $code）"
exit 1
