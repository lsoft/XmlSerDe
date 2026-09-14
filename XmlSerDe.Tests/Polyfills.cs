#if !NET5_0_OR_GREATER

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// В net472 этого типа нет, а <c>init</c>-сеттер компилятор без него не
    /// разбирает. Ищет он его по полному имени, а не по сборке, поэтому
    /// объявления здесь достаточно.
    ///
    /// Нужен ровно одному месту - <see cref="XmlSerDe.Tests.Compat.CompatInitSubject"/>:
    /// дыра, ради которой он заведён, воспроизводится только на настоящем
    /// <c>init</c>-свойстве.
    /// </summary>
    internal static class IsExternalInit
    {
    }
}

#endif
