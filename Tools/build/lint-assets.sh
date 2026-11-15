#!/usr/bin/env bash
# EdgeChronicles · 资产 Gate（命名 / 贴图规格 / 材质槽 / LFS 边界 / 版本 / 密钥）
#
# 用法：
#   bash Tools/build/lint-assets.sh [--tier low|medium|high] [--version <版本号>] [--unity <路径>]
#
# 判据全部由脚本判定，没有人工确认项。任一条不通过即以非零码退出。
#
# 退出码：
#   0  全部通过    2 参数错误    3 环境不可用    4 基线配置非法
#   7  有 Gate 未通过    8 缺少依赖
#
# 门控条目：
#   G1 构建场景已登记且启用
#   G2 美术资产命名（UI_ICON_/UI_PANEL_/VFX_）
#   G3 贴图尺寸与压缩（母版 1024、切片 ≤128、不超过档位上限、不得未压缩）
#   G4 模型材质槽 ≤ 5
#   G5 .unity/.prefab 不得走 LFS
#   G6 .meta 配对完整
#   G7 Player Settings 的 bundleVersion 与 VERSION 一致
#   G8 仓库内无密钥文件
#   G9 LFS 二进制资产已落地（不是指针）

set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

# shellcheck source=lib/common.sh
. "$SCRIPT_DIR/lib/common.sh"
# shellcheck source=lib/unity-resolve.sh
. "$SCRIPT_DIR/lib/unity-resolve.sh"

JSONQ_TOOL="$SCRIPT_DIR/tools/jsonq.py"
export JSONQ_TOOL

TIER="low"
VERSION=""
UNITY_OVERRIDE=""
STRICT="true"

STAGE_DIR=""
STAGE_FILE=""
STAGE_META=""
STAGE_DIR_META=""

while [ $# -gt 0 ]; do
  case "$1" in
    --tier)    TIER="${2:-}"; shift 2 ;;
    --version) VERSION="${2:-}"; shift 2 ;;
    --unity)   UNITY_OVERRIDE="${2:-}"; shift 2 ;;
    --non-strict) STRICT="false"; shift ;;
    -h|--help) sed -n '2,36p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'; exit 0 ;;
    *) die "$EXIT_USAGE" "未知参数: $1" ;;
  esac
done

BASELINE_FILE="$SCRIPT_DIR/baselines/$TIER.json"
[ -f "$BASELINE_FILE" ] || die "$EXIT_USAGE" "没有档位 $TIER 的基线文件: $BASELINE_FILE"
json_validate "$BASELINE_FILE" || die "$EXIT_BASELINE" "机型基线不是合法 JSON"
MAX_TEX="$(json_get "$BASELINE_FILE" "textures.maxTextureSize")"
[ -n "$MAX_TEX" ] || die "$EXIT_BASELINE" "基线缺少 textures.maxTextureSize"

if [ -z "$VERSION" ]; then
  [ -f "$ROOT/VERSION" ] || die "$EXIT_SOURCE" "缺少 $ROOT/VERSION"
  VERSION="$(tr -d ' \t\r\n' <"$ROOT/VERSION")"
fi

cleanup() {
  if [ -n "$STAGE_FILE" ]; then
    rm -f "$STAGE_FILE" "$STAGE_META" 2>/dev/null || true
    if [ -n "$STAGE_DIR" ] && [ -d "$STAGE_DIR" ]; then
      rmdir "$STAGE_DIR" 2>/dev/null || true
      [ -n "$STAGE_DIR_META" ] && rm -f "$STAGE_DIR_META" 2>/dev/null || true
    fi
  fi
}
trap cleanup EXIT INT TERM

# ── Unity 与工程占用 ───────────────────────────────────────────────
if [ -n "$UNITY_OVERRIDE" ]; then
  UNITY="$UNITY_OVERRIDE"
else
  UNITY="$(resolve_unity)" || die "$EXIT_ENV" "找不到 Unity 可执行文件。用 --unity <路径> 或设置 UNITY_PATH"
fi
[ -f "$UNITY" ] || die "$EXIT_ENV" "Unity 可执行文件不存在: $UNITY"
assert_unity_matches_project "$UNITY" "$ROOT" \
  || die "$EXIT_ENV" "Unity 版本与工程不符（工程要求 $(project_unity_version "$ROOT")）"

if OCCUPANCY="$(unity_editor_is_running "$ROOT")"; then
  log_error "Unity Editor 正占用本工程，无法跑 Gate：$OCCUPANCY"
  die "$EXIT_ENV" "工程被占用"
fi

# ── 注入资产 Gate 入口 ─────────────────────────────────────────────
ENTRY_SOURCE="$SCRIPT_DIR/unity/EdgeChronicles.ReleaseOps.Editor.cs"
if ! grep -rql --include="*.cs" "namespace EdgeChronicles.ReleaseOps" "$ROOT/Assets" 2>/dev/null; then
  [ -f "$ENTRY_SOURCE" ] || die "$EXIT_GATE" "Gate 入口源文件缺失: $ENTRY_SOURCE"
  STAGE_DIR="$ROOT/Assets/_Project/Editor/ReleaseOps"
  STAGE_FILE="$STAGE_DIR/EdgeChronicles.ReleaseOps.Editor.cs"
  STAGE_META="$STAGE_FILE.meta"
  STAGE_DIR_META="$STAGE_DIR.meta"
  mkdir -p "$STAGE_DIR" || die "$EXIT_GATE" "无法创建注入目录: $STAGE_DIR"
  cp -f "$ENTRY_SOURCE" "$STAGE_FILE" || die "$EXIT_GATE" "注入 Gate 入口失败"
fi

# ── 执行 Gate ──────────────────────────────────────────────────────
LOG_DIR="$ROOT/Logs"
mkdir -p "$LOG_DIR"
LOG="$LOG_DIR/lint-assets.log"

log_step "资产 Gate（档位 $TIER · 版本 $VERSION）"

"$UNITY" -batchmode -nographics -quit \
  -projectPath "$ROOT" \
  -executeMethod EdgeChronicles.ReleaseOps.AssetGate.Lint \
  -lintBaseline "$TIER" \
  -lintVersion "$VERSION" \
  -lintMaxTextureSize "$MAX_TEX" \
  -logFile "$LOG"
UNITY_RC=$?

if [ ! -f "$LOG" ]; then
  die "$EXIT_GATE" "Unity 未生成 Gate 日志，无法判定结果"
fi

FAILS="$(grep -c "^\[LINT\] FAIL" "$LOG" 2>/dev/null || true)"
FAILS="${FAILS:-0}"
PASSES="$(grep -c "^\[LINT\] PASS" "$LOG" 2>/dev/null || true)"
PASSES="${PASSES:-0}"
WARNS="$(grep -c "^\[LINT\] WARN" "$LOG" 2>/dev/null || true)"
WARNS="${WARNS:-0}"

echo
grep -E "^\[LINT\] (PASS|FAIL|WARN)" "$LOG" || true
echo

if [ "$FAILS" -gt 0 ]; then
  log_error "资产 Gate 未通过：$FAILS 条不合规（通过 $PASSES 条，告警 $WARNS 条）"
  die "$EXIT_GATE" "资产 Gate 未通过"
fi

if [ "$UNITY_RC" -ne 0 ]; then
  log_error "Unity 退出码 $UNITY_RC"
  tail -40 "$LOG" | sed 's/^/         /' >&2
  die "$EXIT_GATE" "Gate 执行异常"
fi

if [ "$PASSES" -eq 0 ]; then
  log_error "Gate 没有产出任何通过记录，判定不可信"
  die "$EXIT_GATE" "Gate 结果不可信"
fi

log_ok "资产 Gate 全部通过（$PASSES 条通过，$WARNS 条告警）"
exit 0
