#!/usr/bin/env bash
# EdgeChronicles · 产物校验（回滚与问题定位的唯一依据）
#
# 用法：
#   bash Tools/build/verify-artifact.sh <产物目录> [--manifest <manifest 路径>]
#
# 做什么：
#   1) 按 build-manifest.json 的 files 清单逐文件复算 sha256；
#   2) 复算整棵产物树的根校验和，与 manifest.payload.sha256 比对；
#   3) 比对字节数与文件数；
#   4) 比对 manifest 记录的 commit 与当前仓库 HEAD 是否一致（仅提示，不判失败）。
#
# 退出码：
#   0  校验通过    2 参数错误    6 校验失败

set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

# shellcheck source=lib/common.sh
. "$SCRIPT_DIR/lib/common.sh"
# shellcheck source=lib/manifest.sh
. "$SCRIPT_DIR/lib/manifest.sh"

JSONQ_TOOL="$SCRIPT_DIR/tools/jsonq.py"
export JSONQ_TOOL

ARTIFACT_DIR=""
MANIFEST=""

while [ $# -gt 0 ]; do
  case "$1" in
    --manifest) MANIFEST="${2:-}"; shift 2 ;;
    -h|--help) sed -n '2,20p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'; exit 0 ;;
    *) ARTIFACT_DIR="$1"; shift ;;
  esac
done

[ -n "$ARTIFACT_DIR" ] || die "$EXIT_USAGE" "用法: verify-artifact.sh <产物目录> [--manifest <路径>]"
[ -d "$ARTIFACT_DIR" ] || die "$EXIT_USAGE" "产物目录不存在: $ARTIFACT_DIR"

if [ -z "$MANIFEST" ]; then
  MANIFEST="$ARTIFACT_DIR/build-manifest.json"
fi

log_step "产物校验 · $ARTIFACT_DIR"
manifest_verify "$ARTIFACT_DIR" "$MANIFEST" || die "$EXIT_VERIFY" "产物与 manifest 不一致，禁止分发"

# 版本与来源信息（只提示，不判失败：校验旧包时仓库可能已经前进）
M_VERSION="$(json_get "$MANIFEST" "version.full" 2>/dev/null || echo "")"
M_COMMIT="$(json_get "$MANIFEST" "source.commit" 2>/dev/null || echo "")"
M_TIER="$(json_get "$MANIFEST" "build.tier" 2>/dev/null || echo "")"
M_BACKEND="$(json_get "$MANIFEST" "build.scriptingBackend" 2>/dev/null || echo "")"
M_UNITY="$(json_get "$MANIFEST" "unity.editorVersion" 2>/dev/null || echo "")"

echo
log_info "版本    : $M_VERSION"
log_info "commit  : $M_COMMIT"
log_info "档位    : $M_TIER（后端 $M_BACKEND，Unity $M_UNITY）"

HEAD_COMMIT="$(git -C "$ROOT" rev-parse HEAD 2>/dev/null || echo "")"
if [ -n "$HEAD_COMMIT" ] && [ "$HEAD_COMMIT" != "$M_COMMIT" ]; then
  log_warn "manifest 记录的 commit 与当前 HEAD 不同（这是正常的，回滚旧包时必然不同）"
fi

log_ok "校验通过，可分发"
exit 0
