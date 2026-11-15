#!/usr/bin/env bash
# Unity 可执行文件解析 + 工程占用检测。
#
# 解析顺序（先命中先用，全部失败才报错）：
#   1. 环境变量 UNITY_PATH —— 唯一显式指定，优先级最高
#   2. 环境变量 UNITY_HUB_ROOT / UNITY_INSTALL_ROOT —— 指定 Hub 安装根目录
#   3. 扫描已知安装根目录（按优先级）：
#        /e/UnityHub            ← 本机实际路径（Unity 中国版 LTS）
#        E:/UnityHub
#        $HOME/Unity/Hub/Editor
#        /c/Program Files/Unity/Hub/Editor
#        C:/Program Files/Unity/Hub/Editor
#   4. 在扫描结果里优先选「版本与 ProjectSettings/ProjectVersion.txt 一致」的那个；
#      找不到一致版本时退而取第一个，并在 stderr 打 WARN（不静默）
#
# 附：is_project_locked —— Unity Editor 正在打开本工程时，batchmode 会因工程被占用而失败。
#      五个 check 脚本据此给出明确提示，而不是让 Unity 报一堆看不懂的错。

# 读取工程要求的 Unity 版本（m_EditorVersion: 2022.3.62f3c1）
project_unity_version() {
  local root="$1"
  local file="$root/ProjectSettings/ProjectVersion.txt"
  [ -f "$file" ] || return 1
  sed -n 's/^m_EditorVersion:[[:space:]]*//p' "$file" | head -1 | tr -d '\r\n'
}

# 列出候选 Unity 可执行文件
_unity_candidates() {
  local c
  for c in \
    "/e/UnityHub"/*/Editor/Unity.exe \
    "E:/UnityHub"/*/Editor/Unity.exe \
    "$HOME/Unity/Hub/Editor"/*/Editor/Unity.exe \
    "$HOME/Unity/Hub/Editor"/*/Editor/Unity \
    "/c/Program Files/Unity/Hub/Editor"/*/Editor/Unity.exe \
    "C:/Program Files/Unity/Hub/Editor"/*/Editor/Unity.exe
  do
    [ -f "$c" ] && echo "$c"
  done

  # 环境变量指定的额外根目录（放在常规目录之后，保持上面的优先级）
  local extra
  for extra in "${UNITY_HUB_ROOT:-}" "${UNITY_INSTALL_ROOT:-}"; do
    [ -n "$extra" ] || continue
    for c in "$extra"/*/Editor/Unity.exe "$extra"/*/Editor/Unity "$extra"/Editor/Unity.exe; do
      [ -f "$c" ] && echo "$c"
    done
  done
}

# 解析 Unity 可执行文件；成功时 echo 路径并返回 0，失败返回 1
resolve_unity() {
  if [ -n "${UNITY_PATH:-}" ]; then
    if [ -f "$UNITY_PATH" ]; then
      echo "$UNITY_PATH"
      return 0
    fi
    echo "[FAIL] UNITY_PATH 指向的文件不存在：$UNITY_PATH" >&2
    return 1
  fi

  local root candidates want got dir ver
  root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
  want="$(project_unity_version "$root" || true)"

  candidates="$(_unity_candidates)"
  if [ -z "$candidates" ]; then
    echo "[FAIL] 找不到 Unity。请设置 UNITY_PATH 环境变量指向 Unity.exe，" >&2
    echo "       或把 Unity 装到以下任一目录：/e/UnityHub、\$HOME/Unity/Hub/Editor、" >&2
    echo "       C:/Program Files/Unity/Hub/Editor。" >&2
    return 1
  fi

  # 优先选与 ProjectVersion.txt 一致的版本（工程要求「全队锁同一 patch」）
  if [ -n "$want" ]; then
    while IFS= read -r got; do
      [ -n "$got" ] || continue
      dir="$(dirname "$(dirname "$got")")"
      ver="$(basename "$dir")"
      if [ "$ver" = "$want" ]; then
        echo "$got"
        return 0
      fi
    done <<< "$candidates"
  fi

  got="$(echo "$candidates" | head -1)"
  if [ -n "$want" ]; then
    echo "[WARN] 未找到与 ProjectSettings/ProjectVersion.txt 一致的 Unity（要求 $want），" >&2
    echo "[WARN] 退而使用：$got" >&2
  fi
  echo "$got"
  return 0
}

# 工程是否被占用（Unity Editor 打开中）。占用时 batchmode 必然失败。
is_project_locked() {
  local root="$1"
  [ -f "$root/Temp/UnityLockfile" ]
}

# 统一的「工程被占用」提示；返回 3（区别于 2 = 找不到 Unity）
reject_if_locked() {
  local root="$1"
  if is_project_locked "$root"; then
    echo "[FAIL] 工程正在被 Unity Editor 占用（Temp/UnityLockfile 存在）。" >&2
    echo "       batchmode 无法在工程被打开时运行；请关闭 Editor 后重试，" >&2
    echo "       或在 Editor 内用菜单执行对应的检查项。" >&2
    return 3
  fi
  return 0
}
