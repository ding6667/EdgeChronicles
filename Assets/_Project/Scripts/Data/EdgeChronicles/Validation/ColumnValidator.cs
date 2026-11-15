using System;
using System.Globalization;
using System.Reflection;
using EdgeChronicles.Data.Schema;

namespace EdgeChronicles.Data.Validation
{
    /// <summary>
    /// 列级校验：必填、值域、枚举。
    /// <para>
    /// 这一层只管「单个字段自身合不合法」；跨行、跨表的判定在 <see cref="Assertions"/>。
    /// </para>
    /// </summary>
    public static class ColumnValidator
    {
        /// <summary>校验一张表的全部行。</summary>
        public static void Validate(TableData table, ValidationReport report)
        {
            foreach (var bound in table.Rows)
            {
                foreach (var column in table.Schema.Columns)
                {
                    var field = table.Schema.RowType.GetField(column.Name, BindingFlags.Public | BindingFlags.Instance);
                    if (field == null) continue;

                    var value = field.GetValue(bound.Row);
                    if (value == null)
                    {
                        if (column.Required && IsRequiredType(column.Type))
                        {
                            report.Error("REQ", table.Name, bound.Key, column.Name, "必填列为空");
                        }
                        continue;
                    }

                    if (value is string text)
                    {
                        if (column.Required && text.Length == 0)
                        {
                            report.Error("REQ", table.Name, bound.Key, column.Name, "必填列为空串");
                        }
                        if (column.Allowed != null && text.Length > 0)
                        {
                            bool ok = false;
                            foreach (var allowed in column.Allowed)
                            {
                                if (string.Equals(allowed, text, StringComparison.Ordinal)) { ok = true; break; }
                            }
                            if (!ok)
                            {
                                report.Error("ENUM", table.Name, bound.Key, column.Name,
                                    $"取值「{text}」不在合法集合内：{string.Join(" / ", column.Allowed)}");
                            }
                        }
                    }
                    else if (value is int intValue)
                    {
                        if (column.Min.HasValue && intValue < column.Min.Value)
                        {
                            report.Error("RANGE", table.Name, bound.Key, column.Name,
                                $"取值 {intValue} 小于下界 {column.Min.Value}");
                        }
                        if (column.Max.HasValue && intValue > column.Max.Value)
                        {
                            report.Error("RANGE", table.Name, bound.Key, column.Name,
                                $"取值 {intValue} 大于上界 {column.Max.Value}");
                        }
                    }
                    else if (value is float floatValue)
                    {
                        if (column.Min.HasValue && floatValue < column.Min.Value - 1e-6f)
                        {
                            report.Error("RANGE", table.Name, bound.Key, column.Name,
                                $"取值 {floatValue} 小于下界 {column.Min.Value}");
                        }
                        if (column.Max.HasValue && floatValue > column.Max.Value + 1e-6f)
                        {
                            report.Error("RANGE", table.Name, bound.Key, column.Name,
                                $"取值 {floatValue} 大于上界 {column.Max.Value}");
                        }
                    }
                    else if (value is string[] texts)
                    {
                        if (column.Required && texts.Length == 0 && column.Type == ColumnType.StringArray &&
                            !AllowsEmptyArray(column))
                        {
                            report.Error("REQ", table.Name, bound.Key, column.Name, "必填数组为空");
                        }
                    }
                    else if (value is bool)
                    {
                        // 布尔无值域
                    }
                }
            }
        }

        private static bool IsRequiredType(ColumnType type)
        {
            return type != ColumnType.StringArray && type != ColumnType.IntArray &&
                   type != ColumnType.ObjectArray && type != ColumnType.KeyFloatArray;
        }

        /// <summary>
        /// 是否允许空数组。
        /// <para>
        /// 规格里写「可空数组」的列（如 <c>prereq_ids</c>、<c>compose_from</c> 的最高级项）允许空；
        /// 写成「是（可空数组）」的列必须显式写 <c>[]</c> 而不是省略——省略会被当成缺字段。
        /// 这里按「列名是否在规格里显式允许为空」判定：允许空的列集合如下。
        /// </para>
        /// </summary>
        private static bool AllowsEmptyArray(ColumnSpec column)
        {
            switch (column.Name)
            {
                case "prereq_ids":
                case "compose_from":
                case "derivative_ids":
                case "conditions":
                case "affixes":
                case "mechanic_targets":
                case "entry_tags":
                case "hit_shape_ids":
                    return true;
                default:
                    return false;
            }
        }
    }
}
