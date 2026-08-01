#if !NET5_0_OR_GREATER

namespace System.Diagnostics.CodeAnalysis
{
    /// <summary>
    /// В netstandard2.0 этого атрибута нет: он появился в netstandard2.1/net5.
    /// Компилятор ищет его по полному имени, а не по сборке, поэтому достаточно
    /// объявить его здесь - анализ достижимости кода начнёт учитывать методы,
    /// которые всегда бросают исключение, ровно как на современных таргетах.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    internal sealed class DoesNotReturnAttribute : Attribute
    {
    }
}

#endif
