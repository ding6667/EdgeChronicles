using System;

namespace EdgeChronicles.Data.Schema
{
    /// <summary>列的数据类型。</summary>
    public enum ColumnType
    {
        /// <summary>文本。</summary>
        String = 0,
        /// <summary>整数。</summary>
        Int = 1,
        /// <summary>浮点。</summary>
        Float = 2,
        /// <summary>布尔。</summary>
        Bool = 3,
        /// <summary>文本数组。</summary>
        StringArray = 4,
        /// <summary>整数数组。</summary>
        IntArray = 5,
        /// <summary>嵌套对象数组。</summary>
        ObjectArray = 6,
        /// <summary>嵌套对象（单数）。</summary>
        Object = 7,
        /// <summary>键 → 值 数组（<c>{key, value}</c>）。</summary>
        KeyFloatArray = 8
    }

    /// <summary>
    /// 一列的规格：类型、必填、值域、枚举、外键、是否派生。
    /// <para>
    /// 派生列（<see cref="Derived"/> = true）= <b>禁手填</b>：导表时由公式覆盖，
    /// 源数据里若存在手填值且与重算值不等，即报 error（派生值不得成为第二真值源）。
    /// </para>
    /// </summary>
    public sealed class ColumnSpec
    {
        /// <summary>列名（snake_case）。</summary>
        public string Name;

        /// <summary>类型。</summary>
        public ColumnType Type;

        /// <summary>是否必填。</summary>
        public bool Required;

        /// <summary>数值下界（含）。</summary>
        public double? Min;

        /// <summary>数值上界（含）。</summary>
        public double? Max;

        /// <summary>合法枚举值（文本列）。</summary>
        public string[] Allowed;

        /// <summary>外键目标表。</summary>
        public string ForeignTable;

        /// <summary>外键目标列（默认主键第一列）。</summary>
        public string ForeignColumn;

        /// <summary>是否派生列（禁手填）。</summary>
        public bool Derived;

        /// <summary>
        /// 值域/枚举的<b>生效条件</b>：本列只有在「<see cref="ConditionalField"/> 满足条件」时才参与校验。
        /// <para>
        /// 条件列（如 <c>fame_unlock_tier</c> 只对 kind=fame 的行有意义）在不适用的行上取 0 / 空串，
        /// 若一律按值域判定，会把「这一行本来就不适用」判成越界。
        /// </para>
        /// </summary>
        public string ConditionalField;

        /// <summary>条件值：非空 = 该字段必须等于它；为空 = 该字段必须非空。</summary>
        public string ConditionalValue;

        /// <summary>备注（口径说明）。</summary>
        public string Note;

        public ColumnSpec(
            string name,
            ColumnType type,
            bool required = false,
            double? min = null,
            double? max = null,
            string[] allowed = null,
            string foreignTable = null,
            string foreignColumn = null,
            bool derived = false,
            string note = null,
            string conditionalField = null,
            string conditionalValue = null)
        {
            ConditionalField = conditionalField;
            ConditionalValue = conditionalValue;
            Name = name;
            Type = type;
            Required = required;
            Min = min;
            Max = max;
            Allowed = allowed;
            ForeignTable = foreignTable;
            ForeignColumn = foreignColumn;
            Derived = derived;
            Note = note;
        }
    }

    /// <summary>一张表的规格。</summary>
    public sealed class TableSchema
    {
        /// <summary>表名。</summary>
        public string Name;

        /// <summary>行类型。</summary>
        public Type RowType;

        /// <summary>主键列（按顺序）。</summary>
        public string[] PrimaryKey;

        /// <summary>列规格。</summary>
        public ColumnSpec[] Columns;

        /// <summary>
        /// 是否为「20 张规格表」之外的扩展表。
        /// <para>
        /// EL-03 §1.8 明确 <c>affix_pool</c> 与 v3.0 §4 明确 <c>equipment_base</c> 存在并被断言引用，
        /// 但 IMPL-v2.0 §5 的 13 张表 + IMPL-v3.0 §7 的 7 张新表 = 20 张里没有它们。
        /// 扩展表计入审计但<b>不计入 20 张规格表计数</b>（CI 判据里的 <c>tables=20</c> 保持原义）。
        /// </para>
        /// </summary>
        public bool Auxiliary;

        public TableSchema(string name, Type rowType, bool auxiliary, string[] primaryKey, params ColumnSpec[] columns)
        {
            Name = name;
            RowType = rowType;
            Auxiliary = auxiliary;
            PrimaryKey = primaryKey;
            Columns = columns;
        }

        /// <summary>取列规格。</summary>
        public ColumnSpec Column(string name)
        {
            for (int i = 0; i < Columns.Length; i++)
            {
                if (string.Equals(Columns[i].Name, name, StringComparison.Ordinal)) return Columns[i];
            }
            return null;
        }
    }
}
