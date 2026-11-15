using System;
using System.Collections.Generic;

namespace EdgeChronicles.Data.Validation
{
    /// <summary>问题级别。</summary>
    public enum IssueLevel
    {
        /// <summary>错误：任一 error 即阻断，不生成产物。</summary>
        Error = 0,

        /// <summary>警告：不阻断，但必须出现在报告里（不静默）。</summary>
        Warning = 1
    }

    /// <summary>一条校验问题。</summary>
    public sealed class ValidationIssue
    {
        /// <summary>级别。</summary>
        public IssueLevel Level;

        /// <summary>断言编号（如 <c>A31</c>、<c>C3</c>、<c>FK</c>）。</summary>
        public string Code;

        /// <summary>表名。</summary>
        public string Table;

        /// <summary>行主键（精确到行）。</summary>
        public string RowKey;

        /// <summary>字段名（可空）。</summary>
        public string Field;

        /// <summary>说明。</summary>
        public string Message;

        public override string ToString()
        {
            string location = Table ?? "?";
            if (!string.IsNullOrEmpty(RowKey)) location += "[" + RowKey + "]";
            if (!string.IsNullOrEmpty(Field)) location += "." + Field;
            return $"[{Level}] {Code} {location}: {Message}";
        }
    }

    /// <summary>一条断言的执行结果。</summary>
    public sealed class AssertionResult
    {
        /// <summary>断言编号。</summary>
        public string Id;

        /// <summary>是否通过。</summary>
        public bool Passed;

        /// <summary>是否被跳过（缺源数据 / 该断言不在本级跑）。</summary>
        public bool Skipped;

        /// <summary>说明。</summary>
        public string Detail;

        /// <summary>产生的问题（error 列表）。</summary>
        public readonly List<ValidationIssue> Issues = new List<ValidationIssue>();

        public override string ToString()
        {
            string state = Skipped ? "SKIP" : (Passed ? "PASS" : "FAIL");
            return $"{Id} {state}{(string.IsNullOrEmpty(Detail) ? "" : " — " + Detail)}";
        }
    }

    /// <summary>校验报告：所有问题 + 所有断言结果。</summary>
    public sealed class ValidationReport
    {
        private readonly List<ValidationIssue> _issues = new List<ValidationIssue>();
        private readonly List<AssertionResult> _assertions = new List<AssertionResult>();

        /// <summary>全部问题。</summary>
        public IReadOnlyList<ValidationIssue> Issues => _issues;

        /// <summary>全部断言结果。</summary>
        public IReadOnlyList<AssertionResult> Assertions => _assertions;

        /// <summary>错误数。</summary>
        public int ErrorCount { get; private set; }

        /// <summary>警告数。</summary>
        public int WarningCount { get; private set; }

        /// <summary>是否有错误（阻断条件）。</summary>
        public bool HasErrors => ErrorCount > 0;

        /// <summary>记一条错误。</summary>
        public void Error(string code, string table, string rowKey, string field, string message)
        {
            _issues.Add(new ValidationIssue
            {
                Level = IssueLevel.Error,
                Code = code,
                Table = table,
                RowKey = rowKey,
                Field = field,
                Message = message
            });
            ErrorCount++;
        }

        /// <summary>记一条警告。</summary>
        public void Warning(string code, string table, string rowKey, string field, string message)
        {
            _issues.Add(new ValidationIssue
            {
                Level = IssueLevel.Warning,
                Code = code,
                Table = table,
                RowKey = rowKey,
                Field = field,
                Message = message
            });
            WarningCount++;
        }

        /// <summary>登记一条断言结果。</summary>
        public void AddAssertion(AssertionResult result)
        {
            _assertions.Add(result);
            foreach (var issue in result.Issues)
            {
                _issues.Add(issue);
                if (issue.Level == IssueLevel.Error) ErrorCount++;
                else WarningCount++;
            }
        }

        /// <summary>汇总行（CI 判据读这一行）。</summary>
        public string SummaryLine()
        {
            return $"ValidationSummary: error={ErrorCount} warning={WarningCount}";
        }

        /// <summary>断言汇总行。</summary>
        public string AssertionSummaryLine()
        {
            int passed = 0, failed = 0, skipped = 0;
            foreach (var a in _assertions)
            {
                if (a.Skipped) skipped++;
                else if (a.Passed) passed++;
                else failed++;
            }
            return $"AssertionSummary: total={_assertions.Count} passed={passed} failed={failed} skipped={skipped}";
        }
    }
}
