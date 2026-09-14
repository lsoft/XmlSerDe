#if NETSTANDARD
using System;

namespace XmlSerDe.Generator.Producer
{
    /// <summary>
    /// Отказ генератора, у которого есть внятная причина, - в отличие от падения,
    /// у которого её нет.
    ///
    /// Разница видна в сборке: обычное исключение генератор показывает вместе со
    /// стектрейсом, потому что стектрейс там - единственная зацепка. Здесь зацепка
    /// не нужна: причина названа словами, с именем типа и члена, и стектрейс к ней
    /// ничего не добавляет, зато превращает одну строку в экран.
    /// </summary>
    public sealed class GenerationRefusedException : Exception
    {
        public GenerationRefusedException(string message)
            : base(message)
        {
        }
    }
}
#endif
