using System;

namespace EdgeChronicles.Core.Diagnostics
{
    /// <summary>
    /// 日志门面：唯一写日志的入口。
    /// <para>
    /// 它把「同源」这件事钉死：<b>全局帧号只在这里申请一次</b>，
    /// 日志行里的 <c>frame_id</c> 与截图文件名里的帧号必然同值。
    /// 任何其他模块想自己编帧号，都会在这里被挡住（没有公开的「自造编号」入口）。
    /// </para>
    /// </summary>
    public static class CombatLog
    {
        private static ICombatLogSink _sink;

        /// <summary>当前挂载的落地实现（未挂载时为 null）。</summary>
        public static ICombatLogSink Sink => _sink;

        /// <summary>挂载落地实现（替换旧的）。</summary>
        public static void Attach(ICombatLogSink sink)
        {
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        }

        /// <summary>摘除落地实现（先冲刷）。</summary>
        public static void Detach()
        {
            _sink?.Flush();
            _sink = null;
        }

        /// <summary>取下一个全局帧号（截图与日志共用同一编号源）。</summary>
        public static FrameId NextFrameId()
        {
            return GlobalFrameId.Instance.Next();
        }

        /// <summary>写一条记录。无 sink 时静默丢弃（不抛异常，避免日志问题影响战斗）。</summary>
        public static void Emit(in CombatLogRecord record)
        {
            _sink?.Write(record);
        }

        /// <summary>
        /// 构造并写一条记录。调用方只提供业务字段，帧号与时间戳由门面统一取，
        /// 保证「同源 + 双时间戳」不被漏掉。
        /// </summary>
        public static void Emit(
            in FrameStamp stamp,
            string eventName,
            string subjectId,
            FrameId hitFrameId,
            in Vec2 position,
            in Vec2 delta,
            double deltaSeconds,
            int amount = 0,
            float scalar = 0f,
            string note = null)
        {
            if (_sink == null) return;

            var record = new CombatLogRecord(
                stamp.Id,
                stamp,
                eventName,
                subjectId,
                hitFrameId,
                position,
                delta,
                deltaSeconds,
                amount,
                scalar,
                note);
            _sink.Write(record);
        }
    }
}
