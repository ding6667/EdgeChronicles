#!/usr/bin/env bash
# EdgeChronicles · CI Unity 预检（解析 Unity / 校验版本 / 激活许可证）
#
# 三条工作流（build / test / lint）的第一步共用这个脚本，避免三处重复且走样。
#
# 用法：
#   bash Tools/build/ci-unity-preflight.sh
#
# 环境变量（由工作流的 job 级 env 从 vars / secrets 映射进来）：
#   UNITY_PATH        仓库变量 → Unity.exe 的路径（可省略，会自动扫描）
#   UNITY_LICENSE     secret → Unity .ulf 许可证内容（可选）
#   UNITY_EMAIL       secret → 激活用的账号（可选）
#   UNITY_PASSWORD    secret → 激活用的密码（可选）
#   UNITY_SERIAL      secret → 激活用的序列号（可选）
#
# 退出码：
#   0  预检通过
#   3  环境不可用（找不到 Unity / 版本不符 / 没有许可证来源 / 许可证写入失败）
#
# 安全：许可证内容只写文件、只读不打印；日志里只出现「已写入哪个路径」。

set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

# shellcheck source=lib/common.sh
. "$SCRIPT_DIR/lib/common.sh"
# shellcheck source=lib/unity-resolve.sh
. "$SCRIPT_DIR/lib/unity-resolve.sh"

JSONQ_TOOL="$SCRIPT_DIR/tools/jsonq.py"
export JSONQ_TOOL

# ── 1. 解析 Unity ──────────────────────────────────────────────────
UNITY=""
if [ -n "${UNITY_PATH:-}" ]; then
  if [ -f "$UNITY_PATH" ]; then
    UNITY="$UNITY_PATH"
  elif command -v cygpath >/dev/null 2>&1; then
    # Windows 形式的路径（E:\UnityHub\...\Unity.exe）在 bash 的 -f 测试下不一定通过
    UNITY_POSIX="$(cygpath -u "$UNITY_PATH" 2>/dev/null || true)"
    if [ -n "$UNITY_POSIX" ] && [ -f "$UNITY_POSIX" ]; then
      UNITY="$UNITY_POSIX"
      log_info "UNITY_PATH 已从 Windows 形式转成 POSIX 形式: $UNITY"
    fi
  fi
  if [ -z "$UNITY" ]; then
    die "$EXIT_ENV" "UNITY_PATH 指向的文件不存在: ${UNITY_PATH}"
  fi
else
  UNITY="$(resolve_unity)" || die "$EXIT_ENV" "找不到 Unity：请设置仓库变量 UNITY_PATH"
fi

log_info "Unity 可执行: $UNITY"

# ── 2. 版本一致性 ──────────────────────────────────────────────────
WANT="$(project_unity_version "$ROOT")"
HAVE="$(editor_binary_version "$UNITY" || true)"
if [ -z "$WANT" ]; then
  die "$EXIT_ENV" "读不到 ProjectSettings/ProjectVersion.txt 的版本"
fi
if [ -z "$HAVE" ]; then
  log_warn "取不到 Unity 自身版本，跳过一致性校验（工程要求 $WANT）"
elif [ "$WANT" != "$HAVE" ]; then
  die "$EXIT_ENV" "Unity 版本不符：工程要求 $WANT，runner 上是 $HAVE。用错版本构建会写出不同序列化版本的资产。"
else
  log_ok "Unity 版本一致: $HAVE"
fi

# ── 3. 许可证 ──────────────────────────────────────────────────────
# Unity 读取许可证的两个位置：
#   机器级：C:\ProgramData\Unity\Unity_lic.ulf   （写它通常需要管理员权限）
#   用户级：%APPDATA%\Unity\Unity_lic.ulf        （普通权限即可，优先用这个）
APP_DATA_POSIX=""
if [ -n "${APPDATA:-}" ]; then
  APP_DATA_POSIX="$(cygpath -u "$APPDATA" 2>/dev/null || echo "$APPDATA")"
elif [ -n "${USERPROFILE:-}" ]; then
  APP_DATA_POSIX="$(cygpath -u "${USERPROFILE}/AppData/Roaming" 2>/dev/null || echo "$USERPROFILE/AppData/Roaming")"
fi

write_ulf() {
  local target_dir="$1"
  mkdir -p "$target_dir" 2>/dev/null || return 1
  printf '%s' "$UNITY_LICENSE" > "$target_dir/Unity_lic.ulf" 2>/dev/null || return 1
  return 0
}

LICENSE_OK="false"

if [ -n "${UNITY_LICENSE:-}" ]; then
  if [ -n "$APP_DATA_POSIX" ] && write_ulf "$APP_DATA_POSIX/Unity"; then
    log_ok "ULF 许可证已写入 $APP_DATA_POSIX/Unity/Unity_lic.ulf（用户级）"
    LICENSE_OK="true"
  else
    log_warn "用户级许可证目录不可写，尝试机器级路径"
  fi
  if [ "$LICENSE_OK" != "true" ]; then
    if write_ulf "/c/ProgramData/Unity"; then
      log_ok "ULF 许可证已写入 /c/ProgramData/Unity/Unity_lic.ulf（机器级）"
      LICENSE_OK="true"
    else
      log_warn "机器级许可证目录不可写（通常需要管理员权限）"
    fi
  fi
  if [ "$LICENSE_OK" != "true" ]; then
    die "$EXIT_ENV" "ULF 许可证无处可写。请改用 UNITY_EMAIL + UNITY_PASSWORD + UNITY_SERIAL 方式激活。"
  fi
elif [ -n "${UNITY_EMAIL:-}" ] && [ -n "${UNITY_PASSWORD:-}" ]; then
  log_info "用账号方式激活 Unity …"
  "$UNITY" -batchmode -nographics -quit \
    -serial "${UNITY_SERIAL:-}" \
    -username "$UNITY_EMAIL" -password "$UNITY_PASSWORD" >/dev/null 2>&1 || true
  # 激活是否成功无法从退出码可靠判断，以「许可证文件是否出现」为准
  if [ -n "$APP_DATA_POSIX" ] && [ -f "$APP_DATA_POSIX/Unity/Unity_lic.ulf" ]; then
    log_ok "账号方式激活完成（检测到 ULF 许可证文件）"
    LICENSE_OK="true"
  else
    die "$EXIT_ENV" "账号方式激活后没有找到 ULF 许可证文件，激活可能失败"
  fi
else
  die "$EXIT_ENV" "没有可用的 Unity 许可证来源：需要 secret UNITY_LICENSE，或 UNITY_EMAIL + UNITY_PASSWORD (+ UNITY_SERIAL)"
fi

log_ok "Unity 预检通过"
exit 0
