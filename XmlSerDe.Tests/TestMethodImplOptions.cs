using System.Runtime.CompilerServices;

/// <summary>
/// Объявлен в глобальном пространстве имён, чтобы быть видимым из всех фикстур
/// без дополнительных using.
/// </summary>
internal static class TestMethodImplOptions
{
#if NETCOREAPP3_0_OR_GREATER
    public const MethodImplOptions AggressiveOptimization = MethodImplOptions.AggressiveOptimization;
#else
    /// <summary>
    /// В .NET Framework (то есть в net472-прогоне, который существует ради
    /// netstandard2.0-веток Common/Components) этого флага нет: он появился
    /// в netcoreapp3.0. На поведение он не влияет - только просит JIT сразу
    /// компилировать метод в полную оптимизацию, минуя tiered-прогрев, - так
    /// что отсутствие флага здесь безопасно.
    /// </summary>
    public const MethodImplOptions AggressiveOptimization = default;
#endif
}
