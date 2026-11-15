#!/usr/bin/env bash
# EdgeChronicles · 一键出包（Windows / Linux / macOS 单机玩家包）
#
# 用法：
#   bash Tools/build/build-player.sh --tier <low|medium|high> [选项]
#
# 选项：
#   --tier <id>            机型基线档位：low（验收底线）/ medium（默认发行）/ high（上限参照）
#   --backend <il2cpp|mono>  脚本后端，默认取基线 JSON 的 scripting.backend
#   --target <win64|linux64|mac>  目标平台，默认 win64
#   --output <目录>        产物输出目录，默认 Build/<tier>-<backend>-<target>
#   --version <版本号>     覆盖 VERSION 文件里的版本号
#   --unity <路径>         Unity 可执行文件路径（等价于设置 UNITY_PATH）
#   --scenes "<a,b,c>"     指定构建场景（逗号分隔）。不给则读 EditorBuildSettings
#   --non-strict           关闭严格门控（仅用于开发期自测包，不可用于发行）
#   --allow-dirty          允许在非干净工作树上出包。只放宽溯源这一项
#                          （manifest 会记 dirty=true），URP/场景等质量门控仍保持严格
#   --no-stage-entrypoint  禁止注入构建入口（工程已自带同名入口时用）
#   --dry-run              只做预检与计划输出，不调用 Unity
#
# 退出码：
#   0  成功            2  参数错误        3  环境不可用（Unity 缺失/版本不符/工程被占用）
#   4  基线配置非法    5  构建失败        6  产物校验失败
#   7  门控未通过      8  缺少依赖        10 版本源不可信（工作树非干净）
#
# 硬性行为：
#   - 任何失败路径都以非零码退出，不允许静默通过。
#   - Unity Editor 正占用本工程时，脚本必须报错退出，不得伪装构建成功。
#   - 本脚本不执行 git commit / git push。

set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

# shellcheck source=lib/common.sh
. "$SCRIPT_DIR/lib/common.sh"
# shellcheck source=lib/unity-resolve.sh
. "$SCRIPT_DIR/lib/unity-resolve.sh"
# shellcheck source=lib/manifest.sh
. "$SCRIPT_DIR/lib/manifest.sh"

JSONQ_TOOL="$SCRIPT_DIR/tools/jsonq.py"
export JSONQ_TOOL

# ── 默认参数 ───────────────────────────────────────────────────────
TIER=""
BACKEND=""
TARGET="win64"
OUTPUT=""
VERSION_OVERRIDE=""
UNITY_OVERRIDE=""
SCENES_OVERRIDE=""
STRICT="true"
ALLOW_STAGE="true"
ALLOW_DIRTY="false"
DRY_RUN="false"

STAGE_DIR=""
STAGE_FILE=""
STAGE_META=""
STAGE_DIR_META=""
BACKUP_SETTINGS=""
BACKUP_QUALITY=""
WARN_FILE=""
PARAMS_FILE=""
BUILD_END_EPOCH=""
SCRIPT_TMP=""

usage() {
  sed -n '2,32p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
}

# ── 参数解析 ───────────────────────────────────────────────────────
while [ $# -gt 0 ]; do
  case "$1" in
    --tier)              TIER="${2:-}"; shift 2 ;;
    --backend)           BACKEND="${2:-}"; shift 2 ;;
    --target)            TARGET="${2:-}"; shift 2 ;;
    --output)            OUTPUT="${2:-}"; shift 2 ;;
    --version)           VERSION_OVERRIDE="${2:-}"; shift 2 ;;
    --unity)             UNITY_OVERRIDE="${2:-}"; shift 2 ;;
    --scenes)            SCENES_OVERRIDE="${2:-}"; shift 2 ;;
    --non-strict)        STRICT="false"; shift ;;
    --allow-dirty)       ALLOW_DIRTY="true"; shift ;;
    --no-stage-entrypoint) ALLOW_STAGE="false"; shift ;;
    --dry-run)           DRY_RUN="true"; shift ;;
    -h|--help)           usage; exit 0 ;;
    *) die "$EXIT_USAGE" "未知参数: $1（用 --help 看用法）" ;;
  esac
done

[ -n "$TIER" ] || die "$EXIT_USAGE" "必须指定 --tier <low|medium|high>"
BASELINE_FILE="$SCRIPT_DIR/baselines/$TIER.json"
[ -f "$BASELINE_FILE" ] || die "$EXIT_USAGE" "没有档位 $TIER 的基线文件: $BASELINE_FILE"

case "$TARGET" in
  win64)   BUILD_TARGET="StandaloneWindows64"; EXE_NAME="EdgeChronicles.exe" ;;
  linux64) BUILD_TARGET="StandaloneLinux64";   EXE_NAME="EdgeChronicles.x86_64" ;;
  mac)     BUILD_TARGET="StandaloneOSX";       EXE_NAME="EdgeChronicles.app" ;;
  *) die "$EXIT_USAGE" "不支持的 --target: $TARGET（可选 win64 / linux64 / mac）" ;;
esac

# ── 清理钩子 ───────────────────────────────────────────────────────
# restore_if_unchanged_since_build <文件> <备份> <标签>
# 构建期 Unity 会改 ProjectSettings。还原策略要防两件事：
#   1) 漏还原 → 工作树被构建污染；
#   2) 误还原 → 把并行同事在构建之后写下的改动抹掉。
# 判据：备份与当前内容相同 → 无需还原；当前文件的修改时间晚于构建结束时刻
#      → 说明构建后还有第三方写入，跳过还原并明确告警，交人工核对。
restore_if_unchanged_since_build() {
  local file="$1" backup="$2" label="$3"
  [ -f "$backup" ] || return 0
  [ -f "$file" ] || { log_warn "$label 已消失，跳过还原"; return 0; }

  local cur_sum bak_sum
  cur_sum="$(sha256_of "$file")"
  bak_sum="$(sha256_of "$backup")"
  if [ "$cur_sum" = "$bak_sum" ]; then
    return 0
  fi

  local cur_mtime=""
  cur_mtime="$(stat -c %Y "$file" 2>/dev/null || stat -f %m "$file" 2>/dev/null || echo "")"
  if [ -n "$cur_mtime" ] && [ -n "$BUILD_END_EPOCH" ] && [ "$cur_mtime" -gt "$BUILD_END_EPOCH" ]; then
    log_warn "$label 在构建结束后被再次修改，已跳过还原（避免覆盖并行改动）。请人工核对："
    log_warn "  构建前的内容保存在 $backup"
    return 0
  fi

  cp -f "$backup" "$file" 2>/dev/null \
    && log_info "已还原 $label" \
    || log_warn "还原 $label 失败，请人工检查"
}

cleanup() {
  local rc=$?
  # 1) 还原构建期临时改动的工程设置
  restore_if_unchanged_since_build "$ROOT/ProjectSettings/ProjectSettings.asset" "$BACKUP_SETTINGS" "ProjectSettings.asset"
  restore_if_unchanged_since_build "$ROOT/ProjectSettings/QualitySettings.asset" "$BACKUP_QUALITY" "QualitySettings.asset"
  # 2) 移除注入的构建入口
  if [ -n "$STAGE_FILE" ]; then
    rm -f "$STAGE_FILE" "$STAGE_META" 2>/dev/null || true
    if [ -n "$STAGE_DIR" ] && [ -d "$STAGE_DIR" ]; then
      rmdir "$STAGE_DIR" 2>/dev/null || true
      [ -n "$STAGE_DIR_META" ] && rm -f "$STAGE_DIR_META" 2>/dev/null || true
    fi
  fi
  rm -f "$WARN_FILE" 2>/dev/null || true
  rm -f "$PARAMS_FILE" 2>/dev/null || true
  [ -n "$SCRIPT_TMP" ] && rm -rf "$SCRIPT_TMP" 2>/dev/null || true
  return $rc
}
trap cleanup EXIT INT TERM

# ── 1. 预检：版本源 ───────────────────────────────────────────────
log_step "1/7 预检 · 版本源"

if [ -n "$VERSION_OVERRIDE" ]; then
  VERSION="$VERSION_OVERRIDE"
elif [ -f "$ROOT/VERSION" ]; then
  VERSION="$(tr -d ' \t\r\n' <"$ROOT/VERSION")"
else
  die "$EXIT_SOURCE" "缺少 $ROOT/VERSION，无法确定版本号"
fi
[ -n "$VERSION" ] || die "$EXIT_SOURCE" "版本号为空（VERSION 文件或 --version）"

if ! printf '%s' "$VERSION" | grep -Eq '^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?(\+[0-9A-Za-z.-]+)?$'; then
  die "$EXIT_SOURCE" "版本号不符合 MAJOR.MINOR.PATCH[-预发布] 格式: $VERSION"
fi

log_info "版本号: $VERSION"

COMMIT="$(git -C "$ROOT" rev-parse HEAD 2>/dev/null || true)"
[ -n "$COMMIT" ] || die "$EXIT_SOURCE" "取不到 git commit（不是 git 仓库或没有提交）"
BRANCH="$(git -C "$ROOT" rev-parse --abbrev-ref HEAD 2>/dev/null || echo "unknown")"
DIRTY="false"
DIRTY_LIST="$(git -C "$ROOT" status --porcelain 2>/dev/null || true)"
if [ -n "$DIRTY_LIST" ]; then
  DIRTY="true"
  log_warn "工作树非干净，出包内容无法唯一对应到某个 commit："
  printf '%s\n' "$DIRTY_LIST" | head -20 | sed 's/^/         /' >&2
  if [ "$STRICT" = "true" ] && [ "$ALLOW_DIRTY" != "true" ]; then
    die "$EXIT_SOURCE" "严格模式禁止在非干净工作树上出包。先提交（或用 --allow-dirty 出溯源可查的开发包）"
  fi
  if [ "$ALLOW_DIRTY" = "true" ]; then
    log_warn "已指定 --allow-dirty：本次出包来自非干净工作树，manifest 会记 source.dirty=true"
  fi
fi
log_info "commit: ${COMMIT:0:12}  分支: $BRANCH  工作树: $DIRTY"

# ── 2. 预检：基线配置 ─────────────────────────────────────────────
log_step "2/7 预检 · 机型基线"

json_validate "$BASELINE_FILE" || die "$EXIT_BASELINE" "机型基线不是合法 JSON: $BASELINE_FILE"

REQUIRED_FIELDS=(
  "id" "displayName" "target.resolution.0" "target.resolution.1" "target.targetFrameRate"
  "target.renderScale" "quality.unityQualityLevel" "quality.msaa" "quality.postProcessing"
  "quality.shadows.enabled" "quality.shadows.distance" "quality.lodBias" "quality.maximumLODLevel"
  "particles.maxParticles" "textures.maxTextureSize" "textures.compression"
  "memory.workingSetMB" "memory.textureMemoryMB" "limits.drawCalls" "limits.triangles"
  "limits.buildSizeMB" "scripting.backend" "scripting.managedStrippingLevel"
  "acceptance.minFps" "acceptance.p95FrameTimeMs"
)
json_require "$BASELINE_FILE" "${REQUIRED_FIELDS[@]}" || die "$EXIT_BASELINE" "机型基线字段不完整"

BASELINE_ID="$(json_get "$BASELINE_FILE" "id")"
[ "$BASELINE_ID" = "$TIER" ] || die "$EXIT_BASELINE" "基线 id 与文件名不一致: $BASELINE_ID != $TIER"

if [ -z "$BACKEND" ]; then
  BACKEND="$(json_get "$BASELINE_FILE" "scripting.backend")"
fi
case "$BACKEND" in
  il2cpp|mono) ;;
  *) die "$EXIT_USAGE" "不支持的脚本后端: $BACKEND（可选 il2cpp / mono）" ;;
esac

BASELINE_SHA="$(sha256_of "$BASELINE_FILE")"
RES_W="$(json_get "$BASELINE_FILE" "target.resolution.0")"
RES_H="$(json_get "$BASELINE_FILE" "target.resolution.1")"
FPS="$(json_get "$BASELINE_FILE" "target.targetFrameRate")"
QLEVEL="$(json_get "$BASELINE_FILE" "quality.unityQualityLevel")"
log_info "档位: $BASELINE_ID（$QLEVEL） 分辨率 ${RES_W}x${RES_H}@${FPS}fps 后端 $BACKEND 目标 $BUILD_TARGET"
log_info "基线校验和: $BASELINE_SHA"

# 把机型基线摊平成「点号路径=值」，交给 Unity 构建入口。
# 这样 JSON 结构只在 Tools/build/baselines/*.json 里定义一次，Unity 侧不重复实现解析器。
flatten_baseline() {
  local file="$1"
  "$JSONQ_SH" --flatten "$file"
}

# ── 3. 预检：Unity 与工程占用 ─────────────────────────────────────
log_step "3/7 预检 · Unity 与工程占用"

if [ -n "$UNITY_OVERRIDE" ]; then
  UNITY="$UNITY_OVERRIDE"
else
  UNITY="$(resolve_unity)" || die "$EXIT_ENV" "找不到 Unity 可执行文件。用 --unity <路径> 或设置 UNITY_PATH"
fi
[ -f "$UNITY" ] || die "$EXIT_ENV" "Unity 可执行文件不存在: $UNITY"

UNITY_VERSION="$(project_unity_version "$ROOT")"
UNITY_REVISION="$(sed -n 's/^m_EditorVersionWithRevision:[[:space:]]*[^ ]* (\(.*\))/\1/p' \
  "$ROOT/ProjectSettings/ProjectVersion.txt" 2>/dev/null | head -1)"
assert_unity_matches_project "$UNITY" "$ROOT" \
  || die "$EXIT_ENV" "Unity 版本与工程不符，拒绝构建（工程要求 $UNITY_VERSION）"

log_info "Unity: $UNITY  版本: $UNITY_VERSION"

# 先清理陈旧锁（无 Unity 进程却留着锁文件 = 上次异常退出留下的假阳性）
unity_clear_stale_lock "$ROOT"

if OCCUPANCY="$(unity_editor_is_running "$ROOT")"; then
  log_error "Unity Editor 正占用本工程，构建无法进行：$OCCUPANCY"
  log_error "请先关闭 Unity Editor（含 Hub 里的残留实例），再重新执行本脚本。"
  die "$EXIT_ENV" "工程被占用（batchmode 会因工程被占用失败）"
fi
log_ok "工程未被占用"

# ── 4. 预检：构建场景 ─────────────────────────────────────────────
log_step "4/7 预检 · 构建场景"

resolve_scenes() {
  # 优先：显式指定
  if [ -n "$SCENES_OVERRIDE" ]; then
    printf '%s' "$SCENES_OVERRIDE"
    return 0
  fi
  # 其次：EditorBuildSettings 里已启用的场景
  local enabled
  enabled="$(sed -n '/m_Scenes:/,$p' "$ROOT/ProjectSettings/EditorBuildSettings.asset" 2>/dev/null \
    | grep -oE 'path: [^ ]+\.unity' | sed 's/^path: //' | while IFS= read -r p; do
        if [ -f "$ROOT/$p" ]; then printf '%s\n' "$p"; fi
      done)"
  if [ -n "$enabled" ]; then
    printf '%s' "$enabled"
    return 0
  fi
  # 兜底：工程里恰好只有一份场景时使用它（会明确告警并记录来源，不静默）
  local candidates
  candidates="$(find "$ROOT/Assets" -name "*.unity" -type f 2>/dev/null \
    | sed "s|^$ROOT/||" | LC_ALL=C sort)"
  local count
  count="$(printf '%s\n' "$candidates" | grep -c . || true)"
  if [ "$count" = "1" ]; then
    printf '%s' "$candidates"
    return 0
  fi
  return 1
}

SCENES="$(resolve_scenes)" || {
  CANDIDATES="$(find "$ROOT/Assets" -name "*.unity" -type f 2>/dev/null | sed "s|^$ROOT/||" | LC_ALL=C sort)"
  log_error "没有可用的构建场景。"
  log_error "EditorBuildSettings.asset 的 m_Scenes 为空，且工程里找到 $(printf '%s\n' "$CANDIDATES" | grep -c . || echo 0) 份 .unity。"
  [ -n "$CANDIDATES" ] && printf '%s\n' "$CANDIDATES" | sed 's/^/         候选: /' >&2
  log_error "请在 Editor 的 Build Settings 里加入场景，或用 --scenes \"路径1,路径2\" 显式指定。"
  die "$EXIT_BUILD" "构建场景缺失"
}
SCENE_SOURCE="editorBuildSettings"
if [ -n "$SCENES_OVERRIDE" ]; then
  SCENE_SOURCE="commandLine"
elif ! sed -n '/m_Scenes:/,$p' "$ROOT/ProjectSettings/EditorBuildSettings.asset" 2>/dev/null \
      | grep -qE 'path: [^ ]+\.unity'; then
  SCENE_SOURCE="autoSingleCandidate"
  log_warn "EditorBuildSettings 未登记场景，已采用工程中唯一的那份场景（来源记录为 autoSingleCandidate）"
fi
log_info "场景: $(printf '%s' "$SCENES" | tr '\n' ' ')  来源: $SCENE_SOURCE"

# ── 5. 输出目录与计划 ─────────────────────────────────────────────
log_step "5/7 输出目录"

if [ -z "$OUTPUT" ]; then
  OUTPUT="$ROOT/Build/${TIER}-${BACKEND}-${TARGET}"
fi
mkdir -p "$OUTPUT" || die "$EXIT_BUILD" "无法创建输出目录: $OUTPUT"
OUTPUT="$(cd "$OUTPUT" && pwd)"
log_info "输出目录: $OUTPUT"

STARTED_UTC="$(date -u +"%Y-%m-%dT%H:%M:%SZ")"
START_TS="$(date +%s)"

log_step "6/7 构建"
if [ "$DRY_RUN" = "true" ]; then
  log_info "dry-run：跳过 Unity 调用与产物生成"
  cat <<PLAN
  ── 出包计划 ──────────────────────────────────────────────
  版本        : $VERSION
  commit      : ${COMMIT:0:12}（分支 $BRANCH，工作树 $DIRTY）
  Unity       : $UNITY_VERSION（$UNITY）
  档位        : $BASELINE_ID / $QLEVEL / ${RES_W}x${RES_H}@${FPS}fps
  脚本后端    : $BACKEND
  目标平台    : $BUILD_TARGET
  构建场景    : $(printf '%s' "$SCENES" | tr '\n' ' ')
  严格门控    : $STRICT
  输出目录    : $OUTPUT
  基线校验和  : $BASELINE_SHA
  ─────────────────────────────────────────────────────────
PLAN
  log_ok "预检通过（dry-run 未执行构建）"
  exit 0
fi

# ── 注入构建入口（Assets/** 归工程/美术所有，构建侧只做临时注入并清理）──
ENTRY_SOURCE="$SCRIPT_DIR/unity/EdgeChronicles.ReleaseOps.Editor.cs"
ENTRY_CLASS="EdgeChronicles.ReleaseOps.PlayerBuild"

entrypoint_exists_in_project() {
  grep -rql --include="*.cs" "class PlayerBuild" "$ROOT/Assets" 2>/dev/null \
    && grep -rql --include="*.cs" "namespace EdgeChronicles.ReleaseOps" "$ROOT/Assets" 2>/dev/null
}

if entrypoint_exists_in_project; then
  log_info "工程已自带 $ENTRY_CLASS，跳过注入"
elif [ "$ALLOW_STAGE" != "true" ]; then
  die "$EXIT_BUILD" "工程内找不到 $ENTRY_CLASS，且已指定 --no-stage-entrypoint"
elif [ ! -f "$ENTRY_SOURCE" ]; then
  die "$EXIT_BUILD" "构建入口源文件缺失: $ENTRY_SOURCE"
else
  STAGE_DIR="$ROOT/Assets/_Project/Editor/ReleaseOps"
  STAGE_FILE="$STAGE_DIR/EdgeChronicles.ReleaseOps.Editor.cs"
  STAGE_META="$STAGE_FILE.meta"
  STAGE_DIR_META="$STAGE_DIR.meta"
  mkdir -p "$STAGE_DIR" || die "$EXIT_BUILD" "无法创建注入目录: $STAGE_DIR"
  cp -f "$ENTRY_SOURCE" "$STAGE_FILE" || die "$EXIT_BUILD" "注入构建入口失败"
  log_info "已临时注入构建入口（构建结束后自动清理）"
fi

# ── 备份工程设置（构建期会临时改 PlayerSettings / QualitySettings）──
SCRIPT_TMP="$(script_tmp_dir)" || die "$EXIT_FAIL" "无法创建脚本临时目录"
BACKUP_SETTINGS="$SCRIPT_TMP/ProjectSettings.asset.bak"
BACKUP_QUALITY="$SCRIPT_TMP/QualitySettings.asset.bak"
cp -f "$ROOT/ProjectSettings/ProjectSettings.asset" "$BACKUP_SETTINGS" || die "$EXIT_BUILD" "备份 ProjectSettings.asset 失败"
cp -f "$ROOT/ProjectSettings/QualitySettings.asset" "$BACKUP_QUALITY" || die "$EXIT_BUILD" "备份 QualitySettings.asset 失败"

# ── 调用 Unity ─────────────────────────────────────────────────────
UNITY_LOG="$OUTPUT/unity-build.log"
STRICT_FLAG="false"
[ "$STRICT" = "true" ] && STRICT_FLAG="true"

PARAMS_FILE="$SCRIPT_TMP/build-params.txt"
flatten_baseline "$BASELINE_FILE" >"$PARAMS_FILE" || die "$EXIT_BASELINE" "摊平机型基线失败"
[ -s "$PARAMS_FILE" ] || die "$EXIT_BASELINE" "机型基线摊平结果为空"
log_info "机型参数条目: $(wc -l <"$PARAMS_FILE" | tr -d ' ')"

ARGS=(
  -batchmode -nographics -quit
  -projectPath "$ROOT"
  -executeMethod "$ENTRY_CLASS.Build"
  -buildTier "$TIER"
  -buildOutput "$OUTPUT"
  -buildBackend "$BACKEND"
  -buildTarget "$BUILD_TARGET"
  -buildVersion "$VERSION"
  -buildParams "$PARAMS_FILE"
  -buildScenes "$(printf '%s' "$SCENES" | tr '\n' ',' | sed 's/,$//')"
  -buildStrict "$STRICT_FLAG"
  -logFile "$UNITY_LOG"
)

log_info "调用 Unity …（日志: $UNITY_LOG）"
"$UNITY" "${ARGS[@]}"
UNITY_RC=$?
BUILD_END_EPOCH="$(date +%s)"

if [ "$UNITY_RC" -ne 0 ]; then
  log_error "Unity 退出码 $UNITY_RC"
  if [ -f "$UNITY_LOG" ]; then
    log_error "日志尾部："
    tail -60 "$UNITY_LOG" | sed 's/^/         /' >&2
  fi
  die "$EXIT_BUILD" "构建失败（Unity 退出码 $UNITY_RC）"
fi

if [ ! -f "$UNITY_LOG" ]; then
  die "$EXIT_BUILD" "Unity 没有生成日志文件，无法确认构建结果"
fi

# Unity 侧用 EditorApplication.Exit(code) 表达失败，退出码可能仍是 0 → 必须查日志标记
if grep -qE "\[BUILD\] fail|\[BUILD\] error|BuildFailedException" "$UNITY_LOG" 2>/dev/null; then
  log_error "Unity 日志中出现构建失败标记："
  grep -E "\[BUILD\] fail|\[BUILD\] error|BuildFailedException|error CS" "$UNITY_LOG" | head -30 | sed 's/^/         /' >&2
  die "$EXIT_BUILD" "构建失败（日志标记）"
fi

# ── 7. 产物与 manifest ─────────────────────────────────────────────
log_step "7/7 产物与 manifest"

PLAYER_EXE="$OUTPUT/$EXE_NAME"
if [ ! -e "$PLAYER_EXE" ]; then
  log_error "产物缺失: $PLAYER_EXE"
  log_error "输出目录内容："
  find "$OUTPUT" -maxdepth 2 | head -30 | sed 's/^/         /' >&2
  die "$EXIT_BUILD" "构建没有产出玩家包"
fi

RESULT_JSON="$OUTPUT/unity-build-result.json"
if [ ! -f "$RESULT_JSON" ]; then
  die "$EXIT_BUILD" "Unity 未写出 unity-build-result.json，构建入口没有完整执行"
fi
log_info "构建回执: $(json_get "$RESULT_JSON" "result")  档位: $(json_get "$RESULT_JSON" "tier")  场景数: $(json_get "$RESULT_JSON" "sceneCount")"

PLAYER_SETTINGS_SUMMARY="$(printf '{"resolution": [%s, %s], "targetFrameRate": %s, "qualityLevel": "%s", "scriptingBackend": "%s", "buildTarget": "%s"}' \
  "$RES_W" "$RES_H" "$FPS" "$(json_escape "$QLEVEL")" "$(json_escape "$BACKEND")" "$(json_escape "$BUILD_TARGET")")"

WARN_FILE="$SCRIPT_TMP/build-warnings.txt"
grep -E "^\[BUILD\] warn" "$UNITY_LOG" 2>/dev/null | sed 's/^\[BUILD\] warn //' >"$WARN_FILE" || true

ELAPSED=$(( $(date +%s) - START_TS ))

MANIFEST_PATH="$(manifest_write \
  --out "$OUTPUT" \
  --version "$VERSION" \
  --commit "$COMMIT" \
  --branch "$BRANCH" \
  --dirty "$DIRTY" \
  --unity-version "$UNITY_VERSION" \
  --unity-revision "$UNITY_REVISION" \
  --tier "$TIER" \
  --baseline-file "baselines/$TIER.json" \
  --baseline-sha "$BASELINE_SHA" \
  --backend "$BACKEND" \
  --target "$BUILD_TARGET" \
  --scenes "$(printf '%s' "$SCENES" | tr '\n' ',')" \
  --scene-source "$SCENE_SOURCE" \
  --started-utc "$STARTED_UTC" \
  --seconds "$ELAPSED" \
  --settings-json "$PLAYER_SETTINGS_SUMMARY" \
  --warnings "$WARN_FILE")" || die "$EXIT_BUILD" "写 build-manifest.json 失败"

manifest_verify "$OUTPUT" "$MANIFEST_PATH" || die "$EXIT_VERIFY" "产物与 manifest 不一致"

log_ok "构建完成"
log_info "玩家包      : $PLAYER_EXE"
log_info "manifest    : $MANIFEST_PATH"
log_info "包体        : $(json_get "$MANIFEST_PATH" "payload.sizeMB") MB（$(json_get "$MANIFEST_PATH" "payload.fileCount") 个文件）"
log_info "产物根校验和: $(json_get "$MANIFEST_PATH" "payload.sha256")"
exit 0
