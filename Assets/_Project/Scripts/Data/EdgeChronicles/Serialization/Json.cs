using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace EdgeChronicles.Data.Serialization
{
    /// <summary>JSON 值类型。</summary>
    public enum JsonKind
    {
        /// <summary>空值。</summary>
        Null = 0,
        /// <summary>布尔。</summary>
        Bool = 1,
        /// <summary>数字。</summary>
        Number = 2,
        /// <summary>字符串。</summary>
        String = 3,
        /// <summary>数组。</summary>
        Array = 4,
        /// <summary>对象。</summary>
        Object = 5
    }

    /// <summary>JSON 树节点。</summary>
    public sealed class JsonValue
    {
        private readonly Dictionary<string, JsonValue> _members;
        private readonly List<string> _keys;
        private readonly List<JsonValue> _items;

        private JsonValue(JsonKind kind)
        {
            Kind = kind;
            if (kind == JsonKind.Object)
            {
                _members = new Dictionary<string, JsonValue>(StringComparer.Ordinal);
                _keys = new List<string>();
            }
            else if (kind == JsonKind.Array)
            {
                _items = new List<JsonValue>();
            }
        }

        /// <summary>值类型。</summary>
        public JsonKind Kind { get; }

        /// <summary>布尔值（仅 <see cref="JsonKind.Bool"/>）。</summary>
        public bool BoolValue { get; private set; }

        /// <summary>数值（仅 <see cref="JsonKind.Number"/>）。</summary>
        public double NumberValue { get; private set; }

        /// <summary>文本（仅 <see cref="JsonKind.String"/>）。</summary>
        public string StringValue { get; private set; }

        /// <summary>数组元素（仅 <see cref="JsonKind.Array"/>）。</summary>
        public IReadOnlyList<JsonValue> Items => _items;

        /// <summary>对象成员名（保持写入顺序）。</summary>
        public IReadOnlyList<string> Keys => _keys;

        /// <summary>创建空值。</summary>
        public static JsonValue Null()
        {
            return new JsonValue(JsonKind.Null);
        }

        /// <summary>创建布尔值。</summary>
        public static JsonValue Bool(bool value)
        {
            var node = new JsonValue(JsonKind.Bool);
            node.BoolValue = value;
            return node;
        }

        /// <summary>创建数值。</summary>
        public static JsonValue Number(double value)
        {
            var node = new JsonValue(JsonKind.Number);
            node.NumberValue = value;
            return node;
        }

        /// <summary>创建整数值。</summary>
        public static JsonValue Int(int value)
        {
            return Number(value);
        }

        /// <summary>创建字符串。</summary>
        public static JsonValue Text(string value)
        {
            var node = new JsonValue(JsonKind.String);
            node.StringValue = value ?? string.Empty;
            return node;
        }

        /// <summary>创建数组。</summary>
        public static JsonValue Array()
        {
            return new JsonValue(JsonKind.Array);
        }

        /// <summary>创建对象。</summary>
        public static JsonValue Object()
        {
            return new JsonValue(JsonKind.Object);
        }

        /// <summary>取成员（不存在返回 null）。</summary>
        public JsonValue Get(string key)
        {
            if (_members == null || key == null) return null;
            return _members.TryGetValue(key, out var value) ? value : null;
        }

        /// <summary>写入成员（同名覆盖，保留首次写入的位置）。</summary>
        public void Set(string key, JsonValue value)
        {
            if (_members == null) throw new InvalidOperationException("只有对象节点支持 Set");
            if (!_members.ContainsKey(key)) _keys.Add(key);
            _members[key] = value ?? Null();
        }

        /// <summary>追加数组元素。</summary>
        public void Add(JsonValue value)
        {
            if (_items == null) throw new InvalidOperationException("只有数组节点支持 Add");
            _items.Add(value ?? Null());
        }

        /// <summary>是否为 null / 空。</summary>
        public bool IsNull => Kind == JsonKind.Null;
    }

    /// <summary>JSON 解析异常。</summary>
    public sealed class JsonParseException : Exception
    {
        public JsonParseException(string message, int position)
            : base($"{message}（位置 {position}）")
        {
            Position = position;
        }

        /// <summary>出错位置（字符下标）。</summary>
        public int Position { get; }
    }

    /// <summary>
    /// 极简 JSON 读写器（纯 C#，不依赖 UnityEngine，也不引入第三方 JSON 包）。
    /// <para>
    /// 为什么不用 <c>JsonUtility</c>：它不支持顶层数组、不支持字典、字段名必须与类型逐字对应，
    /// 无法承载「一张表 = 一个行数组」与「键 → 值 集」这两种规格里明确出现的数据形态。
    /// 存档序列化仍按 TOOLCHAIN C5 用内建 <c>JsonUtility</c>，本类只服务配表管线。
    /// </para>
    /// </summary>
    public static class Json
    {
        /// <summary>解析一段 JSON 文本。</summary>
        public static JsonValue Parse(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            int index = 0;
            SkipWhitespace(text, ref index);
            var value = ParseValue(text, ref index);
            SkipWhitespace(text, ref index);
            if (index != text.Length)
            {
                throw new JsonParseException("JSON 结束后仍有残留字符", index);
            }
            return value;
        }

        /// <summary>序列化为缩进文本（键序 = 写入序）。</summary>
        public static string Write(JsonValue value, bool indented = true)
        {
            var sb = new StringBuilder(1024);
            WriteValue(sb, value, indented, 0);
            if (indented) sb.Append('\n');
            return sb.ToString();
        }

        /// <summary>
        /// camelCase → snake_case 归一化。
        /// <para>
        /// 导表器「兼容接受既有 13 表的 camelCase、输出统一 snake_case」用这条规则实现：
        /// <c>animClip → anim_clip</c>、<c>nodeA → node_a</c>、<c>hitShapeRef → hit_shape_ref</c>。
        /// 已经是 snake_case 的名字原样返回。
        /// </para>
        /// </summary>
        public static string ToSnakeCase(string name)
        {
            if (string.IsNullOrEmpty(name)) return name ?? string.Empty;
            if (name.IndexOf('_') >= 0) return name;

            var sb = new StringBuilder(name.Length + 8);
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (char.IsUpper(c))
                {
                    if (i > 0) sb.Append('_');
                    sb.Append(char.ToLowerInvariant(c));
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        private static JsonValue ParseValue(string text, ref int index)
        {
            if (index >= text.Length) throw new JsonParseException("内容意外结束", index);
            char c = text[index];
            switch (c)
            {
                case '{': return ParseObject(text, ref index);
                case '[': return ParseArray(text, ref index);
                case '"': return JsonValue.Text(ParseString(text, ref index));
                case 't': Expect(text, ref index, "true"); return JsonValue.Bool(true);
                case 'f': Expect(text, ref index, "false"); return JsonValue.Bool(false);
                case 'n': Expect(text, ref index, "null"); return JsonValue.Null();
                default: return ParseNumber(text, ref index);
            }
        }

        private static JsonValue ParseObject(string text, ref int index)
        {
            var result = JsonValue.Object();
            index++; // '{'
            SkipWhitespace(text, ref index);
            if (index < text.Length && text[index] == '}') { index++; return result; }

            while (true)
            {
                SkipWhitespace(text, ref index);
                if (index >= text.Length || text[index] != '"')
                {
                    throw new JsonParseException("对象的键必须是字符串", index);
                }

                string key = ParseString(text, ref index);
                SkipWhitespace(text, ref index);
                if (index >= text.Length || text[index] != ':')
                {
                    throw new JsonParseException($"键「{key}」后缺少 ':'", index);
                }
                index++;

                SkipWhitespace(text, ref index);
                result.Set(key, ParseValue(text, ref index));
                SkipWhitespace(text, ref index);

                if (index >= text.Length) throw new JsonParseException("对象未闭合", index);
                if (text[index] == ',') { index++; continue; }
                if (text[index] == '}') { index++; return result; }
                throw new JsonParseException("对象中期望 ',' 或 '}'", index);
            }
        }

        private static JsonValue ParseArray(string text, ref int index)
        {
            var result = JsonValue.Array();
            index++; // '['
            SkipWhitespace(text, ref index);
            if (index < text.Length && text[index] == ']') { index++; return result; }

            while (true)
            {
                SkipWhitespace(text, ref index);
                result.Add(ParseValue(text, ref index));
                SkipWhitespace(text, ref index);

                if (index >= text.Length) throw new JsonParseException("数组未闭合", index);
                if (text[index] == ',') { index++; continue; }
                if (text[index] == ']') { index++; return result; }
                throw new JsonParseException("数组中期望 ',' 或 ']'", index);
            }
        }

        private static string ParseString(string text, ref int index)
        {
            index++; // opening quote
            var sb = new StringBuilder();
            while (true)
            {
                if (index >= text.Length) throw new JsonParseException("字符串未闭合", index);
                char c = text[index++];
                if (c == '"') return sb.ToString();

                if (c != '\\')
                {
                    sb.Append(c);
                    continue;
                }

                if (index >= text.Length) throw new JsonParseException("转义序列未结束", index);
                char esc = text[index++];
                switch (esc)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (index + 4 > text.Length) throw new JsonParseException("\\u 转义不完整", index);
                        int code = int.Parse(text.Substring(index, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                        sb.Append((char)code);
                        index += 4;
                        break;
                    default:
                        throw new JsonParseException($"非法转义 \\{esc}", index);
                }
            }
        }

        private static JsonValue ParseNumber(string text, ref int index)
        {
            int start = index;
            if (index < text.Length && (text[index] == '-' || text[index] == '+')) index++;
            while (index < text.Length && (char.IsDigit(text[index]) || text[index] == '.' ||
                                          text[index] == 'e' || text[index] == 'E' ||
                                          ((text[index] == '-' || text[index] == '+') && (text[index - 1] == 'e' || text[index - 1] == 'E'))))
            {
                index++;
            }

            string slice = text.Substring(start, index - start);
            if (!double.TryParse(slice, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            {
                throw new JsonParseException($"非法数字「{slice}」", start);
            }
            return JsonValue.Number(value);
        }

        private static void Expect(string text, ref int index, string literal)
        {
            if (index + literal.Length > text.Length ||
                string.CompareOrdinal(text, index, literal, 0, literal.Length) != 0)
            {
                throw new JsonParseException($"期望字面量 {literal}", index);
            }
            index += literal.Length;
        }

        private static void SkipWhitespace(string text, ref int index)
        {
            while (index < text.Length)
            {
                char c = text[index];
                if (c == ' ' || c == '\t' || c == '\r' || c == '\n') index++;
                else break;
            }
        }

        private static void WriteValue(StringBuilder sb, JsonValue value, bool indented, int depth)
        {
            switch (value.Kind)
            {
                case JsonKind.Null:
                    sb.Append("null");
                    break;
                case JsonKind.Bool:
                    sb.Append(value.BoolValue ? "true" : "false");
                    break;
                case JsonKind.Number:
                    WriteNumber(sb, value.NumberValue);
                    break;
                case JsonKind.String:
                    WriteString(sb, value.StringValue);
                    break;
                case JsonKind.Array:
                    WriteArray(sb, value, indented, depth);
                    break;
                case JsonKind.Object:
                    WriteObject(sb, value, indented, depth);
                    break;
            }
        }

        private static void WriteNumber(StringBuilder sb, double value)
        {
            if (Math.Abs(value - Math.Round(value)) < 1e-9 && Math.Abs(value) < 1e15)
            {
                sb.Append(((long)Math.Round(value)).ToString(CultureInfo.InvariantCulture));
                return;
            }
            sb.Append(value.ToString("R", CultureInfo.InvariantCulture));
        }

        private static void WriteString(StringBuilder sb, string value)
        {
            sb.Append('"');
            foreach (char c in value ?? string.Empty)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        private static void WriteArray(StringBuilder sb, JsonValue value, bool indented, int depth)
        {
            var items = value.Items;
            if (items.Count == 0) { sb.Append("[]"); return; }

            sb.Append('[');
            for (int i = 0; i < items.Count; i++)
            {
                if (i > 0) sb.Append(',');
                if (indented) { sb.Append('\n'); Indent(sb, depth + 1); }
                WriteValue(sb, items[i], indented, depth + 1);
            }
            if (indented) { sb.Append('\n'); Indent(sb, depth); }
            sb.Append(']');
        }

        private static void WriteObject(StringBuilder sb, JsonValue value, bool indented, int depth)
        {
            var keys = value.Keys;
            if (keys.Count == 0) { sb.Append("{}"); return; }

            sb.Append('{');
            for (int i = 0; i < keys.Count; i++)
            {
                if (i > 0) sb.Append(',');
                if (indented) { sb.Append('\n'); Indent(sb, depth + 1); }
                WriteString(sb, keys[i]);
                sb.Append(':');
                if (indented) sb.Append(' ');
                WriteValue(sb, value.Get(keys[i]), indented, depth + 1);
            }
            if (indented) { sb.Append('\n'); Indent(sb, depth); }
            sb.Append('}');
        }

        private static void Indent(StringBuilder sb, int depth)
        {
            for (int i = 0; i < depth; i++) sb.Append("  ");
        }
    }
}
