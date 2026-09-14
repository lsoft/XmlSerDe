using System;
using System.Xml;
using XmlSerDe;
using XmlSerDe.Internal;
using Xunit;

namespace XmlSerDe.Tests
{
    /// <summary>
    /// Поведение стражей по одному флагу (docs/opt-in-xml-guards.md §11).
    ///
    /// Устройство проверки одно на весь файл: один и тот же документ идёт на
    /// хост без атрибута и на хост ровно с одним флагом. Первый обязан его
    /// <b>принять</b> - иначе строгость включили всем, - второй обязан
    /// отказать, и только на своём нарушении. Доказывать стража на хосте
    /// с полным набором нельзя: там сработать мог бы сосед.
    /// </summary>
    public class XmlGuardFixture
    {
        private const string WellFormed =
            "<GuardSubject id=\"1\"><Title>t</Title><Child><Name>c</Name></Child>"
            + "<Items><GuardChild><Name>i</Name></GuardChild></Items></GuardSubject>";

        /// <summary>
        /// U+0001 - не Char по XML 1.0 §2.2, и представить его в документе
        /// нечем: числовая ссылка на него тоже незаконна. Именованной константой,
        /// а не литералом в строке: управляющий символ прямо в исходнике
        /// невидим глазом и теряется при любой правке файла инструментом.
        /// </summary>
        private const string IllegalChar = "\u0001";

        #region default: страж не включён никому

        [Fact]
        public void Default_WellFormed_RoundTrips_Test()
        {
            var result = ParseDefault(WellFormed);

            Assert.Equal(1, result.Id);
            Assert.Equal("t", result.Title);
            Assert.Equal("c", result.Child!.Name);
            Assert.Single(result.Items!);
            Assert.Equal("i", result.Items![0].Name);
        }

        [Fact]
        public void Default_SecondRoot_IsAccepted_Test()
        {
            //хвост съедается молча - ровно то поведение, которое SingleRoot
            //делает опциональным, а не отменяет для всех
            var result = ParseDefault(WellFormed + WellFormed);

            Assert.Equal(1, result.Id);
        }

        [Fact]
        public void Default_GarbageAfterRoot_IsAccepted_Test()
        {
            var result = ParseDefault(WellFormed + "not-xml");

            Assert.Equal(1, result.Id);
        }

        [Fact]
        public void Default_ForeignClosingTag_IsAccepted_Test()
        {
            //любой close заканчивает тело класса: имя сверяется только у скаляра
            var result = ParseDefault("<GuardSubject id=\"1\"><Title>t</Title></Wrong>");

            Assert.Equal("t", result.Title);
        }

        [Fact]
        public void Default_TruncatedComplexType_IsAccepted_Test()
        {
            //тело класса кончилось концом ввода, и это сегодня успех
            //с частично заполненным объектом
            var result = ParseDefault("<GuardSubject id=\"1\"><Title>t</Title>");

            Assert.Equal("t", result.Title);
            Assert.Null(result.Child);
        }

        [Fact]
        public void Default_DuplicateAttribute_TakesFirst_Test()
        {
            var result = ParseDefault("<GuardSubject id=\"1\" id=\"2\"><Title>t</Title></GuardSubject>");

            Assert.Equal(1, result.Id);
        }

        [Fact]
        public void Default_IllegalCharInString_IsAccepted_Test()
        {
            //литеральный U+0001 - не Char по XML 1.0 §2.2, но строковый член
            //его сегодня принимает
            var result = ParseDefault("<GuardSubject id=\"1\"><Title>a" + IllegalChar + "b</Title></GuardSubject>");

            Assert.Equal("a" + IllegalChar + "b", result.Title);
        }

        #endregion

        #region SingleRoot

        [Fact]
        public void SingleRoot_WellFormed_RoundTrips_Test()
        {
            GuardSingleRootHost.Deserialize(
                DefaultInjector.Instance,
                WellFormed.AsSpan(),
                out GuardSubject result
                );

            Assert.Equal("t", result.Title);
        }

        [Fact]
        public void SingleRoot_TrailingWhitespace_IsAccepted_Test()
        {
            GuardSingleRootHost.Deserialize(
                DefaultInjector.Instance,
                (WellFormed + "  \r\n\t").AsSpan(),
                out GuardSubject result
                );

            Assert.Equal("t", result.Title);
        }

        [Fact]
        public void SingleRoot_SecondRoot_Throws_Test()
        {
            var inner = AssertGuardFailure(
                () => ParseSingleRoot(WellFormed + WellFormed)
                );

            Assert.Equal("There are multiple root elements.", inner.Message);
        }

        [Fact]
        public void SingleRoot_GarbageAfterRoot_Throws_Test()
        {
            var inner = AssertGuardFailure(
                () => ParseSingleRoot(WellFormed + "not-xml")
                );

            Assert.Equal("Data at the root level is invalid.", inner.Message);
        }

        [Fact]
        public void SingleRoot_BodylessRoot_ChecksTailToo_Test()
        {
            //у пустого корня тело не читается вовсе, и хвост считается от головы
            GuardSingleRootHost.Deserialize(
                DefaultInjector.Instance,
                "<GuardSubject id=\"3\" />".AsSpan(),
                out GuardSubject ok
                );
            Assert.Equal(3, ok.Id);

            AssertGuardFailure(
                () => ParseSingleRoot("<GuardSubject id=\"3\" /><GuardSubject id=\"4\" />")
                );
        }

        /// <summary>
        /// Хост без <see cref="XmlFeature.Markup"/> комментарий в хвосте не
        /// принимает - он и внутри документа его не понимает.
        /// </summary>
        [Fact]
        public void SingleRoot_WithoutMarkup_RejectsTrailingComment_Test()
        {
            AssertGuardFailure(
                () => ParseSingleRoot(WellFormed + "<!-- bye -->")
                );
        }

        /// <summary>
        /// А хост с разметкой - принимает: после корня XML разрешает Misc, и
        /// <c>System.Xml</c> такой документ читает молча. Это единственное
        /// место, где страж смотрит на набор фич (§5.3).
        /// </summary>
        [Theory]
        [InlineData("<!-- bye -->")]
        [InlineData("  <!-- bye -->  <?pi data?>\r\n")]
        [InlineData("<?pi?>")]
        public void SingleRoot_WithMarkup_AcceptsTrailingMisc_Test(string tail)
        {
            GuardFullMarkupHost.Deserialize(
                DefaultInjector.Instance,
                (WellFormed + tail).AsSpan(),
                out GuardSubject result
                );

            Assert.Equal("t", result.Title);
        }

        [Fact]
        public void SingleRoot_WithMarkup_StillRejectsSecondRoot_Test()
        {
            AssertGuardFailure(
                () => GuardFullMarkupHost.Deserialize(
                    DefaultInjector.Instance,
                    (WellFormed + "<!-- bye -->" + WellFormed).AsSpan(),
                    out GuardSubject _
                    )
                );
        }

        [Fact]
        public void SingleRoot_UnterminatedTrailingComment_Throws_Test()
        {
            AssertGuardFailure(
                () => GuardFullMarkupHost.Deserialize(
                    DefaultInjector.Instance,
                    (WellFormed + "<!-- bye").AsSpan(),
                    out GuardSubject _
                    )
                );
        }

        #endregion

        #region MatchingEndTags

        [Fact]
        public void MatchingEndTags_WellFormed_RoundTrips_Test()
        {
            GuardMatchingEndTagsHost.Deserialize(
                DefaultInjector.Instance,
                WellFormed.AsSpan(),
                out GuardSubject result
                );

            Assert.Equal("t", result.Title);
            Assert.Equal("c", result.Child!.Name);
            Assert.Single(result.Items!);
        }

        [Fact]
        public void MatchingEndTags_ForeignClosingTag_Throws_Test()
        {
            var inner = AssertMatchingEndTagsFailure(
                "<GuardSubject id=\"1\"><Title>t</Title></Wrong>"
                );

            Assert.Equal(
                "The 'GuardSubject' start tag does not match the end tag of 'Wrong'.",
                inner.Message
                );
        }

        /// <summary>
        /// Второй цикл генератора - элементы коллекции. Закрывается здесь
        /// элемент-контейнер, и его имя проверяется отдельной вставкой:
        /// доказать страж только на теле класса значило бы не проверить
        /// половину кода (docs/opt-in-xml-guards.md §5.0).
        /// </summary>
        [Fact]
        public void MatchingEndTags_ForeignClosingTagOfCollection_Throws_Test()
        {
            var inner = AssertMatchingEndTagsFailure(
                "<GuardSubject id=\"1\"><Items><GuardChild><Name>i</Name></GuardChild></Wrong></GuardSubject>"
                );

            Assert.Equal(
                "The 'Items' start tag does not match the end tag of 'Wrong'.",
                inner.Message
                );
        }

        [Fact]
        public void MatchingEndTags_ForeignClosingTagOfNestedClass_Throws_Test()
        {
            AssertMatchingEndTagsFailure(
                "<GuardSubject id=\"1\"><Child><Name>c</Name></Wrong></GuardSubject>"
                );
        }

        [Fact]
        public void MatchingEndTags_TruncatedComplexType_Throws_Test()
        {
            var inner = AssertMatchingEndTagsFailure(
                "<GuardSubject id=\"1\"><Title>t</Title>"
                );

            Assert.Equal("Unexpected end of file has occurred.", inner.Message);
        }

        [Fact]
        public void MatchingEndTags_SecondRoot_IsStillAccepted_Test()
        {
            //соседний страж без своего флага не срабатывает
            GuardMatchingEndTagsHost.Deserialize(
                DefaultInjector.Instance,
                (WellFormed + WellFormed).AsSpan(),
                out GuardSubject result
                );

            Assert.Equal(1, result.Id);
        }

        #endregion

        #region UniqueAttributes

        [Fact]
        public void UniqueAttributes_WellFormed_RoundTrips_Test()
        {
            GuardUniqueAttributesHost.Deserialize(
                DefaultInjector.Instance,
                WellFormed.AsSpan(),
                out GuardSubject result
                );

            Assert.Equal(1, result.Id);
        }

        [Fact]
        public void UniqueAttributes_Duplicate_Throws_Test()
        {
            var inner = AssertUniqueAttributesFailure(
                "<GuardSubject id=\"1\" id=\"2\"><Title>t</Title></GuardSubject>"
                );

            Assert.Equal("'id' is a duplicate attribute name.", inner.Message);
        }

        [Fact]
        public void UniqueAttributes_DuplicateOnNestedHead_Throws_Test()
        {
            //голова ребёнка проверяется тем же вызовом в цикле разбора членов
            AssertUniqueAttributesFailure(
                "<GuardSubject id=\"1\"><Child a=\"1\" a=\"2\"><Name>c</Name></Child></GuardSubject>"
                );
        }

        [Fact]
        public void UniqueAttributes_DuplicateOnCollectionItem_Throws_Test()
        {
            AssertUniqueAttributesFailure(
                "<GuardSubject id=\"1\"><Items><GuardChild b=\"1\" b=\"2\"><Name>i</Name></GuardChild></Items></GuardSubject>"
                );
        }

        [Fact]
        public void UniqueAttributes_DuplicatePrefixed_Throws_Test()
        {
            var inner = AssertUniqueAttributesFailure(
                "<GuardSubject id=\"1\" p:t=\"a\" p:t=\"b\"><Title>t</Title></GuardSubject>"
                );

            Assert.Equal("'p:t' is a duplicate attribute name.", inner.Message);
        }

        /// <summary>
        /// Одно имя с разными префиксами - разные атрибуты по букве XML 1.0:
        /// сверяется qualified name, общего резолва префиксов у ядра нет
        /// (docs/opt-in-xml-guards.md §5.4).
        /// </summary>
        [Fact]
        public void UniqueAttributes_SameLocalNameDifferentPrefix_IsAccepted_Test()
        {
            GuardUniqueAttributesHost.Deserialize(
                DefaultInjector.Instance,
                "<GuardSubject id=\"1\" p:t=\"a\" q:t=\"b\"><Title>t</Title></GuardSubject>".AsSpan(),
                out GuardSubject result
                );

            Assert.Equal(1, result.Id);
        }

        [Fact]
        public void UniqueAttributes_ForeignClosingTag_IsStillAccepted_Test()
        {
            GuardUniqueAttributesHost.Deserialize(
                DefaultInjector.Instance,
                "<GuardSubject id=\"1\"><Title>t</Title></Wrong>".AsSpan(),
                out GuardSubject result
                );

            Assert.Equal("t", result.Title);
        }

        /// <summary>
        /// Голов с восемью атрибутами достаточно, чтобы поймать реализацию,
        /// которая объявляет дублем всё, что не влезло в буфер фиксированного
        /// размера (§11.4).
        /// </summary>
        [Fact]
        public void UniqueAttributes_ManyDistinctAttributes_IsAccepted_Test()
        {
            GuardUniqueAttributesHost.Deserialize(
                DefaultInjector.Instance,
                ("<GuardSubject id=\"1\" a1=\"1\" a2=\"2\" a3=\"3\" a4=\"4\" a5=\"5\" a6=\"6\""
                + " a7=\"7\" a8=\"8\" a9=\"9\" a10=\"10\"><Title>t</Title></GuardSubject>").AsSpan(),
                out GuardSubject result
                );

            Assert.Equal(1, result.Id);
        }

        [Fact]
        public void UniqueAttributes_DuplicateAtTheEndOfLongHead_Throws_Test()
        {
            AssertUniqueAttributesFailure(
                "<GuardSubject id=\"1\" a1=\"1\" a2=\"2\" a3=\"3\" a4=\"4\" a5=\"5\" a1=\"x\"><Title>t</Title></GuardSubject>"
                );
        }

        //Уже увиденные имена страж держит в буфере на стеке фиксированного
        //размера (XmlScan.EnsureUniqueAttributes). Голова, которая в него не
        //влезла, - это не ошибка документа, а переход на медленный попарный
        //путь, и три следующих теста стерегут именно границу буфера: за ней
        //легальное должно оставаться легальным, а дубль - находиться, где бы
        //он ни лежал, до границы или за ней.

        [Fact]
        public void UniqueAttributes_MoreDistinctAttributesThanTheBufferHolds_IsAccepted_Test()
        {
            GuardUniqueAttributesHost.Deserialize(
                DefaultInjector.Instance,
                BuildLongHead(64, duplicateAt: -1).AsSpan(),
                out GuardSubject result
                );

            Assert.Equal(1, result.Id);
        }

        [Fact]
        public void UniqueAttributes_DuplicateBeyondTheBuffer_Throws_Test()
        {
            //оба имени лежат за границей буфера: найти их может только
            //запасной путь
            AssertUniqueAttributesFailure(BuildLongHead(64, duplicateAt: 60, copyOf: 50));
        }

        [Fact]
        public void UniqueAttributes_DuplicateOfAnEarlyNameOnAnOverflowingHead_Throws_Test()
        {
            //первое вхождение внутри буфера, второе - далеко за ним
            AssertUniqueAttributesFailure(BuildLongHead(64, duplicateAt: 60, copyOf: 2));
        }

        /// <summary>
        /// Голова с <paramref name="count"/> атрибутами <c>a0..aN</c>. Если
        /// <paramref name="duplicateAt"/> не отрицателен, атрибут на этом месте
        /// получает имя атрибута <paramref name="copyOf"/>.
        /// </summary>
        private static string BuildLongHead(int count, int duplicateAt, int copyOf = 0)
        {
            var sb = new System.Text.StringBuilder("<GuardSubject id=\"1\"");

            for (var i = 0; i < count; i++)
            {
                var name = i == duplicateAt ? copyOf : i;
                sb.Append(" a").Append(name).Append("=\"").Append(i).Append('"');
            }

            sb.Append("><Title>t</Title></GuardSubject>");

            return sb.ToString();
        }

        #endregion

        #region IllegalChars

        [Fact]
        public void IllegalChars_WellFormed_RoundTrips_Test()
        {
            GuardIllegalCharsHost.Deserialize(
                DefaultInjector.Instance,
                WellFormed.AsSpan(),
                out GuardSubject result
                );

            Assert.Equal("t", result.Title);
        }

        [Fact]
        public void IllegalChars_InElementText_Throws_Test()
        {
            var inner = AssertIllegalCharsFailure(
                "<GuardSubject id=\"1\"><Title>a" + IllegalChar + "b</Title></GuardSubject>"
                );

            Assert.Contains("hexadecimal value 0x01, is an invalid character.", inner.Message);
        }

        [Fact]
        public void IllegalChars_InAttributeValue_Throws_Test()
        {
            AssertIllegalCharsFailure(
                "<GuardSubject id=\"1\" tag=\"a" + IllegalChar + "b\"><Title>t</Title></GuardSubject>"
                );
        }

        [Fact]
        public void IllegalChars_InCollectionItem_Throws_Test()
        {
            AssertIllegalCharsFailure(
                "<GuardSubject id=\"1\"><Items><GuardChild><Name>a" + IllegalChar + "b</Name></GuardChild></Items></GuardSubject>"
                );
        }

        /// <summary>
        /// Через ссылку тот же символ не проходит и без стража - это разбор
        /// ссылки, а не страж, - но со стражем форма ошибки становится общей.
        /// </summary>
        [Fact]
        public void IllegalChars_ViaCharacterReference_Throws_Test()
        {
            AssertIllegalCharsFailure(
                "<GuardSubject id=\"1\"><Title>a&#x1;b</Title></GuardSubject>"
                );
        }

        /// <summary>
        /// Строку разбирает пользовательский инжектор, и страж его не выбрасывает
        /// (opt-in-xml-features.md §16, п. 8): проверка стоит после разбора,
        /// поэтому работает на обоих путях - и через инжектор, и через декодер
        /// CDATA у хоста с фичами.
        /// </summary>
        [Fact]
        public void IllegalChars_WithCDataHost_Throws_Test()
        {
            AssertGuardFailure(
                () => GuardFullMarkupHost.Deserialize(
                    DefaultInjector.Instance,
                    ("<GuardSubject id=\"1\"><Title><![CDATA[a" + IllegalChar + "b]]></Title></GuardSubject>").AsSpan(),
                    out GuardSubject _
                    )
                );
        }

        [Theory]
        //легальные пробельные символы
        [InlineData("a\tb")]
        [InlineData("a\r\nb")]
        //суррогатная пара - это U+10000, а не два незаконных символа
        [InlineData("a𐀀b")]
        public void IllegalChars_LegalText_IsAccepted_Test(string text)
        {
            GuardIllegalCharsHost.Deserialize(
                DefaultInjector.Instance,
                ("<GuardSubject id=\"1\"><Title>" + text + "</Title></GuardSubject>").AsSpan(),
                out GuardSubject result
                );

            Assert.Equal(1, result.Id);
        }

        [Fact]
        public void IllegalChars_DuplicateAttribute_IsStillAccepted_Test()
        {
            GuardIllegalCharsHost.Deserialize(
                DefaultInjector.Instance,
                "<GuardSubject id=\"1\" id=\"2\"><Title>t</Title></GuardSubject>".AsSpan(),
                out GuardSubject result
                );

            Assert.Equal(1, result.Id);
        }

        #endregion

        #region ловушки: страж не должен ронять валидное (§11.4)

        [Theory]
        //пустое тело у сложного типа
        [InlineData("<GuardSubject id=\"1\"></GuardSubject>")]
        //корень вообще без тела
        [InlineData("<GuardSubject id=\"1\" />")]
        //тело из одних пробелов
        [InlineData("<GuardSubject id=\"1\">\r\n  </GuardSubject>")]
        //пустая коллекция и пустой вложенный класс
        [InlineData("<GuardSubject id=\"1\"><Items></Items><Child></Child></GuardSubject>")]
        [InlineData("<GuardSubject id=\"1\"><Items /><Child /></GuardSubject>")]
        //член, у которого пустое тело осмысленно
        [InlineData("<GuardSubject id=\"1\"><Title></Title></GuardSubject>")]
        [InlineData("<GuardSubject id=\"1\"><Title /></GuardSubject>")]
        //чужое поддерево перед своим закрывающим тегом
        [InlineData("<GuardSubject id=\"1\"><Unknown><a><b/></a></Unknown><Title>t</Title></GuardSubject>")]
        //чужое поддерево последним, сразу перед закрытием
        [InlineData("<GuardSubject id=\"1\"><Title>t</Title><Unknown>x</Unknown></GuardSubject>")]
        //несколько элементов коллекции подряд
        [InlineData("<GuardSubject id=\"1\"><Items><GuardChild><Name>a</Name></GuardChild>"
            + "<GuardChild><Name>b</Name></GuardChild></Items></GuardSubject>")]
        public void MatchingEndTags_ValidDocument_IsNotBroken_Test(string xml)
        {
            GuardMatchingEndTagsHost.Deserialize(
                DefaultInjector.Instance,
                xml.AsSpan(),
                out GuardSubject result
                );

            Assert.Equal(1, result.Id);
        }

        /// <summary>
        /// Тот же корпус на хосте с полным набором: страж не должен ронять
        /// валидное и в компании соседей.
        /// </summary>
        [Theory]
        [InlineData("<GuardSubject id=\"1\"></GuardSubject>")]
        [InlineData("<GuardSubject id=\"1\" />")]
        [InlineData("<GuardSubject id=\"1\"><Items /><Child /></GuardSubject>")]
        [InlineData("<GuardSubject id=\"1\"><Unknown><a><b/></a></Unknown><Title>t</Title></GuardSubject>")]
        public void FullSet_ValidDocument_IsNotBroken_Test(string xml)
        {
            GuardFullHost.Deserialize(
                DefaultInjector.Instance,
                xml.AsSpan(),
                out GuardSubject result
                );

            Assert.Equal(1, result.Id);
        }

        #endregion

        #region ортогональность и форма атрибута

        [Fact]
        public void GuardNone_IsSameAsNoAttribute_Test()
        {
            GuardNoneHost.Deserialize(
                DefaultInjector.Instance,
                (WellFormed + "not-xml").AsSpan(),
                out GuardSubject result
                );

            Assert.Equal(1, result.Id);
        }

        [Fact]
        public void TwoAttributes_AreOred_Test()
        {
            //SingleRoot из первого атрибута работает
            AssertGuardFailure(
                () => GuardTwoAttributesHost.Deserialize(
                    DefaultInjector.Instance,
                    (WellFormed + WellFormed).AsSpan(),
                    out GuardSubject _
                    )
                );
        }

        #endregion

        #region форма исключения (§6)

        /// <summary>
        /// Хост без стражей отдаёт сегодняшнее исключение как есть: заворачивать
        /// его в пару BCL значило бы платить аллокацией на пути, который эту
        /// строгость и не включал.
        /// </summary>
        [Fact]
        public void Default_KeepsPlainDocumentException_Test()
        {
            //обрезанный скаляр ловится и без стражей - имя закрывающего тега
            //у скалярного тела сверяется всегда (§2.6, §5.7)
            var thrown = Assert.Throws<XmlDocumentException>(
                () => ParseDefault("<GuardSubject id=\"1\"><Title>t")
                );

            Assert.Null(thrown.InnerException);
            Assert.Equal("Closing tag not found.", thrown.Message);
        }

        /// <summary>
        /// А хост со стражами - ту же пару, что <c>System.Xml.Serialization</c>
        /// над читателем без <c>IXmlLineInfo</c>: общий текст снаружи, настоящая
        /// причина в <see cref="XmlException"/> внутри, без строки и колонки.
        /// </summary>
        [Fact]
        public void Guarded_WrapsIntoSystemXmlShape_Test()
        {
            var outer = Assert.Throws<InvalidOperationException>(
                () => ParseSingleRoot(WellFormed + "not-xml")
                );

            Assert.Equal("There is an error in the XML document.", outer.Message);

            var inner = Assert.IsType<XmlException>(outer.InnerException);
            Assert.DoesNotContain("Line", inner.Message);
            Assert.DoesNotContain("position", inner.Message);
            Assert.Equal(0, inner.LineNumber);
            Assert.Equal(0, inner.LinePosition);
        }

        /// <summary>
        /// Обёртка одна на весь разбор, а не по одной на примитив.
        /// </summary>
        [Fact]
        public void Guarded_WrapsOnlyOnce_Test()
        {
            var outer = Assert.Throws<InvalidOperationException>(
                () => ParseSingleRoot(WellFormed + "not-xml")
                );

            Assert.IsNotType<InvalidOperationException>(outer.InnerException);
        }

        #endregion

        #region ForeignRootNamespace

        /// <summary>
        /// Единственное место, где мы <b>принимали</b> документ, который штатный
        /// сериализатор отвергает: имена совпали текстуально, а <c>xmlns</c> для
        /// нас просто неизвестный атрибут в голове. Тихий неверный успех - худший
        /// исход из возможных, потому здесь страж.
        /// </summary>
        [Fact]
        public void Default_ForeignRootNamespace_IsAccepted_Test()
        {
            //поведение по умолчанию не изменилось: страж чужой документ ловит,
            //а хост без атрибута платить за это не должен
            var result = ParseDefault(WithDefaultNamespace);

            Assert.Equal("t", result.Title);
        }

        [Fact]
        public void ForeignRootNamespace_DefaultNamespaceOnRoot_Throws_Test()
        {
            var inner = AssertForeignRootNamespaceFailure(WithDefaultNamespace);

            Assert.Contains("urn:acme:orders", inner.Message, StringComparison.Ordinal);
        }

        /// <summary>
        /// <c>xmlns=""</c> - законное «здесь пространства нет», и документ в нём
        /// ровно тот же, что без объявления вовсе.
        /// </summary>
        [Fact]
        public void ForeignRootNamespace_EmptyDeclaration_IsAccepted_Test()
        {
            ParseForeignRootNamespace(
                WellFormed.Replace("<GuardSubject ", "<GuardSubject xmlns=\"\" ")
                );
        }

        /// <summary>
        /// Главный предохранитель: <c>xmlns:xsi</c> и <c>xmlns:xsd</c> штатный
        /// сериализатор пишет на каждом документе. Страж, отказывающий по любому
        /// объявлению, отверг бы всё, что BCL производит, - то есть ровно те
        /// документы, ради которых совместимость и делается.
        /// </summary>
        [Fact]
        public void ForeignRootNamespace_PrefixDeclarationsOfTheBcl_AreAccepted_Test()
        {
            ParseForeignRootNamespace(
                WellFormed.Replace(
                    "<GuardSubject ",
                    "<GuardSubject xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\""
                        + " xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" "
                    )
                );
        }

        /// <summary>
        /// Предел стража, названный явно: объявление на вложенном элементе он не
        /// видит. Честное сравнение имён требует стека областей видимости,
        /// которого у однопроходного парсера нет, - это долг, а не недосмотр.
        /// </summary>
        [Fact]
        public void ForeignRootNamespace_DeclarationBelowRoot_IsNotSeen_Test()
        {
            ParseForeignRootNamespace(
                WellFormed.Replace("<Child>", "<Child xmlns=\"urn:acme:orders\">")
                );
        }

        #endregion

        private const string WithDefaultNamespace =
            "<GuardSubject xmlns=\"urn:acme:orders\" id=\"1\"><Title>t</Title>"
            + "<Child><Name>c</Name></Child>"
            + "<Items><GuardChild><Name>i</Name></GuardChild></Items></GuardSubject>";

        private static GuardSubject ParseDefault(string xml)
        {
            GuardDefaultHost.Deserialize(
                DefaultInjector.Instance,
                xml.AsSpan(),
                out GuardSubject result
                );

            return result;
        }

        private static XmlException AssertIllegalCharsFailure(string xml)
        {
            return AssertGuardFailure(
                () => GuardIllegalCharsHost.Deserialize(
                    DefaultInjector.Instance,
                    xml.AsSpan(),
                    out GuardSubject _
                    )
                );
        }

        private static XmlException AssertUniqueAttributesFailure(string xml)
        {
            return AssertGuardFailure(
                () => GuardUniqueAttributesHost.Deserialize(
                    DefaultInjector.Instance,
                    xml.AsSpan(),
                    out GuardSubject _
                    )
                );
        }

        private static XmlException AssertMatchingEndTagsFailure(string xml)
        {
            return AssertGuardFailure(
                () => GuardMatchingEndTagsHost.Deserialize(
                    DefaultInjector.Instance,
                    xml.AsSpan(),
                    out GuardSubject _
                    )
                );
        }

        private static XmlException AssertForeignRootNamespaceFailure(string xml)
        {
            return AssertGuardFailure(
                () => GuardForeignRootNamespaceHost.Deserialize(
                    DefaultInjector.Instance,
                    xml.AsSpan(),
                    out GuardSubject _
                    )
                );
        }

        private static void ParseForeignRootNamespace(string xml)
        {
            GuardForeignRootNamespaceHost.Deserialize(
                DefaultInjector.Instance,
                xml.AsSpan(),
                out GuardSubject _
                );
        }

        private static void ParseSingleRoot(string xml)
        {
            GuardSingleRootHost.Deserialize(
                DefaultInjector.Instance,
                xml.AsSpan(),
                out GuardSubject _
                );
        }

        /// <summary>
        /// Ошибка хоста со стражами - это всегда пара §6, и проверять её надо
        /// целиком, а не по наружному типу: наружный тип совпадает и у ошибки
        /// пользовательского инжектора.
        /// </summary>
        private static XmlException AssertGuardFailure(Action action)
        {
            var outer = Assert.Throws<InvalidOperationException>(action);

            Assert.Equal("There is an error in the XML document.", outer.Message);

            return Assert.IsType<XmlException>(outer.InnerException);
        }
    }
}
