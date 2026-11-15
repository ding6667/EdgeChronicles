namespace EdgeChronicles.Core
{
    /// <summary>
    /// 时钟轴标签。
    /// <para>
    /// <b>C 轴</b>（Logical）：逻辑 / 战斗轴，固定 60 Hz。战斗的一切帧语义都在这条轴上。
    /// <b>W 轴</b>（Wallclock）：墙钟 / 渲染轴，真实时间。真实耗时与输入延迟在这条轴上。
    /// </para>
    /// <para>
    /// 治理约束：<b>两根轴永不互相换算、绝不互相相减</b>（GDD 治理规则 9：任何判据必须声明量纲与时钟轴）。
    /// 日志与存档里两轴必须<b>分字段标注</b>，不合并成一个「帧」。
    /// </para>
    /// </summary>
    public enum ClockAxis
    {
        /// <summary>C 轴：逻辑 / 战斗帧。</summary>
        Logical = 0,

        /// <summary>W 轴：墙钟 / 渲染帧。</summary>
        Wallclock = 1
    }

    /// <summary>时钟轴的短标签，写日志与文件名时用（C / W）。</summary>
    public static class ClockAxisLabel
    {
        /// <summary>C 轴标签。</summary>
        public const string Logical = "C";

        /// <summary>W 轴标签。</summary>
        public const string Wallclock = "W";

        /// <summary>取轴标签。</summary>
        public static string Of(ClockAxis axis)
        {
            return axis == ClockAxis.Logical ? Logical : Wallclock;
        }
    }

    /// <summary>
    /// 一帧的<b>时刻快照</b>：同时携带两轴的帧号与时间量。
    /// <para>
    /// 帧号（<see cref="StateFrame"/> / <see cref="WallclockFrame"/>）<b>只作标识与排序</b>；
    /// 时间量（<see cref="StateSeconds"/> / <see cref="WallclockSeconds"/>）才可参与算术。
    /// 要表达「从命中帧起过了多久」，用 <see cref="ElapsedSince"/>，它内部相减的是<b>时间量</b>，
    /// 不是帧号 —— 帧号自带「0/1 起点、含不含首尾」两层约定，拿它做差必错 1。
    /// </para>
    /// </summary>
    public readonly struct FrameStamp
    {
        /// <summary>全局帧号（同源编号源）。</summary>
        public readonly FrameId Id;

        /// <summary>C 轴逻辑帧序号（标识用）。</summary>
        public readonly long StateFrame;

        /// <summary>C 轴 elapsed 秒（时间量）。</summary>
        public readonly double StateSeconds;

        /// <summary>W 轴墙钟帧序号（标识用）。</summary>
        public readonly long WallclockFrame;

        /// <summary>W 轴 elapsed 秒（时间量）。</summary>
        public readonly double WallclockSeconds;

        public FrameStamp(
            FrameId id,
            long stateFrame,
            double stateSeconds,
            long wallclockFrame,
            double wallclockSeconds)
        {
            Id = id;
            StateFrame = stateFrame;
            StateSeconds = stateSeconds;
            WallclockFrame = wallclockFrame;
            WallclockSeconds = wallclockSeconds;
        }

        /// <summary>
        /// 相对某个起点时刻的 elapsed 时间（<c>T_xxx</c>）。
        /// 相减的是<b>时间量</b>（秒），不涉及帧号算术。
        /// </summary>
        public ElapsedTimes ElapsedSince(in FrameStamp origin)
        {
            return new ElapsedTimes(StateSeconds - origin.StateSeconds, WallclockSeconds - origin.WallclockSeconds);
        }
    }

    /// <summary>
    /// elapsed 时刻对（<c>T_xxx</c>）。两轴分开，禁止跨轴相减。
    /// </summary>
    public readonly struct ElapsedTimes
    {
        /// <summary>C 轴 elapsed 秒。</summary>
        public readonly double StateSeconds;

        /// <summary>W 轴 elapsed 秒。</summary>
        public readonly double WallclockSeconds;

        public ElapsedTimes(double stateSeconds, double wallclockSeconds)
        {
            StateSeconds = stateSeconds;
            WallclockSeconds = wallclockSeconds;
        }

        /// <summary>是否为负（负 elapsed 说明起点晚于当前点，通常意味着取错了轴）。</summary>
        public bool IsNegative => StateSeconds < 0d || WallclockSeconds < 0d;
    }
}
