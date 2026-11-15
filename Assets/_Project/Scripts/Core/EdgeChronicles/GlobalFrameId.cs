using System;
using System.Globalization;
using System.Threading;

namespace EdgeChronicles.Core
{
    /// <summary>
    /// 全局帧号：全局唯一、单调递增的<b>标识符</b>。
    /// <para>
    /// 它解决的问题：<b>日志与截图必须同源取值</b>。同一段战斗的日志行与截图文件
    /// 各自编号会出现「日志第 37 行 ≠ 第 37 张图」的错位，事后无法交叉比对。
    /// 本类给出唯一编号源：日志文件名、截图文件名、日志行内的 <c>frame_id</c> 全部取自它。
    /// </para>
    /// <para>
    /// 治理约束：<b>帧号只用于标识对象，禁止参与算术</b>（GDD 治理规则 10）。
    /// 需要「经过了多久」时，取 <see cref="LogicalClock"/> 的<b>时间量</b>，不取帧号差。
    /// </para>
    /// </summary>
    public readonly struct FrameId : IEquatable<FrameId>, IComparable<FrameId>
    {
        /// <summary>无效帧号（未赋值的场合）。</summary>
        public static readonly FrameId None = new FrameId(0L);

        /// <summary>帧号值（≥1 为有效）。</summary>
        public readonly long Value;

        public FrameId(long value)
        {
            Value = value;
        }

        /// <summary>是否有效（非 <see cref="None"/>）。</summary>
        public bool IsValid => Value > 0;

        /// <summary>固定宽度文本形式（<c>F000000123</c>），供文件名与日志行共用。</summary>
        public string ToToken()
        {
            return "F" + Value.ToString("D9", CultureInfo.InvariantCulture);
        }

        public bool Equals(FrameId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is FrameId other && Equals(other);

        public override int GetHashCode() => Value.GetHashCode();

        public int CompareTo(FrameId other) => Value.CompareTo(other.Value);

        public override string ToString() => ToToken();
    }

    /// <summary>
    /// 全局帧号源（单例）。线程安全：编号用原子自增，跨线程取号不会重复。
    /// </summary>
    public sealed class GlobalFrameId
    {
        private static readonly object InitLock = new object();
        private static GlobalFrameId s_instance;

        private long _next;

        private GlobalFrameId()
        {
            _next = 1;
        }

        /// <summary>全局唯一实例。</summary>
        public static GlobalFrameId Instance
        {
            get
            {
                if (s_instance != null) return s_instance;
                lock (InitLock)
                {
                    if (s_instance == null) s_instance = new GlobalFrameId();
                    return s_instance;
                }
            }
        }

        /// <summary>下一个将被分配的帧号值（不消耗）。</summary>
        public long PeekNext => Interlocked.Read(ref _next);

        /// <summary>取下一个全局帧号。</summary>
        public FrameId Next()
        {
            long value = Interlocked.Increment(ref _next) - 1;
            return new FrameId(value);
        }

        /// <summary>
        /// 重置编号（仅在「一段全新录制 / 一个新会话」开始时调用）。
        /// 录制中途调用会让此前的日志与截图失去可比性。
        /// </summary>
        public void Reset()
        {
            Interlocked.Exchange(ref _next, 1);
        }
    }
}
