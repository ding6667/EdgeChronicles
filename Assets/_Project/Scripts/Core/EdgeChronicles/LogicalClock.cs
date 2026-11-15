using System;

namespace EdgeChronicles.Core
{
    /// <summary>
    /// 逻辑时钟：同时持有两根时钟轴，**互不换算、互不混用**。
    /// <list type="bullet">
    /// <item><b>C 轴（逻辑 / 战斗轴）</b>：固定 <see cref="TicksPerSecond"/> Hz。
    /// 战斗 tick、技能相、取消窗口、顿帧全部以 C 轴帧为准。
    /// 任何「帧」在规格语境下若无特别声明，一律指 C 轴帧。</item>
    /// <item><b>W 轴（墙钟 / 渲染轴）</b>：真实时间轴。渲染帧数、真实耗时、
    /// 输入到生效的墙钟延迟以 W 轴为准。</item>
    /// </list>
    /// <para>
    /// 治理约束（IMPL-v3.0 §3.1、GDD 治理规则 9/10）：
    /// ① 帧号只用于<b>标识与排序</b>，禁止参与算术；② 任何跨轴相减都非法；
    /// ③ 要表达「一段时间」必须取<b>时间量</b>（秒），不取帧号差。
    /// 本类把两轴的时间量（秒）与帧号（标识）分开暴露，调用方无从混用。
    /// </para>
    /// </summary>
    public sealed class LogicalClock
    {
        /// <summary>逻辑帧率（Hz）。规格固定值，Kernel 内全整数帧即以此为准。</summary>
        public const int TicksPerSecond = 60;

        /// <summary>单逻辑帧时长（秒）。<b>派生量</b>，禁止在别处再写 1/60。</summary>
        public static readonly double SecondsPerTick = 1.0 / TicksPerSecond;

        /// <summary>
        /// 单次推进最多补的逻辑帧数（防止长卡顿后的「死亡螺旋」）。
        /// 这是<b>策略参数</b>而非规格常量：由调用方注入，默认 8 帧（= 133 ms 墙钟）。
        /// </summary>
        public readonly int MaxCatchUpTicksPerUpdate;

        private double _accumulatorSeconds;
        private long _logicalFrameIndex;
        private double _logicalElapsedSeconds;
        private long _wallFrameIndex;
        private double _wallElapsedSeconds;

        public LogicalClock(int maxCatchUpTicksPerUpdate = 8)
        {
            if (maxCatchUpTicksPerUpdate < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxCatchUpTicksPerUpdate), maxCatchUpTicksPerUpdate, "至少允许补 1 帧");
            }

            MaxCatchUpTicksPerUpdate = maxCatchUpTicksPerUpdate;
        }

        /// <summary>
        /// C 轴：已推进的逻辑帧序号。<b>只作标识 / 排序，禁止参与算术</b>。
        /// </summary>
        public long LogicalFrameIndex => _logicalFrameIndex;

        /// <summary>C 轴：已推进的逻辑时间（秒，elapsed）。<b>时间量</b>，差值可参与算术。</summary>
        public double LogicalElapsedSeconds => _logicalElapsedSeconds;

        /// <summary>
        /// W 轴：已推进的墙钟帧（渲染帧）序号。<b>只作标识 / 排序，禁止参与算术</b>。
        /// </summary>
        public long WallFrameIndex => _wallFrameIndex;

        /// <summary>W 轴：已推进的墙钟时间（秒，elapsed）。<b>时间量</b>，差值可参与算术。</summary>
        public double WallElapsedSeconds => _wallElapsedSeconds;

        /// <summary>当前 C 轴帧内未消费的墙钟时间（秒）。</summary>
        public double PendingSeconds => _accumulatorSeconds;

        /// <summary>
        /// 推进一个渲染帧：W 轴前进 1 帧，墙钟时间累加，并按累加器折算成整数个逻辑帧。
        /// </summary>
        /// <param name="wallDeltaSeconds">本渲染帧的墙钟增量（秒）。</param>
        /// <returns>本次应当执行的逻辑帧数（0 表示本帧不跑 tick）。</returns>
        public int Advance(double wallDeltaSeconds)
        {
            if (wallDeltaSeconds < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(wallDeltaSeconds), wallDeltaSeconds, "墙钟增量不能为负（时间不可逆）");
            }

            _wallFrameIndex++;
            _wallElapsedSeconds += wallDeltaSeconds;
            _accumulatorSeconds += wallDeltaSeconds;

            int ticks = 0;
            while (_accumulatorSeconds >= SecondsPerTick)
            {
                if (ticks >= MaxCatchUpTicksPerUpdate)
                {
                    // 超出补帧上限：丢弃积压而不是继续追帧。
                    // 丢弃的是「墙钟时间」，不改变任何已发生过的 C 轴时刻。
                    _accumulatorSeconds = 0d;
                    break;
                }

                _accumulatorSeconds -= SecondsPerTick;
                _logicalFrameIndex++;
                _logicalElapsedSeconds += SecondsPerTick;
                ticks++;
            }

            return ticks;
        }

        /// <summary>
        /// 手动推进一个逻辑帧（供回放 / 单测使用；不依赖墙钟）。
        /// </summary>
        public void AdvanceLogicalTick()
        {
            _logicalFrameIndex++;
            _logicalElapsedSeconds += SecondsPerTick;
        }

        /// <summary>重置到起点（会话开始时调用）。</summary>
        public void Reset()
        {
            _accumulatorSeconds = 0d;
            _logicalFrameIndex = 0;
            _logicalElapsedSeconds = 0d;
            _wallFrameIndex = 0;
            _wallElapsedSeconds = 0d;
        }
    }
}
