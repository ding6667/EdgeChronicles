#!/usr/bin/env bash
# EdgeChronicles · 出包报告（把 manifest 与构建日志汇总成一份可直接贴进发布记录的摘要）
#
# 用法：
#   bash Tools/build/report-package.sh <产物目录>
#
# 输出六块：
#   1. 版本与来源
#   2. 档位与基线
#   3. 产物与校验和
#   4. 构建期实际落地的参数（appliedSettings）
#   5. 构建期保证不了的项（全部 [BUILD] warn，逐条）
#   6. 跳过的不兼容参数（全部 skippedSettings）
#
# 退出码：
#   0  报告生成成功
#   2  参数错误或产物目录不存在
#   6  manifest 校验不通过（产物不可分发）

set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

# shellcheck source=lib/common.sh
. "$SCRIPT_DIR/lib/common.sh"
# shellcheck source=lib/manifest.sh
. "$SCRIPT_DIR/lib/manifest.sh"

ARTIFACT_DIR="${1:-}"
[ -n "$ARTIFACT_DIR" ] || die "$EXIT_USAGE" "用法: report-package.sh <产物目录>"
[ -d "$ARTIFACT_DIR" ] || die "$EXIT_USAGE" "产物目录不存在: $ARTIFACT_DIR"

MANIFEST="$ARTIFACT_DIR/build-manifest.json"
[ -f "$MANIFEST" ] || die "$EXIT_USAGE" "找不到 manifest: $MANIFEST"

log_step "出包报告 · $ARTIFACT_DIR"

echo
echo "════════ 1. 版本与来源 ════════"
printf '版本号        : %s\n' "$(json_get "$MANIFEST" "version.full")"
printf 'commit        : %s\n' "$(json_get "$MANIFEST" "source.commit")"
printf '分支          : %s\n' "$(json_get "$MANIFEST" "source.branch")"
printf '工作树是否干净: %s\n' "$(json_get "$MANIFEST" "source.dirty")"
if [ "$(json_get "$MANIFEST" "source.dirty")" = "true" ]; then
  echo '                ↑ 非干净工作树出的包：仅可用于开发期自测，不可分发'
fi
printf 'Unity 版本    : %s（修订 %s）\n' \
  "$(json_get "$MANIFEST" "unity.editorVersion")" "$(json_get "$MANIFEST" "unity.editorRevision")"
printf '构建开始      : %s\n' "$(json_get "$MANIFEST" "build.startedUtc")"
printf '构建耗时      : %s 秒\n' "$(json_get "$MANIFEST" "build.seconds")"

echo
echo "════════ 2. 档位与基线 ════════"
printf '档位 id       : %s\n' "$(json_get "$MANIFEST" "build.tier")"
printf '基线文件      : %s\n' "$(json_get "$MANIFEST" "build.baselineFile")"
printf '基线校验和    : %s\n' "$(json_get "$MANIFEST" "build.baselineSha256")"
printf '脚本后端      : %s\n' "$(json_get "$MANIFEST" "build.scriptingBackend")"
printf '目标平台      : %s\n' "$(json_get "$MANIFEST" "build.buildTarget")"
printf '构建场景      : %s\n' "$(json_get "$MANIFEST" "build.scenes")"
printf '场景来源      : %s\n' "$(json_get "$MANIFEST" "build.sceneSource")"
printf '落地参数摘要  : %s\n' "$(json_get "$MANIFEST" "build.playerSettings")"

echo
echo "════════ 3. 产物与校验和 ════════"
printf '包体大小      : %s MB（%s 字节）\n' \
  "$(json_get "$MANIFEST" "payload.sizeMB")" "$(json_get "$MANIFEST" "payload.bytes")"
printf '文件数        : %s\n' "$(json_get "$MANIFEST" "payload.fileCount")"
printf '根校验和      : %s\n' "$(json_get "$MANIFEST" "payload.sha256")"
printf '校验方式      : bash Tools/build/verify-artifact.sh %s\n' "$ARTIFACT_DIR"

echo
echo "════════ 4. 构建期实际落地的参数 ════════"
RESULT_JSON="$ARTIFACT_DIR/unity-build-result.json"
if [ -f "$RESULT_JSON" ]; then
  applied="$(json_get "$RESULT_JSON" "appliedSettings" 2>/dev/null || echo "")"
  if [ -n "$applied" ]; then
    printf '%s\n' "$applied" | tr ',' '\n' | sed 's/^[[:space:]]*//; s/"//g' | grep -v '^$' | sed 's/^/  · /'
  else
    echo "  （无记录）"
  fi
else
  echo "  （没有 unity-build-result.json）"
fi

echo
echo "════════ 5. 构建期保证不了的项（全部 [BUILD] warn）════════"
WARN_LOG="$ARTIFACT_DIR/unity-build.log"
WARN_COUNT=0
if [ -f "$WARN_LOG" ]; then
  while IFS= read -r line; do
    [ -n "$line" ] || continue
    WARN_COUNT=$((WARN_COUNT + 1))
    printf '  %d. %s\n' "$WARN_COUNT" "${line#\[BUILD\] warn }"
  done < <(grep -E "^\[BUILD\] warn" "$WARN_LOG" 2>/dev/null | sed -E 's/^\[BUILD\] warn //')
else
  echo "  （没有 unity-build.log）"
fi
if [ "$WARN_COUNT" -eq 0 ]; then
  echo "  （无）"
else
  echo
  echo "  ↑ 这 ${WARN_COUNT} 条都是「构建期保证不了、被推到运行期或资产侧」的项，"
  echo "    逐条的归属与验证方式见 docs/release/BUILD-PROFILE.md §5。"
fi

echo
echo "════════ 6. 跳过的不兼容参数（skippedSettings）════════"
if [ -f "$RESULT_JSON" ]; then
  skipped="$(json_get "$RESULT_JSON" "skippedSettings" 2>/dev/null || echo "")"
  if [ -n "$skipped" ]; then
    printf '%s\n' "$skipped" | tr ',' '\n' | sed 's/^[[:space:]]*//; s/"//g' | grep -v '^$' | sed 's/^/  · /'
  else
    echo "  （无）"
  fi
else
  echo "  （没有 unity-build-result.json）"
fi

echo
echo "════════ 产物完整性校验 ════════"
manifest_verify "$ARTIFACT_DIR" "$MANIFEST" || die "$EXIT_VERIFY" "产物与 manifest 不一致，不可分发"
echo
echo "报告完毕。"
exit 0
