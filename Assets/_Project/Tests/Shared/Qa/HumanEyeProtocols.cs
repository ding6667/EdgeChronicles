using System;
using System.Collections.Generic;

namespace EdgeChronicles.Tests.Qa
{
    /// <summary>
    /// 人眼项录屏判定协议。
    ///
    /// 【为什么必须写死机位 / 距离 / FOV / 慢放倍数 / 判定动作】
    /// 「换个人来判结论也要一样」是硬要求。
    /// 只写"第三人称""看起来偏重""稍微有点卡"这类描述，
    /// 换一个人判出来的结论必然不同 → 它就不构成 Gate，只是一个意见。
    ///
    /// 【字段】
    /// · 机位：相机相对被测物的位置与朝向（写死到角度与高度）
    /// · 距离：被测物到相机的距离（米）
    /// · FOV：视场角（度）
    /// · 慢放倍数：录屏回放时的降速倍率（倍）
    /// · 判定动作：测试者被要求做的**唯一**动作，必须可二值化
    /// · 通过标准：多少样本、什么比例算过
    /// · 记录字段：每次判定必须记下来的东西（用于事后复核）
    /// </summary>
    public sealed class HumanEyeProtocol
    {
        public string Id = "";
        /// <summary>对应的走查判据编号。</summary>
        public string CriterionId = "";
        public string Title = "";
        /// <summary>机位（写死）。</summary>
        public string Camera = "";
        /// <summary>距离（m，写死）。</summary>
        public string Distance = "";
        /// <summary>视场角（度，写死）。</summary>
        public string Fov = "";
        /// <summary>慢放倍数（倍，写死）。</summary>
        public string SlowMotion = "";
        /// <summary>判定动作（写死，二值化）。</summary>
        public string JudgementAction = "";
        /// <summary>通过标准。</summary>
        public string PassCriteria = "";
        /// <summary>必须记录的字段。</summary>
        public string RecordFields = "";
        /// <summary>样本量。</summary>
        public string SampleSize = "";
        public string Note = "";
    }

    public static class HumanEyeProtocols
    {
        public static readonly List<HumanEyeProtocol> All = new List<HumanEyeProtocol>
        {
            new HumanEyeProtocol
            {
                Id = "HP-01「, CriterionId = 」WC-01「, Title = 」闪避末帧与待机首帧姿势差",
                Camera = "相机在角色正后方 0° 方位、抬高 1.2m、俯角 8°，镜头始终指向角色胸口（角色不转向）",
                Distance = "4.0m（角色与相机的水平距离，误差 ±0.1m，用地面刻度定位）",
                Fov = "60°（垂直 FOV，固定不可调）",
                SlowMotion = "0.25 倍（60fps 素材降速到 15fps 播放）",
                JudgementAction = "播放闪避最后 3 帧与待机首帧并排，逐帧看角色膝盖与肩线；只回答一个问题：两帧的躯干夹角差是否 ≤5°（大于 5° 判否）",
                PassCriteria = "8 技能位 × 3 次重复 = 24 次，每次只判「是 / 否」；≥22 次为否则通过",
                RecordFields = "技能位、重复序号、判定结果、观察到的角度差估计值",
                SampleSize = "24 次判定（5 人各 4 次 + 1 人复核 8 次）",
                Note = "只看躯干，不看位移；位移由 SC-01 自动测。"
            },
            new HumanEyeProtocol
            {
                Id = "HP-02「, CriterionId = 」WC-02「, Title = 」闪避无敌：连续 10 次纵斩全部落空",
                Camera = "相机固定在场地正侧方 90° 方位、抬高 1.0m、俯角 15°，画面同时容纳攻击者与闪避者",
                Distance = "5.0m（相机到攻击者落点）",
                Fov = "60°",
                SlowMotion = "1 倍（实时判定，只在判不清时用 0.25 倍复核）",
                JudgementAction = "被攻击者用闪避通过哑刀「断崖一刀」共 10 次；每次只记一件事：这次有没有掉血",
                PassCriteria = "10 次全部不掉血算通过；掉 1 次即不通过（这是 10/10，不是 9/10）",
                RecordFields = "第几次、是否掉血、掉血数值",
                SampleSize = "10 次连续（不重试、不挑时机）",
                Note = "必须用「断崖一刀」——它是 §7.3 指定的唯一纵斩来源，换别的招式结论不可比。"
            },
            new HumanEyeProtocol
            {
                Id = "HP-03「, CriterionId = 」WC-04「, Title = 」受击轻重盲测（只播前 12 帧）",
                Camera = "相机在角色正后方 0° 方位、抬高 1.5m、俯角 10°，画面裁到只剩角色上半身",
                Distance = "3.0m",
                Fov = "45°",
                SlowMotion = "0.5 倍",
                JudgementAction = "只播受击开始后的前 12 帧，不显示任何数值；测试者只回答一个问题：这一下是轻 / 中 / 重（三选一，不许答「看不出来」）",
                PassCriteria = "单条片段答对记 1 分；同一档的 3 条片段 ≥2 条答对即该档通过；三档全过才算本项通过",
                RecordFields = "片段编号、测试者选择、正确答案",
                SampleSize = "3 档 × 3 片段 = 9 段，5 人各判一遍",
                Note = "不许答「看不出来」——答不出来本身要记为一次错误选择；这是逼测试者做判断，不是收集意见。"
            },
            new HumanEyeProtocol
            {
                Id = "HP-04「, CriterionId = 」WC-05「, Title = 」4 正轴向受击方向可读（观察项）",
                Camera = "相机在角色正后方 0° 方位、抬高 1.2m、俯角 8°",
                Distance = "5.0m",
                Fov = "60°",
                SlowMotion = "0.5 倍",
                JudgementAction = "播放 20 段打乱编号的受击片段；测试者只回答：这下打的哪个方向（前 / 后 / 左 / 右）",
                PassCriteria = "本项是观察项：记录 20 段的正确率即可，不设通过线（斜向片段不计入）",
                RecordFields = "片段编号、测试者回答、正确答案、是否斜向",
                SampleSize = "20 段（16 正轴向 + 4 斜向，编号打乱）",
                Note = "斜向片段不参与统计——没有对应动画变体，判它等于判错对象。"
            },
            new HumanEyeProtocol
            {
                Id = "HP-05「, CriterionId = 」WC-10「, Title = 」顿帧：卡住且被顶开",
                Camera = "相机在角色正侧方 90° 方位、抬高 1.0m、俯角 0°，相机不跟随位移",
                Distance = "4.0m",
                Fov = "50°",
                SlowMotion = "0.25 倍",
                JudgementAction = "播放一次受击全过程；测试者只回答两个问题：① 命中瞬间画面是否「卡住」（人物停在原地不动）② 卡住的同时身体是否仍然被推开",
                PassCriteria = "两个问题必须同时为是；只满足其一即不通过",
                RecordFields = "片段编号、①是否卡住、②是否被推开",
                SampleSize = "轻 / 中 / 重三档各 2 次 = 6 次",
                Note = "自动部分只测 time_scale 与 PhaseFrame 连续性；「卡住且被顶开」这一半只能人眼，故机位与慢放必须写死。"
            },
            new HumanEyeProtocol
            {
                Id = "HP-06「, CriterionId = 」WC-12「, Title = 」B1 仅色差组（定位通道）",
                Camera = "固定机位截屏：角色占画面高度 40%，背景为该副本实拍背景",
                Distance = "5.0m",
                Fov = "60°",
                SlowMotion = "1 倍（静帧判定）",
                JudgementAction = "看两套只差颜色的描边静帧；只回答：能不能分辨出这是两个不同状态",
                PassCriteria = "本组是定位通道，**不设通过线**；结果只用于说明「哪条通道在起作用」，不达标不改方案",
                RecordFields = "静帧编号、测试者能否分辨",
                SampleSize = "5 人 × 10 张",
                Note = "必须记录显示器型号与色彩配置；换机器结论可能变，所以它不是 Gate。"
            },
            new HumanEyeProtocol
            {
                Id = "HP-07「, CriterionId = 」WC-13「, Title = 」B2 仅形态组（关键组）",
                Camera = "固定机位截屏：角色占画面高度 40%；20 张图序打乱（同一文件夹内序号随机）",
                Distance = "5.0m",
                Fov = "60°",
                SlowMotion = "1 倍（静帧判定）",
                JudgementAction = "看两张描边静帧，只回答：这是霸体还是无敌（两个选项，不许答「都像」）",
                PassCriteria = "每人 10 张 ≥9 张答对，且 5 人中 ≥4 人达标；20 张必须含铁砧窟橙底 ≥2 张、雾隐竹海浓雾 ≥2 张",
                RecordFields = "静帧编号、测试者选择、正确答案、背景预设",
                SampleSize = "5 人 × 10 张",
                Note = "**对照组（D5）必须先跑并同时消除颜色 / 描边形态 / 入窗时机三个通道**；只消颜色不消其余两通道时，本组的通过率无效。"
            },
            new HumanEyeProtocol
            {
                Id = "HP-08「, CriterionId = 」WC-14「, Title = 」B3 相机跟随（观察项）",
                Camera = "相机在角色正后方 0° 方位、抬高 1.6m、俯角 12°",
                Distance = "5.0m",
                Fov = "60°",
                SlowMotion = "1 倍（实时手感）",
                JudgementAction = "连续跑 3 圈固定路线；测试者只回答：镜头有没有让角色被框出画面（是 / 否）",
                PassCriteria = "观察项，**不设通过线**，不毙 Gate；记录结果转后续修复处理",
                RecordFields = "圈数、是否出画、出画时长",
                SampleSize = "3 圈",
                Note = "本项没有可量化的误判条件，所以只能是观察项——不能升级为 Gate。"
            },
            new HumanEyeProtocol
            {
                Id = "HP-09「, CriterionId = 」WC-15「, Title = 」Boss 攻击范围 30 帧内可读",
                Camera = "相机在玩家正后方 0° 方位、抬高 2.0m、俯角 20°，玩家固定站位不做位移",
                Distance = "6.0m（相机到玩家）",
                Fov = "70°",
                SlowMotion = "0.25 倍",
                JudgementAction = "播放攻击从预警出现到命中的全过程；测试者在预警出现的那一刻按暂停，只回答：我现在能看出这一下打在哪个范围吗（能 / 不能）",
                PassCriteria = "8 次攻击中 ≥7 次答「能」算通过；答「能」的还必须指出范围形状，形状答错按「不能」计",
                RecordFields = "攻击编号、回答、测试者指认的范围形状",
                SampleSize = "8 次攻击",
                Note = "必须在预警出现的那一刻暂停，不许看到命中后才回答——那测的是回忆不是可读性。"
            },
            new HumanEyeProtocol
            {
                Id = "HP-10「, CriterionId = 」WC-17「, Title = 」刀光竖直面与离地",
                Camera = "相机在角色正侧方 90° 方位、抬高 0.8m、俯角 0°",
                Distance = "3.0m",
                Fov = "50°",
                SlowMotion = "0.25 倍",
                JudgementAction = "播放刀光命中帧前后各 5 帧；测试者只回答两个问题：① 刀光面是竖直的还是平躺的 ② 刀光下沿离地是否 ≥0.5m",
                PassCriteria = "①全为竖直、②全部 ≥0.5m 才通过；任一条不满足即不通过",
                RecordFields = "技能位、①的判断、②的判断",
                SampleSize = "8 技能各 1 次 = 8 次",
                Note = "遮挡比由 SC-13 自动测；本协议只判「面朝哪」和「离地多远」这两个几何事实。"
            },
            new HumanEyeProtocol
            {
                Id = "HP-11「, CriterionId = 」WC-18「, Title = 」攻击预警三重编码",
                Camera = "相机在玩家正后方 0° 方位、抬高 2.0m、俯角 20°",
                Distance = "6.0m",
                Fov = "70°",
                SlowMotion = "1 倍（实时）",
                JudgementAction = "对每种攻击只做一件事：逐条确认地面预警圈 / 形状差异 / 音效提示三条通道是否都存在（是 / 否 × 3）",
                PassCriteria = "每种攻击三条通道全部为是才算通过；缺任意一条即该攻击不通过",
                RecordFields = "攻击编号、地面圈、形状、音效（三项各是 / 否）",
                SampleSize = "3 个 Boss 的全部攻击类型",
                Note = "全身 AOE 允许用「全屏色调 + 音效」替代地面圈，但必须在记录里写明替代事实。"
            },
            new HumanEyeProtocol
            {
                Id = "HP-12「, CriterionId = 」WC-19「, Title = 」墨线 3px 实际观感",
                Camera = "固定机位截屏：角色占画面高度 50%",
                Distance = "5.0m",
                Fov = "60°",
                SlowMotion = "1 倍（静帧）",
                JudgementAction = "看静帧，只回答：角色轮廓能不能和背景分开（能 / 不能）",
                PassCriteria = "全部副本预设的静帧都能分开才算通过；亮度差由 SC-14 自动算，本协议只判「看起来分不分得开」",
                RecordFields = "副本预设、测试者回答",
                SampleSize = "5 个副本预设各 2 张 = 10 张",
                Note = "必须用各副本实拍背景截屏，不许用纯色测试底。"
            }
        };

        public static HumanEyeProtocol Find(string id)
        {
            for (int i = 0; i < All.Count; i++)
            {
                if (All[i].Id == id) { return All[i]; }
            }
            return null;
        }
    }
}
