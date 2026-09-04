using System;
using XmlSerDe.Common;

namespace XmlSerDe.Compat
{
    /// <summary>
    /// Срезает пролог документа, оставляя корневой элемент.
    ///
    /// Повторяет <c>BuiltinCodeHelper.CutXmlHead</c>, а не зовёт его: тот метод
    /// генератор порождает прямо в сборку потребителя, и отсюда его не видно.
    /// Заводить ради этого зависимость в обратную сторону или трогать
    /// существующий горячий путь незачем - речь о двух десятках строк, которые
    /// исполняются один раз на документ.
    /// </summary>
    public static class XmlPrologue
    {
        public static ReadOnlySpan<char> Cut(ReadOnlySpan<char> xml)
        {
            return Cut(markup: true, xml);
        }

        public static ReadOnlySpan<char> Cut(
            bool markup,
            ReadOnlySpan<char> xml
            )
        {
            var startOfHead = "<?xml".AsSpan();

            var trimmedXml = xml.Trim();

            ReadOnlySpan<char> headless;
            if (trimmedXml.StartsWith(startOfHead)
                && trimmedXml.Length > startOfHead.Length
                && (trimmedXml[startOfHead.Length] is ' ' or '\t' or '\r' or '\n'))
            {
                var endOfHead = "?>".AsSpan();
                var index = trimmedXml.IndexOf(endOfHead);
                if (index < 0)
                {
                    throw new InvalidOperationException("Closing '?>' not found for xml declaration.");
                }

                headless = trimmedXml.Slice(index + endOfHead.Length);
            }
            else
            {
                headless = trimmedXml;
            }

            var features = markup
                ? XmlFeature.Markup
                : XmlFeature.None;

            return XmlNode2.SkipPrologMisc(features, headless);
        }
    }
}
