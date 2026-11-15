using System;
using System.Globalization;
using System.Text;

namespace EdgeChronicles.Core.Diagnostics
{
    /// <summary>
    /// 战斗日志记录（一行 = 一条记录）。
    /// <para>
    /// 四件套的载体：① 全局帧号（同源）② 双时间戳（<c>state_frame</c> C 轴 + <c>wallclock_frame</c> W 轴）
    /// ③ <c>event=hit</c> 带 <c>hit_frame_id</c> ④ 逐帧位移向量。
    /// </para>
    /// <para>
    /// 字段口径：
    /// <list type="bullet">
    /// <item><c>frame_id</c>：全局帧号，<b>同源编号</b>——日志行与截图文件名取同一个值。</item>
    /// <item><c>state_frame</c> / <c>wallclock_frame</c>：两轴帧号，<b>只作标识</b>。</item>
    /// <item><c>t_state_s</c> / <c>t_wall_s</c>：两轴 elapsed 秒，<b>时间量</b>，差值可参与算术。</item>
    /// <item><c>hit_frame_id</c>：命中帧的全局帧号（标识用）。命中事件必填。</item>
    /// <item><c>dx_cm</c> / <c>dy_cm</c>：本帧位移向量（厘米）。</item>
    /// <item><c>speed_m_s</c>：由位移向量与 <c>dt_state_s</c> 推出的速率（m/s）。
    /// 规格要求速度一律 m/s，不写 m/帧（30fps 下 m/帧 会直接翻倍）。</item>
    /// </list>
    /// </para>
    /// </summary>
    public readonly struct CombatLogRecord
    {
        /// <summary>全局帧号（同源编号）。</summary>
        public readonly FrameId Id;

        /// <summary>帧时刻快照（两轴帧号 + 两轴时间量）。</summary>
        public readonly FrameStamp Stamp;

        /// <summary>事件名（<c>hit</c> / <c>move</c> / <c>phase</c> / <c>state</c> …）。</summary>
        public readonly string Event;

        /// <summary>事件主体 id（对象标识用）。</summary>
        public readonly string SubjectId;

        /// <summary>命中帧的全局帧号；非命中事件为 <see cref="FrameId.None"/>。</summary>
        public readonly FrameId HitFrameId;

        /// <summary>主体逻辑坐标（厘米）。</summary>
        public readonly Vec2 Position;

        /// <summary>本帧位移向量（厘米）。</summary>
        public readonly Vec2 Delta;

        /// <summary>本帧在 C 轴上的时长（秒，elapsed 时间量）。</summary>
        public readonly double DeltaSeconds;

        /// <summary>整数量（伤害、层数、帧数…）。</summary>
        public readonly int Amount;

        /// <summary>标量（倍率、系数…）。</summary>
        public readonly float Scalar;

        /// <summary>备注（可空）。</summary>
        public readonly string Note;

        public CombatLogRecord(
            FrameId id,
            in FrameStamp stamp,
            string eventName,
            string subjectId,
            FrameId hitFrameId,
            in Vec2 position,
            in Vec2 delta,
            double deltaSeconds,
            int amount,
            float scalar,
            string note)
        {
            Id = id;
            Stamp = stamp;
            Event = eventName;
            SubjectId = subjectId;
            HitFrameId = hitFrameId;
            Position = position;
            Delta = delta;
            DeltaSeconds = deltaSeconds;
            Amount = amount;
            Scalar = scalar;
            Note = note;
        }

        /// <summary>是否为命中事件（<c>event=hit</c>）。</summary>
        public bool IsHit => string.Equals(Event, "hit", StringComparison.Ordinal);
    }

    /// <summary>日志落地接口（表现层 / 存档层 / 文件层各自实现）。</summary>
    public interface ICombatLogSink
    {
        /// <summary>写一条记录。</summary>
        void Write(in CombatLogRecord record);

        /// <summary>冲刷缓冲（崩溃前必须调用）。</summary>
        void Flush();
    }

    /// <summary>
    /// 日志格式化：<b>统一键序的 key=value 单行格式</b>。
    /// <para>
    /// 为什么必须统一：GDD W2 验收项写明「9 条纯脚本走查项要能读出数——日志口径不统一，
    /// 9 条判据就会各自读各自的口径，等于又造出 9 个真值源」。因此这里固定键序，
    /// 任何读日志的脚本都按同一套键名解析。
    /// </para>
    /// </summary>
    public static class CombatLogFormatter
    {
        /// <summary>把一条记录格式化为单行文本（不含换行）。</summary>
        public static string Format(in CombatLogRecord record)
        {
            var sb = new StringBuilder(256);
            Append(sb, record);
            return sb.ToString();
        }

        /// <summary>把一条记录追加到缓冲区（不含换行）。</summary>
        public static void Append(StringBuilder sb, in CombatLogRecord record)
        {
            sb.Append("frame_id=").Append(record.Id.ToToken());
            sb.Append(" state_frame=").Append(record.Stamp.StateFrame.ToString(CultureInfo.InvariantCulture));
            sb.Append(" wallclock_frame=").Append(record.Stamp.WallclockFrame.ToString(CultureInfo.InvariantCulture));
            sb.Append(" t_state_s=").Append(FormatSeconds(record.Stamp.StateSeconds));
            sb.Append(" t_wall_s=").Append(FormatSeconds(record.Stamp.WallclockSeconds));
            sb.Append(" event=").Append(record.Event ?? string.Empty);
            sb.Append(" subject=").Append(record.SubjectId ?? string.Empty);
            sb.Append(" hit_frame_id=").Append(record.HitFrameId.IsValid ? record.HitFrameId.ToToken() : "-");
            sb.Append(" x_cm=").Append(record.Position.X.ToString(CultureInfo.InvariantCulture));
            sb.Append(" y_cm=").Append(record.Position.Y.ToString(CultureInfo.InvariantCulture));
            sb.Append(" dx_cm=").Append(record.Delta.X.ToString(CultureInfo.InvariantCulture));
            sb.Append(" dy_cm=").Append(record.Delta.Y.ToString(CultureInfo.InvariantCulture));
            sb.Append(" dt_state_s=").Append(FormatSeconds(record.DeltaSeconds));
            sb.Append(" speed_m_s=").Append(FormatSpeed(record.Delta, record.DeltaSeconds));
            sb.Append(" amount=").Append(record.Amount.ToString(CultureInfo.InvariantCulture));
            sb.Append(" scalar=").Append(record.Scalar.ToString("R", CultureInfo.InvariantCulture));
            sb.Append(" note=").Append(record.Note ?? string.Empty);
        }

        private static string FormatSeconds(double seconds)
        {
            return seconds.ToString("F4", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 由位移向量与 C 轴时长换算速率（m/s）。
        /// <b>禁止输出 m/帧</b>：规格固定常量错例表第 5 条——击退速度写 m/帧 在 30fps 下直接翻倍。
        /// </summary>
        private static string FormatSpeed(in Vec2 delta, double deltaSeconds)
        {
            if (deltaSeconds <= 0d) return "0.0000";

            double meters = delta.LengthSquared <= 0
                ? 0d
                : Math.Sqrt(delta.LengthSquared) / Vec2.UnitsPerMeter;
            double speed = meters / deltaSeconds;
            return speed.ToString("F4", CultureInfo.InvariantCulture);
        }
    }
}
