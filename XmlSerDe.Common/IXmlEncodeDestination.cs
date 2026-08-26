using System;

namespace XmlSerDe.Common
{
    /// <summary>
    /// Сток для экранирования без промежуточной строки: литеральный кусок
    /// исходника, готовая ASCII-замена или небольшой span (<c>&amp;#169;</c>).
    /// </summary>
    public interface IXmlEncodeDestination
    {
        void WriteSlice(string value, int start, int count);

        void WriteLiteral(string literal);

        void WriteChars(ReadOnlySpan<char> chars);
    }
}
