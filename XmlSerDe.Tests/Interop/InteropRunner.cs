using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace XmlSerDe.Tests.Interop
{
    /// <summary>
    /// Читает XML в объект средствами XmlSerDe. Обычная <c>Func</c> здесь не подходит:
    /// у сгенерированного <c>Deserialize</c> результат идёт через <c>out</c>, а вход -
    /// это <c>ReadOnlySpan&lt;char&gt;</c>, который нельзя подставить параметром типа.
    /// </summary>
    public delegate void XmlSerDeReader<T>(ReadOnlySpan<char> xml, out T result);

    /// <summary>
    /// Насколько XmlSerDe и <see cref="XmlSerializer"/> сходятся на одной и той же форме POCO.
    /// Три направления проверяются независимо, потому что расходиться они могут порознь:
    /// формат может отличаться, но обе стороны продолжат друг друга понимать.
    /// </summary>
    public sealed class InteropResult
    {
        /// <summary>
        /// XML, который написал XmlSerDe.
        /// </summary>
        public string XmlSerDeXml { get; set; }

        /// <summary>
        /// XML, который написал <see cref="XmlSerializer"/>.
        /// </summary>
        public string SystemXmlXml { get; set; }

        /// <summary>
        /// XmlSerDe прочитал то, что написал <see cref="XmlSerializer"/>, без потерь.
        /// </summary>
        public bool CanReadSystemXml { get; set; }

        /// <summary>
        /// <see cref="XmlSerializer"/> прочитал то, что написал XmlSerDe, без потерь.
        /// </summary>
        public bool SystemXmlCanReadOurs { get; set; }

        /// <summary>
        /// Оба написали одно и то же с точностью до канонизации
        /// (см. <see cref="InteropRunner.Canonicalize"/>).
        /// </summary>
        public bool SameShape { get; set; }

        /// <summary>
        /// Чем закончилось чтение нашего XML нами же (направление CanReadSystemXml),
        /// если оно упало. Иначе null.
        /// </summary>
        public string ReadSystemXmlError { get; set; }

        /// <summary>
        /// Чем закончилось чтение нашего XML средствами BCL, если оно упало. Иначе null.
        /// </summary>
        public string SystemXmlReadError { get; set; }

        public bool FullyCompatible => CanReadSystemXml && SystemXmlCanReadOurs && SameShape;

        public string Describe()
        {
            var sb = new StringBuilder();
            sb.AppendLine("XmlSerDe  -> " + XmlSerDeXml);
            sb.AppendLine("System.Xml-> " + InteropRunner.Canonicalize(SystemXmlXml).Replace(Environment.NewLine, " "));
            sb.AppendLine("XmlSerDe читает System.Xml: " + (CanReadSystemXml ? "да" : "НЕТ " + ReadSystemXmlError));
            sb.AppendLine("System.Xml читает XmlSerDe: " + (SystemXmlCanReadOurs ? "да" : "НЕТ " + SystemXmlReadError));
            sb.AppendLine("Форма совпадает: " + (SameShape ? "да" : "НЕТ"));
            if (!SameShape)
            {
                sb.AppendLine("--- канон XmlSerDe ---");
                sb.AppendLine(InteropRunner.Canonicalize(XmlSerDeXml));
                sb.AppendLine("--- канон System.Xml ---");
                sb.AppendLine(InteropRunner.Canonicalize(SystemXmlXml));
            }

            return sb.ToString();
        }
    }

    /// <summary>
    /// Дифференциальная сверка XmlSerDe с <see cref="XmlSerializer"/> на одной форме POCO.
    ///
    /// Утверждение "формат совместим с System.Xml.Serialization" до сих пор держалось
    /// на нескольких точечных сверках (ComplexFixture, DeepFixture) с руками написанным
    /// эталонным XML. Здесь оно проверяется как свойство: для каждой формы гоняем объект
    /// через обе реализации во всех сочетаниях и смотрим, где именно они разошлись.
    ///
    /// Сравнение объектов идёт не по Equals (его пришлось бы писать на каждую форму
    /// корпуса), а по XML, который на них выдаёт сам BCL: если два объекта дают
    /// побайтово одинаковый XmlSerializer-вывод, то по всем сериализуемым членам
    /// они равны, а несериализуемых в корпусе нет.
    /// </summary>
    public static class InteropRunner
    {
        public static InteropResult Verify<T>(
            T original,
            Func<T, string> xmlSerDeWrite,
            XmlSerDeReader<T> xmlSerDeRead
            )
        {
            if (original is null)
            {
                throw new ArgumentNullException(nameof(original));
            }

            var result = new InteropResult();

            var expectedXml = SystemXmlSerialize(original);
            result.SystemXmlXml = expectedXml;
            result.XmlSerDeXml = xmlSerDeWrite(original);

            //направление 1: XmlSerDe читает то, что написал BCL
            try
            {
                var body = global::XmlSerDe.Generator.Producer.BuiltinCodeHelper.CutXmlHead(
                    expectedXml.AsSpan()
                    );
                xmlSerDeRead(body, out var ours);
                result.CanReadSystemXml = SystemXmlSerialize(ours) == expectedXml;
                if (!result.CanReadSystemXml)
                {
                    result.ReadSystemXmlError = "прочиталось, но объект получился другой: "
                        + Diff(expectedXml, SystemXmlSerialize(ours));
                }
            }
            catch (Exception excp)
            {
                result.CanReadSystemXml = false;
                result.ReadSystemXmlError = excp.GetType().Name + ": " + excp.Message;
            }

            //направление 2: BCL читает то, что написал XmlSerDe
            try
            {
                var theirs = SystemXmlDeserialize<T>(result.XmlSerDeXml);
                result.SystemXmlCanReadOurs = SystemXmlSerialize(theirs) == expectedXml;
                if (!result.SystemXmlCanReadOurs)
                {
                    result.SystemXmlReadError = "прочиталось, но объект получился другой: "
                        + Diff(expectedXml, SystemXmlSerialize(theirs));
                }
            }
            catch (Exception excp)
            {
                result.SystemXmlCanReadOurs = false;
                result.SystemXmlReadError = excp.GetType().Name + ": " + excp.Message;
            }

            //направление 3: совпадает ли сам формат
            result.SameShape = Canonicalize(result.XmlSerDeXml) == Canonicalize(expectedXml);

            return result;
        }

        public static string SystemXmlSerialize<T>(T obj)
        {
            var serializer = new XmlSerializer(typeof(T));
            var sb = new StringBuilder();
            using (var writer = new StringWriter(sb))
            {
                serializer.Serialize(writer, obj);
            }

            return sb.ToString();
        }

        public static T SystemXmlDeserialize<T>(string xml)
        {
            var serializer = new XmlSerializer(typeof(T));
            using (var reader = new StringReader(xml))
            {
                return (T)serializer.Deserialize(reader);
            }
        }

        /// <summary>
        /// Приводит XML к виду, в котором сравнение отвечает на вопрос "одна ли это форма",
        /// а не "одинаковы ли байты". Гасятся ровно те различия, которые ни одна из сторон
        /// не считает значащими:
        /// <list type="bullet">
        /// <item>отступы и переносы (BCL пишет с отступами, XmlSerDe - сплошняком);</item>
        /// <item>&lt;a/&gt; против &lt;a&gt;&lt;/a&gt; (XDocument их и так не различает);</item>
        /// <item>объявления xmlns (BCL вешает xmlns:xsi и xmlns:xsd на корень, XmlSerDe - нет);</item>
        /// <item>префикс в имени атрибута xsi:type (у BCL он xsi, у XmlSerDe - тоже, но
        /// на чтении обе стороны резолвят любой префикс, привязанный к нужному URI).</item>
        /// </list>
        /// А вот порядок элементов сохраняется намеренно: он и есть предмет проверки.
        /// </summary>
        public static string Canonicalize(string xml)
        {
            var doc = XDocument.Parse(xml);
            var sb = new StringBuilder();
            CanonicalizeElement(doc.Root, sb, 0);
            return sb.ToString();
        }

        private static void CanonicalizeElement(XElement element, StringBuilder sb, int depth)
        {
            var indent = new string(' ', depth * 2);

            sb.Append(indent).Append(element.Name.LocalName);

            var attributes = element.Attributes()
                .Where(a => !a.IsNamespaceDeclaration)
                .Select(a => a.Name.LocalName + "=" + a.Value)
                .OrderBy(a => a, StringComparer.Ordinal)
                .ToList();
            if (attributes.Count > 0)
            {
                sb.Append(" [").Append(string.Join(", ", attributes)).Append("]");
            }

            var children = element.Elements().ToList();
            if (children.Count > 0)
            {
                sb.AppendLine();
                foreach (var child in children)
                {
                    CanonicalizeElement(child, sb, depth + 1);
                }
            }
            else
            {
                //лист: значение сравниваем как есть, но без отступов, которыми его
                //мог окружить форматирующий писатель
                var value = element.Value;
                sb.Append(" = '").Append(value.Trim()).AppendLine("'");
            }
        }

        private static string Diff(string expected, string actual)
        {
            var e = Canonicalize(expected).Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            var a = Canonicalize(actual).Split(new[] { Environment.NewLine }, StringSplitOptions.None);

            var lines = new List<string>();
            for (var i = 0; i < Math.Max(e.Length, a.Length); i++)
            {
                var el = i < e.Length ? e[i] : "<нет строки>";
                var al = i < a.Length ? a[i] : "<нет строки>";
                if (el != al)
                {
                    lines.Add($"[{i}] ожидалось '{el}', получилось '{al}'");
                }
            }

            return string.Join("; ", lines.Take(5));
        }
    }
}
