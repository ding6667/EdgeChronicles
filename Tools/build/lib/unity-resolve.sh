#!/usr/bin/env bash
# EdgeChronicles · Unity 解析与工程占用检测
#
# 关键事实（已核实）：
#   - 本机 Unity：E:/UnityHub/2022.3.62f3c1/Editor/Unity.exe（Unity 中国版 LTS）
#   - ProjectSettings/ProjectVersion.txt 的 m_EditorVersion 是版本真值
#   - Unity Editor 打开工程时会持有工程锁，batchmode 会因「工程被占用」失败。
#     本库提供占用检测，调用方必须据此报错退出，不得伪装成功。

# resolve_unity：打印 Unity 可执行文件绝对路径；找不到返回非零。
resolve_unity() {
  if [ -n "${UNITY_PATH:-}" ]; then
    if [ -f "$UNITY_PATH" ]; then
      printf '%s\n' "$UNITY_PATH"
      return 0
    fi
    printf '[FAIL] UNITY_PATH 指向的文件不存在: %s\n' "$UNITY_PATH" >&2
    return 1
  fi

  local c
  for c in "/e/UnityHub"/*/Editor/Unity.exe \
           "E:/UnityHub"/*/Editor/Unity.exe \
           "$HOME/Unity/Hub/Editor"/*/Editor/Unity.exe \
           "$HOME/Unity/Hub/Editor"/*/Editor/Unity \
           "/c/Program Files/Unity/Hub/Editor"/*/Editor/Unity.exe \
           "/c/Program Files/Unity/Hub/Editor"/*/Editor/Unity \
           "/opt/unity/editors"/*/Editor/Unity; do
    if [ -f "$c" ]; then
      printf '%s\n' "$c"
      return 0
    fi
  done
  printf '[FAIL] 找不到 Unity。请设置 UNITY_PATH 环境变量指向 Unity.exe\n' >&2
  return 1
}

# project_unity_version <工程根>：读 ProjectSettings/ProjectVersion.txt 的版本号。
project_unity_version() {
  local root="$1"
  sed -n 's/^m_EditorVersion:[[:space:]]*//p' "$root/ProjectSettings/ProjectVersion.txt" 2>/dev/null | head -1
}

# project_unity_revision <工程根>：读 ProjectSettings/ProjectVersion.txt 的修订号。
project_unity_revision() {
  local root="$1"
  sed -n 's/^m_EditorVersionWithRevision:[[:space:]]*[^ ]* (\(.*\))/\1/p' \
    "$root/ProjectSettings/ProjectVersion.txt" 2>/dev/null | head -1
}

# editor_binary_version <Unity 可执行>：取 Unity 自身版本（失败时返回空）。
# 三个来源依次尝试：
#   1) Editor 目录下的 Unity.version
#   2) 安装根目录下的 Unity.version
#   3) 安装路径里的版本号目录名（Hub 的布局是 <root>/<version>/Editor/Unity.exe）
editor_binary_version() {
  local exe="$1"
  local edirdir="${exe%/*}"
  local root="${edirdir%/*}"

  if [ -f "$edirdir/Unity.version" ]; then
    head -1 "$edirdir/Unity.version" 2>/dev/null
    return 0
  fi
  if [ -f "$root/Unity.version" ]; then
    head -1 "$root/Unity.version" 2>/dev/null
    return 0
  fi

  local base
  base="$(basename "$root")"
  if printf '%s' "$base" | grep -Eq '^[0-9]+\.[0-9]+\.[0-9]+'; then
    printf '%s\n' "$base"
    return 0
  fi
  return 1
}

# assert_unity_matches_project <Unity 可执行> <工程根>
# 版本不一致时构建出来的包与工程资产序列化版本可能不同，必须拒绝构建。
assert_unity_matches_project() {
  local exe="$1" root="$2"
  local want have
  want="$(project_unity_version "$root")"
  have="$(editor_binary_version "$exe" || true)"
  if [ -z "$want" ]; then
    printf '[FAIL] 读不到 ProjectSettings/ProjectVersion.txt 的版本\n' >&2
    return 1
  fi
  if [ -z "$have" ]; then
    printf '[WARN ] 取不到 Unity 自身版本，跳过版本一致性校验（工程要求 %s）\n' "$want" >&2
    return 0
  fi
  if [ "$want" != "$have" ]; then
    printf '[FAIL] Unity 版本不符：工程要求 %s，实际 %s\n' "$want" "$have" >&2
    printf '       用错版本构建会写出不同序列化版本的资产，必须换回 %s 再构建。\n' "$want" >&2
    return 1
  fi
  return 0
}

# unity_editor_is_running <工程根>
# 判定 Unity Editor 是否正占用本工程。命中任一即视为占用：
#   1) 工程锁文件 Temp/UnityLockfile 存在（Unity 打开工程时创建）
#   2) 存在 Unity.exe 进程（Windows；Linux 用 Unity 进程名匹配）
unity_editor_is_running() {
  local root="$1"

  if [ -f "$root/Temp/UnityLockfile" ]; then
    printf '工程锁文件存在: Temp/UnityLockfile\n'
    return 0
  fi

  if command -v tasklist >/dev/null 2>&1; then
    if tasklist //FI "IMAGENAME eq Unity.exe" 2>/dev/null | grep -qi "Unity.exe"; then
      printf '检测到正在运行的 Unity.exe 进程\n'
      return 0
    fi
  fi

  if command -v pgrep >/dev/null 2>&1; then
    if pgrep -f "Unity.*-projectPath.*$(basename "$root")" >/dev/null 2>&1; then
      printf '检测到正在运行的 Unity 进程\n'
      return 0
    fi
  fi

  return 1
}
