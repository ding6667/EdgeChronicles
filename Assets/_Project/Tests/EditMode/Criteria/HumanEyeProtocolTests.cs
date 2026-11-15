using EdgeChronicles.Tests.Qa;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace EdgeChronicles.Tests.Criteria
{
    /// <summary>
    /// 人眼项协议自检。
    ///
    /// 【红线】禁止把"人工确认"写成通过判据。
    /// 人眼项必须给出机器可辅助的复现条件：机位 / 距离 / FOV / 慢放倍数 / 判定动作全部写死。
    /// 少了任何一项，换一个人来判就会得到不同结论 → 它就不构成 Gate。
    /// </summary>
    public class HumanEyeProtocolTests
    {
        /// <summary>协议里不允许出现的模糊措辞。</summary>
        private static readonly string[] ForbiddenPhrases =
        {
            "第三人称", "看起来", "明显", "主观", "适当", "合理", "大概", "差不多", "稍微", "尽量", "酌情"
        };

        [Test]
        public void 每条人眼与混合判据_都指向存在的录屏协议()
        {
            var problems = new List<string>();
            for (int i = 0; i < CriterionCatalog.Walkthrough.Count; i++)
            {
                CriterionRecord c = CriterionCatalog.Walkthrough[i];
                if (c.Executability != CriterionExecutability.HumanEye && c.Executability != CriterionExecutability.Mixed)
                {
                    continue;
                }
                string hp = ExtractProtocolId(c.ExecutableAs);
                if (hp == null) { problems.Add(c.Id + " 的 ExecutableAs 里没有 HP-xx 协议编号"); continue; }
                if (HumanEyeProtocols.Find(hp) == null) { problems.Add(c.Id + " 指向的协议 " + hp + " 不存在"); }
            }
            Assert.IsEmpty(problems, "人眼项协议缺失：\n" + string.Join("\n", problems.ToArray()));
        }

        [Test]
        public void 每条协议_机位距离视场角慢放判定动作全部写死()
        {
            var problems = new List<string>();
            for (int i = 0; i < HumanEyeProtocols.All.Count; i++)
            {
                HumanEyeProtocol p = HumanEyeProtocols.All[i];
                if (string.IsNullOrEmpty(p.Camera)) { problems.Add(p.Id + " 缺机位"); }
                if (string.IsNullOrEmpty(p.Distance)) { problems.Add(p.Id + " 缺距离"); }
                if (string.IsNullOrEmpty(p.Fov)) { problems.Add(p.Id + " 缺视场角"); }
                if (string.IsNullOrEmpty(p.SlowMotion)) { problems.Add(p.Id + " 缺慢放倍数"); }
                if (string.IsNullOrEmpty(p.JudgementAction)) { problems.Add(p.Id + " 缺判定动作"); }
                if (string.IsNullOrEmpty(p.PassCriteria)) { problems.Add(p.Id + " 缺通过标准"); }
                if (string.IsNullOrEmpty(p.RecordFields)) { problems.Add(p.Id + " 缺记录字段"); }
            }
            Assert.IsEmpty(problems, "以下协议字段不全（缺一项就换个人判不一致）：\n" + string.Join("\n", problems.ToArray()));
        }

        [Test]
        public void 每条协议_判定动作必须可二值化且含具体数值()
        {
            var problems = new List<string>();
            for (int i = 0; i < HumanEyeProtocols.All.Count; i++)
            {
                HumanEyeProtocol p = HumanEyeProtocols.All[i];
                if (!HasDigit(p.Distance)) { problems.Add(p.Id + " 的距离没有写死数值"); }
                if (!HasDigit(p.Fov)) { problems.Add(p.Id + " 的视场角没有写死数值"); }
                if (!HasDigit(p.SlowMotion)) { problems.Add(p.Id + " 的慢放倍数没有写死数值"); }
                if (!HasDigit(p.JudgementAction) && !HasDigit(p.PassCriteria)) { problems.Add(p.Id + " 的判定动作 / 通过标准里没有任何具体数值"); }
            }
            Assert.IsEmpty(problems, "以下协议存在"看情况"成分：\n" + string.Join("\n", problems.ToArray()));
        }

        [Test]
        public void 每条协议_不含模糊措辞()
        {
            var problems = new List<string>();
            for (int i = 0; i < HumanEyeProtocols.All.Count; i++)
            {
                HumanEyeProtocol p = HumanEyeProtocols.All[i];
                string all = p.Camera + "|" + p.Distance + "|" + p.Fov + "|" + p.SlowMotion + "|"
                    + p.JudgementAction + "|" + p.PassCriteria;
                for (int f = 0; f < ForbiddenPhrases.Length; f++)
                {
                    if (all.Contains(ForbiddenPhrases[f]))
                    {
                        problems.Add(p.Id + " 含模糊措辞「" + ForbiddenPhrases[f] + "」");
                    }
                }
            }
            Assert.IsEmpty(problems, "以下协议含不可复现的措辞：\n" + string.Join("\n", problems.ToArray()));
        }

        [Test]
        public void 观察项与定位通道_必须显式声明不构成Gate()
        {
            // WC-05 / WC-12 / WC-14 三条明确"不进验收 / 不毙 Gate"。
            // 它们不配 Gate 级负向用例，但必须在目录里写明豁免理由——
            // 否则"忘了写判据"和"决定不设 Gate"就分不清了。
            string[] observationIds = { "WC-05", "WC-12", "WC-14" };
            var problems = new List<string>();
            for (int i = 0; i < observationIds.Length; i++)
            {
                CriterionRecord c = CriterionCatalog.FindCriterion(observationIds[i]);
                if (c == null) { problems.Add(observationIds[i] + " 不在目录里"); continue; }
                if (!string.IsNullOrEmpty(c.NegativeCaseWaiver)) { continue; }
                if (c.Veto) { problems.Add(observationIds[i] + " 被标成一票否决，却没有负向用例"); }
            }
            Assert.IsEmpty(problems, "观察项豁免未登记：\n" + string.Join("\n", problems.ToArray()));
        }

        [Test]
        public void 协议编号_不得重复()
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var problems = new List<string>();
            for (int i = 0; i < HumanEyeProtocols.All.Count; i++)
            {
                string id = HumanEyeProtocols.All[i].Id;
                if (!seen.Add(id)) { problems.Add("协议编号重复：" + id); }
            }
            Assert.IsEmpty(problems, string.Join("\n", problems.ToArray()));
        }

        private static string ExtractProtocolId(string executableAs)
        {
            if (string.IsNullOrEmpty(executableAs)) { return null; }
            int idx = executableAs.IndexOf("HP-", StringComparison.Ordinal);
            if (idx < 0) { return null; }
            int end = idx + 3;
            while (end < executableAs.Length && char.IsDigit(executableAs[end])) { end++; }
            return executableAs.Substring(idx, end - idx);
        }

        private static bool HasDigit(string s)
        {
            if (string.IsNullOrEmpty(s)) { return false; }
            for (int i = 0; i < s.Length; i++)
            {
                if (char.IsDigit(s[i])) { return true; }
            }
            return false;
        }
    }
}
