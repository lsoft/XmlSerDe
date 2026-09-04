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
    /// SOAP-кодирование уводят вызов в штатный сериализатор целиком, а документ
    /// на быстром пути пишется без отступов.
    /// </summary>
    public class XmlSerializer : System.Xml.Serialization.XmlSerializer
    {
        /// <summary>
        /// Сообщения штатного сериализатора, слово в слово. Сняты прогоном самого
        /// <see cref="System.Xml.Serialization.XmlSerializer"/>, а не вычитаны:
        /// у чтения есть ещё вариант с позицией («There is an error in XML document
        /// (1, 25).», без артикля), но позицию быстрый путь назвать не может -
        /// он разбирает спан, а не <see cref="XmlReader"/>. Поэтому здесь всегда
        /// та форма, которую BCL выдаёт читателю без <see cref="IXmlLineInfo"/>.
        ///
        /// Текст один на все таргеты - английский, как на .NET Core. На .NET Framework
        /// у BCL есть локализованные ресурсы, и там его собственные сообщения зависят
        /// от UI-культуры машины; повторить это нечем, а ловят исключение по типу.
        /// </summary>
        private const string SerializationErrorMessage = "There was an error generating the XML document.";
        private const string DeserializationErrorMessage = "There is an error in the XML document.";

        /// <summary>
        /// Объявление документа. Кодировка в нём - <b>свойство стока</b>, а не
        /// константа: <see cref="System.Xml.Serialization.XmlSerializer"/> берёт её
        /// из <see cref="TextWriter.Encoding"/> и пишет
        /// <see cref="Encoding.WebName"/> как есть (снято прогоном:
        /// <see cref="StringWriter"/> даёт utf-16, <see cref="StreamWriter"/> над
        /// ASCII - us-ascii, над <see cref="Encoding.BigEndianUnicode"/> - utf-16BE).
        /// Перегрузка, принимающая <see cref="Stream"/>, кодировку не выбирает вовсе
        /// и всегда пишет utf-8.
        ///
        /// Прежде быстрый путь писал utf-8 в любой сток, и это была не мелочь:
        /// документ, записанный в utf-16 и объявленный как utf-8, читатель
        /// вправе не принять - утверждение в прологе противоречит байтам.
        /// </summary>
        private const string XmlDeclarationHead = "<?xml version=\"1.0\"";

        private const string Utf8XmlDeclaration = XmlDeclarationHead + " encoding=\"utf-8\"?>";

        private readonly Type _type;
        private readonly bool _accelerated;
        private readonly XmlSerDeEntry _entry;

        //добавочные аргументы конструктора хранятся целиком: ускорить с ними нечего,
        //но штатный сериализатор обязан быть построен ровно с ними, иначе фолбэк
        //отдал бы documент, о котором вызывающий не просил
        private readonly XmlAttributeOverrides? _overrides;
        private readonly Type[]? _extraTypes;
        private readonly XmlRootAttribute? _root;
        private readonly string? _defaultNamespace;

        /// <summary>
        /// Штатный сериализатор создаётся только если понадобился: его построение
        /// стоит дорого, а на ускоренном типе он не нужен вовсе.
        ///
        /// Создание идёт под замком, хотя раньше обходилось без него: гонка была
        /// безобидна, пока два экземпляра были взаимозаменяемы. С появлением
        /// подписок на события они перестали быть взаимозаменяемыми - подписчик,
        /// пришедший к проигравшему экземпляру, просто не позвался бы.
        /// </summary>
        private System.Xml.Serialization.XmlSerializer? _fallback;

        private readonly object _sync = new object();

        public XmlSerializer(Type type)
            : this(type, null, null, null, null)
        {
        }

        /// <summary>
        /// Перегрузки штатного конструктора существуют здесь <b>ради компиляции</b>.
        /// Ускорить вызов с непустым добавочным аргументом нечем: пространство имён
        /// XmlSerDe объявить не может, <see cref="XmlAttributeOverrides"/> меняет
        /// разметку тех типов, что генератор уже разобрал на этапе сборки, а
        /// <c>extraTypes</c> добавляет наследников, о которых сгенерированный код
        /// не знает. Поэтому такой сериализатор целиком уходит штатным путём.
        ///
        /// Отсутствие этих конструкторов раньше означало, что подмена одной строкой
        /// <c>global using</c> ломает компиляцию везде, где вызывающий ими
        /// пользовался, - то есть цена перехода была тем выше, чем больше проект.
        /// Работает ли ускорение на конкретном экземпляре, отвечает
        /// <see cref="IsAccelerated"/>.
        /// </summary>
        public XmlSerializer(Type type, string? defaultNamespace)
            : this(type, null, null, null, defaultNamespace)
        {
        }

        public XmlSerializer(Type type, Type[]? extraTypes)
            : this(type, null, extraTypes, null, null)
        {
        }

        public XmlSerializer(Type type, XmlAttributeOverrides? overrides)
            : this(type, overrides, null, null, null)
        {
        }

        public XmlSerializer(Type type, XmlRootAttribute? root)
            : this(type, null, null, root, null)
        {
        }

        public XmlSerializer(
            Type type,
            XmlAttributeOverrides? overrides,
            Type[]? extraTypes,
            XmlRootAttribute? root,
            string? defaultNamespace
            )
            : base()
        {
            _type = type ?? throw new ArgumentNullException(nameof(type));
            _overrides = overrides;
            _extraTypes = extraTypes;
            _root = root;
            _defaultNamespace = defaultNamespace;

            if (IsPlain)
            {
                _accelerated = XmlSerDeRegistry.TryGet(_type, out _entry);
            }
            else
            {
                _entry = default;
                _accelerated = false;
            }
        }

        /// <summary>
        /// Ни одного добавочного аргумента - то есть ровно то, что генератор
        /// разбирал на этапе сборки.
        /// </summary>
        private bool IsPlain =>
            _overrides is null
            && (_extraTypes is null || _extraTypes.Length == 0)
            && _root is null
            && string.IsNullOrEmpty(_defaultNamespace)
            ;

        /// <summary>
        /// Тип, ради которого сериализатор создан.
        /// </summary>
        public Type SubjectType => _type;

        /// <summary>
        /// Обслуживается ли тип быстрым путём. Полезно в тестах и в диагностике:
        /// молчаливое падение на штатный сериализатор иначе не увидеть.
        /// </summary>
        public bool IsAccelerated => _accelerated;

        #region события разбора

        //События объявлены заново через new по той же причине, что и быстрые
        //перегрузки: у базового класса они не виртуальные, и подписку, сделанную
        //на нём, наследник увидеть не может.
        //
        //Видеть её обязательно, потому что поднять эти события быстрый путь не в
        //состоянии в принципе: у XmlElementEventArgs, XmlNodeEventArgs и
        //XmlAttributeEventArgs нет ни одного публичного конструктора (проверено
        //рефлексией), а XmlSerializationReader.Init, через который BCL передаёт
        //читателю список подписчиков, внутренний. То есть аргументы события
        //снаружи сборки не создать вовсе - ни разбором спана, ни как-либо иначе.
        //
        //Поэтому подписка означает не «поднимем сами», а «этот экземпляр разбирает
        //документ штатным сериализатором целиком»: события поднимет он, с настоящим
        //XmlElement, с номером строки и со списком ожидавшихся элементов. Ускорения
        //при этом нет - зато нет и молчания там, где потребитель просил сообщить.
        private XmlNodeEventHandler? _unknownNode;
        private XmlElementEventHandler? _unknownElement;
        private XmlAttributeEventHandler? _unknownAttribute;
        private UnreferencedObjectEventHandler? _unreferencedObject;

        private volatile bool _hasDeserializationHandlers;

        public new event XmlNodeEventHandler UnknownNode
        {
            add
            {
                lock (_sync)
                {
                    _unknownNode += value;
                    if (_fallback is not null) { _fallback.UnknownNode += value; }
                    RefreshHandlerFlag();
                }
            }
            remove
            {
                lock (_sync)
                {
                    _unknownNode -= value;
                    if (_fallback is not null) { _fallback.UnknownNode -= value; }
                    RefreshHandlerFlag();
                }
            }
        }

        public new event XmlElementEventHandler UnknownElement
        {
            add
            {
                lock (_sync)
                {
                    _unknownElement += value;
                    if (_fallback is not null) { _fallback.UnknownElement += value; }
                    RefreshHandlerFlag();
                }
            }
            remove
            {
                lock (_sync)
                {
                    _unknownElement -= value;
                    if (_fallback is not null) { _fallback.UnknownElement -= value; }
                    RefreshHandlerFlag();
                }
            }
        }

        public new event XmlAttributeEventHandler UnknownAttribute
        {
            add
            {
                lock (_sync)
                {
                    _unknownAttribute += value;
                    if (_fallback is not null) { _fallback.UnknownAttribute += value; }
                    RefreshHandlerFlag();
                }
            }
            remove
            {
                lock (_sync)
                {
                    _unknownAttribute -= value;
                    if (_fallback is not null) { _fallback.UnknownAttribute -= value; }
                    RefreshHandlerFlag();
                }
            }
        }

        public new event UnreferencedObjectEventHandler UnreferencedObject
        {
            add
            {
                lock (_sync)
                {
                    _unreferencedObject += value;
                    if (_fallback is not null) { _fallback.UnreferencedObject += value; }
                    RefreshHandlerFlag();
                }
            }
            remove
            {
                lock (_sync)
                {
                    _unreferencedObject -= value;
                    if (_fallback is not null) { _fallback.UnreferencedObject -= value; }
                    RefreshHandlerFlag();
                }
            }
        }

        private void RefreshHandlerFlag()
        {
            //отписка возвращает ускорение: флаг считается заново, а не взводится
            //навсегда
            _hasDeserializationHandlers =
                _unknownNode is not null
                || _unknownElement is not null
                || _unknownAttribute is not null
                || _unreferencedObject is not null
                ;
        }

        /// <summary>
        /// Быстрый разбор допустим, только если тип ускорен и на события никто не
        /// подписан. Сериализации это не касается: событий про запись у
        /// <see cref="System.Xml.Serialization.XmlSerializer"/> нет вовсе.
        ///
        /// Свойство публичное по той же причине, что и <see cref="IsAccelerated"/>:
        /// «почему у меня не ускорилось» обязано иметь ответ, а подписка на событие
        /// стоит далеко от того места, где меряют скорость.
        /// </summary>
        public bool IsDeserializationAccelerated => CanDeserializeFast;

        private bool CanDeserializeFast => _accelerated && !_hasDeserializationHandlers;

        #endregion

        private System.Xml.Serialization.XmlSerializer Fallback
        {
            get
            {
                var fallback = _fallback;
                if (fallback is not null)
                {
                    return fallback;
                }

                lock (_sync)
                {
                    if (_fallback is not null)
                    {
                        return _fallback;
                    }

                    //простой случай строится простым же конструктором, а не пятиместным
                    //со всеми null: так поведение совпадает с тем, что было до появления
                    //перегрузок, буквально, а не по рассуждению
                    fallback = IsPlain
                        ? new System.Xml.Serialization.XmlSerializer(_type)
                        : new System.Xml.Serialization.XmlSerializer(
                            _type,
                            _overrides,
                            _extraTypes ?? Type.EmptyTypes,
                            _root,
                            _defaultNamespace
                            );

                    //подписки, сделанные до первого обращения, достаются экземпляру
                    //здесь: это единственное место, где он появляется на свет
                    if (_unknownNode is not null) { fallback.UnknownNode += _unknownNode; }
                    if (_unknownElement is not null) { fallback.UnknownElement += _unknownElement; }
                    if (_unknownAttribute is not null) { fallback.UnknownAttribute += _unknownAttribute; }
                    if (_unreferencedObject is not null) { fallback.UnreferencedObject += _unreferencedObject; }

                    _fallback = fallback;
                    return fallback;
                }
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
                //сток здесь свой, а не пользовательский, поэтому и кодировку выбирает
                //метод: utf-8 - единственный осмысленный ответ для строки, которую
                //дальше запишут в файл. StringWriter объявил бы utf-16, и один и тот же
                //метод отдавал бы разное объявление в зависимости от того, ускорен тип
                //или нет, - расхождение с самим собой хуже расхождения с BCL
                var sw = new Utf8StringWriter();
                Fallback.Serialize(sw, o);

                var text = sw.ToString();

                return appendXmlDeclaration
                    ? text
                    : CutDeclaration(text);
            }

            try
            {
                return SerializeFast(o, appendXmlDeclaration ? Utf8XmlDeclaration : null);
            }
            catch (Exception e)
            {
                throw SerializationFailed(e);
            }
        }

        /// <summary>
        /// Разбор прямо из спана - тот самый путь, ради которого существует XmlSerDe.
        /// Пролог срезается здесь, делегату достаётся уже корневой элемент.
        /// </summary>
        public object? Deserialize(ReadOnlySpan<char> xml)
        {
            if (!CanDeserializeFast)
            {
                //штатному сериализатору спан не скормить, деваться некуда
                using var reader = new StringReader(xml.ToString());
                return Fallback.Deserialize(reader);
            }

            try
            {
                return DeserializeFast(xml);
            }
            catch (Exception e)
            {
                throw DeserializationFailed(e);
            }
        }

        /// <summary>
        /// Собственно быстрая запись, без обёртывания ошибок: заворачивает их
        /// ровно один раз тот публичный метод, которого позвали. Вложенные вызовы
        /// иначе дали бы <see cref="InvalidOperationException"/> внутри такого же.
        ///
        /// Сток - двухпроходный: сначала
        /// <see cref="LengthEstimatorExhauster"/> (размер документа фасаду
        /// неизвестен), потом <see cref="PooledCharExhauster"/>. Иначе
        /// <see cref="StringBuilderExhauster"/> держал бы буфер и ещё раз
        /// копировал его в <c>string</c>.
        /// </summary>
        private string SerializeFast(object o, string? xmlDeclaration)
        {
            using var exhauster = CreatePooledChar(o, xmlDeclaration);
            return exhauster.ToString();
        }

        private PooledCharExhauster CreatePooledChar(object o, string? xmlDeclaration)
        {
            var estimator = new LengthEstimatorExhauster();
            _entry.Serialize(estimator, o, false);

            var extra = xmlDeclaration is null ? 0 : xmlDeclaration.Length;
            var exhauster = new PooledCharExhauster(estimator.EstimatedTotalLength + extra);
            try
            {
                if (xmlDeclaration is not null)
                {
                    exhauster.Append(xmlDeclaration);
                }

                _entry.Serialize(exhauster, o, false);
                return exhauster;
            }
            catch
            {
                exhauster.Dispose();
                throw;
            }
        }

        private void SerializeFastToStream(Stream stream, object o)
        {
            if (stream is MemoryStream memory)
            {
                var estimator = new LengthEstimatorExhauster();
                _entry.Serialize(estimator, o, false);
                TryEnsureMemoryStreamCapacity(
                    memory,
                    estimator.EstimatedTotalLength + Utf8XmlDeclaration.Length
                    );
            }

            using var utf8 = new Utf8StreamExhauster(stream);
            utf8.Append(Utf8XmlDeclaration);
            _entry.Serialize(utf8, o, false);
        }

        /// <summary>
        /// Оценка - в символах XML, байты UTF-8 для ASCII совпадают. Сверху
        /// 10%: на CJK и редкое занижение цифр этого часто хватает, чтобы
        /// поток не рос. Тройной запас <c>GetMaxByteCount</c> на HUGE раздул
        /// бы Capacity втрое.
        /// </summary>
        private static void TryEnsureMemoryStreamCapacity(MemoryStream memory, int charCount)
        {
            var slack = charCount / 10;
            var withSlack = charCount > int.MaxValue - slack
                ? int.MaxValue
                : charCount + slack;

            var needed = memory.Position + withSlack;
            if (needed > int.MaxValue || needed <= memory.Capacity)
            {
                return;
            }

            try
            {
                memory.Capacity = (int)needed;
            }
            catch (NotSupportedException)
            {
                //буфер, построенный поверх чужого массива, расширять нельзя
            }
        }

        /// <summary>
        /// Объявление под конкретный сток. null вместо кодировки - не ошибка:
        /// у самодельного <see cref="TextWriter"/> свойство вправе вернуть null,
        /// и объявление тогда пишется без неё.
        /// </summary>
        private static string BuildXmlDeclaration(Encoding? encoding)
        {
            if (encoding is null)
            {
                return XmlDeclarationHead + "?>";
            }

            return XmlDeclarationHead + " encoding=\"" + encoding.WebName + "\"?>";
        }

        /// <summary>
        /// <see cref="System.Xml.Serialization.XmlSerializer"/> отключить объявление
        /// не даёт, поэтому на медленном пути оно срезается с готового документа.
        /// Аккуратничать тут не с чем: объявление, если оно есть, стоит первым и
        /// кончается на первом же <c>?&gt;</c> - внутри него этих символов не бывает.
        /// </summary>
        private static string CutDeclaration(string xml)
        {
            if (!xml.StartsWith(XmlDeclarationHead, StringComparison.Ordinal))
            {
                return xml;
            }

            var end = xml.IndexOf("?>", StringComparison.Ordinal);
            if (end < 0)
            {
                return xml;
            }

            return xml.Substring(end + 2).TrimStart();
        }

        /// <summary>
        /// <see cref="StringWriter"/> объявляет свою кодировку utf-16 - это правда
        /// про строку в памяти и неправда про тот же текст, записанный в файл.
        /// </summary>
        private sealed class Utf8StringWriter : StringWriter
        {
            public override Encoding Encoding => Encoding.UTF8;
        }

        private object? DeserializeFast(ReadOnlySpan<char> xml)
        {
            return Deserialize(XmlPrologue.Cut(xml), _entry);
        }

        /// <summary>
        /// Ошибка на быстром пути обязана выглядеть как ошибка штатного
        /// сериализатора: тот заворачивает всё, что случилось внутри, в
        /// <see cref="InvalidOperationException"/> с настоящей причиной внутри.
        /// Без этого <c>catch (InvalidOperationException)</c>, стоявший в коде
        /// потребителя до подмены, перестал бы ловить - подмена не была бы прозрачной.
        /// </summary>
        private static InvalidOperationException SerializationFailed(Exception inner)
        {
            return new InvalidOperationException(SerializationErrorMessage, inner);
        }

        /// <summary>
        /// То же, но у ошибки документа форма строже: BCL отдаёт
        /// <see cref="InvalidOperationException"/> с <see cref="XmlException"/>
        /// внутри, и цепочка должна совпасть, иначе <c>catch</c> по
        /// <see cref="XmlException"/> в чужом коде перестанет ловить
        /// (docs/opt-in-xml-guards.md §6).
        ///
        /// Три случая, а не один. Сгенерированный хост со стражами заворачивает
        /// сам, и его результат надо пропустить как есть - иначе получится
        /// <c>IOE</c> внутри <c>IOE</c>. Пролог и проверка корня на
        /// <c>xsi:nil</c> исполняются здесь, до сгенерированного кода, и их
        /// ошибку заворачивать некому. Всё остальное - не про документ, и
        /// выдавать его за <see cref="XmlException"/> было бы враньём.
        /// </summary>
        private static InvalidOperationException DeserializationFailed(Exception inner)
        {
            if (XmlDocumentErrors.IsWrapped(inner))
            {
                return (InvalidOperationException)inner;
            }

            if (inner is XmlDocumentException document)
            {
                return XmlDocumentErrors.Wrap(document);
            }

            return new InvalidOperationException(DeserializationErrorMessage, inner);
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
            XmlScan.ReadHeadMarkup(
                cdata: true,
                flexibleXsiPrefix: true,
                root, ReadOnlySpan<char>.Empty, ref head
                );

            if (head.IsBodyless && head.IsNil())
            {
                return null;
            }

            return entry.Deserialize(root);
        }

        public new void Serialize(TextWriter textWriter, object o)
        {
            //проверка стоит до развилки, потому что у BCL она стоит до всего
            //остального: это ArgumentNullException с именем «output», а не
            //завёрнутая ошибка записи
            if (textWriter is null)
            {
                throw new ArgumentNullException("output");
            }

            if (!_accelerated || o is null)
            {
                Fallback.Serialize(textWriter, o);
                return;
            }

            try
            {
                //Encoding читается внутри try намеренно: у BCL обращение к свойству
                //стока тоже происходит внутри его собственного try, и своё исключение
                //самодельный TextWriter отдаст завёрнутым, а не голым
                using var exhauster = CreatePooledChar(o, BuildXmlDeclaration(textWriter.Encoding));
                exhauster.WriteTo(textWriter);
            }
            catch (Exception e)
            {
                throw SerializationFailed(e);
            }
        }

        public new void Serialize(Stream stream, object o)
        {
            if (stream is null)
            {
                throw new ArgumentNullException("output");
            }

            if (!_accelerated || o is null)
            {
                Fallback.Serialize(stream, o);
                return;
            }

            try
            {
                //перегрузка со Stream кодировку не выбирает: BCL пишет в неё utf-8
                //независимо ни от чего (проверено), поэтому объявление здесь константа
                SerializeFastToStream(stream, o);
            }
            catch (Exception e)
            {
                throw SerializationFailed(e);
            }
        }

        public new object? Deserialize(TextReader textReader)
        {
            if (!CanDeserializeFast)
            {
                return Fallback.Deserialize(textReader);
            }

            //ArgumentNullException здесь не выставляется намеренно: BCL на null-читателе
            //даёт не его, а завёрнутый XmlException «Root element is missing» - то есть
            //ровно то, что даст обёртка ниже
            try
            {
                using var text = PooledCharText.ReadAll(textReader);
                return DeserializeFast(text.Span);
            }
            catch (Exception e)
            {
                throw DeserializationFailed(e);
            }
        }

        public new object? Deserialize(Stream stream)
        {
            if (stream is null)
            {
                throw new ArgumentNullException("input");
            }

            if (!CanDeserializeFast)
            {
                return Fallback.Deserialize(stream);
            }

            try
            {
                //поток чужой, закрывать его нельзя: leaveOpen оставляет его открытым
                using var reader = new StreamReader(
                    stream,
                    Encoding.UTF8,
                    detectEncodingFromByteOrderMarks: true,
                    bufferSize: 1024,
                    leaveOpen: true
                    );
                using var text = PooledCharText.ReadAll(reader);
                return DeserializeFast(text.Span);
            }
            catch (Exception e)
            {
                throw DeserializationFailed(e);
            }
        }

        #endregion

        #region входы, которые обязаны уйти штатному сериализатору целиком

        //Всё, что здесь объявлено заново через new, объединено одним свойством:
        //быстрый путь такой вызов обслужить не может, а базовый класс о нашем
        //существовании не знает и молча довёл бы его до контракта предгенерированных
        //сборок - то есть до быстрого пути. Отсюда и приём: перекрыть перегрузку
        //там, где статический тип в точке вызова - фасад (а это и есть сценарий
        //подмены одной строкой), и отдать её штатному сериализатору.
        //
        //Через ссылку базового типа перекрытие не работает - у базового класса эти
        //перегрузки не виртуальные. Это описано в README как известная дыра; закрыть
        //её нечем, кроме как перестать быть drop-in.

        /// <summary>
        /// Список подписчиков, переданный прямо в вызов, - вход в обход подписки
        /// на самом сериализаторе. Поднять эти события мы всё равно не можем
        /// (см. регион событий), поэтому непустой список означает штатный путь.
        /// </summary>
        public new object? Deserialize(XmlReader xmlReader, XmlDeserializationEvents events)
        {
            if (!_accelerated || HasHandlers(events))
            {
                return Fallback.Deserialize(xmlReader, events);
            }

            return Deserialize(xmlReader);
        }

        public new object? Deserialize(XmlReader xmlReader, string? encodingStyle, XmlDeserializationEvents events)
        {
            if (!_accelerated || encodingStyle is not null || HasHandlers(events))
            {
                return Fallback.Deserialize(xmlReader, encodingStyle, events);
            }

            return Deserialize(xmlReader);
        }

        /// <summary>
        /// SOAP-кодирование XmlSerDe не умеет вовсе, и притвориться тут нечем:
        /// на непустом encodingStyle штатный сериализатор, собранный не через
        /// <c>SoapReflectionImporter</c>, бросает «The encoding style ... is not
        /// valid for this call» (проверено прогоном). Быстрый путь молча написал бы
        /// обычный документ - хуже некуда: вызывающий просил другую форму.
        /// </summary>
        public new object? Deserialize(XmlReader xmlReader, string? encodingStyle)
        {
            if (!CanDeserializeFast || encodingStyle is not null)
            {
                return Fallback.Deserialize(xmlReader, encodingStyle);
            }

            return Deserialize(xmlReader);
        }

        public new void Serialize(XmlWriter xmlWriter, object? o, XmlSerializerNamespaces? namespaces, string? encodingStyle)
        {
            if (!_accelerated || encodingStyle is not null)
            {
                Fallback.Serialize(xmlWriter, o, namespaces, encodingStyle);
                return;
            }

            Serialize(xmlWriter, o, namespaces);
        }

        public new void Serialize(XmlWriter xmlWriter, object? o, XmlSerializerNamespaces? namespaces, string? encodingStyle, string? id)
        {
            if (!_accelerated || encodingStyle is not null || id is not null)
            {
                Fallback.Serialize(xmlWriter, o, namespaces, encodingStyle, id);
                return;
            }

            Serialize(xmlWriter, o, namespaces);
        }

        private static bool HasHandlers(XmlDeserializationEvents events)
        {
            return
                events.OnUnknownNode is not null
                || events.OnUnknownElement is not null
                || events.OnUnknownAttribute is not null
                || events.OnUnreferencedObject is not null
                ;
        }

        /// <summary>
        /// Штатная фабрика «сериализатор на каждый тип». Перекрыта затем же, зачем
        /// объявлены конструкторы: чтобы подмена одной строкой не теряла ускорение
        /// там, где вызывающий пользуется именно ей. Поведение снято прогоном:
        /// null на входе - это пустой массив, а не исключение.
        /// </summary>
        public static new System.Xml.Serialization.XmlSerializer[] FromTypes(Type[]? types)
        {
            if (types is null)
            {
                return new System.Xml.Serialization.XmlSerializer[0];
            }

            var result = new System.Xml.Serialization.XmlSerializer[types.Length];
            for (var i = 0; i < types.Length; i++)
            {
                result[i] = new XmlSerializer(types[i]);
            }

            return result;
        }

        #endregion

        #region контракт предгенерированных сборок - работает и через базовый тип

        //ошибки здесь намеренно не заворачиваются: публичные Serialize/Deserialize
        //базового класса, которые до сюда доводят, уже стоят в try и заворачивают
        //сами. Второй слой дал бы InvalidOperationException внутри такого же, а у
        //BCL уровень ровно один - проверено прогоном

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
            using var exhauster = CreatePooledChar(o, xmlDeclaration: null);
            exhauster.WriteRawTo(xmlWriter);
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

            //подписка, сделанная через ссылку типа фасада, видна и здесь: путь через
            //базовый тип поднять события тоже не может - читатель наш, а список
            //подписчиков базовый класс кладёт в него внутренним методом
            if (!CanDeserializeFast)
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
        /// Ровно то же, что делает <see cref="System.Xml.Serialization.XmlSerializer"/>:
        /// сравнивает имя корневого элемента (проверено прогоном - документ в чужом
        /// пространстве имён даёт false, а тип с <c>[XmlRoot("иное")]</c> отзывается
        /// только на это имя, но не на имя типа). Разница в цене: у штатного
        /// сериализатора ответ на этот вопрос требует сперва разобрать тип рефлексией
        /// и построить план сериализации целиком.
        ///
        /// Читатель после вызова остаётся стоять на корневом элементе - <c>MoveToContent</c>
        /// делает и та, и другая реализация, - так что следом идущий <c>Deserialize</c>
        /// работает.
        /// </summary>
        public override bool CanDeserialize(XmlReader xmlReader)
        {
            if (!_accelerated)
            {
                return Fallback.CanDeserialize(xmlReader);
            }

            //null здесь намеренно не проверяется: BCL на нём даёт NullReferenceException,
            //а не ArgumentNullException (проверено), и обращение ниже даст его же
            return xmlReader.IsStartElement(_entry.RootElementName, string.Empty);
        }

        #endregion
    }
}
