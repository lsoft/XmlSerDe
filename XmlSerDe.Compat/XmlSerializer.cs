using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using XmlSerDe.Common;
using XmlSerDe.Components.Exhauster;

namespace XmlSerDe.Compat
{
    /// <summary>
    /// Замена <see cref="System.Xml.Serialization.XmlSerializer"/>, подставляемая
    /// одной строкой на проект:
    ///
    /// <code>global using XmlSerializer = XmlSerDe.Compat.XmlSerializer;</code>
    ///
    /// Устроена на трёх решениях.
    ///
    /// <b>Это настоящий <see cref="System.Xml.Serialization.XmlSerializer"/>.</b>
    /// Наследование возможно потому, что у базового класса есть
    /// <c>protected XmlSerializer()</c>, не запускающий разбор типа рефлексией
    /// (им пользуются предгенерированные сборки). Экземпляр можно передавать
    /// в чужие API и класть в поля существующего кода.
    ///
    /// <b>Никакой тип не ломается.</b> Незарегистрированный тип уходит штатному
    /// сериализатору и работает как раньше, просто без ускорения. Поэтому
    /// генератор вправе промолчать на любом типе, который он не умеет, - и обязан
    /// молчать, а не выдавать почти правильный код.
    ///
    /// <b>Быстро там, где вызов виден.</b> Перегрузки, принимающие
    /// <see cref="TextWriter"/>, <see cref="Stream"/> и <see cref="TextReader"/>,
    /// у базового класса не виртуальные, поэтому здесь они объявлены заново через
    /// <c>new</c>: когда статический тип в точке вызова - фасад, идём span-путём.
    /// Когда чужой код зовёт через базовый тип, работает контракт
    /// <c>CreateWriter</c>/<c>CreateReader</c> - медленнее, но правильно.
    ///
    /// Чего фасад не обещает: <see cref="XmlSerializerNamespaces"/> и
    /// SOAP-кодирование уводят вызов в штатный сериализатор целиком, а объявление
    /// <c>&lt;?xml?&gt;</c> на быстром пути всегда utf-8 и без отступов.
    /// </summary>
    public class XmlSerializer : System.Xml.Serialization.XmlSerializer
    {
        private readonly Type _type;
        private readonly bool _accelerated;
        private readonly XmlSerDeEntry _entry;

        /// <summary>
        /// Штатный сериализатор создаётся только если понадобился: его построение
        /// стоит дорого, а на ускоренном типе он не нужен вовсе. Гонка здесь
        /// безобидна - в худшем случае два экземпляра, оба рабочие.
        /// </summary>
        private System.Xml.Serialization.XmlSerializer? _fallback;

        public XmlSerializer(Type type)
            : base()
        {
            _type = type ?? throw new ArgumentNullException(nameof(type));
            _accelerated = XmlSerDeRegistry.TryGet(_type, out _entry);
        }

        /// <summary>
        /// Тип, ради которого сериализатор создан.
        /// </summary>
        public Type SubjectType => _type;

        /// <summary>
        /// Обслуживается ли тип быстрым путём. Полезно в тестах и в диагностике:
        /// молчаливое падение на штатный сериализатор иначе не увидеть.
        /// </summary>
        public bool IsAccelerated => _accelerated;

        private System.Xml.Serialization.XmlSerializer Fallback
        {
            get
            {
                var fallback = _fallback;
                if (fallback is null)
                {
                    fallback = new System.Xml.Serialization.XmlSerializer(_type);
                    _fallback = fallback;
                }

                return fallback;
            }
        }

        #region быстрый путь

        /// <summary>
        /// Кратчайший путь: ни <see cref="XmlWriter"/>, ни промежуточного потока.
        /// </summary>
        public string SerializeToString(object o, bool appendXmlDeclaration = true)
        {
            //null - это не «нечего писать»: BCL пишет <T xsi:nil="true" />, и генератору
            //такой документ выдать нечем (метод сериализации начинается с разыменования).
            //Тот же отказ уже стоит на контракте предгенерированных сборок, ниже
            if (!_accelerated || o is null)
            {
                var sw = new StringWriter();
                Fallback.Serialize(sw, o);
                return sw.ToString();
            }

            var exhauster = new DefaultStringBuilderExhauster();
            _entry.Serialize(exhauster, o, appendXmlDeclaration);
            return exhauster.ToString();
        }

        /// <summary>
        /// Разбор прямо из спана - тот самый путь, ради которого существует XmlSerDe.
        /// Пролог срезается здесь, делегату достаётся уже корневой элемент.
        /// </summary>
        public object? Deserialize(ReadOnlySpan<char> xml)
        {
            if (!_accelerated)
            {
                //штатному сериализатору спан не скормить, деваться некуда
                using var reader = new StringReader(xml.ToString());
                return Fallback.Deserialize(reader);
            }

            return Deserialize(XmlPrologue.Cut(xml), _entry);
        }

        /// <summary>
        /// Корень с <c>xsi:nil="true"</c> - это документ про null, а не про пустой
        /// объект: именно так <see cref="System.Xml.Serialization.XmlSerializer"/>
        /// пишет null, и читает его обратно тоже в null. Сгенерированный метод такого
        /// ответа дать не может - он всегда конструирует объект, - поэтому корень
        /// проверяется здесь. Внутри документа тем же занимается сгенерированный код,
        /// у которого голова члена уже под рукой.
        /// </summary>
        private static object? Deserialize(ReadOnlySpan<char> root, XmlSerDeEntry entry)
        {
            var head = new XmlHead();
            XmlScan.ReadHead(true, true, root, ReadOnlySpan<char>.Empty, ref head);

            if (head.IsBodyless && head.IsNil())
            {
                return null;
            }

            return entry.Deserialize(root);
        }

        public new void Serialize(TextWriter textWriter, object o)
        {
            if (!_accelerated || o is null)
            {
                Fallback.Serialize(textWriter, o);
                return;
            }

            textWriter.Write(SerializeToString(o));
        }

        public new void Serialize(Stream stream, object o)
        {
            if (!_accelerated || o is null)
            {
                Fallback.Serialize(stream, o);
                return;
            }

            var bytes = Encoding.UTF8.GetBytes(SerializeToString(o));
            stream.Write(bytes, 0, bytes.Length);
        }

        public new object? Deserialize(TextReader textReader)
        {
            if (!_accelerated)
            {
                return Fallback.Deserialize(textReader);
            }

            return Deserialize(textReader.ReadToEnd().AsSpan());
        }

        public new object? Deserialize(Stream stream)
        {
            if (!_accelerated)
            {
                return Fallback.Deserialize(stream);
            }

            //поток чужой, закрывать его нельзя, поэтому StreamReader не оборачивается
            //в using: он закрыл бы и поток тоже
            var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            return Deserialize(reader.ReadToEnd().AsSpan());
        }

        #endregion

        #region контракт предгенерированных сборок - работает и через базовый тип

        protected override XmlSerializationWriter CreateWriter()
        {
            return new XmlSerDeSerializationWriter();
        }

        protected override void Serialize(object? o, XmlSerializationWriter writer)
        {
            var typed = (XmlSerDeSerializationWriter)writer;
            var xmlWriter = typed.Target;

            var declaredNamespaces = RebuildNamespaces(typed.DeclaredNamespaces);

            if (!_accelerated || declaredNamespaces is not null || o is null)
            {
                //пространства имён приходится собирать заново: до виртуального метода
                //доходит уже разобранный базовым классом список, а не тот объект,
                //который передал вызывающий
                Fallback.Serialize(xmlWriter, o, declaredNamespaces);
                return;
            }

            //объявление <?xml?> здесь уже написал сам XmlWriter, второе было бы
            //нарушением формата
            var exhauster = new DefaultStringBuilderExhauster();
            _entry.Serialize(exhauster, o, false);
            xmlWriter.WriteRaw(exhauster.ToString());
        }

        /// <summary>
        /// Возвращает null, если вызывающий пространств имён не передавал, - именно
        /// это и означает "быстрый путь допустим".
        /// </summary>
        private static XmlSerializerNamespaces? RebuildNamespaces(System.Collections.ArrayList? declared)
        {
            if (declared is null || declared.Count == 0)
            {
                return null;
            }

            var result = new XmlSerializerNamespaces();
            foreach (var item in declared)
            {
                if (item is XmlQualifiedName qualified)
                {
                    result.Add(qualified.Name, qualified.Namespace);
                }
            }

            return result;
        }

        protected override XmlSerializationReader CreateReader()
        {
            return new XmlSerDeSerializationReader();
        }

        protected override object Deserialize(XmlSerializationReader reader)
        {
            var xmlReader = ((XmlSerDeSerializationReader)reader).Target;

            if (!_accelerated)
            {
                return Fallback.Deserialize(xmlReader)!;
            }

            //XmlReader отдаёт документ только по кускам, поэтому здесь он
            //материализуется целиком - плата за вход не через спан. Быстрые
            //перегрузки выше этого не делают
            xmlReader.MoveToContent();
            var xml = xmlReader.ReadOuterXml();

            return Deserialize(XmlPrologue.Cut(xml.AsSpan()), _entry)!;
        }

        /// <summary>
        /// Уходит в штатный сериализатор: проверка сводится к имени корневого
        /// элемента, а его в реестре нет. Метод зовут редко, и правильный ответ
        /// здесь важнее быстрого.
        /// </summary>
        public override bool CanDeserialize(XmlReader xmlReader)
        {
            return Fallback.CanDeserialize(xmlReader);
        }

        #endregion
    }
}
