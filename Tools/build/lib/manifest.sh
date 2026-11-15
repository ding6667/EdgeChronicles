#!/usr/bin/env bash
# EdgeChronicles · build-manifest.json 生成与校验
#
# build-manifest.json 是「回滚与问题定位的唯一依据」：
#   - 出什么问题 → 拿 commit 去仓库找
#   - 装的是不是这一版 → 拿 payload.sha256 去比
#   - 是不是这个档位出的包 → 拿 build.tier + build.baselineSha256 去比
# 因此 manifest 必须自足：版本、commit、Unity 版本、档位、包体大小、逐文件校验和。
#
# 依赖：common.sh（json_escape / sha256_of / log_*）

# manifest_payload_listing <产物目录>
# 输出排序后的「sha256<TAB>字节数<TAB>相对路径」清单，供拼装 manifest 与复算校验和。
# 排除三类元数据文件：它们描述构建过程，不属于玩家包本体（否则日志一改校验和就变）。
#   build-manifest.json  本清单自身
#   unity-build.log      Unity 日志
#   unity-build-result.json  Unity 侧的应用参数回执
manifest_payload_listing() {
  local dir="$1"
  (
    cd "$dir" 2>/dev/null || exit 1
    find . -type f \
      ! -name "build-manifest.json" \
      ! -name "unity-build.log" \
      ! -name "unity-build-result.json" \
      -print0 \
      | LC_ALL=C sort -z \
      | while IFS= read -r -d '' f; do
          rel="${f#./}"
          sum="$(sha256_of "$f")" || exit 1
          bytes="$(wc -c <"$f" | tr -d ' ')"
          printf '%s\t%s\t%s\n' "$sum" "$bytes" "$rel"
        done
  )
}

# manifest_payload_root <产物目录>
# 整个产物树的根校验和：对逐文件清单再做一次 sha256。
# 任何一个文件被改动一个字节 → 根校验和变化。
manifest_payload_root() {
  local dir="$1"
  manifest_payload_listing "$dir" | sha256_of /dev/stdin
}

# manifest_write <参数...>
# 参数：
#   --out <产物目录>            产物根目录（manifest 写在这里）
#   --version <完整版本号>
#   --commit <完整 commit>
#   --branch <分支名>
#   --dirty <true|false>        构建时工作树是否非干净
#   --unity-version <编辑器版本>
#   --unity-revision <编辑器修订号>
#   --tier <档位 id>
#   --baseline-file <基线文件名>
#   --baseline-sha <基线文件校验和>
#   --backend <il2cpp|mono>
#   --target <BuildTarget 名>
#   --scenes <逗号分隔场景路径>
#   --scene-source <来源说明>
#   --started-utc <ISO8601>
#   --seconds <构建耗时秒>
#   --settings-json <PlayerSettings 摘要 JSON 片段>
#   --warnings <每行一条警告>
manifest_write() {
  local out="" version="" commit="" branch="" dirty="false"
  local unity_version="" unity_revision="" tier="" baseline_file="" baseline_sha=""
  local backend="" target="" scenes="" scene_source="" started_utc="" seconds="0"
  local settings_json="{}" warnings_file=""

  while [ $# -gt 0 ]; do
    case "$1" in
      --out)            out="$2"; shift 2 ;;
      --version)        version="$2"; shift 2 ;;
      --commit)         commit="$2"; shift 2 ;;
      --branch)         branch="$2"; shift 2 ;;
      --dirty)          dirty="$2"; shift 2 ;;
      --unity-version)  unity_version="$2"; shift 2 ;;
      --unity-revision) unity_revision="$2"; shift 2 ;;
      --tier)           tier="$2"; shift 2 ;;
      --baseline-file)  baseline_file="$2"; shift 2 ;;
      --baseline-sha)   baseline_sha="$2"; shift 2 ;;
      --backend)        backend="$2"; shift 2 ;;
      --target)         target="$2"; shift 2 ;;
      --scenes)         scenes="$2"; shift 2 ;;
      --scene-source)   scene_source="$2"; shift 2 ;;
      --started-utc)    started_utc="$2"; shift 2 ;;
      --seconds)        seconds="$2"; shift 2 ;;
      --settings-json)  settings_json="$2"; shift 2 ;;
      --warnings)       warnings_file="$2"; shift 2 ;;
      *) echo "manifest_write: 未知参数 $1" >&2; return 2 ;;
    esac
  done

  [ -n "$out" ] || { echo "manifest_write: 缺少 --out" >&2; return 2; }

  local listing_file
  listing_file="$(script_tmp_dir)/payload-listing.txt"
  manifest_payload_listing "$out" >"$listing_file" || { rm -f "$listing_file"; return 1; }

  local total_bytes=0 file_count=0
  total_bytes="$(awk -F'\t' '{s+=$2} END {print s+0}' "$listing_file")"
  file_count="$(wc -l <"$listing_file" | tr -d ' ')"

  local root_sum
  root_sum="$(sha256_of "$listing_file")"

  local manifest="$out/build-manifest.json"

  {
    printf '{\n'
    printf '  "schemaVersion": 1,\n'
    printf '  "game": { "id": "edgechronicles", "productName": "EdgeChronicles", "displayName": "拾锋录" },\n'
    printf '  "version": { "full": "%s", "bundleVersion": "%s" },\n' \
      "$(json_escape "$version")" "$(json_escape "$version")"
    printf '  "source": { "commit": "%s", "branch": "%s", "dirty": %s },\n' \
      "$(json_escape "$commit")" "$(json_escape "$branch")" "$dirty"
    printf '  "unity": { "editorVersion": "%s", "editorRevision": "%s" },\n' \
      "$(json_escape "$unity_version")" "$(json_escape "$unity_revision")"
    printf '  "build": {\n'
    printf '    "tier": "%s",\n' "$(json_escape "$tier")"
    printf '    "baselineFile": "%s",\n' "$(json_escape "$baseline_file")"
    printf '    "baselineSha256": "%s",\n' "$(json_escape "$baseline_sha")"
    printf '    "scriptingBackend": "%s",\n' "$(json_escape "$backend")"
    printf '    "buildTarget": "%s",\n' "$(json_escape "$target")"
    printf '    "scenes": "%s",\n' "$(json_escape "$scenes")"
    printf '    "sceneSource": "%s",\n' "$(json_escape "$scene_source")"
    printf '    "startedUtc": "%s",\n' "$(json_escape "$started_utc")"
    printf '    "seconds": %s,\n' "${seconds:-0}"
    printf '    "playerSettings": %s\n' "$settings_json"
    printf '  },\n'
    printf '  "payload": {\n'
    printf '    "bytes": %s,\n' "$total_bytes"
    printf '    "sizeMB": %s,\n' "$(awk -v b="$total_bytes" 'BEGIN {printf "%.2f", b/1048576}')"
    printf '    "fileCount": %s,\n' "$file_count"
    printf '    "sha256": "%s",\n' "$root_sum"
    printf '    "files": [\n'
    local first=1 sum bytes rel
    while IFS=$'\t' read -r sum bytes rel; do
      [ -n "${rel:-}" ] || continue
      if [ "$first" -eq 1 ]; then first=0; else printf ',\n'; fi
      printf '      { "path": "%s", "bytes": %s, "sha256": "%s" }' \
        "$(json_escape "$rel")" "$bytes" "$sum"
    done <"$listing_file"
    printf '\n    ]\n'
    printf '  },\n'
    printf '  "gate": { "warnings": ['
    if [ -n "$warnings_file" ] && [ -f "$warnings_file" ]; then
      local w first_w=1
      while IFS= read -r w; do
        [ -n "$w" ] || continue
        if [ "$first_w" -eq 1 ]; then first_w=0; else printf ', '; fi
        printf '"%s"' "$(json_escape "$w")"
      done <"$warnings_file"
    fi
    printf '] },\n'
    printf '  "tooling": { "buildScript": "Tools/build/build-player.sh", "manifestSchema": 1 }\n'
    printf '}\n'
  } >"$manifest"

  rm -f "$listing_file"
  printf '%s\n' "$manifest"
}

# manifest_verify <产物目录> [manifest 路径]
# 逐文件复算校验和 + 复算根校验和，与 manifest 比对。全部一致返回 0。
manifest_verify() {
  local dir="$1"
  local manifest="${2:-$dir/build-manifest.json}"

  [ -f "$manifest" ] || { log_error "找不到 manifest: $manifest"; return "$EXIT_VERIFY"; }

  local expect_root expect_bytes expect_files
  expect_root="$(json_get "$manifest" "payload.sha256" 2>/dev/null)" || expect_root=""
  expect_bytes="$(json_get "$manifest" "payload.bytes" 2>/dev/null)" || expect_bytes=""
  expect_files="$(json_get "$manifest" "payload.fileCount" 2>/dev/null)" || expect_files=""

  if [ -z "$expect_root" ] || [ -z "$expect_bytes" ] || [ -z "$expect_files" ]; then
    log_error "manifest 缺少 payload 字段（sha256 / bytes / fileCount）: $manifest"
    return "$EXIT_VERIFY"
  fi

  local listing actual_root actual_bytes actual_files bad=0
  listing="$(script_tmp_dir)/payload-listing-verify.txt"
  manifest_payload_listing "$dir" >"$listing" || { rm -f "$listing"; return "$EXIT_VERIFY"; }
  actual_root="$(sha256_of "$listing")"
  actual_bytes="$(awk -F'\t' '{s+=$2} END {print s+0}' "$listing")"
  actual_files="$(wc -l <"$listing" | tr -d ' ')"
  rm -f "$listing"

  if [ "$actual_root" != "$expect_root" ]; then
    log_error "产物根校验和不符：manifest=$expect_root 实测=$actual_root"
    bad=1
  fi
  if [ "$actual_bytes" != "$expect_bytes" ]; then
    log_error "产物字节数不符：manifest=$expect_bytes 实测=$actual_bytes"
    bad=1
  fi
  if [ "$actual_files" != "$expect_files" ]; then
    log_error "产物文件数不符：manifest=$expect_files 实测=$actual_files"
    bad=1
  fi

  if [ "$bad" -ne 0 ]; then
    return "$EXIT_VERIFY"
  fi
  log_ok "产物校验通过（$actual_files 个文件，$actual_bytes 字节，根校验和 $actual_root）"
  return 0
}
