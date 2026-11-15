## 1. 7 张新表的字段级规格

> **列名一律 snake_case；既有 13 表的 camelCase 由导表器兼容接受，输出统一 snake_case。** 同一个字段出现两种写法，就是两个真值源——不许例外。（本文件全部列名为 snake_case：`visual_tier` / `unlock_type` / `expected_power` / `reset_cost` / `reset_policy` / `unlock_derivative_id` / `mechanic_mod` / `mechanic_param` / `effect_key` / `effect_value` / `target_system` / `fame_unlock_tier`。）
> **「禁手填」= 导表时由公式覆盖，任何手填值与重算值不等即报 error**（沿用 v2.0 治理口径：派生值不得成为第二真值源）。
>
> **不存在"派生表"**：派生条目清单的唯一来源是 [`IMPL-v3.0.md` §4.8.5](./IMPL-v3.0.md#485-派生条目-8-条与断言建议)（8 条），由 `tech_tier.unlock_derivative_id` 与 `skill.derivative_ids` **双向承载**——两张表互为对方的完整性校验（断言 A33-1 / A33-2），**不另立第三张表**。
> **本文唯一引用的外部表是 `core_link`**（既有 13 表之一）：`tech_tier.pending_rule_key` 按 `state_id` 引用。

### 1.1 `star_node`（星盘节点 · 12 行 = 4 系 × 3 节点）

| 字段 | 类型 | 取值范围 | 必填 | 外键 | 派生 |
|---|---|---|---|---|---|
| `node_id` | string | `sn_*`，全局唯一 | 是 | — | 否 |
| `series` | enum | `blade`锋 / `blood`血 / `speed`速 / `momentum`势（形状即语义，见 AD-03 §3.1） | 是 | — | 否 |
| `pos_x` / `pos_y` | int ×2 | [-12,12]；同系 3 节点不得同坐标 | 是 | — | 否 |
| `prereq_ids` | string[] | 0–2 项，去重、无自引用 | 是（可空数组） | `star_node.node_id` | 否 |
| `unlock_tier` | int | **1–5 = 名望档位（I–V）**，与 `progression.fame_unlock_tier` 同值；各系 N1=1 / N2=2 / N3=3 | 是 | `progression.fame_unlock_tier` | 否 |
| `cost_eu` | int | 1–60 EU | 是 | — | 否 |
| `effect_key` | string | `eff_*`；**离散 / 次数 / 帧类节点用它**，与 `mechanic_mod` **互斥** | 条件必填 | 效果表 | 否 |
| `effect_value` | float | 0.01–1.00（**只存系数，不存绝对值**）；与 `mechanic_mod` **互斥** | 条件必填 | — | 否 |
| `node_type` | enum | `discrete`离散 / `count`次数 / `frame`帧 / `mechanic`机制（**手填**；C6 复算消耗的类型系数输入：离散 / 帧 = 1，次数 / 机制 = 2） | 是 | — | 否 |
| `mechanic_mod` | enum | `MM_*`（§2.1 枚举，**共 28 项**）；**机制类节点用它**，与 `effect_key` **互斥** | 条件必填 | — | 否 |
| `mechanic_param` | int | 0–100，含义随 `mechanic_mod` 定（见 §2.1 末列） | 条件必填 | — | 否 |
| `prereq_closure` | string[] | — | **禁手填** | — | 是：`prereq_ids` 传递闭包（DFS，去重） |
| `link_partners` | string[] | — | **禁手填** | — | 是：与本节点在 `star_link` 成对且同系的节点 id（双向） |

### 1.2 `star_link`（同系连携 · 8 行）

| 字段 | 类型 | 取值范围 | 必填 | 外键 | 派生 |
|---|---|---|---|---|---|
| `link_id` | string | `sl_*` | 是 | — | 否 |
| `node_a` / `node_b` | string ×2 | `node_id` 对，`node_a ≠ node_b`；**约定 `node_a` 为链式靠前的那一个**（N1+N2 / N2+N3） | 是 | `star_node.node_id` | 否 |
| `series` | enum | `blade` / `blood` / `speed` / `momentum`；**同系连携 = 两节点同系**（A32 静态部分读本列） | **禁手填** | `star_node.series` | 是：取 `node_a` 与 `node_b` 的共同 `series`（两者异系即 error） |
| `link_condition` | enum | `same_series_adjacent`（唯一合法值） | 是 | — | 否 |
| `trigger_node` | string | **哪个节点点亮时触发本连携**（= `node_b`，链式靠后的那个） | 是 | `star_node.node_id` | 否 |
| `effect_key` | string | `eff_link_*` | 是 | — | 否 |
| `effect_value` | float | 0.01–10.00（倍率或层数，看 `effect_key`） | 是 | — | 否 |
| `active_state` | enum | `inactive` / `active` | **禁手填** | — | 是：`node_a` 与 `node_b` **均已点亮**且同系 → `active` |

### 1.3 `tech_tier`（技传 · 8 技能 × 5 阶 = 40 行）

> **表名 = `tech_tier`**：不叫 `tech_skill`，也不拆成 `tech_skill_stat` / `tech_skill_derivative`。
> 阶位、属性、派生三类信息**同表同行**；技术层以 `tech_tier` 为唯一实现（IMPL-v3.0.md §7）。

| 字段 | 类型 | 取值范围 | 必填 | 外键 | 派生 |
|---|---|---|---|---|---|
| `skill_id` | string | `sk_*`，8 个门派技能 | 是 | `skill.skill_id` | 否 |
| `tier` | int | 1–5，**从 1 连续无缺口** | 是 | — | 否 |
| `unlock_type` | enum | `stat`数值强化 / `resource`资源规则 / `derivative`派生效果 / `window`窗口变化 / `mechanic`机制改写（**5 阶为 `mechanic` 时不得同时改写 L1–L6 核心循环，见 A41**） | 是 | — | 否 |
| `visual_tier` | enum | `none` / `param` / `generic` / `new_graphic`（none/param **不产生任何表现事件**；generic 产生「规则符浮现」；new_graphic 触发专属 VFX id） | 是 | — | 否 |
| `cost_eu` | int | 1–80 EU（谱份数：1/2/3/4/6 对应 1–5 阶） | 是 | — | 否 |
| `material_key` | string | `mat_*` | 是 | — | 否 |
| `unlock_derivative_id` | string | `dv_*`，**派生条目所在阶位填**（实况落在 **2 / 3 / 5** 阶）；派生条目清单 = [IMPL-v3.0.md §4.8.5](./IMPL-v3.0.md#485-派生条目-8-条与断言建议) 的 8 条（唯一来源，**不存在独立的派生表**） | 否 | 派生条目清单（非独立表） | 否 |
| `pending_rule_key` | string | `pr_*`，可空；**非空 = 该阶设置一个"待触发标记"**（如破势 → 下一个回锋变体）。空串 = 效果结算时直接生效，不需要标记 | 否 | `core_link.state_id` | 否 |
| `mechanic_targets` | string[] | 取值 ∈ `loop_l1`..`loop_l6`；`unlock_type == 'mechanic'` 时必填且**长度 ≤ 1**（见 A41），其余行必须为空数组 | 条件必填 | 否 |
| `stat_key` / `stat_value` | string / float | `eff_*` / 0.01–1.00 | 否 | — | 否 |
| `cum_cost_eu` | int | — | **禁手填** | — | 是：同 `skill_id` 的 Σ`cost_eu`(tier ≤ t) |
| `effective_mods` | key→float 集 | — | **禁手填** | — | 是：tier 1..t 的 `stat_key/stat_value` 逐项叠加 |

### 1.4 `specialty`（手札特技 · 6 条 · 3 槽）

| 字段 | 类型 | 取值范围 | 必填 | 外键 | 派生 |
|---|---|---|---|---|---|
| `spec_id` | string | `sp_*` | 是 | — | 否 |
| `slot` | enum | `slot_1` / `slot_2` / `slot_3` | 是 | — | 否 |
| `exclude_group` | string | 互斥组 id（如 `resource_cap`、`cooldown`） | 是 | — | 否 |
| `trigger` | enum | `always`常驻 / `on_cast` / `on_hit` / `on_dodge` / `on_perfect_dodge` / `on_core_state`（`always` = 随养成快照生效、无触发帧；IMPL-v3.0.md §4.8.6 要求的第 6 个取值） | 是 | — | 否 |
| `condition_tag` | string | `cond_*`，可空串 | 否 | — | 否 |
| `effect_key` / `effect_value` | string / float | `eff_*` / 0.01–1.00 | 是 | — | 否 |
| `cooldown_frames` | int | 0–600 | 是 | — | 否 |
| `cost_eu` | int | 1–40 EU（推导式：`8 × 组内位次`，组内第 1 条 = 8、第 2 条 = 16；6 条合计 72 EU——推导式只在 IMPL-v3.0.md §4.8.6，数值只在本表） | 是 | — | 否 |

### 1.5 `meridian`（络脉 · 8 节点 = 1 枢纽 + 3 分支 + 4 叶）

| 字段 | 类型 | 取值范围 | 必填 | 外键 | 派生 |
|---|---|---|---|---|---|
| `node_id` | string | `md_*` | 是 | — | 否 |
| `pos_x` / `pos_y` | int ×2 | [-12,12]；**网状布局坐标**（枢纽在原点，分支中环，叶外环），**锚点布局用，不是像素坐标** | 是 | — | 否 |
| `prereq_ids` | string[] | 0–3 项；**枢纽 = 3 分支 id**、分支 = 空、叶 = 对应分支 id | 是（可空数组） | `meridian.node_id` | 否 |
| `node_role` | enum | `branch`分支 / `leaf`叶 / `hub`枢纽（**手填**；C6 复算消耗的角色系数输入：1 / 3 / 9） | 是 | — | 否 |
| `leaf_order` | int | 1–8（**手填**；叶序系数 = 同一分支下的第 k 个叶 = k。破脉·省 = 1、破脉·连 = 2） | 是 | — | 否 |
| `mechanic_mod` | enum | **A35 枚举内的 `MM_*`**（见 §2.1） | 否 | — | 否 |
| `mechanic_param` | int | 0–100（含义随 `mechanic_mod` 定，见 §2.1 枚举表末列） | 否 | — | 否 |
| `stat_key` / `stat_value` | string / float | **恒为空**。依 v3.0 §4.1「络脉只改机制、不加战力」+ §4.8.3，本表 8 行**全部是机制类**，数值列一律不填；保留两列只为表结构统一，**导表器对这两列做"必须为空"校验** | **恒空** | — | 否 |
| `cost_eu` | int | 1–50 EU | 是 | — | 否 |
| `depth` | int | 0–3 | **禁手填** | — | 是：`prereq_ids` 闭包最大深度 |

### 1.6 `socket_item`（锋饰 · 3 槽 × 3 类 = 9 类目，多等级）

| 字段 | 类型 | 取值范围 | 必填 | 外键 | 派生 |
|---|---|---|---|---|---|
| `socket_id` | string | `so_*` | 是 | — | 否 |
| `item_type` | enum | `atk` / `def` / `util`（**决定可插孔位**） | 是 | — | 否 |
| `slot_kind` | enum | `slot_1` / `slot_2` / `slot_3` | 是 | — | 否 |
| `level` | int | 1–10 | 是 | — | 否 |
| `compose_from` | string[] | 2–3 个 `socket_id`，**最高级为空数组** | 是（可空数组） | `socket_item.socket_id` | 否 |
| `effect_key` / `effect_value` | string / float | `eff_*` / 0.01–1.00 | 是 | — | 否 |
| `compose_chain` | string[] | — | **禁手填** | — | 是：`compose_from` 传递闭包 |
| `is_max_level` | bool | — | **禁手填** | — | 是：不存在以本项为材料的更高等级项 |

### 1.7 `refine_stage`（洗练「重磨」+ 解锋）

| 字段 | 类型 | 取值范围 | 必填 | 外键 | 派生 |
|---|---|---|---|---|---|
| `stage_id` | string | `rs_*` | 是 | — | 否 |
| `slot_index` | enum | `affix_1` / `affix_2` / `affix_3` | 是 | — | 否 |
| `keep_count` | int | 0–2（保留几条不被重掷） | 是 | — | 否 |
| `cost_eu` | int | 1–60 EU（洗练石折算） | 是 | — | 否 |
| `pity_threshold` | int | 5–30（保底计数阈值） | 是 | — | 否 |
| `pity_target` | string | `affix_id`，可空串（空 = 保底随机） | 否 | `affix_pool.affix_id` | 否 |
| `refund_tier_down` | bool | true = 解锋（品阶回退返还） | 是 | — | 否 |
| `refund_ratio` | float | 0.00–1.00 | 是 | — | 否 |
| `pity_counter` | int | 0–29 | **禁手填**（表内恒 0） | — | 是：**存档内计数**，读档时回填 |

### 1.8 扩展表的增量字段

| 表 | 增量字段 | 类型 / 范围 | 必填 | 派生 |
|---|---|---|---|---|
| `affix_pool` | `skill_tag` | string `sk_*`，空串 = 不挂技能 | 否 | 否 |
| | `mechanic_mod` | enum `MM_*`（A35 清单），空串 = 无机制修改 | 否 | 否 |
| `skill` | `tech_tiers` | int[] ⊆ [1,5] | **禁手填** | 是：`tech_tier` 中该 `skill_id` 存在的 tier 升序 |
| | `derivative_ids` | string[] `dv_*` | 否 | 否 |
| `progression` | `kind` | enum `cultivation` / `fame` / `reset`（**同表承载 6 条修为线 + 名望 + 重置规则**） | 是 | 否 |
| | `material_key` | string `mat_*`；该行对应的养成材料（**修为线行必填** = 该线专属材料；名望行 = 空串，名望按通关计数、不消耗材料；重置行 = 空串） | 修为线行必填 | 材料总表（IMPL-v3.0.md §4.3） | 否 |
| | `cost_eu` | int 0–40；**0 级 → 1 级的单级升级价**（EU；修为线行 = `tiers[0].cost_eu`；名望行 / 重置行 = 0） | **禁手填** | — | 是：取 `tiers[0].cost_eu`（修为线行）/ 0（其余行） |
| | `target_system` | enum `cultivation` / `star` / `meridian` / `equipment`；`kind=reset` 时必填（指向被重置的系统） | 条件必填 | 否 |
| | `reset_policy` | enum **只允许 `free`**（§4.8.4：`partial` / `paid` 已删除，避免未用枚举值；将来要收费改 `reset_cost` 数字即可，不新增枚举值） | `kind=reset` 时必填 | 否 |
| | `reset_cost` | int 0–200（**当前全部为 0 = 免费**，含解锋）；`kind=reset` 时必填 | 条件必填 | 否 |
| | `fame_unlock_tier` | int **1–5 = 名望档位**（I–V，**手填的档位编号**）；与 `star_node.unlock_tier` **同值域** | `kind=fame` 时必填 | 否 |
| | `fame_lv_min` / `fame_lv_max` | int 1–10 | **禁手填** | 是：`档 = ceil(名望线Lv / 2)` → 档 t 对应 Lv ∈ `[2(t−1)+1, 2t]`（t=1→[1,2]，t=3→[5,6]，t=5→[9,10]） |
| | `fame_source` | enum `clear_count`通关数 / `difficulty`难度档达成 / `mixed`两者合计；`kind=fame` 时必填 | 条件必填 | 否 |
| | `expected_power` | int | **禁手填** | 是：`120 + Σ(线权重×线原始值) + 装备基底 + 词条加权` |

### 1.9 战斗侧表的增量字段（本轮跨线需求）

| 表 | 增量字段 | 类型 / 范围 | 必填 | 派生 |
|---|---|---|---|---|
| `enemy` | `shield_layers` | int 0–3；**0 = 无护盾**。孤锋崖三影「打错身」+1 层，**到 3 层不再增加** | 否（默认 0） | 否 |
| | `shield_max` | int 1–3，**默认 3**；`shield_layers ≤ shield_max`（定案值，与美术「3 片叠瓦弧板」完全等价） | 否（默认 3） | 否 |
| `global_tuning` | `vfx_graphic_budget` | int，**默认 8**（= 派生专属 6 + 通用件 2）。**A43 的阈值从此字段取，不写死在断言里** | 是 | 否 |
| | `vfx_generic_budget` | int，**默认 2**（通用件「规则符 + 阶位刻度环」占格）。**专属上限 6 = 8 − 2 算出来的，不是写进断言的** | 是 | 否 |
| | `drop_pity_threshold` | int，**默认 5**（连续 5 次副本未掉落则第 6 次必出）。A40 的阈值从此字段取 | 是 | 否 |
| `skill` | `combo_segments` | 段数组：`{ startup, active, recovery, hit_shape_ref, vfx_variant }`；**第 4 段缺省时读段三** | 否（单段技能可为空数组） | 否 |
| | `segment_count` | int 1–4；迅锋 5 阶为 4，其余 3 | 否（默认 = `combo_segments.Length`） | 是 |

> **掉落保底计数不放在任何表里**——它是**跨副本的运行状态**，存在存档 `progression.drop_pity[material_key].counter`（见 §4），**不是表字段、不可手填**。

### 1.10 星盘 / 连携 / 络脉的行数据

> 行内容**照抄** [`IMPL-v3.0.md` §4.8.2–§4.8.3](./IMPL-v3.0.md#482-星盘-12-节点4-系--3链式前置同系相邻连携-8-条)——**那份是内容的唯一来源**，本节只做"设计内容 → 字段"的映射，不新增、不改写任何一条效果。
> 坐标是**布局基准值**（锚点布局用），最终位置由 UI 面板决定，**不按像素硬排**。

**`star_node` · 12 行**

> `cost_eu` 数值写在**本表**（数据层唯一真值源）；推导式（公式与系数）在 [IMPL-v3.0.md §4.8.2](./IMPL-v3.0.md#482-星盘-12-节点4-系--3链式前置同系相邻连携-8-条)，校验判据 C1–C7 在 [`docs/design/progression-node-cost-rules.md`](../design/progression-node-cost-rules.md)。**三处各管一件事，数值不复制。**

| `node_id` | 节点 | `series` | `pos_x`,`pos_y` | `prereq_ids` | `unlock_tier` | `cost_eu` | 效果承载 | 参数 | 类型（`node_type`） |
|---|---|---|---|---|---|---|---|---|---|
| `sn_blade_1` | 连击 | blade | -9, 6 | — | 1 | 2 | `eff_combo_2hit_stack` | 1 | `discrete` |
| `sn_blade_2` | 转破势 | blade | -9, 0 | `sn_blade_1` | 2 | 12 | `MM_STAR_BREAKSTATE` | 1 | `mechanic` |
| `sn_blade_3` | 锋尽 | blade | -9, -6 | `sn_blade_2` | 3 | 36 | `eff_channel_extra_settle` | 1 | `count` |
| `sn_blood_1` | 回偿 | blood | -3, 6 | — | 1 | 2 | `eff_hitstun_stack_refund` | 1 | `discrete` |
| `sn_blood_2` | 盾解 | blood | -3, 0 | `sn_blood_1` | 2 | 12 | `MM_STAR_SHIELD_BREAK_FIRST` | 1 | `mechanic` |
| `sn_blood_3` | 代偿 | blood | -3, -6 | `sn_blood_2` | 3 | 36 | `MM_STAR_LIFESTEAL_CONTINUE` | 1 | `mechanic` |
| `sn_speed_1` | 先手 | speed | 3, 6 | — | 1 | 2 | `eff_perfect_dodge_stack` | 2 | `discrete` |
| `sn_speed_2` | 续 | speed | 3, 0 | `sn_speed_1` | 2 | 6 | `eff_cancel_window_frames` | 6 | `frame` |
| `sn_speed_3` | 连 | speed | 3, -6 | `sn_speed_2` | 3 | 36 | `MM_STAR_CHANNEL_MOVE` | 1 | `mechanic` |
| `sn_momentum_1` | 聚 | momentum | 9, 6 | — | 1 | 2 | `eff_channel_stack_per_settle` | 1 | `discrete` |
| `sn_momentum_2` | 溢 | momentum | 9, 0 | `sn_momentum_1` | 2 | 12 | `MM_STAR_OVERCAP_TO_DAMAGE` | 3 | `mechanic` |
| `sn_momentum_3` | 轮 | momentum | 9, -6 | `sn_momentum_2` | 3 | 36 | `MM_STAR_CANCEL_ALL_AFTER_N` | 3 | `mechanic` |

> **`sn_blade_2` 的节点名保持「转破势」**：IMPL-v3.0.md §4.5.2 立了「破势（目标状态）」与「破势回锋（位移变体）」**一名两义**的硬约束，`MM_STAR_BREAKSTATE` 的枚举含义也是「进入**破势**」（敌人身上的状态）。节点若改称「转破势回锋」会把两义混成义，故数据层按枚举含义落名；节点 id `sn_blade_2` 不受影响。

**`star_link` · 8 行**

| `link_id` | `node_a` + `node_b` | `trigger_node` | `effect_key` | `effect_value` | 额外效果 |
|---|---|---|---|---|---|
| `sl_blade_12` | `sn_blade_1` + `sn_blade_2` | `sn_blade_2` | `eff_link_breakstate_stack_mult` | 2.00 | 破势目标身上的连击层数收益翻倍 |
| `sl_blade_23` | `sn_blade_2` + `sn_blade_3` | `sn_blade_3` | `eff_link_channel_extra_settle` | 1 | 锋尽触发的聚锋额外结算次数 +1 |
| `sl_blood_12` | `sn_blood_1` + `sn_blood_2` | `sn_blood_2` | `eff_link_refund_shield_extra` | 1 | 回偿对护盾目标生效时额外返还 1 层 |
| `sl_blood_23` | `sn_blood_2` + `sn_blood_3` | `sn_blood_3` | `eff_link_shield_break_count` | 2 | 盾解一次击碎 2 层护盾 |
| `sl_speed_12` | `sn_speed_1` + `sn_speed_2` | `sn_speed_2` | `eff_link_cancel_window_frames` | 10 | 续的窗口延长改为 10 帧 |
| `sl_speed_23` | `sn_speed_2` + `sn_speed_3` | `sn_speed_3` | `eff_link_channel_move_no_interrupt` | 1 | 连生效时引导中的结算间隔不因移动中断 |
| `sl_momentum_12` | `sn_momentum_1` + `sn_momentum_2` | `sn_momentum_2` | `eff_link_overcap_cap` | 4 | 溢的存储上限 +1（3 → 4） |
| `sl_momentum_23` | `sn_momentum_2` + `sn_momentum_3` | `sn_momentum_3` | `eff_link_cancel_refund_stack` | 1 | 轮的第 3 次起额外返还 1 层锋芒 |

**`meridian` · 8 行（全部机制类，数值 0）**

> `cost_eu` 数值写在**本表**（数据层唯一真值源）；推导式在 [IMPL-v3.0.md §4.8.3](./IMPL-v3.0.md#483-络脉-8-节点1-枢纽--3-分支--4-叶全部为机制类不加战力)，判据 C1–C7 在 [`docs/design/progression-node-cost-rules.md`](../design/progression-node-cost-rules.md)。

| `node_id` | 节点 | 位置 | `prereq_ids` | `cost_eu` | `node_role` | `leaf_order` | `mechanic_mod` | `mechanic_param` | 效果 |
|---|---|---|---|---|---|---|---|---|---|
| `md_guan` | 贯（枢纽） | 0, 0 | `md_fengmai`, `md_shimai`, `md_pomai` | 27 | `hub` | 1 | `MM_MER_CONDUCE_SKILL` | 150（cm = 1.5m） | 锋域在场时，服务技能的增益可**传导**给锋域边缘 1.5m 内的敌人 |
| `md_fengmai` | 锋脉（分支） | -4, 3 | — | 3 | `branch` | 1 | `MM_MER_CONDUCE_RANGE` | 300（cm = 3.0m） | 传导范围 1.5m → 3m |
| `md_fengmai_xu` | 锋脉·续（叶） | -7, 5 | `md_fengmai` | 9 | `leaf` | 1 | `MM_ATK_NO_INTERRUPT_IN_DOMAIN` | 0 | 锋域内普攻不中断（v3.0 §5 交叉表已登记） |
| `md_shimai` | 势脉（分支） | 4, 3 | — | 3 | `branch` | 1 | `MM_MER_CONDUCE_STACK` | 1 | 传导对象由增益改为**层数** |
| `md_shimai_fan` | 势脉·返（叶） | 7, 5 | `md_shimai` | 9 | `leaf` | 1 | `MM_MER_STACK_REFUND` | 1 | 被传导层数的敌人死亡时返还 1 层锋芒 |
| `md_pomai` | 破脉（分支） | 0, -4 | — | 3 | `branch` | 1 | `MM_MER_CONDUCE_FINISH` | 150（cm = 1.5m） | 破锋的引爆可传导给锋域边缘的敌人 |
| `md_pomai_sheng` | 破脉·省（叶） | -3, -7 | `md_pomai` | 9 | `leaf` | 1 | `MM_FINISH_COST_MINUS1` | 1 | 破锋消耗层数可减为 2 层（与词条 / 技传多来源时**取最小值**，A39） |
| `md_pomai_lian` | 破脉·连（叶） | 3, -7 | `md_pomai` | 18 | `leaf` | 2 | `MM_MER_FINISH_CHAIN` | 1 | 引爆可连锁传导给第二个目标 |

> **`cost_eu` 口径（已定稿）**：星盘与络脉的消耗**已由设计线补齐**（数值在本表，推导式在 IMPL，判据 C1–C7 在规则文件）。**导表器对这两表启用消耗值校验**：值域 C1 / C2、同系严格递增 C3、同分支严格递增 C4、拓扑序 C5、**按推导式复算全部 20 行 C6（手填值与重算值不等即 error）**。两表**加战力 = 0**，`cost_eu` **不进 A38**（C7）。

> **段数组化的口径**：`skill.phases`（定长 startup/active/recovery）保留给**单段技能**；多段技能（迅锋）改用 `combo_segments` 数组，二者互斥（`combo_segments` 非空时 `phases` 置空）。`entryTags` 不随段数变化——迅锋 5 阶仍是 `light` 标签，**段数不是标签**。

---

## 2. A31–A43 的可执行判定

> A31–A38 承接 v2.0 §5；**A39–A43 为 team-lead 已裁**（编号与 `IMPL-v3.0.md` §7 索引表一一对应，两处不是两份定义——改判定逻辑只改本文）。

**三级校验流水线**（任一级 error 即阻断，不生成产物）：

| 级 | 时机 | 跑什么 | 失败后果 |
|---|---|---|---|
| L1 导表静态 | `TableImport` 读表后、生成产物前 | A19–A31、A33（含 A33-1…A33-5）、A35–A43 | 不生成 `Assets/Generated/*`（本轮生成物为 JSON） |
| L2 跨表审计 | `ProgressionAudit.Run` | A32 静态部分、A34 静态部分、孤儿外键、派生列覆盖 | CI 失败 |
| L3 运行时 | 存档加载 + PlayMode 集成测试 | A32 动态、A34 动态、A38 / A39 / A40 重算比对 | 读档失败 / 测试红 |

| # | 判定逻辑（可执行写法） | 在哪一级跑 |
|---|---|---|
| **A31** | 建图 `prereq → node`。① 每个 `prereq_ids` 元素必在 `star_node` 存在；② Kahn 拓扑排序后剩余节点数 = 0（无环）；③ `prereq_ids` 去重后长度 = 原长度且不含自身。**任一失败 → error，精确到 `node_id`** | L1 |
| **A32** | 静态：`star_link.node_a/node_b` 必同 `series` 且 `link_condition` = `same_series_adjacent`。动态：三档存档夹具 —— ①仅 A 点亮 ②A、B 同系且均点亮 ③A、B 均点亮但异系（非法夹具，必须构造失败）；断言 ①`inactive` ②`active` ③`inactive`。**夹具②必须真的多出 `effect_value` 的一份加成**（数值比对，不只看状态位） | L2 + L3 |
| **A33** | ① 按 `skill_id` 分组，`tier` 集合 == `{1..max}`（无缺口、无重复、`max ≤ 5`）；② **阶位不变量**（IMPL-v3.0.md §4.5.1）：每个技能的 5 个阶位**恰好覆盖 5 种 `unlock_type` 各一次**——**tier 1 恒为 `stat`、tier 5 恒为 `mechanic`**，中间 3 阶（`resource` / `derivative` / `window`）的先后顺序**按技能自定义**。⚠️ **不存在全局固定的「阶位 → 类型」映射**——逐行取值以 IMPL-v3.0.md §4.5.4 的 40 行为准 | L1 |
| **A33-1** | **派生双向引用一致（不写死条数）**：`tech_tier.unlock_derivative_id` 非空的行，其 id **两两不重复**，且**全部出现在 `skill.derivative_ids` 中**；反向，`skill.derivative_ids` 的每个 id **至少被一个阶位引用**。清单唯一来源 = IMPL-v3.0.md §4.8.5 的 8 条，**由这两张表互为校验，不另立派生表** | L1 |
| **A33-2** | 每个非空 `unlock_derivative_id` 必须属于派生条目清单（IMPL-v3.0.md §4.8.5 的 8 条）；**每个技能至多 2 条**；**该 id 所在行的 `tier` 必须等于该技能「派生效果」类型所占的阶位**（实况落在 **2 / 3 / 5** 阶：破势回锋 在缚锋 2 阶、末跳翻倍与回响 在 3 阶、其余 5 条在 5 阶）——**不存在「派生只可能在 3 / 5 阶」的固定阶位**，派生条目落在哪一阶由该技能的阶位类型顺序决定 | L1 |
| **A33-3** | **裂石的全部 5 阶、且回锋的 1–4 阶，`unlock_derivative_id` 必须为空**（裂石走数值 / 资源 / 窗口 / 行为路线，零新增视觉；回锋仅 5 阶允许 `dv_huifeng_pull_turn`） | L1 |
| **A33-4** | `unlock_type = 'derivative'` 且 `visual_tier = 'new_graphic'` 的行，**必须有非空 `unlock_derivative_id`** | L1 |
| **A33-5** | `visual_tier = 'new_graphic'` 的行数 **== 派生条目清单中标 `new_graphic` 的条数**（实况 6）；**格位配额账在 A43**，此处不重复校验数字 | L1 |
| **A34** | 对任意存档/装备配置：取 3 个槽位的 `spec_id` → 读 `exclude_group` → **两两比较**，同一 `exclude_group` 出现 ≥2 次即 error（`slots=[i,j]` 列出冲突）。夹具：装 `resource_cap` 组的 2 条 → 必须报错 | L2（枚举全组合）+ L3（实际存档） |
| **A35** | 每个非空 `mechanic_mod`（来源 **3 处**：`meridian.mechanic_mod`、`star_node.mechanic_mod`、`affix_pool.mechanic_mod`）必须 ∈ §2.1 的 **28 项**枚举，否则 error 并打印**最近似拼写**。**空串 = 无修改，合法** | L1 |
| **A36** | 对 `compose_from` 建图做拓扑排序：① 无环；② 存在 `level == Lmax` 的项其 `compose_from` 必为空数组；③ 每个 `level < Lmax` 的项，`compose_from` 中至少一项 `level = 本项 level − 1` | L1 |
| **A37** | ① **挂载合法性**：`affix_pool.skill_tag` 非空 ⇒ ∈ `skill.skill_id` 集合；空串跳过。② **词条池口径**（IMPL-v3.0.md §4.1）：**带 `skill_tag` 的词条数 ≥ 6**（对应「18 条中至少 6 条带 skill_tag」）。③ **技能侧反向检查**：`tech_tier` 覆盖的 **8 个门派技能**中，被至少一条词条命中的技能数 **≥ 6** | L1 |
| **A38** | `progression.expected_power` 与公式重算值比对，**差 ≠ 0 即 error**（不是"允许四舍五入"，用整数 EU 精确比）。公式：`base_power + Σ(line.weight × line.raw_value) + Σ(equipment.base_power) + Σ(affix_weighted)`。**基数与权重都取表内值**：`base_power` 取 `global_tuning.base_power`（战力基数），`weight` 取 `progression.weight`（权重表的来源见 GDD §4.2，**此处不复制数值**）。星盘 / 络脉的 `cost_eu` **不进本公式**（两套系统加战力 = 0，判据 C7） | L1 + L3（读档后重算） |
| **A39-1** | **上限类取最大值**：同一 `effect_key` 被多个来源（`affix_pool` / `meridian` / `star_node` / `tech_tier` / `specialty`）同时提供时，**上限类（加值型，如 `resource_cap`：锋芒上限 = max(5, 手札 +1) = 6）取最大值**。⚠️ **不可简化成"一律取最小"**——那会把锋芒上限 5→6 判成非法 | L1 + L3 |
| **A39-2** | **消耗 / 削减类取最小值**：同一 `effect_key` 的**消耗与削减类**（如 `finish_cost`：破锋消耗 = min(3, 词条 2, 络脉 2, 技传 2) = **2**）**取最小值** | L1 + L3 |
| **A39-3** | **其余数值类取最小值、不叠加**：不属于上限类 / 消耗削减类的数值类效果，取所有来源的**最小值**（不是求和） | L1 + L3 |
| **A39-4** | **离散类只生效一次**：布尔 / 开关类效果（`MM_*` 开关、`specialty` 的规则型特技）**生效次数 == 1**，多来源不重复叠加 | L1 + L3 |
| **A39（合并口径）** | A39-1…A39-4 是同一条规则的四类分支，**判定夹具**：枚举每个 `effect_key` 的全部来源组合，断言合并结果 == 预期值；离散类断言生效次数 == 1。**合并在快照构建时（tick #0）一次算完，热路径不做合并**（帧内不得出现 `Dictionary<string,…>` / LINQ / 装箱） | L1 + L3 |
| **A40** | **稀有材料掉落保底**：`drop_pity[material_key].counter ≥ global_tuning.drop_pity_threshold`（默认 **5**）时，该次结算**必须**包含该 `material_key`；实际掉落时计数器清零，未掉落时 +1。**计数器按 `material_key` 全局累计，不随副本重置**——放 `enemy.drop_table` 会让换本刷把计数洗掉，保底形同虚设 | L1 + L3 |
| **A41** | **技传 5 阶不得同时改写核心循环**：对每个 `skill_id` 的 `tier == 5` 行，断言 `unlock_type == 'mechanic'` 时 `mechanic_targets` 的元素数 **≤ 1**（一次只准改写 L1–L6 中的一条；取值 ∈ `loop_l1`..`loop_l6`）。`unlock_type != 'mechanic'` 的行 `mechanic_targets` 必须为空数组 | L1 |
| **A42** | **重置与名望完整性**：① `kind == 'reset'` 的行 `reset_cost ≥ 0`（int，禁负），且当前**实参全为 0**（免费）；② `reset_policy` 实参**必须等于 `free`**（枚举已精简为单值，无未用枚举值）；③ `kind == 'reset'` 的行必须有非空 `target_system`；④ `kind == 'fame'` 的行 `fame_unlock_tier` ∈ [1,5] 且 **1..5 每档至少一行**（防名望缺档），且 **`fame_lv_min` / `fame_lv_max`（禁手填）必须等于 `档 = ceil(名望线Lv / 2)` 的映射结果**（档 t ↔ Lv ∈ [2(t−1)+1, 2t]），`star_node.unlock_tier` 与同名望档同值 | L1 |
| **A43** | **图集格位配额（两层）**：`visual_tier = 'new_graphic'` 的行数 **+ 通用件占格** ≤ `global_tuning.vfx_graphic_budget`（默认 **8**）；通用件占格取 `global_tuning.vfx_generic_budget`（默认 **2**）→ **专属上限 6 = 8 − 2 是算出来的，不是写进断言的**。实况：new_graphic **6** + 通用件 **2** = **8 ≤ 8（刚好用满，无余量）**。要收紧就改 `vfx_graphic_budget` 的值，不改断言 | L1 |

### 2.1 A35 的 `mechanic_mod` 枚举清单（全部 **28 项**，唯一合法集合）

> **为什么从 16 项扩到 28 项**：星盘 12 节点里有 **6 个机制类**、络脉 8 节点**全部是机制类**——共 **14 条机制效果**必须有枚举值，否则 A35 拦不住、A 判定就形同虚设。新增 12 项（星盘 6 + 络脉 6，络脉另 2 条复用 `MM_ATK_NO_INTERRUPT_IN_DOMAIN` / `MM_FINISH_COST_MINUS1`）。
> **参数单位随 id 定**（见末列）：层 / 次 / 帧 / % / cm / 倍率。

**一、通用机制项（16 项，来源：词条 / 络脉 / 技传 / 手札）**

| # | 枚举 id | 中文含义 | `mechanic_param` |
|---|---|---|---|
| 1 | `MM_ATK_NO_INTERRUPT_IN_DOMAIN` | 锋域内普攻不中断 | 忽略（0） |
| 2 | `MM_FINISH_COST_MINUS1` | 破锋消耗 −1 层 | 减层数（1） |
| 3 | `MM_PERFECT_DODGE_REFUND1` | 完美闪避额外回 1 层 | 回层数（1） |
| 4 | `MM_RESOURCE_CAP_PLUS1` | 锋芒上限 +1 | 加层数（1） |
| 5 | `MM_CORE_CD_MINUS_FRAMES` | 拾锋式 CD −3s | 减帧数（180） |
| 6 | `MM_DOMAIN_LAST_JUMP_DOUBLE` | 锋域结束前最后一跳翻倍 | 倍率（200 = ×2.00） |
| 7 | `MM_CHANNEL_INTERVAL_HALF` | 聚锋结算间隔 ×0.5 | 百分比（50） |
| 8 | `MM_DASH_IFRAME_PLUS_FRAMES` | 回锋无敌帧 +3 | 加帧数（3） |
| 9 | `MM_BIND_TRIGGER_VARIANT` | 缚锋命中被缚目标触发派生 | 开关（1） |
| 10 | `MM_FINISH_EXTRA_DOMAIN` | 破锋引爆附带 1 次锋域 | 持续帧（360） |
| 11 | `MM_DOMAIN_HIT_GAIN1` | 锋域内命中 +1 层 | 加层数（1） |
| 12 | `MM_GUARD_REFUND_IN_DOMAIN` | 抱元被击回层锋域内 ×2 | 倍率（200） |
| 13 | `MM_HEAVY_GAIN_EXTRA` | 裂石在锋域内额外 +1 层 | 加层数（1） |
| 14 | `MM_CANCEL_WINDOW_WIDEN` | 取消窗口延长 | 加帧数（1–8） |
| 15 | `MM_DODGE_CD_MINUS_FRAMES` | 闪避 CD −N | 减帧数 |
| 16 | `MM_CRIT_IN_DOMAIN` | 锋域内暴击率 +N% | 加百分点（3） |

**二、星盘机制项（6 项，来源：`star_node.mechanic_mod`）**

| # | 枚举 id | 中文含义 | `mechanic_param` |
|---|---|---|---|
| 17 | `MM_STAR_BREAKSTATE` | 削韧已满的目标被命中时直接进入破势，破势期间命中额外 +1 层 | 额外层数（1） |
| 18 | `MM_STAR_SHIELD_BREAK_FIRST` | 对有护盾的目标命中时，先击碎 N 层护盾再结算伤害 | 击碎层数（1） |
| 19 | `MM_STAR_LIFESTEAL_CONTINUE` | 生命首次归零时改为消耗全部锋芒换 1 次无敌帧续战 | 每场次数（1） |
| 20 | `MM_STAR_CHANNEL_MOVE` | 聚锋引导期间可移动，移动不打断引导 | 开关（1） |
| 21 | `MM_STAR_OVERCAP_TO_DAMAGE` | 锋芒达上限时，多出的层数转为拾锋式伤害加成 | 每层加成 %（3）/ 存储上限层数（3） |
| 22 | `MM_STAR_CANCEL_ALL_AFTER_N` | 同一技能连续释放时，第 N 次起取消窗口全开 | 次数（3） |

**三、络脉机制项（6 项，来源：`meridian.mechanic_mod`；另 2 项复用通用枚举）**

| # | 枚举 id | 中文含义 | `mechanic_param` |
|---|---|---|---|
| 23 | `MM_MER_CONDUCE_SKILL` | 锋域在场时，服务技能的增益可传导给锋域边缘 R 内的敌人 | 半径 cm（150 = 1.5m） |
| 24 | `MM_MER_CONDUCE_RANGE` | 传导范围扩大 | 半径 cm（300 = 3.0m） |
| 25 | `MM_MER_CONDUCE_STACK` | 传导对象由增益改为层数 | 开关（1） |
| 26 | `MM_MER_STACK_REFUND` | 被传导层数的敌人死亡时返还 1 层锋芒 | 回层数（1） |
| 27 | `MM_MER_CONDUCE_FINISH` | 破锋的引爆可传导给锋域边缘的敌人 | 半径 cm（150） |
| 28 | `MM_MER_FINISH_CHAIN` | 引爆可连锁传导给第二个目标 | 链数（1） |

> **复用项**：络脉「锋脉·续」= `MM_ATK_NO_INTERRUPT_IN_DOMAIN`（#1）、「破脉·省」= `MM_FINISH_COST_MINUS1`（#2）。

> **治理约束**：新增 `MM_*` 必须**同时**改 ①本枚举表 ②`EdgeChronicles.Data` 的枚举定义 ③UI 图标映射表——三处不同步，导表断言 A35 立刻拦住。
>
> **图标口径（与美术侧已对齐）**：这 28 项**不配独立图标**（`new_graphic` 名额一个都不占）。统一用 AD-03 §3.2 的通用「机制改写」标记（方框 + 断口 + 铰点）实例化复用；数值类项用圆点 / 无标记。**二分形状即可**（数值 vs 机制），**不复用星盘 4 系形状**——星盘的正圆/六边方/三角/菱形已绑定「锋/血/速/势」语义，机制项再用会造成形状语义冲突。图集 B 不扩。

---

## 3. 养成求值器的运行时顺序

**结论先行**：

1. **战力与属性是快照，不是每帧计算**。全量重算只在「快照重建」时发生一次，重建时机 = 养成提交事务 commit 后。帧内做全量重算会把 GC 与帧耗时污染，热路径只做 O(1) 查表。
2. **快照刷新点 = 战斗 tick 的 #0 阶段**（已在 `IMPL-v2.0.md` §2 的 tick 表中落地为 9 阶段）。tick #1「输入采样」明文规定"不读任何逻辑状态"，所以养成快照必须在此之前就位。
3. **玩家在 UI 里改完星盘后何时生效**：养成面板打开时 `CombatGate` 冻结战斗 tick → 点「确认」时提交事务 + 重建快照 + 关闭面板 → **恢复战斗的第一帧即用新快照**。**绝不当帧生效**（提交发生在该帧 tick 的阶段 5/6 已经读过旧属性之后）。
4. **副本内禁止直接改养成**（面板在副本内锁定）。若将来开放，必须等当前技能相结束 + 额外 1 帧延迟，仍不保证当帧。

**缓存分层**（全部不可变，战斗期只读引用）：

| 缓存 | 内容 | 构建时机 | 热路径读法 |
|---|---|---|---|
| `ProgressionSnapshot` | 养成态引用（等级/装备/星盘/手札/技传/络脉/锋饰） | 存档加载 + 每次养成提交后 | 只读，不查 |
| `StatBlock` | 属性向量（攻/防/暴击/HP/CD/上限/移速/战力） | 随 snapshot 重建 | 结构体直读 |
| `MechanicModSet` | `MM_*` 位集（32 位掩码 + 参数表） | 随 snapshot 重建 | 位测试，O(1) |
| `DerivativeMask` | 技传派生解锁位掩码（按 `dv_*` 索引） | 随 snapshot 重建 | 位测试，O(1) |
| `SkillTagMod` | 词条按技能标签的修正（`skill_id`→系数，定长数组） | 随 snapshot 重建 | 数组直读 |

**养成数据消费点**（**仅描述养成数据在哪些阶段被消费，不重复 tick 全序**）：

> 战斗 tick 的完整顺序见 [`IMPL-v2.0.md` §2](./IMPL-v2.0.md#2-战斗-tick-的确定性顺序)（9 阶段：#0 养成快照 → #1 输入采样 → … → #8 表现层派发）。**tick 顺序的唯一真值源在 v2.0 §2**，本文件只展开 #0 阶段与养成数据的关系。

| tick 阶段（顺序见 v2.0 §2） | 消费的养成缓存 | 影响当帧 |
|---|---|---|
| **#0 养成快照刷新** | 存档态养成数据（仅当 `snapshotDirty`）→ 重建 `ProgressionSnapshot` / `StatBlock` / `MechanicModSet` / `DerivativeMask` / `SkillTagMod` | **是**（本帧起全部用新值） |
| #1 输入采样 | — | 否 |
| #2 预输入缓冲 | — | 否 |
| #3 技能状态机推进 | `DerivativeMask`（派生解锁）、`MechanicModSet`（特技触发条件）、`tech_tier` 阶位（消耗规则）、`pending_rule`（待触发标记） | 是 |
| #4 位移积分 | `StatBlock` 位移系数 | 是 |
| #5 命中判定 | `StatBlock` 攻击/暴击、`SkillTagMod`（带标签词条修正） | 是 |
| #6 受击 / 击退 | `StatBlock` 生命/减伤、`enemy.shield_layers`（护盾层数判定） | 是 |
| #7 资源结算 | `StatBlock` 锋芒上限、CD 修正、`MM_FINISH_COST_MINUS1` | 是 |
| #8 表现层派发 | `StatBlock`（仅供 UI 读快照，不参与逻辑） | 否 |

> 热路径硬约束：**帧内不得出现 `Dictionary<string,…>` 查找、不得出现 LINQ、不得出现装箱**。`MechanicModSet` / `DerivativeMask` 一律用位掩码；`SkillTagMod` 用按 `skill_id` 索引的定长数组。
>
> **多来源合并（A39）在哪一层做**：在**快照构建时（#0）一次算完**，结果直接进 `StatBlock` / `MechanicModSet`。热路径**不做任何多来源合并**——合并是 O(来源数) 的循环，放在帧内会随装备/词条数量线性增长。

### 3.1 跨层新增 DTO 字段（**已获 team-lead 批准 · 加字段不改语义，向后兼容**）

| 字段 | 所属 DTO | 用途 | 方案 | 理由 |
|---|---|---|---|---|
| `absorbed` | `HitEvent`（`ICombatEvents.OnHit` 载荷） | 区分「命中」与「被护盾吸收」（孤锋崖打错身） | **在 `HitEvent` 上加 `bool absorbed`，不新增事件类型** | 保持**单一事件流、单一派发顺序**；新增事件类型会让表现层订阅两个通道再自行合并，反而更容易出现"两路都播"或"都不播"。判定来源明确：由命中判定读 `enemy.shield_layers > 0` 决定，**数据层可判，表现层不猜** |

> **`absorbed == true` 时的精确语义**（与 v3.0 §3.3.1 伪命中规格、AD-03 §4.5 一致）：
> - **不发伤害、不改 HP**；不触发顿帧 / 镜头 / 震动 / 血字 —— **假身命中不走命中结算路径**。
> - **事件仍然派发**：刀光**照常播**，粒子改**竖直火星**（被吸收、向上飞散、不落地），音效只留**金属格挡音**。
> - **唯一正反馈**：`enemy.shield_layers += 1`（到 `shield_max` 上限为止）。
> - 表现层**不得自己猜**"这是不是假身"——只读 `absorbed` 字段分支。

### 3.2 表现口径的工程落点（美术通则修订的直接后果）

| 美术侧修订 | 工程侧必须怎么落 | 禁止的默认画法 |
|---|---|---|
| **锋域零地面图形**（改为竖直光柱阵列 + 柱顶悬浮水平细环，离地 1.6m / 2.4m） | 锋域范围 = **逻辑半径 4m 的数学判定**（`core_link`），**不生成 Collider、不生成地面对象、不存在"地面圆环"这个可视实体**；双层锋域靠光柱密度（12 / 24 根）+ 高度区分，**不靠透明度叠** | 贴地圆环 / 地面贴花 / 地面 Collider |
| **拾取范围不得用地面圈** | 拾取判定 = 逻辑距离（`IPickupEntity.Radius`），提示 = 竖直光柱 + UI 角标 | 地面圆环指示 |
| **抱元破罩反震环竖直朝向相机** | 反震 = 逻辑球形范围判定 + 表现层竖直环 | 水平地面环 |

> 通用判据：**玩家侧的水平圆环一律作废**——水平形状是敌人预警的专属通道（AD-03 §3.4）。这条同时约束**所有**新增表现，不只是锋域。

### 3.3 新增运行时接口（技传 5 阶与 Boss 护盾所需）

| 接口 / 能力 | 定义位置 | 用途 | 边界 |
|---|---|---|---|
| `IPickupEntity` | `EdgeChronicles.Kernel` | 悬挂拾取物：聚锋簇（技传聚锋 5 阶）与心印线拾取范围**共用一套机制**（不各写一套） | 属性：`Id` / `Radius` / `Consume()`；被"下一次聚锋"吸收时 `Consume()` 返回 +1 层锋芒 |
| `IInterruptibleAction.InterruptAction(bool keepPoise)` | `EdgeChronicles.Kernel` | 裂石 5 阶：打断目标当前 action 但**保留霸体** | **不与普通击退 / 硬直混用**——不得走 `HitResult` 的 knockback 通道 |
| `IPullAndFace.PullToward(Vec2, float)` / `FaceTowards(Vec2)` | `EdgeChronicles.Kernel` | 回锋 5 阶：拉近 0.8m + 转向面向玩家 | 位置修正只写**逻辑坐标**，Transform 仍由 Binding 写（v2.0 边界铁律 2） |
| Boss 护盾挂点 | `EdgeChronicles.Binding` | 孤锋崖 Boss 胸口/骨盆 1 个挂点 Transform，护盾为**独立子物体** | **不写进 rig 权重**（"2 套 rig 覆盖 3 Boss"不变）；护盾层数 = 叠瓦弧板片数 1–3 |

> 以上四个接口是 **Kernel / Binding 的能力缺口**，不属养成表；但它们由技传 5 阶与 Boss 机制触发，**G 路（技传派生系统）与 C 路（Boss + Anchor）必须同时具备**，否则技传 5 阶做不出效果。

---

## 4. 存档与版本兼容

**存档结构**（`save_01.sav` = 二进制头 + UTF-8 JSON 本体；沿用 GDD §9.3）：

```jsonc
{
  "header": { "save_version": 3, "content_version": "3.0", "crc32": 4294967295 },
  "core":     { "hero": {}, "inventory": [], "dungeon": {} },        // v1 已有
  "progression": {
    "cultivation": { "line_cuigu": { "level": 0, "raw_value": 0 } },  // 6 线
    "equipment":   { "weapon": {}, "cloth": {}, "pendant": {} },       // 3 部位
    "affix":       [ { "affix_id": "", "value": 0 } ],                 // 每件 1–3 条
    "star":        { "unlocked": [], "spent_eu": 0 },
    "specialty":   { "slot_1": null, "slot_2": null, "slot_3": null },
    "tech":        { "sk_shifeng": 0 },                               // 8 技能
    "meridian":    { "unlocked": [], "spent_eu": 0 },
    "socket":      { "equipped": { "slot_1": null, "slot_2": null, "slot_3": null }, "owned": [] },
    "refine":      { "eq_uid_0001": { "pity": 0 } },
    "fame":        { "tier": 0, "points": 0 },
    "reset_count": { "cultivation": 0, "star": 0, "meridian": 0 },
    "drop_pity":   { "mat_vein_point": { "counter": 0, "threshold": 5 } }
  }
}
```

> **掉落保底计数放哪（已裁口径）**：放 **`progression` 派生层的全局计数**，按 `material_key` 累计，**不随副本重置**。
> - 不放 `enemy.drop_table`：那是**按副本**的表，玩家换一本副本就重置，与"连续 5 次未掉落则第 6 次必出"的语义直接冲突——换本刷就能洗掉计数，保底形同虚设。
> - 放 `progression` 的另一层好处：保底是**养成材料供给节奏**（属"刷什么"这一决策维度），与 v3.0 §4 的"名望/通关数"同源，放一起口径统一。
> - 阈值存 `global_tuning.drop_pity_threshold`（默认 5），**不写死在代码里**（A40 引用它）。

**`save_version` 策略**：

| 情形 | 行为 |
|---|---|
| `file.save_version > 3` | **拒绝读取**，提示"存档来自更新版本"，不尝试降级、不删档 |
| `file.save_version == 3` | 直接读 |
| `file.save_version < 3` | 走迁移链 `SaveMigrator`，每级一步纯函数 `Migrate_vN_to_vN1(json)`，**幂等、可单测**。当前只有 `v2 → v3` 一级（v2→v3 = 把 `core` 拆出 `progression` 并填默认） |
| 新增/删除养成系统 | `save_version + 1`，**只加字段、不改已有字段语义**；改语义 = 新字段 + 迁移 |
| CRC 校验失败 | 抛 `SaveCorruptedException`，回退 `save_01.bak` |

**旧档缺字段的默认值规则**（逐字段给出，可直接写成 `SaveMigration.Defaults` 常量表）：

| 字段 | 缺失时 | 默认值 |
|---|---|---|
| `header.save_version` | 缺 | 视作 `2`，走 `v2→v3` 迁移 |
| `progression` 整段 | 缺 | `{}`，再按下面逐字段填 |
| `progression.cultivation.<line_id>` | 缺 | `{ "level": 0, "raw_value": 0 }`（6 线全部 Lv0） |
| `progression.equipment.<slot>` | 缺 | `{ "grade": 0, "affixes": [] }`（白品阶、0 词条） |
| `progression.equipment.<slot>.affixes[i].affix_id` | 缺 | `""`（**空档位留空串而非 0**，沿用 v1 约定） |
| `progression.star.unlocked` / `.spent_eu` | 缺 | `[]` / `0` |
| `progression.specialty.slot_1..3` | 缺 | `null`（**特技不白送**） |
| `progression.tech.<skill_id>` | 缺 | `0`（未修；阶位 ≥1 才算已修） |
| `progression.meridian.unlocked` / `.spent_eu` | 缺 | `[]` / `0` |
| `progression.socket.equipped.slot_1..3` | 缺 | `null`（3 槽全空） |
| `progression.socket.owned` | 缺 | `[]` |
| `progression.refine.<eq_uid>.pity` | 缺 | `0`（保底计数从头起） |
| `progression.fame.tier` / `.points` | 缺 | `0` / `0`（名望按次数计，不继承） |
| `progression.reset_count.<sys>` | 缺 | `0` |
| `progression.drop_pity.<material_key>.counter` | 缺 | `0`（连续未掉落次数从头起） |
| `progression.drop_pity.<material_key>.threshold` | 缺 | `5`（连续 5 次未掉落则第 6 次必出） |
| `affix_pool.skill_tag`（旧版本表无此列） | 缺 | `""`（不带技能标签） |
| `affix_pool.mechanic_mod`（旧版本表无此列） | 缺 | `""`（无机制修改） |
| `skill.tech_tiers`（旧版本表无此列） | 缺 | **不填**，由 `tech_tier` 重算覆盖 |
| `skill.derivative_ids`（旧版本表无此列） | 缺 | `[]` |

**两条硬规则**：
- **可缺字段必须"可由其它源重算"或"取 0 / 空 / null"**；等级、战力这类不可重算的量必须写进存档。
- **战力不入存档**（`expected_power` 是派生值）。读档后重算；若与 UI 上次显示不一致，以重算为准并打一条 warning。**不因战力对不上而拒绝读档**（战力是展示量，不是状态量）。

---

## 5. CI 自证脚本清单

**全部判据可脚本判定，无"人工确认"**。统一入口：`Tools/ci/*.sh`（Git Bash / CI 容器，工程侧已落地的 5 个脚本）；PowerShell 版 `Tools/Ci/*.ps1` 尚未提供。

| # | 检查项 | 命令 / 脚本 | 通过判据 |
|---|---|---|---|
| ① | **Unity 批处理编译** | `Tools/ci/check-compile.sh` → 见下方命令 1 | 退出码 0；日志无 `error CS`；无 `Scripts have compiler errors` |
| ② | **导表器跑全部断言 A19–A43** | `Tools/ci/check-data.sh` → 命令 2 | 日志含 `ValidationSummary: error=0 warning=0`；退出码 0；`Assets/Generated/` 未被修改（`git status` 干净） |
| ③ | **养成数据完整性** | `Tools/ci/check-progression.sh` → 命令 3 | `ProgressionAudit: tables=20 orphans=0 derivedOverwritten=100%`；A31–A43 全部 `PASS`；`mechanic_mod` 越界计数 = 0；**A43** 两层配额：`new_graphic` 行数 + `vfx_generic_budget` ≤ `vfx_graphic_budget` |
| ④ | **存档往返 + CRC 篡改检测** | EditMode 测试 `Flow.Tests.SaveRoundTripTests` + `TamperDetectTests` → 命令 4 | 往返后字段**全等**（序列化比对）；20 个篡改用例（分别翻转 header/CRC/本体第 1,7,19,37,101,… 字节）**20/20 全部抛 `SaveCorruptedException`**，0 静默通过；旧档夹具（缺 14 类字段）加载后字段全等于默认值表 |
| ⑤ | **连段模拟不产生非法状态** | EditMode 测试 `Kernel.Tests.ComboFuzz` → 命令 4 | 固定种子 1000 组输入序列 × 600 帧；资源恒 ∈ [0, cap]、相变合法、坐标有限；**非法状态计数 = 0**（任一非 0 即红） |
| ⑥ | **战斗与养成联动集成测试** | PlayMode 测试 `Kernel.Tests.ProgressionLinkTests` → 命令 5 | 见下方断言矩阵，逐条数值比对，全绿 |
| ⑦ | **多来源合并（A39-1…A39-4）** | EditMode 测试 `Data.Tests.EffectMergeTests` → 命令 4 | 判定口径见 §2 的 **A39-1 / A39-2 / A39-3 / A39-4**（此处不重复定义）：上限类取最大、消耗削减类取最小、其余数值类取最小不叠加、离散类只生效一次；夹具与预期值同上 |
| ⑧ | **掉落保底（A40）** | EditMode 测试 `Flow.Tests.DropPityTests` → 命令 4 | 连续 5 次空掉落 → 第 6 次**必出**该 `material_key`；实际掉落后计数清零；**切换副本不重置计数**（跨副本连续 5 次仍触发保底） |
| ⑨ | **技传阶位 → 战斗数据一致性** | PlayMode 测试 `Kernel.Tests.TechTierLinkTests` → 命令 5 | 迅锋 5 阶 → `segment_count == 4` 且第 4 段命中可结算；抱元 5 阶 → 破罩 `shield_layers -= 1` 且 `0 ≤ shield_layers ≤ 3`；拾锋式 5 阶 → 双锋域**共享同一锋芒层数池**（放一次核心，层数不 ×2） |

**命令 1 · 批处理编译（真实命令行）**

```bash
UNITY="/e/UnityHub/2022.3.62f3c1/Editor/Unity.exe"   # 实测路径；解析逻辑见 Tools/ci/unity.sh（UNITY_PATH 优先）
"$UNITY" -batchmode -nographics -quit \
  -projectPath "$PWD" \
  -logFile "$PWD/.ci/compile.log"
# 判据：退出码 0；grep -c "error CS" .ci/compile.log == 0
```

**命令 2 · 导表 + 全断言**

```bash
"$UNITY" -batchmode -nographics -quit \
  -projectPath "$PWD" \
  -executeMethod EdgeChronicles.Tools.Editor.Ci.ImportAndValidate \
  -logFile "$PWD/.ci/import.log"
# 判据：grep -q "ValidationSummary: error=0 warning=0" .ci/import.log
```

**命令 3 · 养成数据完整性审计**

```bash
"$UNITY" -batchmode -nographics -quit \
  -projectPath "$PWD" \
  -executeMethod EdgeChronicles.Tools.Editor.Ci.ProgressionAudit \
  -logFile "$PWD/.ci/progression.log"
# 判据：grep -q "ProgressionAudit: tables=20 orphans=0 derivedOverwritten=100%" .ci/progression.log
```

**命令 4 · EditMode 测试（存档往返 / 篡改 / 连段 fuzz）**

```bash
"$UNITY" -batchmode -nographics -quit \
  -projectPath "$PWD" \
  -runTests -testPlatform EditMode \
  -testResults "$PWD/.ci/results-editmode.xml" \
  -logFile "$PWD/.ci/editmode.log"
# 判据：results-editmode.xml 中 failed="0" passed>0
```

**命令 5 · PlayMode 联动测试**

```bash
"$UNITY" -batchmode -nographics -quit \
  -projectPath "$PWD" \
  -runTests -testPlatform PlayMode \
  -testResults "$PWD/.ci/results-playmode.xml" \
  -logFile "$PWD/.ci/playmode.log"
```

**⑥ 的断言矩阵（每行都是"输入 → 期望数值"）**

| 夹具 | 期望 |
|---|---|
| 技传：拾锋式 3 阶 + 锋域内 | 结束前最后一跳伤害 = 基础单跳 × 2.00（`MM_DOMAIN_LAST_JUMP_DOUBLE`） |
| 技传：破锋 5 阶 | 引爆后锋域额外存在 1 次、持续 360 帧（6s） |
| 络脉：`MM_FINISH_COST_MINUS1` | 破锋消耗 = 3 − 1 = **2** 层（不足 2 层时不消耗） |
| 手札：`MM_RESOURCE_CAP_PLUS1` | 锋芒上限 = 5 + 1 = **6**，且可实际累积到 6 层 |
| 手札：`MM_PERFECT_DODGE_REFUND1` | 完美闪避后层数 +1（基础 +1 之外**额外** +1） |
| 词条：`skill_tag = 拾锋式`，值 +12% | 拾锋式伤害 ×1.12，**迅锋伤害不变** |
| 星盘：同系两节点均点亮 | 连携 `effect_value` 生效（伤害 × (1+v)）；只点亮一个 → 不生效 |
| 技传：拾锋式 5 阶双锋域 | 两层锋域同时存在、各自跳伤害；**共享同一锋芒层数池**（放一次核心不产生 2 份层数） |
| 技传：迅锋 5 阶第 4 段 | `segment_count == 4`；第 4 段可命中并结算；**未解锁时仍为 3 段** |
| 技传：抱元 5 阶破罩 | 霸体结束时 `shield_layers -= 1`；`shield_layers` 恒 ∈ [0,3]；护盾归零后不再触发 |
| 伪命中（打错身） | `HitEvent.absorbed == true`；**HP 不变**、护盾 +1 层；表现层据此走火星 + 格挡音分支 |

---

## 6. 工具链前提（工程侧增量）

> 通用工位清单见 `docs/impl/TOOLCHAIN.md`（A/B/C/D/E/F/G 组已覆盖系统、IDE、许可、仓库约定）。**本节只列工程侧必须项与既有清单的差异**，不重复。

| 项 | 要求 | 说明 |
|---|---|---|
| Unity 版本 | **2022.3.x LTS**，基线 `2022.3.62f3c1`（Unity 中国版），**全队锁同一 patch** | `ProjectSettings/ProjectVersion.txt` 是真值（已落为 `2022.3.62f3c1`）；CI 先比对，不符直接拒跑。**引擎版本适配的完整影响清单见 [`docs/architecture/ADR-001-engine-version.md`](../architecture/ADR-001-engine-version.md)** |
| Unity 模块 | Windows Build Support (**Mono + IL2CPP**)、Documentation（可选） | Mono 用于 Editor 内跑测试，IL2CPP 用于出包 |
| MSVC | Visual Studio Build Tools 2022，`MSVC v143` + Windows SDK 10.0.19041+ | IL2CPP 链接必需；缺则出包失败 |
| IDE | **Rider 2024.3 LTS**（推荐）或 VS 2022 17.9+ + Unity 扩展 | Rider 的 asmdef 引用与 Unity 控制台 attach 更稳 |
| .NET SDK | **8.0.4xx** | 写导表器/CI 工具用；无需 9.x |
| Git | **≥ 2.40**（Windows 版，带 Git Bash） | — |
| Git LFS | **≥ 3.5.0** | `git lfs install` 一次性 |
| PowerShell | **7.4+** | CI 脚本宿主（5.1 不支持三元与 `??`） |
| Python（可选） | 3.11+ | 仅用于 CSV→JSON 预处理；纯 C# 管线则不需要 |

**必需的 Package（package id + 版本）** —— 以 `Packages/packages-lock.json` 为最终真值，**任何人不得在编辑器里点 "Upgrade"**：

| package id | 锁定版本 | 必需 | 用途 |
|---|---|---|---|
| `com.unity.render-pipelines.universal` | **14.0.11** | 是 | URP 渲染 + 描边 shader（**14 无 RenderGraph**，描边走 Forward+ 下的 Renderer Feature，见 ADR-001） |
| `com.unity.inputsystem` | **1.7.0** | 是 | 输入（环形缓冲 + 预输入） |
| `com.unity.test-framework` | **1.1.33** | 是 | EditMode / PlayMode 测试 |
| `com.unity.ugui` | **1.0.0** | 是 | HUD / 5 个养成面板 |
| `com.unity.timeline` | 1.7.7 | 否 | 演出镜头（如启用） |
| `com.unity.profiling.core` | 1.0.2 | 是 | 帧耗时与 GC 断言 |
| `com.unity.animation.rigging` | **1.2.1** | 是 | 弓手上半身 Override Layer + AvatarMask + TwoBoneIK |
| `com.unity.ai.navigation` | **1.1.5** | 是 | 敌人导航 |
| `com.unity.addressables` | **不引入** | 否 | **已裁定不用 Addressables**（UI 图集随包加载，不做按需流式） |
| `com.unity.cinemachine` | **不引入** | 否 | 镜头：**自写定点相机**（包未装，禁止临时依赖） |
| `com.unity.2d.sprite` | **不引入** | 否 | 3D 项目里用 **Sprite Packing Tag + SpriteAtlas asset** 打包 UI 图集即可，不需要 2D 包 |

> **C# 语言版本**：2022.3 为 **C# 9**。可用 `record` / `init` / 顶级语句；**不可用** `required` 成员（C# 11）、`file` 类型（C# 11）、原始字符串字面量（C# 11）。**不可用 `System.Text.Json`**（.NET Standard 2.0 / 2.1 均不带该包）——存档与配表都不依赖它。
>
> **存档序列化用 Unity 内置 `JsonUtility`**，**不引入 `com.unity.nuget.newtonsoft-json`**——少一个版本漂移源，存档 JSON 结构扁平（无字典、无多态），内建足够。
> **配表管线不使用 `JsonUtility`**：它不支持顶层数组与字典，承载不了「一张表 = 一个行数组」与「键 → 值 集」；导表器自带极简 JSON 读写（`EdgeChronicles.Data.Serialization.Json`，纯 C#、无第三方包），存档侧仍按上条用 `JsonUtility`。
>
> **UI 图集的工程口径（与美术侧已对齐）**：
> - **打包**：用 Sprite Import Settings 的 **Packing Tag** 归组，打成 **SpriteAtlas asset**（图集 A 1024 / B 1024 / C 512），**随包加载**，不走 Addressables、不走 `Resources`（`Resources` 会让所有图集常驻且破坏 SpriteAtlas 打包）。
> - **采样**：三张图集**统一 mip off + bilinear**。理由：UI 在屏幕空间按 CanvasScaler 等比缩放，mip 的预滤波会让 64px 图标发糊；C 图集 64→32 是 0.5 倍缩放，mip0 仍然够用。**跨图集不混采**——同一张图标只在一张图集内。
> - **不引入 2D Sprite 包**：Packing Tag 在 3D URP 项目里同样可用。

**版本控制与大小文件**：

| 项 | 规则 |
|---|---|
| 方案 | **Git + Git LFS**（单一仓库；美术资产与代码同仓） |
| `.gitattributes` | 二进制资产走 LFS：`*.fbx *.png *.tga *.psd *.wav *.mp3 *.ttf *.bank *.blend`；**`.unity` / `.prefab` / `.asset` 保持文本、不进 LFS**（进 LFS 则 UnityYAMLMerge 无法合并冲突） |
| 合并冲突 | `.unity`/`.prefab` 走 **UnityYAMLMerge**（随 Unity 安装），配 `git config merge.tool.union` |
| 序列化设置 | EditorSettings → `Asset Serialization Mode = Force Text`、`Visible Meta Files = true`、`Version Control Mode = Visible Meta Files`（**否则 Git diff 全炸**） |
| `.gitignore` | `Library/ Temp/ Obj/ Build/ Logs/ UserSettings/ *.csproj *.sln .vs/` |
| 分支 | 一员工一分支，PR 合入 `main`，禁止直推 |

**首次拉取后的初始化步骤（一次性）**：

| # | 步骤 | 命令 / 操作 | 通过判据 |
|---|---|---|---|
| 1 | 拉大文件 | `git lfs install` → `git clone <repo>` → `git lfs pull` | `git lfs ls-files` 无指针残留；FBX 大小正常 |
| 2 | 版本对齐 | `cat ProjectSettings/ProjectVersion.txt` | `m_EditorVersion` == `2022.3.62f3c1` |
| 3 | 首次导入 | Unity Hub → Open（**手工打开一次**完成导入与授权） | `Library/` 重建完成；Console 无 error |
| 4 | 包还原 | Package Manager 等待还原 | `git status` 干净（`packages-lock.json` 未变） |
| 5 | 序列化设置 | EditorSettings 三项按上表设置 | `git status` 干净 |
| 6 | 跑基线 | `Tools/ci/check-compile.sh` → `check-data.sh` | 编译 0 error；A19–A43 全过 |
| 7 | 仓库自检 | `git status` | **完全干净**——不干净说明 `.gitignore`/`.gitattributes` 配错 |

## 7. `ProgressionViewModel` 字段契约（H 路 · 养成 UI 五面板的读取契约）

> **归属**：`ProgressionViewModel` DTO 定义在 **`EdgeChronicles.Data`**（`EdgeChronicles.UI` 允许依赖 `Data`、禁止依赖 `Kernel`）。由 Kernel 在**快照重建时（tick #0）同步生成**，UI 只读。
> **三条边界**：① UI **不得直接读 `ProgressionSnapshot`**，一律走 ViewModel；② UI **每帧最多读一次**（沿用 v2.0 §2「UI 只读快照，每帧最多一次」）；③ ViewModel 是**扁平结构**（无字典、无嵌套类），字段名带面板前缀，便于 IL2CPP 与序列化。
> **列名**：本表用 camelCase（UI 侧 C# 属性名），与数据表 snake_case 的映射由 Kernel 一次性完成。

### 7.1 通用

| 字段名 | 类型 | 来源表 | 是否派生 | UI 用途 |
|---|---|---|---|---|
| `power_total` | int | `progression` | 是 | 全部面板 · 右上角战力 |
| `power_delta` | int | `progression` | 是 | 修为面板 · 预览「投入后战力 A → B」的差值 |
| `material_count` | int[]（按 `material_key` 索引） | 存档 | 否 | 全部面板 · 材料持有量 |
| `fame_tier` | int | 存档 | 否 | 全部面板 · 名望档位（**1–5，与星盘 `unlock_tier` 一一对应**） |
| `fame_points` | int | 存档 | 否 | 全部面板 · 名望点数 |
| `fame_next_requirement` | string | 本地化表 | 是 | 全部面板 · 距下一档还差什么 |

### 7.2 修为面板

| 字段名 | 类型 | 来源表 | 是否派生 | UI 用途 |
|---|---|---|---|---|
| `cult_line_count` | int | `progression` | 是 | 左侧 6 线列表长度 |
| `cult_line_id` | string[] | `progression` | 否 | 行的 id |
| `cult_line_name` | string[] | 本地化表 | 否 | 线名（淬骨/气海/身法/锋锐/心印/名望） |
| `cult_line_level` | int[] | 存档 | 否 | 当前等级（0–10） |
| `cult_line_max_level` | int[] | `progression` | 否 | 上限 |
| `cult_line_next_cost` | int[] | `progression` | 是 | 下一级消耗 |
| `cult_line_next_effect` | string[] | 本地化表 | 是 | 下一级效果文案 |
| `cult_line_progress` | float[] | 存档 | 是 | 刻度进度条 0..1 |

### 7.3 装备面板（含词条 / 锋饰 / 洗练 · 解锋）

| 字段名 | 类型 | 来源表 | 是否派生 | UI 用途 |
|---|---|---|---|---|
| `equip_slot_count` | int | — | 是（常量 3） | 部位数 |
| `equip_grade` | int[3] | 存档 | 否 | 部位品阶（0–5） |
| `equip_affix_count` | int[3] | 存档 | 是 | 该部位词条数 |
| `equip_socket_open` | bool[3] | 存档 | 否 | 该部位是否开放锋饰孔 |
| `equip_power` | int[3] | `progression` | 是 | 该部位战力贡献 |
| `affix_id` | string[] | `affix_pool` | 否 | 词条行 |
| `affix_name` | string[] | 本地化表 | 否 | 词条名 |
| `affix_icon` | Sprite[] | 图集 B | 否 | 词条图标 |
| `affix_value` | float[] | `affix_pool` | 否 | 词条值 |
| `affix_skill_tag` | string[] | `affix_pool` | 否 | 「仅对 拾锋式 生效」角标（空串 = 无） |
| `affix_is_mechanic` | bool[] | `affix_pool` | 是 | **决定用圆点还是通用「机制改写」标记**（AD-03 §3.2） |
| `affix_slot` | int[] | 存档 | 否 | 词条所在槽（洗练用） |
| `socket_item_id` | string[3] | `socket_item` | 否 | 3 锋饰槽当前装备 |
| `socket_item_name` | string[3] | 本地化表 | 否 | 锋饰名 |
| `socket_item_level` | int[3] | `socket_item` | 否 | 锋饰等级 |
| `socket_item_icon` | Sprite[3] | 图集 B | 否 | 锋饰图标 |
| `socket_equipable` | bool[] | `socket_item` | 是 | 背包某件能否装入当前槽（类型匹配 + 孔位开放） |
| `socket_next_level_id` | string[3] | `socket_item` | 是 | 可合成到下一级的目标 id |
| `refine_stage_id` | string[] | `refine_stage` | 否 | 洗练抽屉的档位列表 |
| `refine_keep_count` | int[] | `refine_stage` | 否 | 保留几条不被重掷 |
| `refine_cost` | int[] | `refine_stage` | 是 | 洗练消耗 |
| `refine_pity` | int[] | 存档 | 否 | 保底进度 n |
| `refine_pity_threshold` | int[] | `refine_stage` | 否 | 保底阈值 |
| `refine_refund_ratio` | float[] | `refine_stage` | 否 | 解锋返还比例 |

### 7.4 星盘面板

| 字段名 | 类型 | 来源表 | 是否派生 | UI 用途 |
|---|---|---|---|---|
| `star_node_count` | int | `star_node` | 是 | 画布节点数（12） |
| `star_node_id` | string[] | `star_node` | 否 | 节点 id |
| `star_node_series` | string[] | `star_node` | 否 | 4 系（`blade`/`blood`/`speed`/`momentum`）→ 形状与筛选 |
| `star_node_pos_x` | int[] | `star_node` | 否 | 节点坐标（**锚点布局用，不写死像素**） |
| `star_node_pos_y` | int[] | `star_node` | 否 | 节点坐标 |
| `star_node_unlocked` | bool[] | 存档 | 否 | 已点亮 |
| `star_node_available` | bool[] | 存档 | 是 | 可点亮（前置齐 + 档位够 + 材料够） |
| `star_node_cost` | int[] | `star_node` | 否 | 消耗 |
| `star_node_effect_text` | string[] | 本地化表 | 是 | 效果预览 |
| `star_node_mechanic_mod` | string[] | `star_node` | 否 | **非空 = 机制类节点**（12 节点里 6 个），UI 打通用「机制改写」标记（方框 + 断口 + 铰点）；空串 = 离散 / 次数 / 帧类节点 |
| `star_link_state` | string[] | `star_link` | 是 | **连携三态**：潜在 / 未满足 / 生效（对应 3 张线型贴图） |

### 7.5 手札 + 技传面板

| 字段名 | 类型 | 来源表 | 是否派生 | UI 用途 |
|---|---|---|---|---|
| `spec_slot_count` | int | — | 是（常量 3） | 特技槽数 |
| `spec_id` | string[3] | `specialty` | 否 | 槽位特技 id |
| `spec_name` | string[3] | 本地化表 | 否 | 特技名 |
| `spec_icon` | Sprite[3] | 图集 B | 否 | 特技图标 |
| `spec_effect_text` | string[3] | 本地化表 | 是 | 规则文案 |
| `spec_conflict` | bool[3] | `specialty` | 是 | 与另一槽互斥 → 标红（`exclude_group` 同组） |
| `tech_skill_count` | int | — | 是（常量 8） | 技能数 |
| `tech_tier_count` | int | — | 是（常量 5） | 阶位阶梯格数 |
| `tech_current_tier` | int[8] | 存档 | 否 | 已达阶位 |
| `tech_next_cost` | int[8] | `tech_tier` | 是 | 下一阶消耗（谱份数） |
| `tech_unlock_type` | string[8] | `tech_tier` | 否 | 阶位类型（数值/资源/派生/窗口/机制） |
| `tech_effect_text` | string[8] | 本地化表 | 是 | 效果文案 |
| `tech_derivative_name` | string[8] | 派生表 | 否 | 派生名（如「破势」「末跳翻倍」） |
| `tech_visual_tier` | string[8] | `tech_tier` | 否 | **不出图 / 参数化 / 通用符 / 专属 VFX** → 决定该阶阶位徽记用哪张图 |

### 7.6 络脉面板

| 字段名 | 类型 | 来源表 | 是否派生 | UI 用途 |
|---|---|---|---|---|
| `meridian_node_count` | int | `meridian` | 是（常量 8） | 节点数 |
| `meridian_node_id` | string[] | `meridian` | 否 | 节点 id |
| `meridian_node_is_mechanic` | bool[] | `meridian` | 是 | **恒为 `true`**（络脉 8 节点全是机制类，加战力 = 0）→ UI 一律用方框形状 |
| `meridian_mechanic_mod` | string[] | `meridian` | 否 | 机制修改项 id（`MM_*`），UI 据此取通用机制标记与文案 |
| `meridian_effect_text` | string[] | 本地化表 | 是 | 底部机制说明条文案 |
| `meridian_unlocked` | bool[] | 存档 | 否 | 已解锁 |
| `meridian_available` | bool[] | 存档 | 是 | 可点亮 |

### 7.7 重置（挂在修为 / 星盘 / 络脉面板内）

| 字段名 | 类型 | 来源表 | 是否派生 | UI 用途 |
|---|---|---|---|---|
| `reset_target_count` | int | `progression` | 是 | 「重置」按钮列表长度 |
| `reset_target_system` | string[] | `progression` | 否 | `cultivation` / `star` / `meridian` / `equipment` |
| `reset_cost` | int[] | `progression` | 否 | 消耗（**当前全为 0 = 免费**；将来收费改数字，不加枚举值） |
| `reset_policy` | string[] | `progression` | 否 | 策略（**当前仅 `free`**） |

---

*本文是 `IMPL-v3.0.md` 第 4/7 章的展开件。养成层的字段规格、判定逻辑、求值顺序、存档兼容、自证脚本、ViewModel 契约、工具链前提各只在此处定义一次，其余文档引用本文。*
