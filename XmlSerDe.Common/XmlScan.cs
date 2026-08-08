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
        /// Стоит ли на теге xsi:nil="true". Отличает &lt;Foo xsi:nil="true"/&gt; ("значения
        /// нет") от &lt;Foo/&gt; ("значение пустое"): System.Xml.Serialization пишет так
        /// null-члена, а пустую строку и пустую коллекцию - вторым способом, и без этой
        /// проверки обе формы читались бы одинаково.
        ///
        /// Префикс не проверяется: годится любой атрибут с именем nil. Строго по спецификации
        /// значащим был бы только префикс, привязанный к XMLSchema-instance, но привязка
        /// в документе может и отсутствовать (xsi считают общеизвестным и объявить забывают),
        /// а атрибут с именем nil и другим смыслом - случай, которого на практике не бывает.
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
                roschar.Empty,
                out var parsedAttribute
                );

            return parsedAttribute.Value.SequenceEqual(XmlScan.TrueSpan);
        }
    }

    /// <summary>
    /// Сканирующие примитивы. Общие для устаревшего <see cref="XmlNode2"/>
    /// и для однопроходного разбора.
    ///
    /// Ни один метод здесь не принимает <see cref="XmlDeserializeSettings"/>:
    /// эвристики передаются двумя bool. Это не стилистика, а обход CS8352 -
    /// спан, отданный через out из метода, у которого есть ref-параметр
    /// ref-структурного типа, компилятор обязан считать потенциально
    /// ссылающимся на него, и положить такой спан в поле ref struct уже нельзя.
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
        /// без префикса) либо '/' и '&gt;' (нода закрылась, атрибутов больше нет).
        /// </summary>
        private static readonly SearchValues<char> AttributeNameEndValues =
            SearchValues.Create("=/>:");

        /// <summary>
        /// Значению атрибута нужна нормализация по §3.3.3, только если в нём есть
        /// ссылка либо литеральный TAB/CR/LF.
        /// </summary>
        private static readonly SearchValues<char> AttributeValueSpecials =
            SearchValues.Create("&\t\r\n");
#endif

        #region однопроходный разбор

        /// <summary>
        /// Читает ровно одну голову в позиции курсора и ничего больше: пропускает
        /// пробелы, комментарии, PI, CDATA и текст между элементами, после чего
        /// разбирает открывающий либо закрывающий тег.
        ///
        /// Поддерево НЕ обходится. Именно этим метод отличается от
        /// <see cref="XmlNode2.GetFirst"/>, которому, чтобы отдать ноду, нужно
        /// сначала найти её конец, а для этого - рекурсивно разобрать всё, что
        /// внутри. Из-за этого стоимость разбора там растёт как O(размер x глубина);
        /// здесь - один разбор головы на элемент.
        ///
        /// Где нода заканчивается, вызывающая сторона узнаёт из разбора её тела:
        /// цикл по детям останавливается на <see cref="XmlHeadKind.EndTag"/>.
        /// </summary>
        public static void ReadHead(
            bool containsXmlComments,
            bool containsCDataBlocks,
            roschar cursor,
            roschar xmlnsAttributeName,
            ref XmlHead result
            )
        {
            var index = SkipToTag(cursor, 0);
            if (index < 0)
            {
                result = new XmlHead();
                return;
            }

            var span = cursor.Slice(index);

            if (span.Length > 1 && span[1] == '/')
            {
                //закрывающий тег
                var gt = span.IndexOf('>');
                if (gt < 0)
                {
                    throw new InvalidOperationException("Closing '>' not found for end tag.");
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
                return;
            }

            ScanHead(span, out var endOfName, out var endOfHead);

            var isBodyless = span[endOfHead - 1] == '/';
            var declaredNodeType = span.Slice(1, endOfName - 1);
            var fullHead = span.Slice(0, endOfHead + 1);

            //имя упёрлось прямо в '>' (или в '/' самозакрывающегося тега) - атрибутов нет
            var hasAttributes =
                endOfName != endOfHead
                && !(isBodyless && endOfHead == endOfName + 1);

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

        /// <summary>
        /// Тело ноды со встроенным типом - это текст. Отдаёт его и сообщает,
        /// сколько съедено вместе с закрывающим тегом.
        ///
        /// Пустой <paramref name="declaredNodeType"/> означает "имя закрывающего
        /// тега вызывающему неизвестно": так читается тело члена с
        /// <see cref="System.Xml.Serialization.XmlTextAttribute"/>, до которого
        /// голова не доходит вовсе. Проверить имя в этом случае не на что, но
        /// это и не потеря: до тела уже добрался разбор головы, а он имя сверял.
        /// </summary>
        public static void ReadTextBody(
            bool containsXmlComments,
            bool containsCDataBlocks,
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

            var index = XmlNode2.GetLeadingCommentLengthIfExists(containsXmlComments, body);
            var textStart = index;

            while (true)
            {
                var rest = body.Slice(index);
                var iof = rest.IndexOf('<');
                if (iof < 0)
                {
                    throw new InvalidOperationException("Closing tag not found.");
                }

                index += iof;

                if (containsCDataBlocks && body.Slice(index).StartsWith(CDataHeadSpan))
                {
                    //'<' внутри CDATA не закрывает текст
                    var cdataEnd = body.Slice(index).IndexOf(CDataTailSpan);
                    if (cdataEnd < 0)
                    {
                        throw new InvalidOperationException("Closing ']]>' not found for CDATA block.");
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
        /// Пропускает тело ноды, которой в POCO не соответствует ни один член:
        /// разбирать её содержимое незачем, достаточно досчитать баланс тегов.
        ///
        /// Это и есть "дешёвый скан" - но, в отличие от того, чем занят сегодня
        /// GetFirstLength, он выполняется только для чужих элементов. На документе,
        /// который целиком ложится на POCO, не вызывается ни разу.
        ///
        /// Скан обязан быть quote-aware: '&gt;' внутри значения атрибута легален
        /// (XML 1.0 2.4), и на этом уже спотыкались.
        /// </summary>
        public static int SkipBody(
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
                    throw new InvalidOperationException("Closing tag not found.");
                }

                index += iof;
                rest = body.Slice(index);

                if (rest.Length < 2)
                {
                    throw new InvalidOperationException("Closing tag not found.");
                }

                var c1 = rest[1];

                if (c1 == '/')
                {
                    var gt = rest.IndexOf('>');
                    if (gt < 0)
                    {
                        throw new InvalidOperationException("Closing '>' not found for end tag.");
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
                    index += SkipNonElementMarkup(rest);
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
        /// Пропускает пробелы, текст между элементами, комментарии, CDATA и PI.
        /// Возвращает индекс '&lt;', с которого начинается тег, либо -1.
        /// </summary>
        private static int SkipToTag(roschar span, int index)
        {
            while (true)
            {
                //XML S production (2.3) - ровно четыре символа, все <= ' '.
                //Свой цикл вместо TrimStart: тот идёт через char.IsWhiteSpace
                //с проверкой Unicode-категорий и заодно съедает, например, NBSP,
                //который в XML является обычным текстовым содержимым.
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
                    //текст между дочерними элементами по контракту отбрасывается
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

                index += SkipNonElementMarkup(rest);
            }
        }

        /// <summary>
        /// Длина конструкции, начинающейся с "&lt;!" или "&lt;?": комментарий,
        /// CDATA, DOCTYPE или processing instruction.
        /// </summary>
        private static int SkipNonElementMarkup(roschar rest)
        {
            if (rest.StartsWith(CommentHeadSpan))
            {
                var end = rest.IndexOf(CommentTailSpan);
                if (end < 0)
                {
                    throw new InvalidOperationException("Closing '-->' not found for comment.");
                }

                return end + CommentTailSpan.Length;
            }

            if (rest.StartsWith(CDataHeadSpan))
            {
                var end = rest.IndexOf(CDataTailSpan);
                if (end < 0)
                {
                    throw new InvalidOperationException("Closing ']]>' not found for CDATA block.");
                }

                return end + CDataTailSpan.Length;
            }

            if (rest[1] == '?')
            {
                var end = rest.IndexOf(PiTailSpan);
                if (end < 0)
                {
                    throw new InvalidOperationException("Closing '?>' not found for processing instruction.");
                }

                return end + PiTailSpan.Length;
            }

            //DOCTYPE и прочие объявления: '>' внутри internal subset
            //(например в <!ELEMENT Foo (#PCDATA)>) объявление не закрывает,
            //поэтому считаем глубину скобок, а не ищем первый '>'
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

            throw new InvalidOperationException("Closing '>' not found for declaration.");
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
                throw new InvalidOperationException("Closing tag not found.");
            }

            if (body[index + 1] != '/')
            {
                throw new InvalidOperationException("Closing tag not found for " + declaredNodeType.ToString());
            }

            if (declaredNodeType.IsEmpty)
            {
                //имя сверять не с чем - достаточно дойти до '>'
                var gt = body.Slice(index).IndexOf('>');
                if (gt < 0)
                {
                    throw new InvalidOperationException("Closing tag not found.");
                }

                return gt + 1;
            }

            var eq = MemoryExtensions.SequenceEqual(
                declaredNodeType,
                body.Slice(index + 2, declaredNodeType.Length)
                );
            if (!eq)
            {
                throw new InvalidOperationException("Mismatched closing tag found for " + declaredNodeType.ToString());
            }

            if (body[index + 2 + declaredNodeType.Length] != '>')
            {
                throw new InvalidOperationException("Broken closing tag found for " + declaredNodeType.ToString());
            }

            return required;
        }

        #endregion

        #region разбор головы (общее с XmlNode2)

        /// <summary>
        /// Находит за один проход и конец имени ноды, и конец её головы.
        ///
        /// Конец имени - это первый символ XML S production (#x20 | #x9 | #xD | #xA,
        /// XML 1.0 2.3) либо '/' либо '&gt;'. На net8.0+ все шесть значений ищутся
        /// одним проходом через <see cref="NameEndValues"/>.
        ///
        /// На netstandard2.0 SearchValues нет, а искать все шесть символов одним
        /// IndexOfAny(roschar) нельзя: рантайм имеет SIMD-реализации только для пяти
        /// значений и меньше, а дальше переключается на вероятностный (bloom-filter)
        /// скан. Поэтому там векторизованно ищется только тройка '/', '&gt;', ' ' (это
        /// подавляющее большинство случаев), а оставшиеся символы S production
        /// добираются скалярной проверкой c &lt; ' ' по короткому префиксу до уже
        /// найденной позиции: #x9, #xA и #xD все меньше пробела, легальный символ
        /// имени всегда больше, а обычного пробела в префиксе нет по построению,
        /// так что проверка эквивалентна. Префикс короткий (в среднем 11 символов),
        /// и цикл по нему дешевле ещё одного вызова с настройкой вектора.
        ///
        /// Конец головы в общем случае ищет <see cref="FindUnquotedGt"/>, но два
        /// частых случая до него не доходят:
        /// 1) имя упёрлось прямо в '&gt;' - значит атрибутов нет, а значит нет и
        ///    кавычек, и конец головы это уже найденный индекс;
        /// 2) иначе поиск стартует не с нуля, а с конца имени: раньше него кавычка
        ///    встретиться не может.
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
                    //имя закончилось табом/CR/LF раньше, чем найденным терминатором,
                    //поэтому голову приходится искать с учётом кавычек
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
                    throw new InvalidOperationException("Closing '>' not found for element head.");
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
                    throw new InvalidOperationException("Closing quote not found for attribute value.");
                }

                offset += index + 1 + closingIndex + 1;
            }
        }

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
                ParseFirstFoundAttribute(internalsOfHead, index, out var apr);
                if (apr.Attribute.IsEmpty)
                {
                    result = apr.Attribute;
                    return;
                }

                if (requiredPrefix.IsEmpty || requiredPrefix.SequenceEqual(apr.Attribute.Prefix))
                {
                    if (requiredName.IsEmpty || requiredName.SequenceEqual(apr.Attribute.Name))
                    {
                        if (requiredValue.IsEmpty || requiredValue.SequenceEqual(apr.Attribute.Value))
                        {
                            //нашли что нужно
                            result = apr.Attribute;
                            return;
                        }
                    }
                }

                index += apr.TotalLength;
            }
        }

        private static void ParseFirstFoundAttribute(
            roschar internalsOfHead,
            int iindex,
            out AttributeProcessResult result
            )
        {
            var trimmed = internalsOfHead.Slice(iindex).TrimStart();
            var trimmedLength = internalsOfHead.Length - trimmed.Length;

            //ищем ":" (префиксованное имя) или "=" (имя без префикса) - смотря что встретится раньше
#if NET8_0_OR_GREATER
            var iofa0 = trimmed.IndexOfAny(AttributeNameEndValues);
#else
            var iofa0 = trimmed.IndexOfAny("=/>:".AsSpan());
#endif
            var c = trimmed[iofa0];
            if (c == '/' || c == '>')
            {
                //нода закрылась, атрибутов нету
                result = new AttributeProcessResult();
                return;
            }

            roschar prefix;
            roschar name;
            if (c == ':')
            {
                //префиксованный атрибут (например p3:type)
                prefix = internalsOfHead.Slice(trimmedLength, iofa0);
                trimmed = trimmed.Slice(iofa0 + 1);

                var iofa1 = trimmed.IndexOf('=');
                name = trimmed.Slice(0, iofa1);
                trimmed = trimmed.Slice(iofa1 + 1);
            }
            else
            {
                //атрибут без префикса (без двоеточия в имени), например id="1"
                prefix = roschar.Empty;
                name = internalsOfHead.Slice(trimmedLength, iofa0);
                trimmed = trimmed.Slice(iofa0 + 1);
            }

            //значение атрибута может быть в двойных или одинарных кавычках (XML 1.0 2.3, AttValue)
            var iofa2 = trimmed.IndexOfAny('"', '\'');
            var quoteChar = trimmed[iofa2];
            //найдено начало значения
            trimmed = trimmed.Slice(iofa2 + 1);

            //закрывающая кавычка должна совпадать по типу с открывающей
            var iofa3 = trimmed.IndexOf(quoteChar);
            //найден конец значения
            var rawValue = trimmed.Slice(0, iofa3);

            var totalLength = (internalsOfHead.Length - trimmed.Length) + iofa3 + 1 - iindex;

            result = new AttributeProcessResult(
                new ParsedAttribute(prefix, name, DecodeAttributeValue(rawValue)),
                totalLength
                );
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
        private static roschar DecodeAttributeValue(roschar rawValue)
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
