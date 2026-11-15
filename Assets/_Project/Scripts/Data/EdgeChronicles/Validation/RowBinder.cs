using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using EdgeChronicles.Data.Schema;
using EdgeChronicles.Data.Serialization;

namespace EdgeChronicles.Data.Validation
{
    /// <summary>一行已绑定的数据（= 源数据 + 绑定后的 POCO + 源原值快照）。</summary>
    public sealed class BoundRow
    {
        /// <summary>绑定后的 POCO 实例。</summary>
        public object Row;

        /// <summary>主键文本（多列主键以 <c>|</c> 连接）。</summary>
        public string Key;

        /// <summary>源数据里出现过的列原值（派生列比对用；key = snake_case 列名）。</summary>
        public Dictionary<string, JsonValue> Source = new Dictionary<string, JsonValue>(StringComparer.Ordinal);

        /// <summary>取绑定后的 POCO（泛型）。</summary>
        public T As<T>() where T : class
        {
            return Row as T;
        }
    }

    /// <summary>一张表的数据。</summary>
    public sealed class TableData
    {
        private readonly Dictionary<string, BoundRow> _byKey = new Dictionary<string, BoundRow>(StringComparer.Ordinal);

        /// <summary>表名。</summary>
        public string Name;

        /// <summary>表规格。</summary>
        public TableSchema Schema;

        /// <summary>全部行（保持源数据顺序）。</summary>
        public readonly List<BoundRow> Rows = new List<BoundRow>();

        /// <summary>加入一行（同时建主键索引；主键重复即报 error）。</summary>
        public bool TryAdd(BoundRow row, out BoundRow existing)
        {
            if (_byKey.TryGetValue(row.Key, out existing)) return false;
            _byKey[row.Key] = row;
            Rows.Add(row);
            return true;
        }

        /// <summary>主键是否存在。</summary>
        public bool Has(string key)
        {
            return _byKey.ContainsKey(key);
        }

        /// <summary>按主键取行。</summary>
        public BoundRow Find(string key)
        {
            BoundRow row;
            return _byKey.TryGetValue(key, out row) ? row : null;
        }

        /// <summary>全部主键。</summary>
        public IEnumerable<string> Keys => _byKey.Keys;
    }

    /// <summary>一次导表读进来的全部表。</summary>
    public sealed class TableSet
    {
        private readonly Dictionary<string, TableData> _tables = new Dictionary<string, TableData>(StringComparer.Ordinal);

        /// <summary>表名集合。</summary>
        public IEnumerable<string> Names => _tables.Keys;

        /// <summary>全部表。</summary>
        public IEnumerable<TableData> All => _tables.Values;

        /// <summary>登记一张表。</summary>
        public void Add(TableData table)
        {
            _tables[table.Name] = table;
        }

        /// <summary>取表（不存在返回 null）。</summary>
        public TableData Get(string name)
        {
            TableData table;
            return _tables.TryGetValue(name, out table) ? table : null;
        }

        /// <summary>表是否存在。</summary>
        public bool Has(string name)
        {
            return _tables.ContainsKey(name);
        }

        /// <summary>按类型取行序列。</summary>
        public IEnumerable<T> RowsOf<T>() where T : class
        {
            foreach (var table in _tables.Values)
            {
                foreach (var row in table.Rows)
                {
                    var typed = row.Row as T;
                    if (typed != null) yield return typed;
                }
            }
        }
    }

    /// <summary>
    /// 行绑定器：源 JSON → POCO。
    /// <para>
    /// 规则：① 源数据的键先归一化成 snake_case（<b>camelCase 兼容接受</b>，输出统一 snake_case）；
    /// ② 键在本表内重复（归一化后同名）即 error——那是两个真值源；
    /// ③ 类型不匹配即 error，不做静默转换；
    /// ④ 源数据里写了<b>派生列</b>的值会被记录下来，供派生步骤与重算值比对（禁手填）。
    /// </para>
    /// </summary>
    public static class RowBinder
    {
        /// <summary>绑定一行。</summary>
        public static BoundRow Bind(TableSchema schema, JsonValue source, ValidationReport report)
        {
            var row = new BoundRow();
            var normalized = new Dictionary<string, JsonValue>(StringComparer.Ordinal);
            var duplicateKeys = new List<string>();

            foreach (var key in source.Keys)
            {
                string norm = Json.ToSnakeCase(key);
                if (normalized.ContainsKey(norm))
                {
                    duplicateKeys.Add(key + " / " + norm);
                    continue;
                }
                normalized[norm] = source.Get(key);
            }

            foreach (var dup in duplicateKeys)
            {
                report.Error("BIND", schema.Name, null, null, "源数据里出现两个归一化后同名的键（两个真值源）：" + dup);
            }

            object instance = Activator.CreateInstance(schema.RowType);
            row.Row = instance;
            row.Source = normalized;

            foreach (var column in schema.Columns)
            {
                JsonValue value;
                bool present = normalized.TryGetValue(column.Name, out value);
                if (!present)
                {
                    if (column.Required)
                    {
                        report.Error("REQ", schema.Name, null, column.Name, "必填列缺失");
                    }
                    continue;
                }

                if (value == null || value.IsNull)
                {
                    if (column.Required && column.Type != ColumnType.StringArray &&
                        column.Type != ColumnType.IntArray && column.Type != ColumnType.ObjectArray &&
                        column.Type != ColumnType.KeyFloatArray)
                    {
                        report.Error("TYPE", schema.Name, null, column.Name, "必填列的值为 null");
                    }
                    continue;
                }

                FieldInfo field = schema.RowType.GetField(column.Name, BindingFlags.Public | BindingFlags.Instance);
                if (field == null)
                {
                    report.Error("BIND", schema.Name, null, column.Name, "规格里声明了列，但行类型没有同名字段");
                    continue;
                }

                if (!TryCoerce(value, field.FieldType, schema.Name, column.Name, report, out object coerced))
                {
                    continue;
                }

                field.SetValue(instance, coerced);
            }

            row.Key = BuildKey(schema, instance);
            return row;
        }

        private static string BuildKey(TableSchema schema, object instance)
        {
            var parts = new List<string>(schema.PrimaryKey.Length);
            foreach (var keyColumn in schema.PrimaryKey)
            {
                FieldInfo field = schema.RowType.GetField(keyColumn, BindingFlags.Public | BindingFlags.Instance);
                object value = field != null ? field.GetValue(instance) : null;
                parts.Add(value == null ? string.Empty : Convert.ToString(value, CultureInfo.InvariantCulture));
            }
            return string.Join("|", parts);
        }

        /// <summary>按目标类型转换一个 JSON 值。</summary>
        private static bool TryCoerce(
            JsonValue value,
            Type target,
            string table,
            string field,
            ValidationReport report,
            out object result)
        {
            result = null;

            if (target == typeof(string))
            {
                if (value.Kind != JsonKind.String)
                {
                    report.Error("TYPE", table, null, field, "期望字符串，实际是 " + value.Kind);
                    return false;
                }
                result = value.StringValue;
                return true;
            }

            if (target == typeof(int))
            {
                if (value.Kind != JsonKind.Number)
                {
                    report.Error("TYPE", table, null, field, "期望整数，实际是 " + value.Kind);
                    return false;
                }
                double raw = value.NumberValue;
                if (Math.Abs(raw - Math.Round(raw)) > 1e-9)
                {
                    report.Error("TYPE", table, null, field, "期望整数，实际是 " + raw.ToString("R", CultureInfo.InvariantCulture));
                    return false;
                }
                result = (int)Math.Round(raw);
                return true;
            }

            if (target == typeof(float))
            {
                if (value.Kind != JsonKind.Number)
                {
                    report.Error("TYPE", table, null, field, "期望数值，实际是 " + value.Kind);
                    return false;
                }
                result = (float)value.NumberValue;
                return true;
            }

            if (target == typeof(bool))
            {
                if (value.Kind != JsonKind.Bool)
                {
                    report.Error("TYPE", table, null, field, "期望布尔，实际是 " + value.Kind);
                    return false;
                }
                result = value.BoolValue;
                return true;
            }

            if (target == typeof(string[]))
            {
                if (value.Kind != JsonKind.Array)
                {
                    report.Error("TYPE", table, null, field, "期望数组，实际是 " + value.Kind);
                    return false;
                }
                var list = new List<string>(value.Items.Count);
                foreach (var item in value.Items)
                {
                    if (item.Kind != JsonKind.String)
                    {
                        report.Error("TYPE", table, null, field, "数组元素期望字符串，实际是 " + item.Kind);
                        return false;
                    }
                    list.Add(item.StringValue);
                }
                result = list.ToArray();
                return true;
            }

            if (target == typeof(int[]))
            {
                if (value.Kind != JsonKind.Array)
                {
                    report.Error("TYPE", table, null, field, "期望数组，实际是 " + value.Kind);
                    return false;
                }
                var list = new List<int>(value.Items.Count);
                foreach (var item in value.Items)
                {
                    if (item.Kind != JsonKind.Number)
                    {
                        report.Error("TYPE", table, null, field, "数组元素期望整数，实际是 " + item.Kind);
                        return false;
                    }
                    list.Add((int)Math.Round(item.NumberValue));
                }
                result = list.ToArray();
                return true;
            }

            if (target.IsArray)
            {
                if (value.Kind != JsonKind.Array)
                {
                    report.Error("TYPE", table, null, field, "期望数组，实际是 " + value.Kind);
                    return false;
                }
                Type elementType = target.GetElementType();
                Array array = Array.CreateInstance(elementType, value.Items.Count);
                for (int i = 0; i < value.Items.Count; i++)
                {
                    if (!TryCoerce(value.Items[i], elementType, table, field + "[" + i + "]", report, out object element))
                    {
                        return false;
                    }
                    array.SetValue(element, i);
                }
                result = array;
                return true;
            }

            if (target.IsClass)
            {
                if (value.Kind != JsonKind.Object)
                {
                    report.Error("TYPE", table, null, field, "期望对象，实际是 " + value.Kind);
                    return false;
                }
                object instance = Activator.CreateInstance(target);
                var members = new Dictionary<string, JsonValue>(StringComparer.Ordinal);
                foreach (var key in value.Keys)
                {
                    members[Json.ToSnakeCase(key)] = value.Get(key);
                }
                foreach (FieldInfo member in target.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    JsonValue memberValue;
                    if (!members.TryGetValue(Json.ToSnakeCase(member.Name), out memberValue)) continue;
                    if (memberValue == null || memberValue.IsNull) continue;
                    if (!TryCoerce(memberValue, member.FieldType, table, field + "." + member.Name, report, out object coerced))
                    {
                        continue;
                    }
                    member.SetValue(instance, coerced);
                }
                result = instance;
                return true;
            }

            report.Error("TYPE", table, null, field, "不支持的目标类型：" + target.Name);
            return false;
        }
    }
}
