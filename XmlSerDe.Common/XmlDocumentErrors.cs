using System;
using System.Xml;

namespace XmlSerDe.Common
{
    /// <summary>
    /// Форма ошибки документа у хоста со стражами: та же пара, что даёт
    /// <c>System.Xml.Serialization.XmlSerializer</c> над читателем без
    /// <c>IXmlLineInfo</c> - <see cref="InvalidOperationException"/> с общим
    /// текстом снаружи и <see cref="XmlException"/> с настоящей причиной внутри
    /// (docs/opt-in-xml-guards.md §6).
    ///
    /// Позиции здесь нет и быть не может: разбор идёт по спану, строк и колонок
    /// он не считает. У BCL в этом случае <c>LineNumber</c> и <c>LinePosition</c>
    /// тоже нули, а сообщение - именно эта форма без координат.
    ///
    /// Хост без стражей сюда не заходит: он бросает
    /// <see cref="XmlDocumentException"/> как есть, с сегодняшними текстами.
    /// </summary>
    public static class XmlDocumentErrors
    {
        public const string DeserializationErrorMessage = "There is an error in the XML document.";

        /// <summary>
        /// Оборачивает ошибку документа один раз и на самом верху разбора.
        /// Не по обёртке на примитив: три слоя <see cref="InvalidOperationException"/>
        /// вокруг одной причины - это не совместимость, а шум в логе.
        /// </summary>
        public static InvalidOperationException Wrap(XmlDocumentException inner)
        {
            return new InvalidOperationException(
                DeserializationErrorMessage,
                new XmlException(inner.Message, inner)
                );
        }

        /// <summary>
        /// Уже завёрнутая пара - её нельзя заворачивать второй раз. Нужна фасаду
        /// совместимости: он заворачивает всё, что вылетело из быстрого пути,
        /// а из хоста со стражами оттуда прилетает готовая форма.
        /// </summary>
        public static bool IsWrapped(Exception exception)
        {
            return exception is InvalidOperationException ioe
                && ioe.InnerException is XmlException
                && ioe.Message == DeserializationErrorMessage;
        }
    }
}
