# 解析 Unity 可执行文件；优先 UNITY_PATH 环境变量，其次扫描 Hub 安装目录
resolve_unity() {
  if [ -n "${UNITY_PATH:-}" ] && [ -f "$UNITY_PATH" ]; then echo "$UNITY_PATH"; return 0; fi
  local c
  for c in "$HOME/Unity/Hub/Editor"/*/Editor/Unity.exe \
           "$HOME/Unity/Hub/Editor"/*/Editor/Unity \
           "/c/Program Files/Unity/Hub/Editor"/*/Editor/Unity.exe; do
    if [ -f "$c" ]; then echo "$c"; return 0; fi
  done
  echo "[FAIL] 找不到 Unity。请设置 UNITY_PATH 环境变量指向 Unity.exe" >&2
  return 1
}
