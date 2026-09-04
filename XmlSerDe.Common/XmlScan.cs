using System;
#if NET8_0_OR_GREATER
using System.Buffers;
#endif
using System.Runtime.CompilerServices;
using roschar = System.ReadOnlySpan<char>;

namespace XmlSerDe.Common
{
    /// <summary>
    /// Что стоит в позиции курсора.
    /// </summary>
    public enum XmlHeadKind : byte
    {
        /// <summary>
        /// Разбирать больше нечего.
        /// </summary>
        EndOfInput = 0,

        /// <summary>
        /// Открывающий тег: &lt;Foo ...&gt; или &lt;Foo .../&gt;
        /// </summary>
        StartTag = 1,

        /// <summary>
        /// Закрывающий тег: &lt;/Foo&gt;. Для вызывающей стороны это сигнал,
        /// что дети кончились и нода закрылась.
        /// </summary>
        EndTag = 2,
    }

    /// <summary>
    /// Голова одного тега. В отличие от <see cref="XmlNode2"/> не знает, где нода
    /// заканчивается, и знать не должна: границу устанавливает сам разбор
    /// (см. <see cref="XmlScan.ReadHead"/>).
    /// </summary>
    public readonly ref struct XmlHead
    {
        public readonly XmlHeadKind Kind;

        /// <summary>
        /// Закрытая нода &lt;Foo/&gt; - тела нет.
        /// </summary>
        public readonly bool IsBodyless;

        /// <summary>
        /// Имя упёрлось не в '&gt;', то есть атрибуты возможны. Если false,
        /// атрибутов заведомо нет и весь атрибутный код можно пропустить.
        /// </summary>
        public readonly bool HasAttributes;

        /// <summary>
        /// Сколько символов от начала переданного курсора занимают пропущенные
        /// пробелы/комментарии/PI плюс сама голова. Тело ноды начинается
        /// ровно через столько символов.
        /// </summary>
        public readonly int TotalLength;

        /// <summary>
        /// Имя тега.
        /// </summary>
        public readonly roschar DeclaredNodeType;

        /// <summary>
        /// От '&lt;' до '&gt;' включительно, без ведущих пробелов.
        /// </summary>
        public readonly roschar FullHead;

        /// <summary>
        /// Имя атрибута, которым объявлен xsi (унаследованное либо найденное здесь).
        /// </summary>
        public readonly roschar XmlnsAttributeName;

        public XmlHead()
        {
            Kind = XmlHeadKind.EndOfInput;
            IsBodyless = true;
            HasAttributes = false;
            TotalLength = 0;
            DeclaredNodeType = roschar.Empty;
            FullHead = roschar.Empty;
            XmlnsAttributeName = roschar.Empty;
        }

        public XmlHead(
            XmlHeadKind kind,
            bool isBodyless,
            bool hasAttributes,
            int totalLength,
            roschar declaredNodeType,
            roschar fullHead,
            roschar xmlnsAttributeName
            )
        {
            Kind = kind;
            IsBodyless = isBodyless;
            HasAttributes = hasAttributes;
            TotalLength = totalLength;
            DeclaredNodeType = declaredNodeType;
            FullHead = fullHead;
            XmlnsAttributeName = xmlnsAttributeName;
        }

        public readonly bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Kind == XmlHeadKind.EndOfInput;
        }

        public readonly bool IsEndTag
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Kind == XmlHeadKind.EndTag;
        }

        /// <summary>
        /// Для
        /// &lt;BaseType xmlns:p3="http://www.w3.org/2001/XMLSchema-instance" p3:type="ChildType"&gt;
        /// возвращает ChildType. Если xmlns или type нет - roschar.Empty.
        /// Opt-in <see cref="XmlFeature.FlexibleXsiPrefix"/>; default -
        /// <see cref="GetXsiType"/>.
        ///
        /// Кавычки значения отдельной осью больше не являются: разбор атрибутов
        /// всегда quote-aware, поэтому вариантов у пары type/nil два, а не
        /// четыре.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly roschar GetPreciseNodeType()
        {
            if (!HasAttributes || XmlnsAttributeName.IsEmpty)
            {
                return roschar.Empty;
            }

            XmlScan.ParseAttribute(
                FullHead,
                DeclaredNodeType.Length + 1,
                XmlnsAttributeName,
                XmlScan.TypeSpan,
                roschar.Empty,
                out var parsedAttribute
                );

            return parsedAttribute.Value;
        }

        /// <summary>
        /// Literal prefix <c>xsi</c> and local name <c>type</c>. Default path
        /// without <see cref="XmlFeature.FlexibleXsiPrefix"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly roschar GetXsiType()
        {
            if (!HasAttributes)
            {
                return roschar.Empty;
            }

            XmlScan.ParseAttribute(
                FullHead,
                DeclaredNodeType.Length + 1,
                XmlScan.XsiSpan,
                XmlScan.TypeSpan,
                roschar.Empty,
                out var parsedAttribute
                );

            return parsedAttribute.Value;
        }

        /// <summary>
        /// То же, что <see cref="GetXsiType"/>, но перед разбором стоит
        /// отрицательный фильтр: нет в голове подстроки <c>xsi:type</c> - нет
        /// и такого атрибута, потому что QName разрывов не допускает. Ложное
        /// срабатывание (подстрока внутри чужого значения) проваливается
        /// в честный разбор, поэтому результат совпадает с
        /// <see cref="GetXsiType"/> всегда, а не почти всегда.
        ///
        /// Генератор ставит эту форму типу <b>без наследников</b>: там
        /// <c>xsi:type</c> ничего не диспетчеризует и может только совпасть
        /// с собственным именем типа либо стать ошибкой, то есть в подавляющем
        /// большинстве документов фильтр промахивается - а промах здесь и есть
        /// выигрыш. Типу с наследниками ставится <see cref="GetXsiType"/>:
        /// там атрибут обычно на месте, фильтр нашёл бы его и разбор всё равно
        /// пошёл бы следом, то есть голова читалась бы дважды.
        ///
        /// Замерено (probe <c>--head-walk</c>): на документе из тридцати нод
        /// с тремя атрибутами каждая фильтр даёт x0.81; там, где
        /// <c>xsi:type</c> реально стоит, он стоил бы x1.02. Отсюда и разделение
        /// по наличию наследников, а не один общий метод.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly roschar GetXsiTypePrefiltered()
        {
            if (!HasAttributes || FullHead.IndexOf(XmlScan.XsiTypeSpan) < 0)
            {
                return roschar.Empty;
            }

            XmlScan.ParseAttribute(
                FullHead,
                DeclaredNodeType.Length + 1,
                XmlScan.XsiSpan,
                XmlScan.TypeSpan,
                roschar.Empty,
                out var parsedAttribute
                );

            return parsedAttribute.Value;
        }

        /// <summary>
        /// <see cref="GetPreciseNodeType"/> с тем же отрицательным фильтром,
        /// что и <see cref="GetXsiTypePrefiltered"/>. Префикс здесь известен
        /// только в рантайме, склеить его с <c>":type"</c> без аллокации
        /// нельзя - но подходящий атрибут в любом случае содержит <c>:type</c>,
        /// и этого хватает: фильтр обязан быть строгим только в одну сторону.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly roschar GetPreciseNodeTypePrefiltered()
        {
            if (!HasAttributes
                || XmlnsAttributeName.IsEmpty
                || FullHead.IndexOf(XmlScan.ColonTypeSpan) < 0
                )
            {
                return roschar.Empty;
            }

            XmlScan.ParseAttribute(
                FullHead,
                DeclaredNodeType.Length + 1,
                XmlnsAttributeName,
                XmlScan.TypeSpan,
                roschar.Empty,
                out var parsedAttribute
                );

            return parsedAttribute.Value;
        }

        /// <summary>
        /// Стоит ли на теге xsi:nil="true". Opt-in
        /// <see cref="XmlFeature.FlexibleXsiPrefix"/>: подойдёт любой атрибут
        /// с именем nil. Default - <see cref="IsXsiNil"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool IsNil()
        {
            if (!HasAttributes)
            {
                return false;
            }

            XmlScan.ParseAttribute(
                FullHead,
                DeclaredNodeType.Length + 1,
                roschar.Empty,
                XmlScan.NilSpan,
                XmlScan.TrueSpan,
                out var parsedAttribute
                );

            return !parsedAttribute.IsEmpty;
        }

        /// <summary>
        /// Literal <c>xsi:nil="true"</c>. Default path without
        /// <see cref="XmlFeature.FlexibleXsiPrefix"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool IsXsiNil()
        {
            if (!HasAttributes)
            {
                return false;
            }

            XmlScan.ParseAttribute(
                FullHead,
                DeclaredNodeType.Length + 1,
                XmlScan.XsiSpan,
                XmlScan.NilSpan,
                XmlScan.TrueSpan,
                out var parsedAttribute
                );

            return !parsedAttribute.IsEmpty;
        }
    }

    /// <summary>
    /// Сканирующие примитивы. Общие для устаревшего <see cref="XmlNode2"/>
    /// и для однопроходного разбора.
    ///
    /// Ни один метод здесь не принимает <see cref="XmlDeserializeSettings"/>.
    /// Это не стилистика, а обход CS8352 - спан, отданный через out из метода,
    /// у которого есть ref-параметр ref-структурного типа, компилятор обязан
    /// считать потенциально ссылающимся на него, и положить такой спан в поле
    /// ref struct уже нельзя.
    ///
    /// Набор фич тоже не параметр: какие конструкции хост понимает, решено на
    /// этапе генерации, и хост зовёт соответствующую перегрузку
    /// (docs/opt-in-xml-features.md §9.1). Булевы параметры остались только у
    /// разметочных вариантов - это не POCO-hot-path.
    /// </summary>
    public static class XmlScan
    {
        public static roschar XmlnsSpan
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => "xmlns".AsSpan();
        }

        public static roschar XmlnsHttpSpan
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => "http://www.w3.org/2001/XMLSchema-instance".AsSpan();
        }

        public static roschar TypeSpan
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => "type".AsSpan();
        }

        /// <summary>
        /// Отрицательный фильтр для <see cref="XmlHead.GetXsiTypePrefiltered"/>:
        /// QName разрывов не допускает, поэтому атрибут с префиксом <c>xsi</c>
        /// и именем <c>type</c> обязан лежать в голове этой подстрокой.
        /// </summary>
        public static roschar XsiTypeSpan
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => "xsi:type".AsSpan();
        }

        /// <summary>
        /// То же для <see cref="XmlHead.GetPreciseNodeTypePrefiltered"/>, где
        /// префикс известен только в рантайме: склеить его со спаном без
        /// аллокации нельзя, но любой подходящий атрибут всё равно кончается
        /// на <c>:type</c>.
        /// </summary>
        public static roschar ColonTypeSpan
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ":type".AsSpan();
        }

        public static roschar NilSpan
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => "nil".AsSpan();
        }

        public static roschar TrueSpan
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => "true".AsSpan();
        }

        public static roschar XsiSpan
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => "xsi".AsSpan();
        }

        private static roschar CDataHeadSpan
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => "<![CDATA[".AsSpan();
        }

        private static roschar CDataTailSpan
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => "]]>".AsSpan();
        }

        private static roschar CommentHeadSpan
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => "<!--".AsSpan();
        }

        private static roschar CommentTailSpan
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => "-->".AsSpan();
        }

        private static roschar PiTailSpan
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => "?>".AsSpan();
        }

#if NET8_0_OR_GREATER
        /// <summary>
        /// Конец имени ноды или атрибута: любой символ XML S production
        /// (#x20 | #x9 | #xD | #xA, XML 1.0 §2.3) либо '/' либо '&gt;'.
        ///
        /// Именно этот набор из шести значений <see cref="ScanHead"/> не мог искать
        /// одним вызовом на старых рантаймах: IndexOfAny(roschar) векторизован
        /// только до пяти значений, дальше идёт вероятностный (bloom-filter) скан.
        /// SearchValues строит ASCII-битмап один раз на весь процесс, и ограничение
        /// снимается - вместе с ним уходит и скалярный добор по префиксу.
        /// </summary>
        private static readonly SearchValues<char> NameEndValues =
            SearchValues.Create("/> \t\r\n");

        /// <summary>
        /// Что может закончить имя атрибута: ':' (префиксованное имя), '=' (имя
        /// без префикса), '/' и '&gt;' (нода закрылась, атрибутов больше нет)
        /// либо любой символ S (XML 1.0 §3.1 разрешает пробелы вокруг '=').
        ///
        /// S здесь не для того, чтобы принимать больше, а чтобы имя кончалось
        /// там, где оно кончается по спецификации: без S в наборе
        /// <c>&lt;Foo a = "1"&gt;</c> давал имя <c>"a "</c> (с пробелом) и не
        /// совпадал ни с одним членом, а <c>&lt;Foo id Tag="x"&gt;</c> - имя
        /// <c>"id Tag"</c>. Дополнительного прохода по имени это не стоит:
        /// поиск тот же самый, набор шире на четыре значения.
        /// </summary>
        private static readonly SearchValues<char> AttributeNameEndValues =
            SearchValues.Create("=/>: \t\r\n");

        /// <summary>
        /// Значению атрибута нужна нормализация по §3.3.3, только если в нём есть
        /// ссылка либо литеральный TAB/CR/LF.
        /// </summary>
        private static readonly SearchValues<char> AttributeValueSpecials =
            SearchValues.Create("&\t\r\n");
#endif

        #region однопроходный разбор

        /// <summary>
        /// Default head: S and inter-element text only. A <c>&lt;!</c> or
        /// <c>&lt;?</c> is an error. No xmlns-URI scan.
        ///
        /// Две default-перегрузки ниже - это два разных тела, а не один core
        /// с булевым параметром: core оказался бы слишком велик для инлайна,
        /// константы вызывающей стороны уехали бы в аргументы, и на каждый тег
        /// POCO-документа исполнялось бы ветвление по выключенной фиче
        /// (docs/opt-in-xml-features.md §9.1). Разметочный вариант - один,
        /// с булевыми параметрами: он по определению не default-путь (§4.5).
        /// </summary>
        public static void ReadHead(
            roschar cursor,
            roschar xmlnsAttributeName,
            ref XmlHead result
            )
        {
            var index = SkipToTagStrict(cursor, 0);
            if (index < 0)
            {
                result = new XmlHead();
                return;
            }

            var span = cursor.Slice(index);
            if (TryReadEndTag(span, index, xmlnsAttributeName, ref result))
            {
                return;
            }

            ScanHead(span, out var endOfName, out var endOfHead);
            FinishStartTag(span, index, endOfName, endOfHead, xmlnsAttributeName, ref result);
        }

        /// <summary>
        /// Default skip + xmlns-URI scan (<see cref="XmlFeature.FlexibleXsiPrefix"/>).
        /// </summary>
        public static void ReadHeadFlexibleXsi(
            roschar cursor,
            roschar xmlnsAttributeName,
            ref XmlHead result
            )
        {
            var index = SkipToTagStrict(cursor, 0);
            if (index < 0)
            {
                result = new XmlHead();
                return;
            }

            var span = cursor.Slice(index);
            if (TryReadEndTag(span, index, xmlnsAttributeName, ref result))
            {
                return;
            }

            ScanHead(span, out var endOfName, out var endOfHead);
            FinishStartTagFlexibleXsi(
                span,
                index,
                endOfName,
                endOfHead,
                xmlnsAttributeName,
                ref result
                );
        }

        /// <summary>
        /// Opt-in markup between elements (<see cref="XmlFeature.Markup"/>).
        /// This is not the default POCO hot path, so the two remaining axes live
        /// in parameters instead of in a clone per combination.
        ///
        /// Комментарии, PI и DOCTYPE здесь пропускаются безусловно: на этот
        /// метод хост попадает только с включённым <see cref="XmlFeature.Markup"/>.
        /// Отдельным параметром осталась CDATA - у неё своя цена (разворачивание
        /// в текст) и свой флаг.
        /// </summary>
        public static void ReadHeadMarkup(
            bool cdata,
            bool flexibleXsiPrefix,
            roschar cursor,
            roschar xmlnsAttributeName,
            ref XmlHead result
            )
        {
            var index = SkipToTagMarkup(cursor, 0, cdata);
            if (index < 0)
            {
                result = new XmlHead();
                return;
            }

            var span = cursor.Slice(index);
            if (TryReadEndTag(span, index, xmlnsAttributeName, ref result))
            {
                return;
            }

            ScanHead(span, out var endOfName, out var endOfHead);

            if (flexibleXsiPrefix)
            {
                FinishStartTagFlexibleXsi(
                    span,
                    index,
                    endOfName,
                    endOfHead,
                    xmlnsAttributeName,
                    ref result
                    );
                return;
            }

            FinishStartTag(span, index, endOfName, endOfHead, xmlnsAttributeName, ref result);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryReadEndTag(
            roschar span,
            int index,
            roschar xmlnsAttributeName,
            ref XmlHead result
            )
        {
            if (span.Length <= 1 || span[1] != '/')
            {
                return false;
            }

            var gt = span.IndexOf('>');
            if (gt < 0)
            {
                throw new XmlDocumentException("Closing '>' not found for end tag.");
            }

            result = new XmlHead(
                XmlHeadKind.EndTag,
                true,
                false,
                index + gt + 1,
                span.Slice(2, gt - 2),
                span.Slice(0, gt + 1),
                xmlnsAttributeName
                );
            return true;
        }

        /// <summary>
        /// Голова без гибкого префикса: объявление xsi либо унаследовано, либо
        /// его нет вовсе - атрибуты ради xmlns не сканируются.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void FinishStartTag(
            roschar span,
            int index,
            int endOfName,
            int endOfHead,
            roschar xmlnsAttributeName,
            ref XmlHead result
            )
        {
            var hasAttributes = HeadHasAttributes(span, endOfName, endOfHead, out var isBodyless);

            result = new XmlHead(
                XmlHeadKind.StartTag,
                isBodyless,
                hasAttributes,
                index + endOfHead + 1,
                span.Slice(1, endOfName - 1),
                span.Slice(0, endOfHead + 1),
                xmlnsAttributeName
                );
        }

        /// <summary>
        /// Голова с <see cref="XmlFeature.FlexibleXsiPrefix"/>: если префикс не
        /// унаследован, он ищется по URI XMLSchema-instance.
        /// </summary>
        private static void FinishStartTagFlexibleXsi(
            roschar span,
            int index,
            int endOfName,
            int endOfHead,
            roschar xmlnsAttributeName,
            ref XmlHead result
            )
        {
            var hasAttributes = HeadHasAttributes(span, endOfName, endOfHead, out var isBodyless);
            var declaredNodeType = span.Slice(1, endOfName - 1);
            var fullHead = span.Slice(0, endOfHead + 1);

            roschar xmlns;
            if (!xmlnsAttributeName.IsEmpty)
            {
                xmlns = xmlnsAttributeName;
            }
            else if (!hasAttributes)
            {
                xmlns = roschar.Empty;
            }
            else
            {
                ParseAttribute(
                    fullHead,
                    declaredNodeType.Length + 1,
                    XmlnsSpan,
                    roschar.Empty,
                    XmlnsHttpSpan,
                    out var parsedAttribute
                    );
                xmlns = parsedAttribute.Name;
            }

            result = new XmlHead(
                XmlHeadKind.StartTag,
                isBodyless,
                hasAttributes,
                index + endOfHead + 1,
                declaredNodeType,
                fullHead,
                xmlns
                );
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HeadHasAttributes(
            roschar span,
            int endOfName,
            int endOfHead,
            out bool isBodyless
            )
        {
            isBodyless = span[endOfHead - 1] == '/';

            return endOfName != endOfHead
                && !(isBodyless && endOfHead == endOfName + 1);
        }

        /// <summary>
        /// Тело ноды со встроенным типом - это текст. Default: первый
        /// <c>&lt;</c> ends the text (no CDATA, no leading comments).
        /// </summary>
        public static void ReadTextBody(
            roschar body,
            bool isBodyless,
            roschar declaredNodeType,
            out roschar text,
            out int consumed
            )
        {
            if (isBodyless || body.IsEmpty)
            {
                text = roschar.Empty;
                consumed = 0;
                return;
            }

            var index = body.IndexOf('<');
            if (index < 0)
            {
                throw new XmlDocumentException("Closing tag not found.");
            }

            text = body.Slice(0, index);
            consumed = index + EndTagLength(body, index, declaredNodeType);
        }

        /// <summary>
        /// Opt-in leading comments and/or CDATA inside element text
        /// (<see cref="XmlFeature.Markup"/>; CDATA дополнительно по
        /// <see cref="XmlFeature.CData"/>).
        /// </summary>
        public static void ReadTextBodyMarkup(
            bool cdata,
            roschar body,
            bool isBodyless,
            roschar declaredNodeType,
            out roschar text,
            out int consumed
            )
        {
            if (isBodyless || body.IsEmpty)
            {
                text = roschar.Empty;
                consumed = 0;
                return;
            }

            var index = XmlNode2.GetLeadingCommentLengthIfExists(true, body);
            var textStart = index;

            while (true)
            {
                var rest = body.Slice(index);
                var iof = rest.IndexOf('<');
                if (iof < 0)
                {
                    throw new XmlDocumentException("Closing tag not found.");
                }

                index += iof;

                if (cdata && body.Slice(index).StartsWith(CDataHeadSpan))
                {
                    var cdataEnd = body.Slice(index).IndexOf(CDataTailSpan);
                    if (cdataEnd < 0)
                    {
                        throw new XmlDocumentException("Closing ']]>' not found for CDATA block.");
                    }

                    index += cdataEnd + CDataTailSpan.Length;
                    continue;
                }

                break;
            }

            text = body.Slice(textStart, index - textStart);
            consumed = index + EndTagLength(body, index, declaredNodeType);
        }

        /// <summary>
        /// Пропускает тело неизвестного элемента. Default: простой <c>&gt;</c>,
        /// <c>&lt;!</c>/<c>&lt;?</c> — ошибка.
        /// </summary>
        public static int SkipBody(
            roschar body,
            bool isBodyless
            )
        {
            return SkipBodyCore(
                allowMarkup: false,
                cdata: false,
                body,
                isBodyless
                );
        }

        /// <summary>
        /// Skip unknown body with opt-in markup (<see cref="XmlFeature.Markup"/>).
        /// </summary>
        public static int SkipBodyMarkup(
            bool cdata,
            roschar body,
            bool isBodyless
            )
        {
            return SkipBodyCore(
                allowMarkup: true,
                cdata,
                body,
                isBodyless
                );
        }

        private static int SkipBodyCore(
            bool allowMarkup,
            bool cdata,
            roschar body,
            bool isBodyless
            )
        {
            if (isBodyless || body.IsEmpty)
            {
                return 0;
            }

            var index = 0;
            var depth = 0;

            while (true)
            {
                var rest = body.Slice(index);
                var iof = rest.IndexOf('<');
                if (iof < 0)
                {
                    throw new XmlDocumentException("Closing tag not found.");
                }

                index += iof;
                rest = body.Slice(index);

                if (rest.Length < 2)
                {
                    throw new XmlDocumentException("Closing tag not found.");
                }

                var c1 = rest[1];

                if (c1 == '/')
                {
                    var gt = rest.IndexOf('>');
                    if (gt < 0)
                    {
                        throw new XmlDocumentException("Closing '>' not found for end tag.");
                    }

                    index += gt + 1;
                    if (depth == 0)
                    {
                        return index;
                    }

                    depth--;
                    continue;
                }

                if (c1 == '!' || c1 == '?')
                {
                    if (!allowMarkup)
                    {
                        ThrowUnexpectedMarkup();
                    }

                    index += SkipNonElementMarkup(rest, cdata);
                    continue;
                }

                ScanHead(rest, out _, out var endOfHead);

                index += endOfHead + 1;
                if (rest[endOfHead - 1] != '/')
                {
                    depth++;
                }
            }
        }

        /// <summary>
        /// Пропускает пробелы и текст между элементами. Любая разметка
        /// (<c>&lt;!</c> / <c>&lt;?</c>) здесь - ошибка: это default-путь,
        /// и цикла по конструкциям в нём нет вовсе.
        /// </summary>
        private static int SkipToTagStrict(
            roschar span,
            int index
            )
        {
            while (index < span.Length && span[index] <= ' ')
            {
                index++;
            }

            if (index >= span.Length)
            {
                return -1;
            }

            if (span[index] != '<')
            {
                var next = span.Slice(index).IndexOf('<');
                if (next < 0)
                {
                    return -1;
                }

                index += next;
            }

            if (span.Length - index < 2)
            {
                return -1;
            }

            var c1 = span[index + 1];
            if (c1 == '!' || c1 == '?')
            {
                ThrowUnexpectedMarkup();
            }

            return index;
        }

        /// <summary>
        /// То же, но разметка между элементами пропускается: комментарии, PI и
        /// DOCTYPE безусловно, CDATA - согласно <paramref name="cdata"/>.
        /// </summary>
        private static int SkipToTagMarkup(
            roschar span,
            int index,
            bool cdata
            )
        {
            while (true)
            {
                while (index < span.Length && span[index] <= ' ')
                {
                    index++;
                }

                if (index >= span.Length)
                {
                    return -1;
                }

                if (span[index] != '<')
                {
                    var next = span.Slice(index).IndexOf('<');
                    if (next < 0)
                    {
                        return -1;
                    }

                    index += next;
                }

                var rest = span.Slice(index);
                if (rest.Length < 2)
                {
                    return -1;
                }

                var c1 = rest[1];
                if (c1 != '!' && c1 != '?')
                {
                    return index;
                }

                index += SkipNonElementMarkup(rest, cdata);
            }
        }

        /// <summary>
        /// Длина конструкции, начинающейся с "&lt;!" или "&lt;?": комментарий,
        /// CDATA, DOCTYPE или processing instruction.
        ///
        /// Сюда попадают только хосты с <see cref="XmlFeature.Markup"/>, поэтому
        /// комментарий, PI и DOCTYPE пропускаются без проверок: разделять их
        /// флагами незачем - код у них общий, и цена входа сюда одна на всех.
        /// Отдельно проверяется только CDATA: у неё свой флаг.
        /// </summary>
        private static int SkipNonElementMarkup(roschar rest, bool cdata)
        {
            if (rest.StartsWith(CommentHeadSpan))
            {
                var end = rest.IndexOf(CommentTailSpan);
                if (end < 0)
                {
                    throw new XmlDocumentException("Closing '-->' not found for comment.");
                }

                return end + CommentTailSpan.Length;
            }

            if (rest.StartsWith(CDataHeadSpan))
            {
                if (!cdata)
                {
                    throw new XmlDocumentException("CDATA sections are not enabled.");
                }

                var end = rest.IndexOf(CDataTailSpan);
                if (end < 0)
                {
                    throw new XmlDocumentException("Closing ']]>' not found for CDATA block.");
                }

                return end + CDataTailSpan.Length;
            }

            if (rest[1] == '?')
            {
                var end = rest.IndexOf(PiTailSpan);
                if (end < 0)
                {
                    throw new XmlDocumentException("Closing '?>' not found for processing instruction.");
                }

                return end + PiTailSpan.Length;
            }

            var depth = 0;
            for (var i = 1; i < rest.Length; i++)
            {
                var c = rest[i];
                if (c == '[')
                {
                    depth++;
                }
                else if (c == ']')
                {
                    depth--;
                }
                else if (c == '>' && depth <= 0)
                {
                    return i + 1;
                }
            }

            throw new XmlDocumentException("Closing '>' not found for declaration.");
        }

        private static void ThrowUnexpectedMarkup()
        {
            throw new XmlDocumentException(
                "Unexpected processing instruction, comment, CDATA or DOCTYPE."
                );
        }

        #endregion

        #region стражи (docs/opt-in-xml-guards.md)

        /// <summary>
        /// <see cref="XmlGuard.MatchingEndTags"/>: закрывающий тег обязан
        /// совпасть с открывающим.
        ///
        /// Сверяется имя <b>из документа</b>, а не имя отображённого типа:
        /// well-formedness - свойство пары тегов, и у корня генератор допускает
        /// два имени сразу (<c>[XmlType]</c> и <c>[XmlRoot]</c>), а у
        /// полиморфного члена тело разбирает производный тип, тогда как в
        /// документе стоит имя члена.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EnsureEndTagName(roschar actual, roschar expected)
        {
            if (!actual.SequenceEqual(expected))
            {
                ThrowMismatchedEndTag(actual, expected);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowMismatchedEndTag(roschar actual, roschar expected)
        {
            throw new XmlDocumentException(
                "The '" + expected.ToString() + "' start tag does not match the end tag of '"
                + actual.ToString() + "'."
                );
        }

        /// <summary>
        /// <see cref="XmlGuard.MatchingEndTags"/>: тело кончилось концом ввода,
        /// а своего закрывающего тега так и не было.
        ///
        /// Список незакрытых элементов, который в этом случае печатает BCL,
        /// здесь не воспроизводится: для него нужен стек имён на всю глубину
        /// документа, а это цена на горячем пути ради текста сообщения.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowUnexpectedEof()
        {
            throw new XmlDocumentException("Unexpected end of file has occurred.");
        }

        /// <summary>
        /// <see cref="XmlGuard.UniqueAttributes"/>: XML 1.0 WFC Unique Att Spec -
        /// двух атрибутов с одним qualified name на одной голове быть не может.
        /// Без стража берётся первое совпадение, и <c>id="1" id="2"</c> проходит
        /// молча.
        ///
        /// Зовётся только при <see cref="XmlHead.HasAttributes"/>: на голове без
        /// атрибутов платить не за что, и DEEP-подобный документ этот флаг
        /// не замечает вовсе.
        ///
        /// Уже увиденные имена лежат в <c>stackalloc</c> парами
        /// <c>(offset, length)</c> - на них голова разбирается ровно один раз.
        /// Хранить сами спаны нельзя (<c>Span</c> из
        /// <c>ReadOnlySpan&lt;char&gt;</c> не собирается: <c>roschar</c> -
        /// ref struct), а список или массив имён - это куча на каждой голове
        /// с атрибутами, чего страж себе позволить не может. Числа обходят оба
        /// запрета, и разбор их всё равно считает по дороге.
        ///
        /// Буфер конечен, атрибутов на голове - нет. Переполнение <b>не</b>
        /// значит «дубль»: голова с 33 разными атрибутами легальна. Значит
        /// хвост считается попарно, как раньше - см.
        /// <see cref="EnsureUniqueAttributesPairwise"/>.
        ///
        /// Что это даёт, измерено (<c>--guard-cost</c>). На голове из двух
        /// атрибутов - ничего: попарный вариант делает там ровно один лишний
        /// разбор, и он тонет в дрейфе. Разница появляется там, где атрибутов
        /// много: попарная проверка квадратична по их числу, эта - линейна,
        /// и на голове из двенадцати атрибутов страж стоит 1.40x вместо 2.37x.
        /// То есть буфер здесь не про горячий путь POCO, а про то, чтобы
        /// широкая голова не превращала страж в квадрат.
        /// </summary>
        public static void EnsureUniqueAttributes(roschar fullHead, roschar declaredNodeType)
        {
            //32 имени - это 256 байт стека и заведомо больше, чем бывает
            //на голове, сгенерированной из POCO
            const int MaxTracked = 32;

            Span<int> seen = stackalloc int[MaxTracked * 2];
            var seenCount = 0;

            //'<' плюс имя: дальше начинаются атрибуты
            var start = 1 + declaredNodeType.Length;
            var index = start;

            while (true)
            {
                if (index < 0 || index >= fullHead.Length)
                {
                    return;
                }

                ParseFirstFoundAttribute(fullHead, index, out var current);
                if (current.Attribute.IsEmpty)
                {
                    return;
                }

                if (current.TotalLength <= 0)
                {
                    throw new XmlDocumentException("Attribute parsing made no progress.");
                }

                var qname = fullHead.Slice(current.QNameOffset, current.QNameLength);

                for (var i = 0; i < seenCount; i++)
                {
                    //QName сравнивается целиком: ':' внутри prefix и внутри
                    //локального имени невозможно, так что посимвольное
                    //равенство "prefix:name" равносильно равенству обеих частей
                    if (fullHead.Slice(seen[i * 2], seen[(i * 2) + 1]).SequenceEqual(qname))
                    {
                        ThrowDuplicateAttribute(current.Attribute.Prefix, current.Attribute.Name);
                    }
                }

                if (seenCount == MaxTracked)
                {
                    //место кончилось, атрибуты - нет: досчитать надо, но уже
                    //медленным способом, иначе легальная голова стала бы ошибкой
                    EnsureUniqueAttributesPairwise(fullHead, start);
                    return;
                }

                seen[seenCount * 2] = current.QNameOffset;
                seen[(seenCount * 2) + 1] = current.QNameLength;
                seenCount++;

                index += current.TotalLength;
            }
        }

        /// <summary>
        /// Запасной путь <see cref="EnsureUniqueAttributes"/> для головы, чьи
        /// имена не поместились в буфер: каждый атрибут сверяется с каждым
        /// предыдущим, разбирая их заново. Квадратично по числу атрибутов и
        /// линейно по длине головы на каждый шаг - но зовётся только там, где
        /// атрибутов больше трёх десятков, а такой документ уже не про
        /// горячий путь.
        /// </summary>
        private static void EnsureUniqueAttributesPairwise(roschar fullHead, int start)
        {
            var index = start;

            while (true)
            {
                if (index < 0 || index >= fullHead.Length)
                {
                    return;
                }

                ParseFirstFoundAttribute(fullHead, index, out var current);
                if (current.Attribute.IsEmpty)
                {
                    return;
                }

                if (current.TotalLength <= 0)
                {
                    throw new XmlDocumentException("Attribute parsing made no progress.");
                }

                var scan = start;
                while (scan < index)
                {
                    ParseFirstFoundAttribute(fullHead, scan, out var earlier);
                    if (earlier.Attribute.IsEmpty || earlier.TotalLength <= 0)
                    {
                        break;
                    }

                    if (earlier.Attribute.Name.SequenceEqual(current.Attribute.Name)
                        && earlier.Attribute.Prefix.SequenceEqual(current.Attribute.Prefix)
                        )
                    {
                        ThrowDuplicateAttribute(current.Attribute.Prefix, current.Attribute.Name);
                    }

                    scan += earlier.TotalLength;
                }

                index += current.TotalLength;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowDuplicateAttribute(roschar prefix, roschar name)
        {
            var qualified = prefix.IsEmpty
                ? name.ToString()
                : prefix.ToString() + ":" + name.ToString();

            throw new XmlDocumentException("'" + qualified + "' is a duplicate attribute name.");
        }

        /// <summary>
        /// <see cref="XmlGuard.SingleRoot"/> для хоста без
        /// <see cref="XmlFeature.Markup"/>: после корня допустимы только символы
        /// XML S production. Комментарий и PI такому хосту и внутри документа -
        /// ошибка разбора, так что разрешать их в хвосте было бы странно.
        ///
        /// Хвост чаще всего пуст, и тогда проверка стоит одно сравнение.
        /// </summary>
        public static void EnsureNoTrailingContent(roschar fullNode, int consumed)
        {
            if (consumed >= fullNode.Length)
            {
                return;
            }

            var tail = fullNode.Slice(consumed);

            var index = SkipS(tail);
            if (index >= tail.Length)
            {
                return;
            }

            ThrowTrailingContent(tail.Slice(index));
        }

        /// <summary>
        /// <see cref="XmlGuard.SingleRoot"/> для хоста с
        /// <see cref="XmlFeature.Markup"/> (в том числе для всего compat-слоя):
        /// XML разрешает после корня Misc - пробелы, комментарии и processing
        /// instructions, - и <c>System.Xml</c> такой документ читает молча.
        /// Отвергать <c>&lt;/root&gt;&lt;!-- bye --&gt;</c> значило бы падать на
        /// well-formed вводе, то есть менять один дефект на другой.
        ///
        /// DOCTYPE и CDATA сюда не входят: после корня их нет в продукции Misc.
        /// </summary>
        public static void EnsureNoTrailingContentMarkup(roschar fullNode, int consumed)
        {
            if (consumed >= fullNode.Length)
            {
                return;
            }

            var tail = fullNode.Slice(consumed);
            var index = 0;

            while (true)
            {
                index += SkipS(tail.Slice(index));
                if (index >= tail.Length)
                {
                    return;
                }

                var rest = tail.Slice(index);
                if (rest.Length < 2 || rest[0] != '<')
                {
                    ThrowTrailingContent(rest);
                }

                if (rest.StartsWith(CommentHeadSpan))
                {
                    var end = rest.IndexOf(CommentTailSpan);
                    if (end < 0)
                    {
                        throw new XmlDocumentException("Closing '-->' not found for comment.");
                    }

                    index += end + CommentTailSpan.Length;
                    continue;
                }

                if (rest[1] == '?')
                {
                    var end = rest.IndexOf(PiTailSpan);
                    if (end < 0)
                    {
                        throw new XmlDocumentException("Closing '?>' not found for processing instruction.");
                    }

                    index += end + PiTailSpan.Length;
                    continue;
                }

                ThrowTrailingContent(rest);
            }
        }

        /// <summary>
        /// Различаются два случая по первому непробельному символу, как их
        /// различает <c>System.Xml</c>: '&lt;' - это ещё один элемент, всё
        /// остальное - мусор на уровне документа. Позиция не считается.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowTrailingContent(roschar rest)
        {
            throw new XmlDocumentException(
                rest.Length > 0 && rest[0] == '<'
                    ? "There are multiple root elements."
                    : "Data at the root level is invalid."
                );
        }

        /// <summary>
        /// Сверяет закрывающий тег в позиции index с ожидаемым именем и возвращает
        /// его длину. Это единственная защита от ухода за границу ноды: спан тела
        /// больше не обрезан справа, поэтому битый XML вида &lt;A&gt;&lt;B&gt;&lt;/A&gt;
        /// ловится только здесь.
        /// </summary>
        private static int EndTagLength(roschar body, int index, roschar declaredNodeType)
        {
            var required = 2 + declaredNodeType.Length + 1;
            if (index + required > body.Length)
            {
                throw new XmlDocumentException("Closing tag not found.");
            }

            if (body[index + 1] != '/')
            {
                throw new XmlDocumentException("Closing tag not found for " + declaredNodeType.ToString());
            }

            if (declaredNodeType.IsEmpty)
            {
                //имя сверять не с чем - достаточно дойти до '>'
                var gt = body.Slice(index).IndexOf('>');
                if (gt < 0)
                {
                    throw new XmlDocumentException("Closing tag not found.");
                }

                return gt + 1;
            }

            var eq = MemoryExtensions.SequenceEqual(
                declaredNodeType,
                body.Slice(index + 2, declaredNodeType.Length)
                );
            if (!eq)
            {
                throw new XmlDocumentException("Mismatched closing tag found for " + declaredNodeType.ToString());
            }

            if (body[index + 2 + declaredNodeType.Length] != '>')
            {
                throw new XmlDocumentException("Broken closing tag found for " + declaredNodeType.ToString());
            }

            return required;
        }

        #endregion

        #region разбор головы (общее с XmlNode2)

        /// <summary>
        /// Голова тега: имя кончается на S / '/' / '&gt;', сама голова - на
        /// '&gt;', который не находится внутри значения атрибута (XML 1.0 §2.4).
        ///
        /// Квотирование отдельной фичей не является: замер показал, что
        /// quote-aware поиск стоит на уровне шумового порога (README, "Cost of
        /// turning a flag on"), а без него голова с '&gt;' внутри AttValue
        /// обрезалась молча и неправильно.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ScanHead(
            roschar trimmed,
            out int endOfName,
            out int endOfHead
            )
        {
#if NET8_0_OR_GREATER
            var index = trimmed.IndexOfAny(NameEndValues);
            var searchLimit = index >= 0 ? index : trimmed.Length;
#else
            var index = trimmed.IndexOfAny('/', '>', ' ');
            var searchLimit = index >= 0 ? index : trimmed.Length;

            for (var i = 0; i < searchLimit; i++)
            {
                if (trimmed[i] < ' ')
                {
                    endOfName = i;
                    endOfHead = FindUnquotedGt(trimmed, i);
                    return;
                }
            }
#endif

            endOfName = index;

            if (index >= 0 && trimmed[index] == '>')
            {
                endOfHead = index;
                return;
            }

            endOfHead = FindUnquotedGt(trimmed, searchLimit);
        }

        /// <summary>
        /// Ищет '&gt;', закрывающий голову тега, пропуская те, что находятся внутри
        /// значений атрибутов (XML 1.0 2.4 требует экранирования только '&lt;', '&amp;'
        /// и совпадающей кавычки внутри AttValue, так что '&gt;' там легален).
        /// Посимвольный скан здесь был бы заметно дороже: метод вызывается на
        /// каждую ноду документа. Вместо этого прыгаем по границам векторизованными
        /// поисками - число итераций равно числу атрибутов, а не длине головы.
        ///
        /// Скан начинается с позиции start; вызывающая сторона обязана гарантировать,
        /// что до неё кавычек нет.
        /// </summary>
        public static int FindUnquotedGt(roschar span, int start)
        {
            var offset = start < 0 ? 0 : start;

            while (true)
            {
                var sliced = span.Slice(offset);

                var index = sliced.IndexOfAny('>', '"', '\'');
                if (index < 0)
                {
                    throw new XmlDocumentException("Closing '>' not found for element head.");
                }

                var c = sliced[index];
                if (c == '>')
                {
                    return offset + index;
                }

                //это открывающая кавычка значения атрибута, ищем парную закрывающую
                var rest = sliced.Slice(index + 1);
                var closingIndex = rest.IndexOf(c);
                if (closingIndex < 0)
                {
                    throw new XmlDocumentException("Closing quote not found for attribute value.");
                }

                offset += index + 1 + closingIndex + 1;
            }
        }

        /// <summary>
        /// Перечисление атрибутов головы: разобрать тот, что стоит в
        /// <paramref name="index"/>, и сдвинуть <paramref name="index"/> на его
        /// длину. <c>false</c> - атрибуты кончились.
        ///
        /// Зачем это отдельно от <see cref="ParseAttribute"/>. Тот ищет
        /// <b>один</b> атрибут по имени, и типу с тремя
        /// <c>[XmlAttribute]</c>-членами приходилось звать его трижды, каждый
        /// раз с начала головы: чтобы дойти до третьего атрибута, надо разобрать
        /// первые два, и они разбирались заново на каждом поиске. Здесь голова
        /// разбирается один раз, а кому какой атрибут - решает вызывающий.
        ///
        /// Значение отдаётся <b>сырым</b>, без нормализации §3.3.3: она нужна
        /// только тому атрибуту, который кому-то пригодился, а пригождается
        /// один из всех. Взял значение - зови
        /// <see cref="DecodeAttributeValue"/>.
        /// </summary>
        public static bool NextAttribute(
            roschar internalsOfHead,
            ref int index,
            out ParsedAttribute result
            )
        {
            if (index < 0 || index >= internalsOfHead.Length)
            {
                result = new ParsedAttribute();
                return false;
            }

            ParseFirstFoundAttribute(internalsOfHead, index, out var apr);
            if (apr.Attribute.IsEmpty)
            {
                result = apr.Attribute;
                return false;
            }

            if (apr.TotalLength <= 0)
            {
                throw new XmlDocumentException("Attribute parsing made no progress.");
            }

            index += apr.TotalLength;
            result = apr.Attribute;
            return true;
        }

        /// <summary>
        /// Ищет в голове тега атрибут, подходящий под префикс/имя/значение.
        /// Значение может стоять в любых кавычках (XML 1.0 §2.3 AttValue): это
        /// не opt-in, см. <see cref="ScanHead"/>.
        /// </summary>
        public static void ParseAttribute(
            roschar internalsOfHead,
            int index,
            roschar requiredPrefix,
            roschar requiredName,
            roschar requiredValue,
            out ParsedAttribute result
            )
        {
            while (true)
            {
                if (index < 0 || index >= internalsOfHead.Length)
                {
                    result = new ParsedAttribute();
                    return;
                }

                ParseFirstFoundAttribute(internalsOfHead, index, out var apr);
                if (apr.Attribute.IsEmpty)
                {
                    result = apr.Attribute;
                    return;
                }

                if (apr.TotalLength <= 0)
                {
                    throw new XmlDocumentException("Attribute parsing made no progress.");
                }

                if (requiredPrefix.IsEmpty || requiredPrefix.SequenceEqual(apr.Attribute.Prefix))
                {
                    if (requiredName.IsEmpty || requiredName.SequenceEqual(apr.Attribute.Name))
                    {
                        if (requiredValue.IsEmpty)
                        {
                            result = new ParsedAttribute(
                                apr.Attribute.Prefix,
                                apr.Attribute.Name,
                                DecodeAttributeValue(apr.Attribute.Value)
                                );
                            return;
                        }

                        if (AttributeValueEquals(apr.Attribute.Value, requiredValue))
                        {
                            result = apr.Attribute;
                            return;
                        }
                    }
                }

                index += apr.TotalLength;
            }
        }

        /// <summary>
        /// Разбирает один атрибут начиная с позиции <paramref name="iindex"/>.
        ///
        /// Разбор идёт по XML 1.0 §3.1: <c>Attribute ::= Name Eq AttValue</c>,
        /// <c>Eq ::= S? '=' S?</c>. Ни одна из проверок ниже не является
        /// opt-in (docs/opt-in-xml-guards.md §5.1), и на well-formed голове ни
        /// одна из них не стоит лишнего прохода по спану.
        ///
        /// Каждый поиск проверяется на "не нашлось": без этого отрицательный
        /// индекс шёл прямо в индексатор или в Slice, и голова вида
        /// <c>&lt;Foo a:b&gt;</c> или <c>&lt;Foo a=&gt;</c> роняла разбор
        /// <see cref="IndexOutOfRangeException"/>. Документ приходит снаружи, и
        /// такое исключение - это не диагностика битого документа, а сообщение
        /// о собственной ошибке: поймать его по смыслу нельзя, отличить от чужого
        /// бага в коде вызывающего - тоже.
        ///
        /// Найтись мало - найтись должно там, где положено. Имя кончается на S
        /// наравне с '=' и ':', после имени допускается только <c>S* '='</c>,
        /// после '=' - только <c>S* кавычка</c>. Без этих трёх условий поиск
        /// уходил вперёд по голове и брал разделитель у следующего атрибута:
        /// <c>&lt;Foo id=1 Tag="x"&gt;</c> читался как <c>id="x"</c> (значение
        /// украдено у соседа, Tag потерян), <c>&lt;Foo id Tag="x"&gt;</c> - как
        /// атрибут с именем <c>"id Tag"</c>, а легальный
        /// <c>&lt;Foo a = "1"&gt;</c> - как имя <c>"a "</c>, то есть молча терялся.
        /// </summary>
        private static void ParseFirstFoundAttribute(
            roschar internalsOfHead,
            int iindex,
            out AttributeProcessResult result
            )
        {
            var trimmed = internalsOfHead.Slice(iindex).TrimStart();
            var trimmedLength = internalsOfHead.Length - trimmed.Length;

#if NET8_0_OR_GREATER
            var iofa0 = trimmed.IndexOfAny(AttributeNameEndValues);
#else
            var iofa0 = trimmed.IndexOfAny("=/>: \t\r\n".AsSpan());
#endif
            if (iofa0 < 0)
            {
                throw new XmlDocumentException("Attribute name is not terminated.");
            }

            var c = trimmed[iofa0];
            if (c == '/' || c == '>')
            {
                result = new AttributeProcessResult();
                return;
            }

            roschar prefix;
            roschar name;
            int qnameLength;
            if (c == ':')
            {
                prefix = internalsOfHead.Slice(trimmedLength, iofa0);
                trimmed = trimmed.Slice(iofa0 + 1);

                //QName пробелов вокруг ':' не допускает, поэтому локальное имя
                //начинается ровно здесь и кончается тем же набором
#if NET8_0_OR_GREATER
                var iofa1 = trimmed.IndexOfAny(AttributeNameEndValues);
#else
                var iofa1 = trimmed.IndexOfAny("=/>: \t\r\n".AsSpan());
#endif
                if (iofa1 < 0)
                {
                    throw new XmlDocumentException("Attribute name is not terminated.");
                }

                name = trimmed.Slice(0, iofa1);
                trimmed = trimmed.Slice(iofa1);

                //QName - это prefix, ':' и локальное имя подряд, без пробелов
                qnameLength = iofa0 + 1 + iofa1;
            }
            else
            {
                prefix = roschar.Empty;
                name = internalsOfHead.Slice(trimmedLength, iofa0);
                trimmed = trimmed.Slice(iofa0);

                qnameLength = iofa0;
            }

            //XML 1.0 §3.1: Eq ::= S? '=' S?. Пробелы здесь легальны, а всё
            //остальное - нет: без этой проверки поиск кавычки уходил вперёд по
            //голове и брал её у следующего атрибута, то есть <Foo id=1 Tag="x">
            //давал id="x", а Tag терялся
            var afterName = SkipS(trimmed);
            if (afterName >= trimmed.Length || trimmed[afterName] != '=')
            {
                ThrowUnexpectedToken(trimmed.Slice(afterName), "'='");
            }

            trimmed = trimmed.Slice(afterName + 1);

            var afterEq = SkipS(trimmed);
            if (afterEq >= trimmed.Length
                || (trimmed[afterEq] != '"' && trimmed[afterEq] != '\'')
                )
            {
                ThrowUnexpectedToken(trimmed.Slice(afterEq), "'\"' or '''");
            }

            var quoteChar = trimmed[afterEq];
            trimmed = trimmed.Slice(afterEq + 1);

            var iofa3 = trimmed.IndexOf(quoteChar);
            if (iofa3 < 0)
            {
                throw new XmlDocumentException("Closing quote not found for attribute value.");
            }

            var rawValue = trimmed.Slice(0, iofa3);

            var totalLength = (internalsOfHead.Length - trimmed.Length) + iofa3 + 1 - iindex;

            result = new AttributeProcessResult(
                new ParsedAttribute(prefix, name, rawValue),
                totalLength,
                trimmedLength,
                qnameLength
                );
        }

        /// <summary>
        /// Сколько символов XML S production (#x20 | #x9 | #xD | #xA) стоит
        /// в начале спана. На well-formed голове почти всегда 0 или 1.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SkipS(roschar span)
        {
            var index = 0;
            while (index < span.Length)
            {
                var c = span[index];
                if (c != ' ' && c != '\t' && c != '\r' && c != '\n')
                {
                    break;
                }

                index++;
            }

            return index;
        }

        /// <summary>
        /// Сообщение в форме <c>System.Xml</c>, но без позиции: у span-парсера
        /// строки и колонки нет. Токеном считается то, что стоит на месте
        /// ожидаемого, до ближайшего разделителя - так же, как его называет BCL.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowUnexpectedToken(roschar rest, string expected)
        {
            var length = 0;
            while (length < rest.Length)
            {
                var c = rest[length];
                if (c == ' ' || c == '\t' || c == '\r' || c == '\n'
                    || c == '=' || c == '/' || c == '>' || c == '"' || c == '\''
                    )
                {
                    break;
                }

                length++;
            }

            if (length == 0 && rest.Length > 0)
            {
                //на месте ожидаемого стоит сам разделитель - он и есть токен
                length = 1;
            }

            throw new XmlDocumentException(
                "'" + rest.Slice(0, length).ToString()
                + "' is an unexpected token. The expected token is " + expected + "."
                );
        }

        /// <summary>
        /// Сравнение без материализации строки: сырой спан, если раскрывать нечего,
        /// иначе stackalloc. Нужно, когда ищем xmlns/nil по значению и сам результат
        /// атрибута дальше не храним.
        /// </summary>
        private static bool AttributeValueEquals(roschar rawValue, roschar required)
        {
#if NET8_0_OR_GREATER
            if (rawValue.IndexOfAny(AttributeValueSpecials) < 0)
#else
            if (rawValue.IndexOfAny("&\t\r\n".AsSpan()) < 0)
#endif
            {
                return rawValue.SequenceEqual(required);
            }

            if (rawValue.Length <= 256)
            {
                Span<char> buffer = stackalloc char[rawValue.Length];
                var written = XmlTextDecoder.DecodeAttributeValueInto(rawValue, buffer);
                return buffer.Slice(0, written).SequenceEqual(required);
            }

            return XmlTextDecoder.DecodeAttributeValue(rawValue).AsSpan().SequenceEqual(required);
        }

        /// <summary>
        /// XML 1.0 3.3.3 attribute-value normalization: (1) each literal tab, CR or
        /// LF character is replaced by a single space, and (2) character/general
        /// entity references (e.g. &amp;amp; or &amp;#49;) are resolved. A character
        /// reference that expands to whitespace (e.g. &amp;#10;) is inserted verbatim
        /// and must NOT be collapsed to a space - only *literal* whitespace in the
        /// source is - so step (1) runs on the raw text before entities are expanded.
        /// Only allocates when a literal tab/CR/LF or a reference might actually be present.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static roschar DecodeAttributeValue(roschar rawValue)
        {
            //один векторизованный проход отсекает подавляющее большинство значений,
            //которым нормализация не нужна вообще: ни ссылок, ни литеральных
            //TAB/CR/LF - значит спан можно вернуть как есть, без аллокации
#if NET8_0_OR_GREATER
            if (rawValue.IndexOfAny(AttributeValueSpecials) < 0)
#else
            if (rawValue.IndexOfAny("&\t\r\n".AsSpan()) < 0)
#endif
            {
                return rawValue;
            }

            return XmlTextDecoder.DecodeAttributeValue(rawValue).AsSpan();
        }

        #endregion
    }
}
