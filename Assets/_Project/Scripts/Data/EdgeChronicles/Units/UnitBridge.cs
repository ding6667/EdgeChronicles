namespace EdgeChronicles.Data
{
    /// <summary>
    /// 数据层的单位换算口径。
    /// <para>
    /// <b>为什么在 Data 引用 Core</b>：定点坐标的单位定义（1 m = 100 cm）只存在于
    /// <see cref="EdgeChronicles.Core.Vec2"/> 一处（Core 是定点数学的唯一定义点）。
    /// 数据层要换算的量恰好落在同一套单位上——<c>mechanic_mod</c> 的半径参数是<b>厘米</b>，
    /// 命中形状的半径与曲线积分是<b>米</b>。若数据层自己再写一份「100 cm = 1 m」，
    /// 就成了第二个真值源；跨层换算一旦有一处对不上，伤害与距离会静默偏移。
    /// </para>
    /// <para>
    /// 本类只做<b>换算</b>，不引入任何新的单位定义——换算系数一律取自 Core。
    /// </para>
    /// </summary>
    public static class UnitBridge
    {
        /// <summary>1 米 = 多少厘米（取自 Core 的定点定义）。</summary>
        public const int CentimetersPerMeter = EdgeChronicles.Core.Vec2.UnitsPerMeter;

        /// <summary>厘米 → 米（用于 UI 显示与米制公式）。</summary>
        public static float CentimetersToMeters(int centimeters)
        {
            return centimeters / (float)CentimetersPerMeter;
        }

        /// <summary>米 → 厘米（编辑期输入用；运行期一律用整厘米）。</summary>
        public static int MetersToCentimeters(double meters)
        {
            return EdgeChronicles.Core.Vec2.DivRound(
                (long)System.Math.Round(meters * CentimetersPerMeter, System.MidpointRounding.AwayFromZero), 1L);
        }
    }
}
