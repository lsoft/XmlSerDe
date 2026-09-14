#if NETSTANDARD
using XmlSerDe;
using XmlSerDe.Internal;

namespace XmlSerDe.Generator.Producer
{
    /// <summary>
    /// Compile-time binding of <see cref="XmlGuard"/> to the snippets a host
    /// emits. Built once from the OR of <c>[XmlGuards]</c> (or from
    /// <see cref="XmlGuard.SystemXmlCompatible"/> for compat) and then only
    /// read: flag tests live here, not in <see cref="ClassSourceProducer"/>
    /// generate methods - docs/opt-in-xml-guards.md §5.0.
    ///
    /// Второй binding рядом с <see cref="HostFeatureBinding"/>, а не общий с ним:
    /// вопросы разные ("понимать ли конструкцию" против "отказать ли, если
    /// XML 1.0 её запрещает"), и сливать их в один тип с дюжиной флагов - тот же
    /// беспорядок, только в одном файле. Точек, где оси встречаются, три, и все
    /// три разрешаются здесь, в <see cref="From"/>: набор фич приходит вторым
    /// аргументом и дальше не участвует.
    /// </summary>
    public readonly struct HostGuardBinding
    {
        /// <summary>
        /// Сгенерированный файл пишется с CRLF, как и остальной шаблон.
        /// </summary>
        private const string NewLine = "\r\n";

        private const string XmlScanFullName = "global::XmlSerDe.Internal.XmlScan";
        private const string DocumentErrorsFullName = "global::XmlSerDe.XmlDocumentErrors";
        private const string DocumentExceptionFullName = "global::XmlSerDe.XmlDocumentException";
        private const string CharGuardFullName = "global::XmlSerDe.Internal.XmlCharGuard";

        public readonly XmlGuard Guards;

        /// <summary>
        /// Как корневой вход забирает съеденное телом: <c>out _</c> у хоста без
        /// <see cref="XmlGuard.SingleRoot"/> - число ему не нужно и складывать
        /// его не с чем.
        /// </summary>
        public readonly string RootBodyConsumedArgument;

        /// <summary>
        /// Имя проверки хвоста или пустая строка. Их две, и выбор между ними -
        /// единственное место, где <see cref="XmlGuard.SingleRoot"/> смотрит на
        /// <see cref="XmlFeature.Markup"/>: после корня XML разрешает Misc,
        /// и хост, понимающий комментарии внутри документа, обязан понимать их
        /// и в хвосте.
        /// </summary>
        private readonly string _ensureNoTrailingContent;

        /// <summary>
        /// Лишний параметр <c>DeserializeBody</c> - ожидаемое имя закрывающего
        /// тега - или пустая строка. У хоста без
        /// <see cref="XmlGuard.MatchingEndTags"/> сигнатура не меняется вовсе:
        /// сверять нечего, а передавать спан "на всякий случай" - это и есть
        /// цена, которой default платить не должен.
        /// </summary>
        public readonly string ExpectedEndNameParameter;

        private readonly bool _matchingEndTags;

        private readonly bool _uniqueAttributes;

        private readonly bool _illegalChars;

        private readonly bool _foreignRootNamespace;

        /// <summary>
        /// Есть ли хоть один страж. От этого зависит форма исключения (§6):
        /// хост со стражами отдаёт наружу пару BCL, хост без них - сегодняшний
        /// <see cref="XmlDocumentException"/> как есть.
        /// </summary>
        public bool Any
        {
            get
            {
                return Guards != XmlGuard.None;
            }
        }

        private const string ExpectedEndNameVariable = "expectedEndName";

        private HostGuardBinding(
            XmlGuard guards,
            string rootBodyConsumedArgument,
            string ensureNoTrailingContent,
            bool matchingEndTags,
            bool uniqueAttributes,
            bool illegalChars,
            bool foreignRootNamespace
            )
        {
            Guards = guards;
            RootBodyConsumedArgument = rootBodyConsumedArgument;
            _ensureNoTrailingContent = ensureNoTrailingContent;
            _matchingEndTags = matchingEndTags;
            _uniqueAttributes = uniqueAttributes;
            _illegalChars = illegalChars;
            _foreignRootNamespace = foreignRootNamespace;
            ExpectedEndNameParameter = matchingEndTags
                ? ", roschar " + ExpectedEndNameVariable
                : "";
        }

        public static HostGuardBinding From(XmlGuard guards, XmlFeature features)
        {
            var singleRoot = Has(guards, XmlGuard.SingleRoot);
            var markup = HasFeature(features, XmlFeature.Markup);

            string ensureNoTrailingContent;
            if (!singleRoot)
            {
                ensureNoTrailingContent = "";
            }
            else if (markup)
            {
                ensureNoTrailingContent = nameof(XmlScan.EnsureNoTrailingContentMarkup);
            }
            else
            {
                ensureNoTrailingContent = nameof(XmlScan.EnsureNoTrailingContent);
            }

            return new HostGuardBinding(
                guards,
                singleRoot ? "out var bodyConsumed" : "out _",
                ensureNoTrailingContent,
                Has(guards, XmlGuard.MatchingEndTags),
                Has(guards, XmlGuard.UniqueAttributes),
                Has(guards, XmlGuard.IllegalChars),
                Has(guards, XmlGuard.ForeignRootNamespace)
                );
        }

        /// <summary>
        /// Аргумент к <see cref="ExpectedEndNameParameter"/> на вызове
        /// <c>DeserializeBody</c>. Имя берётся из уже прочитанной головы -
        /// то есть из документа, а не из отображения: у корня генератор
        /// допускает два имени сразу, а у полиморфного члена тело разбирает
        /// производный тип, тогда как в документе стоит имя члена.
        /// </summary>
        public string ExpectedEndNameArgument(string headVar)
        {
            return ExpectedEndNameArgumentRaw(
                headVar,
                headVar + "." + nameof(XmlHead.DeclaredNodeType)
                );
        }

        /// <summary>
        /// То же, но имя уже лежит в отдельной переменной (её заводит цикл
        /// разбора членов).
        ///
        /// Пустое имя означает "закрывающего тега не будет вовсе": у ноды
        /// <c>&lt;Foo/&gt;</c> тела нет, и требовать от неё закрытия - значит
        /// падать на well-formed документе. Признак едет тем же параметром,
        /// потому что это один и тот же вопрос: что именно тело обязано увидеть
        /// в конце.
        /// </summary>
        public string ExpectedEndNameArgumentRaw(string headVar, string nameExpression)
        {
            if (!_matchingEndTags)
            {
                return "";
            }

            return ", " + headVar + "." + nameof(XmlHead.IsBodyless)
                + " ? roschar.Empty : " + nameExpression;
        }

        /// <summary>
        /// Что делает цикл разбора тела, увидев закрывающий тег. Без стража -
        /// то же, что и раньше: любой <c>&lt;/…&gt;</c> заканчивает тело.
        /// </summary>
        public string EndTagStatement(string indent, string headVar, string expectedExpression)
        {
            if (!_matchingEndTags)
            {
                return "";
            }

            return NewLine + indent + XmlScanFullName + "." + nameof(XmlScan.EnsureEndTagName)
                + "(" + headVar + "." + nameof(XmlHead.DeclaredNodeType)
                + ", " + expectedExpression + ");";
        }

        /// <summary>
        /// Что делает тот же цикл, упершись в конец ввода. Без стража это
        /// нормальный выход - и именно поэтому обрезанный документ сегодня
        /// даёт успех с частично заполненным объектом.
        /// </summary>
        public string EndOfInputStatement(string indent, string expectedCondition)
        {
            if (!_matchingEndTags)
            {
                return "";
            }

            return NewLine + indent + "if(" + expectedCondition + ") "
                + XmlScanFullName + "." + nameof(XmlScan.ThrowUnexpectedEof) + "();";
        }

        /// <summary>
        /// Проверка строки, доехавшей до POCO. Ставится <b>после</b> разбора,
        /// а не внутрь декодера, и вот почему: без
        /// <see cref="XmlFeature.CData"/> строку декодирует не ядро, а
        /// пользовательский <c>IInjector</c>, и выбрасывать его из разбора
        /// из-за стража нельзя (opt-in-xml-features.md §16, п. 8). Одна точка
        /// на оба пути дешевле пары checked-перегрузок декодера, которые всё
        /// равно закрыли бы только один из них.
        /// </summary>
        public string CheckStringStatement(string indent, bool isString, string varName)
        {
            if (!_illegalChars || !isString)
            {
                return "";
            }

            return NewLine + indent + CharGuardFullName + "."
                + nameof(XmlCharGuard.EnsureValidInputChars) + "(" + varName + ".AsSpan());";
        }

        /// <summary>
        /// То же для значения атрибута: оно уже разобрано и нормализовано
        /// (<c>ParseAttribute</c> отдаёт спан после §3.3.3), поэтому
        /// проверяется спан, а не строка, и до материализации.
        /// </summary>
        public string CheckSpanStatement(string indent, string spanExpression)
        {
            if (!_illegalChars)
            {
                return "";
            }

            return NewLine + indent + CharGuardFullName + "."
                + nameof(XmlCharGuard.EnsureValidInputChars) + "(" + spanExpression + ");";
        }

        /// <summary>
        /// То же, но перед уже существующим оператором, а не после него:
        /// проверка обязана случиться до материализации строки.
        /// </summary>
        public string CheckSpanStatementBefore(string indent, string spanExpression)
        {
            if (!_illegalChars)
            {
                return "";
            }

            return CharGuardFullName + "." + nameof(XmlCharGuard.EnsureValidInputChars)
                + "(" + spanExpression + ");" + NewLine + indent;
        }

        /// <summary>
        /// Что делают сразу после чтения головы. Проверка уникальности имён
        /// атрибутов - единственное, что здесь бывает, и она вплавлена в уже
        /// разобранную голову: второго прохода по документу нет, а голова без
        /// атрибутов отсекается полем <see cref="XmlHead.HasAttributes"/>,
        /// которое сканер и так посчитал.
        /// </summary>
        public string AfterReadHeadStatement(string indent, string headVar)
        {
            if (!_uniqueAttributes)
            {
                return "";
            }

            return NewLine + indent
                + "if(" + headVar + "." + nameof(XmlHead.HasAttributes) + ") "
                + XmlScanFullName + "." + nameof(XmlScan.EnsureUniqueAttributes)
                + "(" + headVar + "." + nameof(XmlHead.FullHead)
                + ", " + headVar + "." + nameof(XmlHead.DeclaredNodeType) + ");";
        }

        /// <summary>
        /// Что делают после чтения головы <b>корня</b> - и только его.
        ///
        /// Отдельный крюк рядом с <see cref="AfterReadHeadStatement"/>, а не флаг
        /// внутри него, именно потому, что тот зовётся с трёх мест: корень,
        /// ребёнок и элемент коллекции. Проверка пространства имён нужна ровно на
        /// корне - там она стоит один разбор головы за документ, а на каждом
        /// элементе стоила бы столько же, сколько сам разбор.
        /// </summary>
        public string RootHeadStatement(string indent, string headVar)
        {
            if (!_foreignRootNamespace)
            {
                return "";
            }

            return NewLine + indent
                + "if(" + headVar + "." + nameof(XmlHead.HasAttributes) + ") "
                + XmlScanFullName + "." + nameof(XmlScan.EnsureNoForeignRootNamespace)
                + "(" + headVar + "." + nameof(XmlHead.FullHead)
                + ", " + headVar + "." + nameof(XmlHead.DeclaredNodeType) + ");";
        }

        /// <summary>
        /// Имя, которое скалярное тело сверяет со своим закрывающим тегом.
        /// У типа с <c>[XmlText]</c> его сегодня не с чем сверять
        /// (<c>roschar.Empty</c> отключает проверку в <c>EndTagLength</c>),
        /// а со стражем - есть.
        /// </summary>
        public string TextBodyExpectedName(string fallback)
        {
            return _matchingEndTags ? ExpectedEndNameVariable : fallback;
        }

        /// <summary>
        /// Проверка хвоста документа сразу после разбора корня. Голова уже
        /// прочитана, съеденное телом только что получено - складывать больше
        /// нечего и второй раз документ не сканируется.
        /// </summary>
        /// <remarks>
        /// Возвращается вместе с переводом строки и отступом - или пустая строка,
        /// не оставляющая в <c>.g.cs</c> даже пустой строки: текст хоста без
        /// стражей обязан совпадать с прежним побайтно (§14.1). Тот же приём,
        /// что у <c>GenerateDeserializeAttributesInvocation</c>.
        /// </remarks>
        public string RootTailStatement(string indent, string fullNodeVar, string headVar)
        {
            if (_ensureNoTrailingContent.Length == 0)
            {
                return "";
            }

            return "\r\n" + indent + XmlScanFullName + "." + _ensureNoTrailingContent
                + "(" + fullNodeVar
                + ", " + headVar + "." + nameof(XmlHead.TotalLength) + " + bodyConsumed);";
        }

        /// <summary>
        /// Тело публичного корневого метода. У хоста без стражей это тот же
        /// один вызов, что и был; у хоста со стражами - он же под единственным
        /// на весь разбор <c>try</c>. Обёртка одна и на самом верху: три слоя
        /// <c>InvalidOperationException</c> вокруг одной причины совместимости
        /// не добавляют.
        /// </summary>
        public string RootBodyStatement(string indent, string invocation)
        {
            if (!Any)
            {
                return indent + invocation;
            }

            return indent + "try" + "\r\n"
                + indent + "{" + "\r\n"
                + indent + "    " + invocation + "\r\n"
                + indent + "}" + "\r\n"
                + indent + "catch (" + DocumentExceptionFullName + " exception)" + "\r\n"
                + indent + "{" + "\r\n"
                + indent + "    //docs/opt-in-xml-guards.md §6: наружу форма BCL -"
                + " InvalidOperationException, внутри XmlException, без позиции" + "\r\n"
                + indent + "    throw " + DocumentErrorsFullName + "." + nameof(XmlDocumentErrors.Wrap) + "(exception);" + "\r\n"
                + indent + "}";
        }

        private static bool Has(XmlGuard guards, XmlGuard flag)
        {
            return (guards & flag) == flag;
        }

        private static bool HasFeature(XmlFeature features, XmlFeature flag)
        {
            return (features & flag) == flag;
        }
    }
}
#endif
