using System;
using System.Globalization;

namespace EdgeChronicles.Core
{
    /// <summary>
    /// 定点二维向量（整数厘米）。
    /// <para>
    /// 量纲：长度单位 = <b>厘米（cm）</b>。1 m = 100 cm。
    /// 时钟轴：<b>本类型不含时间量</b>，不含任何帧号；它只承载逻辑坐标与逐帧位移向量。
    /// </para>
    /// <para>
    /// 为什么是整数：命中判定是纯数学判定（IMPL-v2.0 §7.1 歧义点 3），
    /// 浮点在跨平台 / 跨帧累加时会产生漂移；整数厘米同时满足「确定性」与「亚厘米级精度足够」
    /// （最小规格位移 0.3 m = 30 cm，最小命中半径 0.3 m）。
    /// </para>
    /// <para>
    /// 契约：<b>所有取整一律「四舍五入、远离零」</b>（见 <see cref="DivRound"/>），
    /// 保证同样的输入在任何平台得到同样的整数结果。
    /// </para>
    /// </summary>
    public readonly struct Vec2 : IEquatable<Vec2>, IComparable<Vec2>
    {
        /// <summary>1 米 = 100 厘米。</summary>
        public const int UnitsPerMeter = 100;

        /// <summary>1 厘米 = 1 单位。</summary>
        public const int UnitsPerCentimeter = 1;

        /// <summary>X 分量（厘米）。</summary>
        public readonly int X;

        /// <summary>Y 分量（厘米）。</summary>
        public readonly int Y;

        public Vec2(int xCm, int yCm)
        {
            X = xCm;
            Y = yCm;
        }

        /// <summary>零向量。</summary>
        public static readonly Vec2 Zero = new Vec2(0, 0);

        /// <summary>单位向量（沿 +X），长度 1 cm。</summary>
        public static readonly Vec2 UnitX = new Vec2(1, 0);

        /// <summary>单位向量（沿 +Y），长度 1 cm。</summary>
        public static readonly Vec2 UnitY = new Vec2(0, 1);

        /// <summary>由米换算为厘米（仅用于配表 / 编辑期输入，运行期一律用整厘米）。</summary>
        public static Vec2 FromMeters(double xMeters, double yMeters)
        {
            return new Vec2(
                DivRound((long)Math.Round(xMeters * UnitsPerMeter, MidpointRounding.AwayFromZero), 1L),
                DivRound((long)Math.Round(yMeters * UnitsPerMeter, MidpointRounding.AwayFromZero), 1L));
        }

        /// <summary>X 分量（米，只读换算）。</summary>
        public double XInMeters => X / (double)UnitsPerMeter;

        /// <summary>Y 分量（米，只读换算）。</summary>
        public double YInMeters => Y / (double)UnitsPerMeter;

        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.X + b.X, a.Y + b.Y);

        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.X - b.X, a.Y - b.Y);

        public static Vec2 operator -(Vec2 a) => new Vec2(-a.X, -a.Y);

        public static Vec2 operator *(Vec2 a, int k) => new Vec2(a.X * k, a.Y * k);

        public static Vec2 operator *(int k, Vec2 a) => a * k;

        public static Vec2 operator /(Vec2 a, int k) => new Vec2(DivRound(a.X, k), DivRound(a.Y, k));

        public static bool operator ==(Vec2 a, Vec2 b) => a.X == b.X && a.Y == b.Y;

        public static bool operator !=(Vec2 a, Vec2 b) => !(a == b);

        /// <summary>点积（cm²）。</summary>
        public long Dot(Vec2 other) => (long)X * other.X + (long)Y * other.Y;

        /// <summary>二维叉积（cm²，标量）。</summary>
        public long Cross(Vec2 other) => (long)X * other.Y - (long)Y * other.X;

        /// <summary>长度平方（cm²），避免开方。</summary>
        public long LengthSquared => (long)X * X + (long)Y * Y;

        /// <summary>长度（cm，向下取整的整数平方根）。</summary>
        public int LengthCm => (int)Isqrt(LengthSquared);

        /// <summary>到另一点的距离（cm）。</summary>
        public int DistanceTo(Vec2 other) => (this - other).LengthCm;

        /// <summary>按比例缩放（分子 / 分母均为整数，结果按远离零四舍五入）。</summary>
        public Vec2 ScaleRatio(int numerator, int denominator)
        {
            return new Vec2(DivRound(X * (long)numerator, denominator), DivRound(Y * (long)numerator, denominator));
        }

        /// <summary>夹到最大长度（cm）；超过则按原方向截断。</summary>
        public Vec2 ClampLength(int maxCm)
        {
            if (maxCm <= 0) return Zero;
            long sq = LengthSquared;
            long maxSq = (long)maxCm * maxCm;
            if (sq <= maxSq) return this;
            int len = LengthCm;
            if (len <= 0) return Zero;
            return ScaleRatio(maxCm, len);
        }

        /// <summary>归一化到整数长度 1 cm（四舍五入远离零）；零向量返回零向量。</summary>
        public Vec2 NormalizedToUnit()
        {
            int len = LengthCm;
            if (len == 0) return Zero;
            return ScaleRatio(1, len);
        }

        /// <summary>分量绝对值。</summary>
        public Vec2 Abs() => new Vec2(Math.Abs(X), Math.Abs(Y));

        public bool Equals(Vec2 other) => X == other.X && Y == other.Y;

        public override bool Equals(object obj) => obj is Vec2 other && Equals(other);

        public override int GetHashCode() => unchecked((X * 397) ^ Y);

        /// <summary>字典序比较：先 X 后 Y。</summary>
        public int CompareTo(Vec2 other)
        {
            if (X != other.X) return X < other.X ? -1 : 1;
            if (Y != other.Y) return Y < other.Y ? -1 : 1;
            return 0;
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "({0},{1})cm", X, Y);
        }

        /// <summary>
        /// 整数除法 + 远离零四舍五入（<c>|余数| × 2 ≥ |除数|</c> 时进 1）。
        /// 全项目唯一的取整口径，避免各处各写一套导致结果不一致。
        /// </summary>
        public static int DivRound(long numerator, long denominator)
        {
            if (denominator == 0) throw new DivideByZeroException("Vec2.DivRound: 除数不能为 0");
            long q = numerator / denominator;
            long r = numerator % denominator;
            if (r == 0) return (int)q;
            long absR = r < 0 ? -r : r;
            long absD = denominator < 0 ? -denominator : denominator;
            if (absR * 2 >= absD)
            {
                q += (numerator < 0) == (denominator < 0) ? 1 : -1;
            }
            return (int)q;
        }

        /// <summary>整数平方根（向下取整），纯整数、无浮点。</summary>
        public static long Isqrt(long value)
        {
            if (value <= 0) return 0;
            long num = value;
            long result = 0;
            long bit = 1L << 62;
            while (bit > num) bit >>= 2;
            while (bit != 0)
            {
                if (num >= result + bit)
                {
                    num -= result + bit;
                    result = (result >> 1) + bit;
                }
                else
                {
                    result >>= 1;
                }
                bit >>= 2;
            }
            return result;
        }
    }
}
