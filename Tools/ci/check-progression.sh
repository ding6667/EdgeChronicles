#!/usr/bin/env bash
# 养成层数据自证：孤儿外键 / 派生列覆盖 / A31-A38 全部断言
set -uo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
. "$ROOT/Tools/ci/unity.sh"
UNITY="$(resolve_unity)" || exit 2
LOG="$ROOT/Tools/ci/check-progression.log"
echo "[CHECK] progression 开始"
"$UNITY" -batchmode -nographics -quit -projectPath "$ROOT" \
  -executeMethod ShiFeng.Tools.Editor.Ci.ProgressionAudit -logFile "$LOG"
code=$?
if grep -q "ProgressionAudit: tables=20 orphans=0 derivedOverwritten=100%" "$LOG" 2>/dev/null; then
  echo "[OK] 养成数据完整性通过"; exit 0
fi
grep "ProgressionAudit" "$LOG" 2>/dev/null | tail -20
echo "[FAIL] 养成数据审计未通过（退出码 $code）"
exit 1
