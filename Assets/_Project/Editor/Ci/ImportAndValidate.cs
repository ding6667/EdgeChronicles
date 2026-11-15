using System;
using System.Collections.Generic;
using System.Globalization;
using EdgeChronicles.Data.Schema;
using EdgeChronicles.Data.Validation;
using EdgeChronicles.Tools.Editor.Import;
using UnityEditor;
using UnityEngine;

namespace EdgeChronicles.Tools.Editor.Ci
{
    /// <summary>
    /// CI 入口：导表 + 全断言（L1 + L2）。
    /// <para>
    /// 判据（EL-03 §5 ②）：日志含 <c>ValidationSummary: error=0 warning=0</c>，退出码 0。
    /// </para>
    /// </summary>
    public static class ImportAndValidate
    {
        /// <summary>批处理入口（<c>-executeMethod EdgeChronicles.Tools.Editor.Ci.ImportAndValidate</c>）。</summary>
        public static void Run()
        {
            var outcome = TableImportPipeline.Run(TableImportPipeline.SourceDir, TableImportPipeline.OutputDir, true);
            ReportWriter.Print("import", outcome);
            EditorApplication.Exit(outcome.Succeeded ? 0 : 1);
        }
    }

    /// <summary>
    /// CI 入口：养成层跨表审计（L2）。
    /// <para>
    /// 判据（EL-03 §5 ③）：日志含 <c>ProgressionAudit: tables=20 orphans=0 derivedOverwritten=100%</c>，
    /// 且 A31–A43 全部 PASS、<c>mechanic_mod</c> 越界计数 = 0。
    /// </para>
    /// </summary>
    public static class ProgressionAudit
    {
        /// <summary>批处理入口（<c>-executeMethod EdgeChronicles.Tools.Editor.Ci.ProgressionAudit</c>）。</summary>
        public static void Run()
        {
            var outcome = TableImportPipeline.Run(TableImportPipeline.SourceDir, TableImportPipeline.OutputDir, true);
            ReportWriter.Print("progression", outcome);

            bool ok = outcome.Succeeded && ReportWriter.HasAllAssertionsPassed(outcome.Report);
            EditorApplication.Exit(ok ? 0 : 1);
        }
    }

    /// <summary>
    /// 报告输出：统一的日志格式（CI 脚本按固定字符串判定，格式不可改）。
    /// </summary>
    public static class ReportWriter
    {
        /// <summary>打印一次导表的结果。</summary>
        public static void Print(string tag, ImportOutcome outcome)
        {
            var report = outcome.Report;

            foreach (var issue in report.Issues)
            {
                if (issue.Level == IssueLevel.Error) Debug.LogError("[CHECK] " + issue);
                else Debug.LogWarning("[CHECK] " + issue);
            }

            foreach (var assertion in report.Assertions)
            {
                string state = assertion.Skipped ? "SKIP" : (assertion.Passed ? "PASS" : "FAIL");
                Debug.Log($"Assertion {assertion.Id}: {state} — {assertion.Detail}");
            }

            int orphans = CountIssues(report, "FK");
            int modOutOfRange = CountIssues(report, "A35");
            int specLoaded = CountSpecTablesLoaded(outcome);
            int auxLoaded = CountAuxTablesLoaded(outcome);
            double derivedCoverage = ComputeDerivedCoverage(outcome);

            // 判据行：格式固定，CI 按整行匹配。
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "ProgressionAudit: tables={0} orphans={1} derivedOverwritten={2}",
                specLoaded, orphans, derivedCoverage.ToString("0.##", CultureInfo.InvariantCulture) + "%"));

            // 扩展表单独记行（不计入 20 张规格表）。
            Debug.Log($"ProgressionAudit: auxTables={auxLoaded} (affix_pool / equipment_base)");

            Debug.Log($"ProgressionAudit: mechanicModOutOfRange={modOutOfRange}");
            Debug.Log(report.AssertionSummaryLine());
            Debug.Log(report.SummaryLine());
            Debug.Log($"[CHECK] {tag}-tables-loaded={outcome.TablesLoaded}");
            if (outcome.MissingTables.Count > 0)
            {
                Debug.Log($"[CHECK] {tag}-missing-tables={string.Join(",", outcome.MissingTables.ToArray())}");
            }
        }

        /// <summary>是否全部断言通过（跳过的不算失败，但缺源数据会另有 error）。</summary>
        public static bool HasAllAssertionsPassed(ValidationReport report)
        {
            foreach (var assertion in report.Assertions)
            {
                if (!assertion.Skipped && !assertion.Passed) return false;
            }
            return !report.HasErrors;
        }

        private static int CountIssues(ValidationReport report, string code)
        {
            int count = 0;
            foreach (var issue in report.Issues)
            {
                if (issue.Level == IssueLevel.Error && string.Equals(issue.Code, code, StringComparison.Ordinal)) count++;
            }
            return count;
        }

        private static int CountSpecTablesLoaded(ImportOutcome outcome)
        {
            int count = 0;
            foreach (var schema in TableCatalog.All())
            {
                if (schema.Auxiliary) continue;
                if (outcome.Tables.Has(schema.Name)) count++;
            }
            return count;
        }

        private static int CountAuxTablesLoaded(ImportOutcome outcome)
        {
            int count = 0;
            foreach (var schema in TableCatalog.All())
            {
                if (!schema.Auxiliary) continue;
                if (outcome.Tables.Has(schema.Name)) count++;
            }
            return count;
        }

        /// <summary>
        /// 派生列覆盖率 = 成功写出的派生列 / 全部派生列。
        /// <para>
        /// 口径：派生列一律由工具算出并覆盖（手填值与重算值不等即 error，已单独计数）。
        /// 本值低于 100% 说明<b>规格里声明的派生列在行类型里没有对应字段</b>——那是一个真实的缺口，必须报出来。
        /// </para>
        /// </summary>
        private static double ComputeDerivedCoverage(ImportOutcome outcome)
        {
            int total = 0;
            int covered = 0;

            foreach (var table in outcome.Tables.All)
            {
                foreach (var column in table.Schema.Columns)
                {
                    if (!column.Derived) continue;
                    total++;
                    var field = table.Schema.RowType.GetField(column.Name);
                    if (field != null) covered++;
                }
            }

            if (total == 0) return 100d;
            return covered * 100d / total;
        }
    }
}
