using System;

namespace XmlSerDe.Common
{
    /// <summary>
    /// Ошибка <b>о документе</b>: разбираемый XML не соответствует XML 1.0 или
    /// не согласован сам с собой. Не о вызывающем (для этого
    /// <see cref="ArgumentException"/>) и не о лексеме, которая на месте, но
    /// числом не является (для этого <see cref="FormatException"/>).
    ///
    /// Наследуется от <see cref="InvalidOperationException"/> намеренно: именно
    /// так о битом документе говорит и сам XmlSerDe, и, завернув чужое,
    /// <see cref="System.Xml.Serialization.XmlSerializer"/>, поэтому написанный
    /// до этого типа <c>catch (InvalidOperationException)</c> продолжает ловить.
    ///
    /// Отдельный тип нужен ровно для одного: хост со стражами
    /// (docs/opt-in-xml-guards.md §6) заворачивает ошибку документа в форму BCL
    /// (<c>InvalidOperationException</c> → <c>XmlException</c>), и отличить её
    /// от исключения пользовательского инжектора или фабрики можно только по
    /// типу - ловить по тексту сообщения нельзя.
    /// </summary>
    public sealed class XmlDocumentException : InvalidOperationException
    {
        public XmlDocumentException(string message)
            : base(message)
        {
        }

        public XmlDocumentException(string message, Exception? innerException)
            : base(message, innerException)
        {
        }
    }
}
