#!/usr/bin/env bash
# setup-github-ci.sh —— GitHub Actions 自托管 Runner + Unity 变量/Secret 的一键准备
#
# 用法：
#   bash Tools/release/setup-github-ci.sh license
#       把本机 Unity 许可证 (.ulf) 复制到剪贴板，并打印 UNITY_PATH。
#       许可证内容不会出现在终端、不进日志、不进仓库。
#
#   bash Tools/release/setup-github-ci.sh runner <注册令牌>
#       下载并非交互配置自托管 Windows Runner（不启动）。
#
#   bash Tools/release/setup-github-ci.sh start
#       在新终端窗口启动 Runner（可见日志，便于看 CI 实时输出）。
#
#   bash Tools/release/setup-github-ci.sh check
#       自检：Unity 路径 / 许可证 / Runner 安装与注册状态。
#
# 三步完整流程见 docs/release/CI-SETUP.md

set -uo pipefail

UNITY_PATH_DEFAULT="E:/UnityHub/2022.3.62f3c1/Editor/Unity.exe"
UNITY_PATH_UNIX="/e/UnityHub/2022.3.62f3c1/Editor/Unity.exe"
LICENSE_CANDIDATES=(
  "/c/ProgramData/Unity/Unity_lic.ulf"
  "/c/Users/${USERNAME:-dingy}/AppData/Roaming/Unity/Unity_lic.ulf"
  "/c/Users/${USERNAME:-dingy}/AppData/LocalLow/Unity/Unity_lic.ulf"
)
RUNNER_DIR="${RUNNER_DIR:-/c/actions-runner}"
RUNNER_NAME="${RUNNER_NAME:-edgechronicles-win-01}"
RUNNER_LABELS="${RUNNER_LABELS:-self-hosted,windows,x64}"
REPO_URL="${REPO_URL:-https://github.com/ding6667/EdgeChronicles}"

die() { echo "[FAIL] $*" >&2; exit 1; }
ok()  { echo "[OK]   $*"; }
warn(){ echo "[WARN] $*"; }

# ---------------------------------------------------------------- license
do_license() {
  echo "=============================================="
  echo " 步骤 1/3 · Unity 许可证 -> 剪贴板"
  echo "=============================================="
  local found=""
  for p in "${LICENSE_CANDIDATES[@]}"; do
    [ -f "$p" ] && { found="$p"; break; }
  done
  [ -n "$found" ] || die "找不到 Unity_lic.ulf。试过的路径：
${LICENSE_CANDIDATES[*]}
若在别处，请用 Unity Hub -> Manage License 查看实际路径，或手动打开该文件复制全文。"

  ok "找到许可证：$found（$(stat -c%s "$found") 字节）"

  # 复制到剪贴板：内容不进终端输出
  if cat "$found" | /c/Windows/System32/clip.exe 2>/dev/null; then
    ok "已复制到剪贴板（内容未打印）"
  else
    warn "复制到剪贴板失败。请手动打开该文件全选复制。"
  fi

  # 自检剪贴板里的东西确实是 XML 许可证（不打印内容）
  local head_tag
  head_tag="$(head -c 60 "$found" | tr -d '\r\n' | grep -o '<root>' | head -1)"
  [ -n "$head_tag" ] && ok "格式校验：根标签 <root> ✓（是 ULF 许可证，不是别的 XML）" \
                     || warn "根标签不是 <root>，请确认这是 Unity 许可证文件"

  echo
  echo "---- 去 GitHub 粘贴这两样 ----"
  echo
  echo "[A] 变量（Variables 标签，不是 Secrets）"
  echo "    名称: UNITY_PATH"
  echo "    值  : $UNITY_PATH_DEFAULT"
  echo
  echo "[B] Secret（Secrets 标签）"
  echo "    名称: UNITY_LICENSE"
  echo "    值  : 刚才复制到剪贴板的那份 .ulf 全文（Ctrl+V 粘贴）"
  echo
  echo "粘贴位置：仓库 -> Settings -> Secrets and variables -> Actions"
  echo "           上面一排标签切到 Variables 建 [A]、切到 Secrets 建 [B]"
  echo
  echo "注意：.ulf 里含机器绑定信息，属于机密——只贴进 Secret，不要贴到 Variables、"
  echo "      不要写进仓库、不要贴到聊天工具。"
}

# ---------------------------------------------------------------- runner
do_runner() {
  local token="${1:-}"
  [ -n "$token" ] || die "缺少注册令牌。用法：setup-github-ci.sh runner <注册令牌>
令牌获取：仓库 -> Settings -> Actions -> Runners -> New self-hosted runner ->
         选 Windows/x64 后页面顶部会显示一条 'config.cmd' 命令，从里面
         复制 --token 后面的那串（一次性有效，1 小时过期）。"

  echo "=============================================="
  echo " 步骤 2/3 · 下载并配置自托管 Runner"
  echo "=============================================="

  # 取最新版本
  local ver
  ver="$(curl -s --max-time 25 https://api.github.com/repos/actions/runner/releases/latest \
        | grep -o '"tag_name": *"v[^"]*"' | head -1 | sed 's/.*"v\(.*\)"/\1/')"
  [ -n "$ver" ] || die "取不到 runner 最新版本（网络不通？）。手动下载：
https://github.com/actions/runner/releases/latest （选 actions-runner-win-x64-<版本>.zip）"
  ok "runner 版本 v$ver"

  mkdir -p "$RUNNER_DIR" || die "无法创建 $RUNNER_DIR"

  if [ -f "$RUNNER_DIR/run.cmd" ]; then
    ok "已存在，跳过下载"
  else
    local zip="$RUNNER_DIR/runner.zip"
    echo "下载中（约 100–200MB）..."
    curl -L --max-time 600 -o "$zip" \
      "https://github.com/actions/runner/releases/download/v$ver/actions-runner-win-x64-$ver.zip" \
      || die "下载失败：$zip"
    ok "下载完成（$(du -m "$zip" | cut -f1) MB）"
    (cd "$RUNNER_DIR" && unzip -oq runner.zip) || die "解压失败"
    rm -f "$zip"
    ok "解压完成"
  fi

  # 非交互配置
  echo "配置中..."
  (cd "$RUNNER_DIR" && cmd //c config.cmd \
      --url "$REPO_URL" \
      --token "$token" \
      --name "$RUNNER_NAME" \
      --labels "$RUNNER_LABELS" \
      --no-default-labels \
      --unattended \
      --replace) || die "config.cmd 失败。若报 token 无效，多半是过期了——去 GitHub 重新取一个。"

  ok "Runner 已注册"
  echo
  echo "=============================================="
  echo " 步骤 3/3 · 启动 Runner"
  echo "=============================================="
  echo "请**新开一个终端窗口**，运行："
  echo
  echo "    $RUNNER_DIR\\run.cmd"
  echo
  echo "（可见日志窗口，CI 跑起来时能实时看到 job 输出；关掉窗口 = Runner 停止）"
  echo
  echo "不想常驻也行：本地跑 Tools/ci/check-*.sh 同样是 Gate，CI 只是把这套命令"
  echo "搬到另一台机器上执行。"
}

do_start() {
  [ -f "$RUNNER_DIR/run.cmd" ] || die "$RUNNER_DIR/run.cmd 不存在，先跑 setup-github-ci.sh runner <令牌>"
  echo "启动 Runner（$RUNNER_DIR\\run.cmd）……"
  echo "提示：这是长驻进程，建议单独开一个终端窗口运行；Ctrl+C 停止。"
  (cd "$RUNNER_DIR" && cmd //c run.cmd)
}

# ---------------------------------------------------------------- check
do_check() {
  echo "=============================================="
  echo " CI 自检"
  echo "=============================================="
  local bad=0

  if [ -f "$UNITY_PATH_UNIX" ]; then ok "Unity 可执行文件：$UNITY_PATH_UNIX"
  else warn "Unity 可执行文件缺失：$UNITY_PATH_UNIX"; bad=1; fi

  local found=""
  for p in "${LICENSE_CANDIDATES[@]}"; do [ -f "$p" ] && { found="$p"; break; }; done
  if [ -n "$found" ]; then ok "Unity 许可证：$found"
  else warn "找不到 Unity_lic.ulf"; bad=1; fi

  if [ -f "$RUNNER_DIR/run.cmd" ]; then
    ok "Runner 已安装：$RUNNER_DIR"
    if [ -f "$RUNNER_DIR/.runner" ]; then ok "Runner 已注册（.runner 存在）"
    else warn "Runner 未注册（缺 .runner），跑 setup-github-ci.sh runner <令牌>"; bad=1; fi
  else warn "Runner 未安装（$RUNNER_DIR）"; bad=1; fi

  # 仓库侧只能靠 GitHub API 查（未登录会 401），这里只提示
  echo
  echo "仓库侧（需你手工确认，脚本读不到）："
  echo "  [ ] Variables -> UNITY_PATH = $UNITY_PATH_DEFAULT"
  echo "  [ ] Secrets   -> UNITY_LICENSE = .ulf 全文"
  echo
  [ "$bad" -eq 0 ] && echo "[结果] 本机项全部就绪" || echo "[结果] 有未就绪项，见上面 WARN"
  return $bad
}

case "${1:-}" in
  license) do_license ;;
  runner)  do_runner "${2:-}" ;;
  start)   do_start ;;
  check)   do_check ;;
  *)
    echo "用法: bash Tools/release/setup-github-ci.sh {license|runner <令牌>|start|check}"
    exit 2 ;;
esac
