# 拾锋录 · 单机 3D 江湖刷本成长游戏

单机 3D 第三人称动作 RPG。1 角色 8 技能 · 3 副本 3 机制 Boss · 6 条修为线 · 12 套养成系统。
Unity 6 URP · 本地存档 · 无服务器 · **无工期与成本约束**（团队为 AI 员工）。

---

## 文档索引

| 文档 | 作用 | 什么时候看 |
|---|---|---|
| [`docs/impl/IMPL-v3.0.md`](docs/impl/IMPL-v3.0.md) | **实现方案（主文档）**：战斗十键与八层联动、12 套养成矩阵、技传派生表、打击感规格、数据契约、资产账目、并行切分 | 要实现，看这里 |
| [`docs/impl/IMPL-v3.0-EL-03-progression.md`](docs/impl/IMPL-v3.0-EL-03-progression.md) | 养成层展开件：13 张表逐字段规格、16 项机制枚举、A31–A42、快照求值器、存档与默认值、CI 判据矩阵 | 要写养成层的代码，看这里 |
| [`docs/impl/IMPL-v3.0-AD-03-art.md`](docs/impl/IMPL-v3.0-AD-03-art.md) | 美术展开件：UI 图集格位、五面板布局、星盘与络脉图形语言、派生 VFX 规格、AI 生成提示词模板 | 要做资产，看这里 |
| [`docs/impl/TOOLCHAIN.md`](docs/impl/TOOLCHAIN.md) | **开工清单**：本机工具、AI 员工运行条件、仓库约定、版本锁定、首次初始化 | 开工前照着勾 |
| [`docs/impl/IMPL-v2.0.md`](docs/impl/IMPL-v2.0.md) | 上一版（战斗内容已被 v3.0 取代），**架构部分仍有效** | 查架构细节时 |
| [`design/gdd/GDD-v1.0.md`](design/gdd/GDD-v1.0.md) | 设计参考：为何这样设计 + 治理规则 + 判据方法论 | 争论设计、查方法论时 |

> **单规格只在一处描述**：养成矩阵与打击感规格只在 IMPL-v3.0 定义；各展开件只写自己那一层，其余引用不復述。

---

## 仓库结构

```
Assets/_Project/
  Scripts/{Core,Data,Kernel,Binding,Presentation,UI,Flow}/   # 8 个程序集，依赖单向无环
  Editor/                                                     # 批处理自证入口 BuildCheck
  Tests/EditMode/                                             # 架构守卫测试
  Art/{Character,Boss,Scene,VFX,UI}/                          # 美术资产
  Data/                                                       # 导表器产出（不入库）
Packages/manifest.json                                        # 版本锁定，必须入库
Tools/ci/                                                     # 五个检查脚本
```

**依赖方向**：`Core → Data → Kernel → Binding → Flow`；`Presentation` / `UI` 只订阅事件。
这条规则由 `ArchitectureTests.cs` 强制——接错依赖测试立刻红，不需要人 review。

---

## 快速开工

```bash
git clone <repo> && cd <repo> && git lfs pull

# 用 Unity Hub 指定 6000.0.xx 打开工程（首次导入重建 Library，10–30 分钟）

bash Tools/ci/check-compile.sh          # 编译
bash Tools/ci/check-data.sh             # 配表结构
bash Tools/ci/check-progression.sh      # 养成层完整性（A31–A42）
bash Tools/ci/check-tests.sh            # EditMode（含架构守卫）
bash Tools/ci/check-tests-playmode.sh   # PlayMode 战斗×养成联动矩阵

git status                               # 必须完全干净
```

工具安装、版本锁定清单见 [`docs/impl/TOOLCHAIN.md`](docs/impl/TOOLCHAIN.md)。

---

## 开工前必读的五条规矩

1. **规格卡由交付方出，冻结即唯一真值源**；跨域数字只认规格卡与单测里的版本。
2. **环境会变的量不许写死**：依赖背景亮度 / 帧率 / 分辨率 / 窗口长度的常量，一律写成推导式或环境参数。
3. **能去耦的争议不裁决**：资产元数据（剪辑长度、面数、贴图规格）由管线注入，不写成配表常量。
4. **同一规格只在一处描述**：其余位置只写「见 §X」。复述即制造第二个来源。
5. **每条判据必须能写出自己的误判条件**，并配一个负向用例；**漏挂即废判据，不是调阈值**。
