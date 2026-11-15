using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using EdgeChronicles.Data.Schema;
using EdgeChronicles.Data.Serialization;
using EdgeChronicles.Data.Validation;

namespace EdgeChronicles.Tools.Editor.Import
{
    /// <summary>
    /// 生成物写出：把绑定后的行序列化成 <b>统一 snake_case</b> 的 JSON。
    /// <para>
    /// 源数据可以是 camelCase（导表器兼容接受），**输出一律 snake_case**——
    /// 同一个字段出现两种写法就是两个真值源。
    /// </para>
    /// </summary>
    public static class GeneratedWriter
    {
        /// <summary>写出全部表。</summary>
        public static void Write(TableSet tables, string outputDir)
        {
            if (string.IsNullOrEmpty(outputDir)) return;
            if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

            foreach (var table in tables.All)
            {
                var root = JsonValue.Object();
                root.Set("table", JsonValue.Text(table.Name));

                var rows = JsonValue.Array();
                foreach (var bound in table.Rows)
                {
                    rows.Add(RowToJson(table, bound));
                }
                root.Set("rows", rows);

                string path = Path.Combine(outputDir, table.Name + ".json");
                File.WriteAllText(path, Json.Write(root, true));
            }
        }

        private static JsonValue RowToJson(TableData table, BoundRow bound)
        {
            var node = JsonValue.Object();
            foreach (var column in table.Schema.Columns)
            {
                var field = table.Schema.RowType.GetField(column.Name, BindingFlags.Public | BindingFlags.Instance);
                if (field == null) continue;

                var value = field.GetValue(bound.Row);
                node.Set(column.Name, ToJson(value));
            }
            return node;
        }

        private static JsonValue ToJson(object value)
        {
            if (value == null) return JsonValue.Null();

            if (value is string text) return JsonValue.Text(text);
            if (value is int intValue) return JsonValue.Int(intValue);
            if (value is float floatValue) return JsonValue.Number(floatValue);
            if (value is bool boolValue) return JsonValue.Bool(boolValue);

            if (value is string[] strings)
            {
                var array = JsonValue.Array();
                foreach (var item in strings) array.Add(JsonValue.Text(item ?? string.Empty));
                return array;
            }

            if (value is int[] ints)
            {
                var array = JsonValue.Array();
                foreach (var item in ints) array.Add(JsonValue.Int(item));
                return array;
            }

            if (value is IEnumerable enumerable)
            {
                var array = JsonValue.Array();
                foreach (var item in enumerable)
                {
                    array.Add(ToJson(item));
                }
                return array;
            }

            // 嵌套对象：按字段名递归
            var obj = JsonValue.Object();
            foreach (var field in value.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                obj.Set(field.Name, ToJson(field.GetValue(value)));
            }
            return obj;
        }
    }
}
