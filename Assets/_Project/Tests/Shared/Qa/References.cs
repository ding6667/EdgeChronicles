using System;
using System.Collections.Generic;

namespace EdgeChronicles.Tests.Qa
{
    // ─────────────────────────────────────────────────────────────────────
    // 参考实现（证伪装置）。
    //
    // 【定位】生产侧会实现同一套几何 / 积分 / 校验逻辑。测试侧再实现一份，
    // 目的不是"写两遍"，而是让判据**可以被证伪**（GDD §12.1 ④）：
    // 若两份实现对同一场景给出不同结论 → 报告红，而不是让"唯一那一份"说了算。
    //
    // 【纪律】参考实现里出现的每一个阈值都来自 CombatSpec（测试侧规格卡），
    // 不允许在方法体内写裸数字——否则会变成第三份真值源。
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>2D 向量（测试侧最小实现；生产侧用 EdgeChronicles.Core 的定点向量）。</summary>
    public readonly struct QaVec2
    {
        public readonly float X;
        public readonly float Y;

        public QaVec2(float x, float y) { X = x; Y = y; }

        public static QaVec2 operator +(QaVec2 a, QaVec2 b) { return new QaVec2(a.X + b.X, a.Y + b.Y); }
        public static QaVec2 operator -(QaVec2 a, QaVec2 b) { return new QaVec2(a.X - b.X, a.Y - b.Y); }
        public static QaVec2 operator *(QaVec2 a, float s) { return new QaVec2(a.X * s, a.Y * s); }

        public float SqrMagnitude { get { return X * X + Y * Y; } }
        public float Magnitude { get { return (float)Math.Sqrt(X * X + Y * Y); } }

        public QaVec2 Normalized
        {
            get
            {
                float m = Magnitude;
                if (m <= 0f) { return new QaVec2(0f, 0f); }
                return new QaVec2(X / m, Y / m);
            }
        }

        /// <summary>从 +X 轴起算的极角（度，0..360）。</summary>
        public float AngleDeg
        {
            get
            {
                float d = (float)Math.Atan2(Y, X) * 180f / (float)Math.PI;
                if (d < 0f) { d += 360f; }
                return d;
            }
        }
    }

    /// <summary>命中几何参考实现（IMPL v2.0 §7.1 歧义点 3：纯数学判定，不用 Collider）。</summary>
    public static class HitGeometryReference
    {
        /// <summary>
        /// 扇形。约定：<paramref name="angleDeg"/> 为**整角**（度），中心线为 <paramref name="forward"/>。
        /// 【已知缺陷】IMPL v2.0 §3.1 的 <c>angle</c> 列未声明是整角还是半角 —— 见 docs/qa 的判据缺陷清单 D-01。
        /// </summary>
        public static bool SectorContains(QaVec2 origin, QaVec2 forward, float radius, float angleDeg, QaVec2 point)
        {
            QaVec2 d = point - origin;
            float distSq = d.SqrMagnitude;
            if (distSq > radius * radius) { return false; }
            if (distSq <= 1e-12f) { return true; } // 与原点重合
            float half = angleDeg * 0.5f;
            QaVec2 f = forward.Normalized;
            if (f.SqrMagnitude <= 1e-12f) { return false; }
            float cosA = (d.X * f.X + d.Y * f.Y) / d.Magnitude;
            if (cosA > 1f) { cosA = 1f; }
            if (cosA < -1f) { cosA = -1f; }
            float ang = (float)Math.Acos(cosA) * 180f / (float)Math.PI;
            return ang <= half + 1e-4f;
        }

        /// <summary>胶囊：线段 AB 半径 r。</summary>
        public static bool CapsuleContains(QaVec2 a, QaVec2 b, float radius, QaVec2 point)
        {
            QaVec2 ab = b - a;
            float lenSq = ab.SqrMagnitude;
            float t = 0f;
            if (lenSq > 1e-12f)
            {
                t = ((point.X - a.X) * ab.X + (point.Y - a.Y) * ab.Y) / lenSq;
                if (t < 0f) { t = 0f; }
                if (t > 1f) { t = 1f; }
            }
            QaVec2 closest = new QaVec2(a.X + ab.X * t, a.Y + ab.Y * t);
            QaVec2 d = point - closest;
            return d.SqrMagnitude <= radius * radius + 1e-6f;
        }

        /// <summary>矩形：中心 C、半长 hx / 半宽 hy、绕 C 旋转 yawDeg。</summary>
        public static bool RectContains(QaVec2 center, float halfX, float halfY, float yawDeg, QaVec2 point)
        {
            float rad = yawDeg * (float)Math.PI / 180f;
            float cosR = (float)Math.Cos(rad);
            float sinR = (float)Math.Sin(rad);
            float dx = point.X - center.X;
            float dy = point.Y - center.Y;
            // 逆旋转到局部坐标
            float lx = dx * cosR + dy * sinR;
            float ly = -dx * sinR + dy * cosR;
            return Math.Abs(lx) <= halfX + 1e-4f && Math.Abs(ly) <= halfY + 1e-4f;
        }
    }

    /// <summary>位移曲线积分参考实现（A24）。</summary>
    public static class CurveIntegratorReference
    {
        /// <summary>
        /// 积分距离（m）= Σ keys × 声明距离。
        /// 【口径声明 · D-02】keys 按**逐帧归一化位移占比**解释（无量纲，Σ = 1）。
        /// 生产 schema 目前写的是"归一化速度"——若按速度解释，Σkeys 的量纲是帧⁻¹，
        /// 上式不成立，A24 必须重写。
        /// </summary>
        public static float IntegrateDistance(QaMovementCurveRow curve)
        {
            float sum = 0f;
            for (int i = 0; i < curve.Keys.Count; i++) { sum += curve.Keys[i]; }
            if (CurveSemantics.Current == CurveKeySemantics.SpeedMetersPerSecond)
            {
                // 逐帧速度（m/s）× dt（s）= 距离（m）
                return sum * CurveSemantics.DtSeconds;
            }
            // 归一化位移占比（Σ = 1）× 声明距离（m）= 距离（m）
            return sum * curve.DeclaredDistance;
        }

        /// <summary>相对偏差（0..1）。声明距离为 0 时返回 +∞（不可判）。</summary>
        public static float RelativeDeviation(QaMovementCurveRow curve)
        {
            float declared = curve.DeclaredDistance;
            if (Math.Abs(declared) < 1e-6f) { return float.PositiveInfinity; }
            return Math.Abs(IntegrateDistance(curve) - declared) / declared;
        }
    }

    /// <summary>CRC-32（IEEE 802.3）参考实现。存档头校验（GDD §9.3、EL-03 §4）。</summary>
    public static class Crc32Reference
    {
        private static readonly uint[] Table = BuildTable();

        private static uint[] BuildTable()
        {
            var t = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint c = i;
                for (int k = 0; k < 8; k++)
                {
                    c = (c & 1u) != 0u ? (0xEDB88320u ^ (c >> 1)) : (c >> 1);
                }
                t[i] = c;
            }
            return t;
        }

        public static uint Compute(byte[] data, int offset, int count)
        {
            uint crc = 0xFFFFFFFFu;
            for (int i = offset; i < offset + count; i++)
            {
                byte b = data[i];
                crc = (crc >> 8) ^ Table[(crc ^ b) & 0xFFu];
            }
            return crc ^ 0xFFFFFFFFu;
        }

        public static uint Compute(byte[] data)
        {
            return Compute(data, 0, data.Length);
        }
    }

    /// <summary>Boss 规则求值参考实现（Trigger → Condition[] → Effect[]，全枚举）。</summary>
    public static class BossRuleReference
    {
        public static readonly string[] Triggers =
        {
            "OnFightStart", "OnTimer", "OnHpBelow", "OnPhaseEnter", "OnCastComplete",
            "OnAddSpawned", "OnPlayerSkillHit", "OnPlayerEnterZone", "OnAnchorStateChanged"
        };

        public static readonly string[] Conditions =
        {
            "HpBelow", "HpAbove", "PhaseEquals", "PlayerInZone", "AnchorInState",
            "CooldownReady", "StackCountGte", "RandomPct", "AggroCountGte"
        };

        public static readonly string[] Effects =
        {
            "SpawnHazard", "MovePlayer", "ModifyTerrain", "ForcePhase", "ApplyBuff",
            "SpawnAdd", "PlayVfx", "ShakeCamera", "LockZone"
        };

        public static bool IsLegalTrigger(string t)
        {
            for (int i = 0; i < Triggers.Length; i++) { if (Triggers[i] == t) { return true; } }
            return false;
        }

        public static bool IsLegalCondition(string c)
        {
            for (int i = 0; i < Conditions.Length; i++) { if (Conditions[i] == c) { return true; } }
            return false;
        }

        public static bool IsLegalEffect(string e)
        {
            for (int i = 0; i < Effects.Length; i++) { if (Effects[i] == e) { return true; } }
            return false;
        }

        /// <summary>
        /// 冷启动求值：按 rule 的声明顺序，产生 Effect 触发序列。
        /// 只做"顺序"与"存在性"，不做战斗数值——与 IMPL v2.0 §6「记录 Effect 触发序列」一致。
        /// </summary>
        public static List<string> EvaluateSequence(QaBossRow boss, List<QaBossRuleRow> rules, QaScenario scenario)
        {
            var seq = new List<string>();
            for (int i = 0; i < rules.Count; i++)
            {
                QaBossRuleRow rule = rules[i];
                if (rule.BossId != boss.BossId) { continue; }
                if (rule.Phase != 1) { continue; } // 冷启动只跑第 1 阶段规则
                seq.Add(rule.RuleId + ":" + rule.Trigger);
                for (int e = 0; e < rule.Effects.Count; e++) { seq.Add(rule.Effects[e]); }
            }
            return seq;
        }

        /// <summary>序列的稳定 hash（FNV-1a 64 位 → 16 进制）。用于"三次冷启动 hash 相同"。</summary>
        public static string SequenceHash(List<string> seq)
        {
            ulong h = 1469598103934665603UL;
            for (int i = 0; i < seq.Count; i++)
            {
                string s = seq[i];
                for (int c = 0; c < s.Length; c++)
                {
                    h ^= (byte)(s[c] & 0xFF);
                    h *= 1099511628211UL;
                }
                h ^= 0x2Cu; // ','
                h *= 1099511628211UL;
            }
            return h.ToString("X16");
        }
    }

    /// <summary>
    /// 已登记的**规格冲突 / 判据缺陷**清单。
    ///
    /// 【为什么要登记而不是修掉】
    /// 判据与被测规格冲突时，正确动作是**改判据或改规格**，不是放宽阈值；
    /// 但改哪一边属于规格裁定权（主理人 / 策划），不在 QA。
    /// QA 的职责是：把冲突**显式登记 + 让它可被查询**，不让它以"静默通过"的形式留在生产代码里。
    /// </summary>
    public sealed class SpecConflict
    {
        public string Id = "";
        public string Summary = "";
        public string SourceA = "";
        public string SourceB = "";
        public string Impact = "";
        /// <summary>处置方式：Registered（已登记，待裁定）/ Fixed（已修）/ Waived（豁免，有依据）。</summary>
        public string Resolution = "Registered";
    }

    public static class KnownSpecConflicts
    {
        public static readonly List<SpecConflict> All = new List<SpecConflict>
        {
            new SpecConflict
            {
                Id = "D-01",
                Summary = "hit_shape.angle 未声明是整角还是半角。同一个 120 既可能是 120° 的扇形，也可能是 ±120°（240°）的扇形。",
                SourceA = "IMPL-v2.0.md §3.1 hitShapes[].angle（无口径说明）",
                SourceB = "IMPL-v2.0.md §3.4 真实技能样例：sector angle=120",
                Impact = "命中判定与 Boss 预警范围（哑刀横斩 180° 扇形 3m）会因解释不同而相差一倍；命中几何单测无法给唯一期望值。",
                Resolution = "Registered"
            },
            new SpecConflict
            {
                Id = "D-02",
                Summary = "movement_curve.keys 未声明量纲与含义（逐帧位移 m？归一化系数？）。",
                SourceA = "IMPL-v2.0.md §5 表 5 movement_curve：keys[], distance, clampMode",
                SourceB = "IMPL-v2.0.md §5 断言 A24：曲线积分距离与声明偏差 ≤5%",
                Impact = "A24 的积分算法无唯一解；不同实现会得到不同偏差值，判据不可复现。工程侧已按\"归一化速度\"写 schema，该形态的具体后果见 D-13。",
                Resolution = "Registered"
            },
            new SpecConflict
            {
                Id = "D-03",
                Summary = "A33 的「tier ↔ unlock_type 一一对应」与 §4.5.4 的 40 阶明细矛盾：迅锋 / 裂石 / 回锋 / 缚锋 共 16 行的阶位类型与模板不一致；缚锋「破势」在 tier 2，同时违反 A33-2（派生 tier ∈ {3,5}）。",
                SourceA = "IMPL-v3.0-EL-03-progression.md §2 A33 + A33-2",
                SourceB = "IMPL-v3.0.md §4.5.4 全 40 阶明细 + §4.5.3 通用派生模板",
                Impact = "照 A33 实现导表器，会拦掉 §4.5.4 定义的 16 条合法阶位数据；照 §4.5.4 实现，A33 永远报红。二者必有一错。",
                Resolution = "Registered"
            },
            new SpecConflict
            {
                Id = "D-04",
                Summary = "受击 hold 时长公式把 anim_clip_length 当成与 hitstun_frames 同量纲相减（帧 − 帧），但两者分属剪辑轴 C 与墙钟轴 W；且 hold 的定义是「硬直扣掉剪辑与顿帧后的剩余」还是「hold 段本身长度」在文中两义。",
                SourceA = "GDD-v1.0.md §9.4 skill 表：hitstun_hold_duration = hitstun_frames − anim_clip_length − hitstop_frames × (1 − hitstop_time_scale)",
                SourceB = "GDD-v1.0.md §7.3 时钟轴约定：t(C_i) = (i − 1) 帧，跨轴相减必错",
                Impact = "公式在 W 轴下算出 3.3 / 3.6 / 3.9，而断言 hold ∈ [2,4] 按此取值成立；一旦剪辑长度改成 12 帧（13→12）即算出 4.3 → 越界。若 hold 语义实为「剩余时长」而非「段长」，则整段判据的物理含义要重写。",
                Resolution = "Registered"
            },
            new SpecConflict
            {
                Id = "D-05",
                Summary = "顿帧上限的因果式与其展开式在「收身起始进度」变化时不同步：断言只校验 (9 − 6) − 0.15h ≥ 2，把 9 写死为常数。",
                SourceA = "GDD-v1.0.md §9.4 断言 12 展开式：(收身起始进度 − 后段窗口帧数) − hitstop_time_scale × hitstop_frames ≥ 2",
                SourceB = "GDD-v1.0.md 治理规则 12：上限要写成因果式；且 3 应写成 (收身起始进度 − 后段窗口帧数)",
                Impact = "收身起始从 C10 挪到 C12 时，硬编码 9 的实现会静默算错——这正是治理规则 12 举的反例本身。",
                Resolution = "Registered"
            },
            new SpecConflict
            {
                Id = "D-06",
                Summary = "「22 条走查表」与「24 A 类 + 6 B 类负向用例」的**权威编号清单**在 GDD-v1.0 / IMPL-v2.0 / IMPL-v3.0 / EL-03 中均无正文定义，只在 §10.1 与 §13 被引用。",
                SourceA = "GDD-v1.0.md §10.1 W4「22 条走查表（9 条纯脚本 / 7 条混合 / 6 条必须人眼，每条带一个证伪案例）」",
                SourceB = "GDD-v1.0.md §13「30 条用例（22 正向 + 24 A 类 + 6 B 类）」——同句内 22+24+6=52 ≠ 30",
                Impact = "编号无法与正本对齐；本目录使用 QA 内部编号 WC-xx / NC-Axx / NC-Bxx 并逐条标注文档出处，等待正本确认后做一次映射。",
                Resolution = "Registered"
            },
            new SpecConflict
            {
                Id = "D-07",
                Summary = "工程基线版本与工程实际不一致：文档锁定 Unity 6000.0.23f1 + URP 17.0.x + Input System 1.11.x，实际工程是 Unity 2022.3.62f3c1 + URP 14.0.11 + Input System 1.7.0。",
                SourceA = "IMPL-v3.0-EL-03-progression.md §6 工具链前提",
                SourceB = "ProjectSettings/ProjectVersion.txt、Packages/manifest.json",
                Impact = "CI 脚本按 6000.0.23f1 找解释器会找不到；URP 14 与 17 的描边 / GBuffer 行为不同，§9.6 视觉通道断言的期望值可能不成立。",
                Resolution = "Registered"
            },
            new SpecConflict
            {
                Id = "D-08",
                Summary = "CI 脚本 check-progression.sh 调用的 -executeMethod 命名空间与实际程序集不一致：脚本写 ShiFeng.Tools.Editor.Ci.ProgressionAudit，工程里是 EdgeChronicles.Tools.Editor。",
                SourceA = "Tools/ci/check-progression.sh",
                SourceB = "Assets/_Project/Editor/EdgeChronicles.Tools.Editor.asmdef（rootNamespace = EdgeChronicles.Tools.Editor）",
                Impact = "养成数据完整性检查这一条 CI 入口目前必然失败（找不到方法）。",
                Resolution = "Registered"
            },
            new SpecConflict
            {
                Id = "D-09",
                Summary = "A29「连段模拟不产生负资源」没有定义**模拟序列**：用哪条连段、单次给多少层、8 秒衰减怎么算，文档里都没有；锋芒层数规则散落在 GDD §9.1 / IMPL §9.1。",
                SourceA = "IMPL-v2.0.md §5 断言 A29：连段模拟不产生负资源",
                SourceB = "GDD-v1.0.md §9.1 / IMPL-v2.0.md §9.1：锋芒上限 5 层、命中按表给层单次 ≤1、8 秒未获新层则每 2 秒掉 1 层",
                Impact = "没有规定连段序列与时间步长，A29 的执行结果不可复现——不同的模拟序列会得出相反的结论（同一串招在 8 秒内打完不衰减，拖过 8 秒就衰减）。判据必须先固定一条**标准连段序列 + 时间步长**，否则它是不可判定的。",
                Resolution = "Registered"
            },
            new SpecConflict
            {
                Id = "D-10",
                Summary = "取消窗口的标签口径不一致：8 个技能的 entryTags 为 core/light/heavy/dash/control/guard/channel/finisher，**没有 \"dodge\"**；但 IMPL-v2.0 §3.4 的真实技能样例 cancelWindows.allowTags = [\"light\", \"dodge\"]。",
                SourceA = "IMPL-v2.0.md §5 断言 A22：cancelWindow 的标签必须存在于某 skill.entryTags",
                SourceB = "IMPL-v2.0.md §3.4 真实技能样例：allowTags 含 \"dodge\"；IMPL-v3.0.md §1.2 的 8 技能 entryTags 无 dodge（回锋为 dash）",
                Impact = "照 A22 实现导表器，会把规范里给出的那条真实样例窗口判为非法——判据与唯一真值源自相矛盾。",
                Resolution = "Registered"
            },
            new SpecConflict
            {
                Id = "D-11",
                Summary = "逻辑坐标的单位是**厘米定点**，而判据体系声明的距离单位是**米**。Core 的 Vec2 以 1/100 m 为定点单位（UnitsPerMeter = 100），判据（轻 0.3–0.6m / 中 0.8–1.2m / 重 1.2–1.5m、击退峰值 18 m/s）全部按米写。",
                SourceA = "生产侧 EdgeChronicles.Core.Vec2：UnitsPerMeter = 100、UnitsPerCentimeter = 1（定点，厘米）",
                SourceB = "GDD-v1.0.md §7.3 / IMPL-v3.0.md §3.2：击退距离以米声明；本目录全部距离量纲 = m",
                Impact = "击退距离类判据（F-KB-TIER / F-KB-PEAK / WC-07）若直接与逻辑坐标相减，会把厘米当米 —— 差 100 倍。判据必须显式声明取数口径（逻辑坐标 ÷ 100 = 米），否则结论永远是假的。**判据侧未声明转换即成立**。",
                Resolution = "Registered"
            },
            new SpecConflict
            {
                Id = "D-12",
                Summary = "hit_shape.frame 的时钟轴标注与比较对象矛盾：Data schema 写「命中帧（相对本技能起手，**C 轴帧**；必须 < 判定帧数）」，但它要与 active 段（墙钟 / 逻辑相段长度）比较。",
                SourceA = "生产侧 EdgeChronicles.Data.HitShapeRow.frame 的注释：C 轴帧",
                SourceB = "IMPL-v2.0.md §5 断言 A20：hitShape.frame < active（active 是相段长度，属逻辑帧）",
                Impact = "跨轴相减必错。若 frame 真的是剪辑轴帧号而 active 是逻辑帧数，两者的比较在不同实现下会差若干帧 —— A20 可能永远报绿，也可能永远报红。必须裁定 frame 归属哪根时钟，并把比较写成同轴符号式。",
                Resolution = "Registered"
            },
            new SpecConflict
            {
                Id = "D-13",
                Summary = "movement_curve.keys 的口径已被生产 schema 写死为「归一化速度（0–1）」，但 A24（曲线积分距离与声明偏差 ≤5%）只有在 keys 是**逐帧归一化位移占比**（Σ = 1）时才成立：按速度解释，Σv 的量纲是帧⁻¹，Σv × d ≠ d。",
                SourceA = "生产侧 EdgeChronicles.Data.CurveKey.value 的注释：归一化速度（0–1）",
                SourceB = "IMPL-v2.0.md §5 断言 A24：曲线积分距离与声明偏差 ≤5%",
                Impact = "A24 目前没有唯一算法：不同实现会算出不同偏差值。本测试侧按**归一化位移占比**实现（积分 = Σ(keys) × 声明距离），并在此登记；若最终裁定为速度，A24 必须重写而不是改阈值。",
                Resolution = "Registered"
            }
        };

        public static SpecConflict Find(string id)
        {
            for (int i = 0; i < All.Count; i++) { if (All[i].Id == id) { return All[i]; } }
            return null;
        }
    }
}
