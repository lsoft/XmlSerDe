using System.Runtime.CompilerServices;

namespace XmlSerDe.Components.Exhauster
{
    /// <summary>
    /// <c>log2(n)+1</c> без <see cref="System.Numerics.BitOperations"/> на ns2.0.
    /// На net8+ это <c>uint.Log2</c> / <c>ulong.Log2</c>.
    /// </summary>
    internal static class DecimalLog2
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Of(uint value)
        {
#if NET8_0_OR_GREATER
            return (int)uint.Log2(value);
#else
            var r = 0;
            if (value >= (1u << 16)) { value >>= 16; r += 16; }
            if (value >= (1u << 8)) { value >>= 8; r += 8; }
            if (value >= (1u << 4)) { value >>= 4; r += 4; }
            if (value >= (1u << 2)) { value >>= 2; r += 2; }
            if (value >= (1u << 1)) { r += 1; }
            return r;
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Of(ulong value)
        {
#if NET8_0_OR_GREATER
            return (int)ulong.Log2(value);
#else
            var r = 0;
            if (value >= (1ul << 32)) { value >>= 32; r += 32; }
            if (value >= (1ul << 16)) { value >>= 16; r += 16; }
            if (value >= (1ul << 8)) { value >>= 8; r += 8; }
            if (value >= (1ul << 4)) { value >>= 4; r += 4; }
            if (value >= (1ul << 2)) { value >>= 2; r += 2; }
            if (value >= (1ul << 1)) { r += 1; }
            return r;
#endif
        }
    }

    /// <summary>
    /// Таблица <c>digits[log2(n)]</c>. Верхняя оценка: для 8 и 9 даёт 2
    /// вместо 1, и так на каждой границе степени двойки ниже степени десяти.
    /// </summary>
    public static class DecimalDigitsLog
    {
        private static readonly byte[] Digits32Table =
        [
            1, 1, 1, 2, 2, 2, 3, 3, 3, 4, 4, 4, 4, 5, 5, 5,
            6, 6, 6, 7, 7, 7, 7, 8, 8, 8, 9, 9, 9, 10, 10, 10
        ];

        private static readonly byte[] Digits64UTable =
        [
            1, 1, 1, 2, 2, 2, 3, 3, 3, 4, 4, 4, 4, 5, 5, 5,
            6, 6, 6, 7, 7, 7, 7, 8, 8, 8, 9, 9, 9, 10, 10, 10,
            10, 11, 11, 11, 12, 12, 12, 13, 13, 13, 13, 14, 14, 14, 15, 15,
            15, 16, 16, 16, 16, 17, 17, 17, 18, 18, 18, 19, 19, 19, 19, 20
        ];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int AtLeast(uint value)
            => Digits32Table[DecimalLog2.Of(value | 1u)];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int AtLeast(ulong value)
            => Digits64UTable[DecimalLog2.Of(value | 1ul)];
    }
}
