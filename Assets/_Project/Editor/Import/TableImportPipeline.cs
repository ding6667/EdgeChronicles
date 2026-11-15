using System;
using System.Collections.Generic;
using System.IO;
using EdgeChronicles.Data.Schema;
using EdgeChronicles.Data.Serialization;
using EdgeChronicles.Data.Validation;

namespace EdgeChronicles.Tools.Editor.Import
{
    /// <summary>一次导表的结果。</summary>
    public sealed class ImportOutcome
    {
        /// <summary>校验报告。</summary>
        public ValidationReport Report;

        /// <summary>读进来的表。</summary>
        public TableSet Tables;

        /// <summary>实际加载到的表数。</summary>
        public int TablesLoaded;

        /// <summary>缺源数据的表名。</summary>
        public readonly List<string> MissingTables = new List<string>();

        /// <summary>是否通过（无 error）。</summary>
        public bool Succeeded => Report != null && !Report.HasErrors;
    }

    /// <summary>
    /// 导表流水线：<b>源数据 → 绑定 → 列级校验 → 派生重算 → 断言 → 生成产物</b>。
    /// <para>
    /// 三级校验流水线（EL-03 §2）：
    /// ① <b>L1 导表静态</b>：本类（读表后、生成前）跑列级校验 + 派生重算 + A19–A31 / A33 / A35–A43 / C1–C7；
    /// ② <b>L2 跨表审计</b>：<c>ProgressionAudit</c> 跑 A32 / A34 静态部分、孤儿外键、派生列覆盖；
    /// ③ <b>L3 运行时</b>：存档加载 + PlayMode 集成测试跑 A32 / A34 动态部分与 A38 / A39 / A40 重算。
    /// </para>
    /// <para>
    /// <b>任一级出现 error 即阻断，不生成产物</b>。
    /// </para>
    /// </summary>
    public static class TableImportPipeline
    {
        /// <summary>源数据目录（入库的是源数据，不是产物）。</summary>
        public const string SourceDir = "Assets/_Project/Data";

        /// <summary>生成物目录（已入 .gitignore，不入库）。</summary>
        public const string OutputDir = "Assets/Generated";

        /// <summary>
        /// 执行导表。
        /// </summary>
        /// <param name="sourceDir">源数据目录。</param>
        /// <param name="outputDir">生成物目录（不写时传 null）。</param>
        /// <param name="writeOutput">是否写生成物。</param>
        public static ImportOutcome Run(string sourceDir, string outputDir, bool writeOutput)
        {
            var outcome = new ImportOutcome
            {
                Report = new ValidationReport(),
                Tables = new TableSet()
            };

            var schemas = TableCatalog.All();
            foreach (var schema in schemas)
            {
                string path = Path.Combine(sourceDir, schema.Name + ".json");
                if (!File.Exists(path))
                {
                    outcome.MissingTables.Add(schema.Name);
                    outcome.Report.Error("MISSING", schema.Name, null, null,
                        $"缺源数据文件：{path}（该表的字段级规格见 EL-03 §1 / IMPL-v2.0 §5，行数据需策划补齐）");
                    continue;
                }

                JsonValue parsed;
                try
                {
                    parsed = Json.Parse(File.ReadAllText(path));
                }
                catch (JsonParseException ex)
                {
                    outcome.Report.Error("PARSE", schema.Name, null, null, "JSON 解析失败：" + ex.Message);
                    continue;
                }

                JsonValue rowsNode = ExtractRows(parsed, schema.Name, outcome.Report);
                if (rowsNode == null) continue;

                var table = new TableData { Name = schema.Name, Schema = schema };
                var seenKeys = new HashSet<string>(StringComparer.Ordinal);

                foreach (var rowNode in rowsNode.Items)
                {
                    if (rowNode.Kind != JsonKind.Object)
                    {
                        outcome.Report.Error("PARSE", schema.Name, null, null, "行必须是 JSON 对象");
                        continue;
                    }

                    var bound = RowBinder.Bind(schema, rowNode, outcome.Report);

                    BoundRow existing;
                    if (!table.TryAdd(bound, out existing))
                    {
                        outcome.Report.Error("PK", schema.Name, bound.Key, null,
                            $"主键重复：{bound.Key}（与另一行冲突）");
                        continue;
                    }
                }

                outcome.Tables.Add(table);
                outcome.TablesLoaded++;
            }

            // L1：派生重算（禁手填：手填值与重算值不等即 error）
            //      **必须排在列级校验之前**：派生列的值此时才是最终值，
            //      否则校验器会拿「尚未重算的 0」去比下界，把工具自己算出来的列判成越界。
            Derivation.DeriveAll(outcome.Tables, outcome.Report);

            // L1：列级校验
            foreach (var table in outcome.Tables.All)
            {
                ColumnValidator.Validate(table, outcome.Report);
            }

            // L1 + L2：断言
            Assertions.RunAll(outcome.Tables, outcome.Report);

            // 阻断：任一 error 不生成产物
            if (writeOutput && !outcome.Report.HasErrors)
            {
                GeneratedWriter.Write(outcome.Tables, outputDir);
            }

            return outcome;
        }

        private static JsonValue ExtractRows(JsonValue parsed, string tableName, ValidationReport report)
        {
            if (parsed.Kind == JsonKind.Array) return parsed;

            if (parsed.Kind == JsonKind.Object)
            {
                var rows = parsed.Get("rows");
                if (rows != null && rows.Kind == JsonKind.Array) return rows;

                report.Error("PARSE", tableName, null, null, "源数据必须是行数组，或含 \"rows\" 数组的对象");
                return null;
            }

            report.Error("PARSE", tableName, null, null, "源数据格式非法");
            return null;
        }
    }
}
