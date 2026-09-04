#nullable disable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Xunit;
using BclXmlSerializer = System.Xml.Serialization.XmlSerializer;
using BclXmlSerializerFactory = System.Xml.Serialization.XmlSerializerFactory;
using XmlSerializer = XmlSerDe.Compat.XmlSerializer;
using XmlSerializerFactory = XmlSerDe.Compat.XmlSerializerFactory;

namespace XmlSerDe.Tests.Compat
{
    /// <summary>
    /// Фасад <see cref="XmlSerDe.Compat.XmlSerializer"/>: проверяется не скорость,
    /// а три обещания, на которых он держится.
    ///
    /// 1. Он <b>является</b> <see cref="BclXmlSerializer"/> - значит его можно
    ///    отдать чужому коду, который про XmlSerDe не знает.
    /// 2. Незарегистрированный тип не ломается, а просто идёт штатным путём.
    /// 3. Быстрый и медленный пути дают один и тот же документ.
    ///
    /// Обратите внимание на alias вверху файла: это ровно та единственная строка,
    /// которую предстоит написать потребителю, - здесь она стоит в самом тесте,
    /// чтобы было видно, что подмена действительно прозрачна.
    /// </summary>
    public class CompatFixture
    {
        //регистрации здесь нет вовсе: её пишет генератор в [ModuleInitializer],
        //а всё, что он для этого видит, - вызовы new XmlSerializer(typeof(T))
        //прямо в этих тестах

        private static CompatSubject CreateSubject() =>
            new CompatSubject
            {
                Number = 42,
                Name = "compat",
                Values = new List<int> { 1, 2, 3 }
            };

        [Fact]
        public void Facade_IsRealXmlSerializer_Test()
        {
            var serializer = new XmlSerializer(typeof(CompatSubject));

            Assert.IsAssignableFrom<BclXmlSerializer>(serializer);
            Assert.True(serializer.IsAccelerated, "тип зарегистрирован, значит должен обслуживаться быстрым путём");
        }

        /// <summary>
        /// Обещание, ради которого генератору позволено отказываться: отказ ничего
        /// не ломает. Структуру XmlSerDe не умеет, и это видно по
        /// <see cref="XmlSerDe.Compat.XmlSerializer.IsAccelerated"/>, - но
        /// round-trip через фасад всё равно проходит, просто штатным путём.
        /// </summary>
        [Fact]
        public void UnacceleratedStruct_FallsBackInsteadOfBreaking_Test()
        {
            var serializer = new XmlSerializer(typeof(UnacceleratedStruct));

            Assert.False(serializer.IsAccelerated, "структуру генератор обслуживать не умеет");

            var subject = new UnacceleratedStruct { Number = 7, Name = "plain" };

            var writer = new StringWriter();
            serializer.Serialize(writer, subject);
            var xml = writer.ToString();

            var back = (UnacceleratedStruct)serializer.Deserialize(new StringReader(xml));

            Assert.Equal(7, back.Number);
            Assert.Equal("plain", back.Name);
        }

        /// <summary>
        /// Тот же отказ, но виноват член, а не сам корень: неподдержанная коллекция
        /// вглубь графа снимает ускорение со всего типа целиком.
        /// </summary>
        [Fact]
        public void UnacceleratedCollection_FallsBackInsteadOfBreaking_Test()
        {
            var serializer = new XmlSerializer(typeof(UnacceleratedCollectionSubject));

            Assert.False(serializer.IsAccelerated, "HashSet<T> генератор обслуживать не умеет");

            var subject = new UnacceleratedCollectionSubject
            {
                Set = new HashSet<int> { 1, 2, },
                After = 3,
            };

            var writer = new StringWriter();
            serializer.Serialize(writer, subject);

            var back = (UnacceleratedCollectionSubject)serializer.Deserialize(
                new StringReader(writer.ToString())
                );

            Assert.Equal(new HashSet<int> { 1, 2, }, back.Set);
            Assert.Equal(3, back.After);
        }

        /// <summary>
        /// Граф глубже одного типа: в точке вызова назван только
        /// <see cref="CompatHolder"/>, а сгенерировать пришлось и ребёнка,
        /// и элемент коллекции, и наследника из <c>[XmlInclude]</c>.
        /// </summary>
        [Fact]
        public void TransitiveGraph_IsWalkedFromTheCallSite_Test()
        {
            var serializer = new XmlSerializer(typeof(CompatHolder));

            Assert.True(serializer.IsAccelerated, "весь граф поддержан, значит тип должен быть ускорен");

            var subject = new CompatHolder
            {
                Child = new CompatChild { Title = "child", Number = 1, },
                Children = new List<CompatChild>
                {
                    new CompatChild { Title = "first", Number = 2, },
                    new CompatChild { Title = "second", Number = 3, },
                },
                Polymorphic = new CompatDerived { BaseNumber = 4, DerivedName = "derived", },
            };

            var xml = serializer.SerializeToString(subject, appendXmlDeclaration: false);

            //документ обязан читаться штатным сериализатором: иначе ускорение
            //куплено ценой совместимости, ради которой всё и затевалось
            var bclBack = (CompatHolder)new BclXmlSerializer(typeof(CompatHolder))
                .Deserialize(new StringReader(xml));

            Assert.Equal("child", bclBack.Child.Title);
            Assert.Equal(1, bclBack.Child.Number);
            Assert.Equal(2, bclBack.Children.Count);
            Assert.Equal("second", bclBack.Children[1].Title);
            Assert.Equal("derived", ((CompatDerived)bclBack.Polymorphic).DerivedName);

            var back = (CompatHolder)serializer.Deserialize(xml.AsSpan());

            Assert.Equal("child", back.Child.Title);
            Assert.Equal(3, back.Children[1].Number);
            Assert.Equal("derived", ((CompatDerived)back.Polymorphic).DerivedName);
        }

        /// <summary>
        /// null - это не «нечего писать»: BCL пишет <c>&lt;T xsi:nil="true" /&gt;</c>.
        /// Быстрый путь такой документ выдать не может (сериализация начинается
        /// с разыменования), поэтому null обязан уходить штатным путём на **всех**
        /// перегрузках, а не только на контракте предгенерированных сборок. Пока это
        /// стояло в одном месте из четырёх, ускоренный тип молча отдавал пустой
        /// документ - потеря объекта целиком.
        /// </summary>
        [Fact]
        public void Null_GoesThroughSystemXml_Test()
        {
            var serializer = new XmlSerializer(typeof(CompatSubject));

            Assert.True(serializer.IsAccelerated, "иначе тест проверяет не то, что должен");

            var bcl = new BclXmlSerializer(typeof(CompatSubject));

            var expected = new StringWriter();
            bcl.Serialize(expected, null);

            var actual = new StringWriter();
            serializer.Serialize(actual, (object)null);

            Assert.Contains("xsi:nil=\"true\"", actual.ToString());
            Assert.Equal(expected.ToString(), actual.ToString());

            //SerializeToString сток выбирает сам и объявляет utf-8, поэтому ожидание
            //для него снимается с BCL через такой же сток, а не через StringWriter
            //с его utf-16
            var expectedUtf8 = new Utf8StringWriter();
            bcl.Serialize(expectedUtf8, null);
            Assert.Equal(expectedUtf8.ToString(), serializer.SerializeToString(null));

            var stream = new MemoryStream();
            serializer.Serialize(stream, (object)null);
            Assert.Contains("xsi:nil=\"true\"", Encoding.UTF8.GetString(stream.ToArray()));

            Assert.Null(serializer.Deserialize(actual.ToString().AsSpan()));
        }

        /// <summary>
        /// Штатные конструкторы существуют здесь ради компиляции: без них подмена
        /// одной строкой <c>global using</c> ломала сборку везде, где вызывающий ими
        /// пользовался, - то есть цена перехода росла вместе с проектом.
        ///
        /// Пустой добавочный аргумент - это тот же простой случай, и ускорение
        /// обязано остаться.
        /// </summary>
        [Fact]
        public void EmptyExtraArguments_StayAccelerated_Test()
        {
            Assert.True(new XmlSerializer(typeof(CompatSubject), (string)null).IsAccelerated);
            Assert.True(new XmlSerializer(typeof(CompatSubject), "").IsAccelerated);
            Assert.True(new XmlSerializer(typeof(CompatSubject), Type.EmptyTypes).IsAccelerated);
            Assert.True(new XmlSerializer(typeof(CompatSubject), (Type[])null).IsAccelerated);
            Assert.True(new XmlSerializer(typeof(CompatSubject), (XmlAttributeOverrides)null).IsAccelerated);
            Assert.True(new XmlSerializer(typeof(CompatSubject), null, null, null, null).IsAccelerated);

            //и документ обязан остаться тем же самым
            Assert.Equal(
                new XmlSerializer(typeof(CompatSubject)).SerializeToString(CreateSubject()),
                new XmlSerializer(typeof(CompatSubject), Type.EmptyTypes).SerializeToString(CreateSubject())
                );
        }

        /// <summary>
        /// Непустой добавочный аргумент ускорить нечем: пространство имён XmlSerDe
        /// объявить не может, <see cref="XmlAttributeOverrides"/> меняет разметку
        /// типов, которую генератор разобрал ещё на этапе сборки, а <c>extraTypes</c>
        /// добавляет наследников, о которых сгенерированный код не знает. Такой
        /// сериализатор обязан уйти штатным путём целиком - и работать.
        /// </summary>
        [Fact]
        public void NonEmptyExtraArguments_FallBackAndStillWork_Test()
        {
            var overrides = new XmlAttributeOverrides();
            overrides.Add(typeof(CompatSubject), "Name", new XmlAttributes { XmlIgnore = true, });

            var cases = new[]
            {
                new XmlSerializer(typeof(CompatSubject), "urn:test"),
                new XmlSerializer(typeof(CompatSubject), new[] { typeof(CompatChild), }),
                new XmlSerializer(typeof(CompatSubject), overrides),
                new XmlSerializer(typeof(CompatSubject), new XmlRootAttribute("other")),
            };

            foreach (var serializer in cases)
            {
                Assert.False(serializer.IsAccelerated, "добавочный аргумент ускорить нечем");
            }

            //фолбэк обязан быть построен именно с этими аргументами, а не с одним типом:
            //иначе он выдал бы документ, о котором вызывающий не просил
            var namespaced = cases[0].SerializeToString(CreateSubject());
            Assert.Contains("urn:test", namespaced);

            var renamed = cases[3].SerializeToString(CreateSubject());
            Assert.Contains("<other", renamed);

            var ignored = cases[2].SerializeToString(CreateSubject());
            Assert.DoesNotContain("compat", ignored);

            //и round-trip через штатный путь всё равно замыкается
            var back = (CompatSubject)cases[0].Deserialize(new StringReader(namespaced));
            Assert.Equal(42, back.Number);
        }

        /// <summary>
        /// Ошибка обязана выглядеть как ошибка штатного сериализатора: тот заворачивает
        /// всё, что случилось внутри, в <see cref="InvalidOperationException"/> с настоящей
        /// причиной внутри. Быстрый путь раньше отдавал причину голой, и
        /// <c>catch (InvalidOperationException)</c>, стоявший в коде потребителя до
        /// подмены, переставал ловить - то есть подмена не была прозрачной.
        ///
        /// Ожидание снимается с самого BCL прямо в тесте, а не записано литералом:
        /// расходиться должны они, а не наше представление о них.
        /// </summary>
        [Fact]
        public void WrongTypeOnSerialize_ThrowsLikeSystemXml_Test()
        {
            var serializer = new XmlSerializer(typeof(CompatSubject));
            Assert.True(serializer.IsAccelerated, "иначе тест проверяет фолбэк, а не быстрый путь");

            var bcl = new BclXmlSerializer(typeof(CompatSubject));
            var alien = new CompatChild { Title = "alien" };

            var expected = Assert.Throws<InvalidOperationException>(
                () => bcl.Serialize(new StringWriter(), alien)
                );

            //все четыре быстрые перегрузки, а не только та, что попалась под руку:
            //расхождение и жило ровно в том, что ветку поставили в одном месте из четырёх
            var actual = new[]
            {
                Assert.Throws<InvalidOperationException>(() => serializer.SerializeToString(alien)),
                Assert.Throws<InvalidOperationException>(() => serializer.Serialize(new StringWriter(), alien)),
                Assert.Throws<InvalidOperationException>(() => serializer.Serialize(new MemoryStream(), alien)),
            };

            foreach (var thrown in actual)
            {
                Assert.IsType(expected.InnerException.GetType(), thrown.InnerException);
#if NETFRAMEWORK
                //на .NET Framework у BCL есть локализованные ресурсы, и текст зависит
                //от UI-культуры машины («Ошибка при создании документа XML.» на ru-RU).
                //Наш литерал один на все таргеты - английский, как на .NET Core;
                //сверять его с локализованным нечем, поэтому здесь только тип и причина
                Assert.IsType<InvalidOperationException>(thrown);
#else
                Assert.Equal(expected.Message, thrown.Message);
#endif
            }
        }

        /// <summary>
        /// То же на чтении. Сообщение сверяется не с сообщением BCL, а с его формой
        /// без позиции: позицию быстрый путь назвать не может - он разбирает спан,
        /// а не <see cref="XmlReader"/>, - и BCL в этом случае (читатель без
        /// <see cref="System.Xml.IXmlLineInfo"/>) выдаёт ровно эту же строку.
        /// </summary>
        [Fact]
        public void BrokenXmlOnDeserialize_ThrowsLikeSystemXml_Test()
        {
            var serializer = new XmlSerializer(typeof(CompatSubject));
            Assert.True(serializer.IsAccelerated, "иначе тест проверяет фолбэк, а не быстрый путь");

            //ссылка стоит в строковом члене намеренно: у числового текст уходит
            //прямо в разбор числа, минуя декодер, и наружу выходит
            //FormatException вместо ошибки документа. Это расхождение известно
            //и записано (docs/opt-in-xml-guards.md §16), а здесь проверяется
            //цепочка, а не оно
            var broken = "<CompatSubject><Name>&nosuchentity;</Name></CompatSubject>";

            var bcl = new BclXmlSerializer(typeof(CompatSubject));
            var expected = Assert.Throws<InvalidOperationException>(
                () => bcl.Deserialize(new StringReader(broken))
                );
            Assert.NotNull(expected.InnerException);

            foreach (var thrown in new[]
                {
                    Assert.Throws<InvalidOperationException>(() => serializer.Deserialize(broken.AsSpan())),
                    Assert.Throws<InvalidOperationException>(() => serializer.Deserialize(new StringReader(broken))),
                    Assert.Throws<InvalidOperationException>(
                        () => serializer.Deserialize(new MemoryStream(Encoding.UTF8.GetBytes(broken)))
                        ),
                })
            {
                Assert.Equal("There is an error in the XML document.", thrown.Message);

                //цепочка совпадает с BCL целиком, а не только наружным типом:
                //InvalidOperationException -> XmlException. До стражей внутри
                //лежало наше InvalidOperationException, и catch (XmlException)
                //в чужом коде не ловил (docs/opt-in-xml-guards.md §6)
                Assert.IsType<XmlException>(expected.InnerException);
                Assert.IsType<XmlException>(thrown.InnerException);
            }
        }

        /// <summary>
        /// Через базовый тип фасад не заворачивает вовсе - это делает сам базовый
        /// класс, и делает <b>дважды</b>: контракт предгенерированных сборок зовёт
        /// <c>Serialize(object, XmlSerializationWriter)</c> из уже завёрнутого места,
        /// а публичная перегрузка заворачивает ещё раз. Свой слой сюда добавлять
        /// нельзя - стало бы три.
        ///
        /// Лишний уровень снять нечем: обе обёртки лежат выше нашего кода. Тип
        /// и сообщение снаружи совпадают - то есть <c>catch</c> ловит, - а настоящая
        /// причина в самом низу цепочки та же, что у BCL. Проверено, что это так,
        /// а не заявлено.
        /// </summary>
        [Fact]
        public void WrongTypeThroughBaseTypedReference_ThrowsLikeSystemXml_Test()
        {
            BclXmlSerializer serializer = new XmlSerializer(typeof(CompatSubject));
            var alien = new CompatChild { Title = "alien" };

            var expected = Assert.Throws<InvalidOperationException>(
                () => new BclXmlSerializer(typeof(CompatSubject)).Serialize(new StringWriter(), alien)
                );
            var thrown = Assert.Throws<InvalidOperationException>(
                () => serializer.Serialize(new StringWriter(), alien)
                );

            Assert.Equal(expected.Message, thrown.Message);
            Assert.IsType(Innermost(expected).GetType(), Innermost(thrown));
        }

        private static Exception Innermost(Exception e)
        {
            while (e.InnerException is not null)
            {
                e = e.InnerException;
            }

            return e;
        }

        /// <summary>
        /// Аргумент, которого нет, - это не ошибка документа: заворачивать его
        /// в ошибку записи было бы враньём. Равняемся здесь на .NET Core: там BCL
        /// бросает <see cref="ArgumentNullException"/> с именами <c>output</c>
        /// и <c>input</c>.
        ///
        /// На .NET Framework тот же BCL на null-писателе и null-потоке чтения падает
        /// <see cref="NullReferenceException"/> из старого <c>XmlTextWriter</c>, а имя
        /// параметра у <c>Serialize(Stream)</c> там другое (<c>stream</c>). Это снято
        /// прогоном и зафиксировано ниже, но не воспроизводится: повторять баг,
        /// исправленный выше по течению, ради буквального совпадения незачем.
        /// </summary>
        [Fact]
        public void NullArguments_ThrowLikeSystemXml_Test()
        {
            var serializer = new XmlSerializer(typeof(CompatSubject));
            var bcl = new BclXmlSerializer(typeof(CompatSubject));

            Assert.Equal("output", Assert.Throws<ArgumentNullException>(
                () => serializer.Serialize((TextWriter)null, CreateSubject())).ParamName);
            Assert.Equal("output", Assert.Throws<ArgumentNullException>(
                () => serializer.Serialize((Stream)null, CreateSubject())).ParamName);
            Assert.Equal("input", Assert.Throws<ArgumentNullException>(
                () => serializer.Deserialize((Stream)null)).ParamName);

            //конструктор одинаков на всех таргетах
            Assert.Equal(
                Assert.Throws<ArgumentNullException>(() => new BclXmlSerializer((Type)null)).ParamName,
                Assert.Throws<ArgumentNullException>(() => new XmlSerializer(null)).ParamName
                );

#if NETFRAMEWORK
            //то, от чего мы здесь сознательно отступаем
            Assert.Throws<NullReferenceException>(() => bcl.Serialize((TextWriter)null, CreateSubject()));
            Assert.Throws<NullReferenceException>(() => bcl.Deserialize((Stream)null));
            Assert.Equal("stream", Assert.Throws<ArgumentNullException>(
                () => bcl.Serialize((Stream)null, CreateSubject())).ParamName);
#else
            Assert.Equal("output", Assert.Throws<ArgumentNullException>(
                () => bcl.Serialize((TextWriter)null, CreateSubject())).ParamName);
            Assert.Equal("output", Assert.Throws<ArgumentNullException>(
                () => bcl.Serialize((Stream)null, CreateSubject())).ParamName);
            Assert.Equal("input", Assert.Throws<ArgumentNullException>(
                () => bcl.Deserialize((Stream)null)).ParamName);
#endif
        }

        [Fact]
        public void SerializeToString_MatchesSystemXml_Test()
        {
            var subject = CreateSubject();

            var ours = new XmlSerializer(typeof(CompatSubject)).SerializeToString(subject, appendXmlDeclaration: false);

            var bcl = new BclXmlSerializer(typeof(CompatSubject));
            var sb = new StringBuilder();
            var settings = new XmlWriterSettings
            {
                OmitXmlDeclaration = true,
                Indent = false
            };
            using (var xw = XmlWriter.Create(sb, settings))
            {
                var noNamespaces = new System.Xml.Serialization.XmlSerializerNamespaces();
                noNamespaces.Add("", "");
                bcl.Serialize(xw, subject, noNamespaces);
            }

            Assert.Equal(sb.ToString(), ours);
        }

        [Fact]
        public void RoundTrip_ThroughFacadeOwnOverloads_Test()
        {
            var serializer = new XmlSerializer(typeof(CompatSubject));

            var writer = new StringWriter();
            serializer.Serialize(writer, CreateSubject());

            var back = (CompatSubject)serializer.Deserialize(new StringReader(writer.ToString()));

            AssertSameSubject(back);
        }

        [Fact]
        public void RoundTrip_ThroughStream_Test()
        {
            var serializer = new XmlSerializer(typeof(CompatSubject));

            var stream = new MemoryStream();
            serializer.Serialize(stream, CreateSubject());
            stream.Position = 0;

            var back = (CompatSubject)serializer.Deserialize(stream);

            AssertSameSubject(back);
        }

        /// <summary>
        /// Главный тест всей затеи: вызов идёт через переменную статического типа
        /// <see cref="BclXmlSerializer"/>, то есть ровно так, как это сделает чужой
        /// код, никогда не слышавший про XmlSerDe.
        /// </summary>
        [Fact]
        public void RoundTrip_ThroughBaseTypedReference_Test()
        {
            BclXmlSerializer serializer = new XmlSerializer(typeof(CompatSubject));

            var writer = new StringWriter();
            serializer.Serialize(writer, CreateSubject());
            var xml = writer.ToString();

            //документ, написанный через базовый тип, обязан читаться штатным
            //сериализатором - иначе прозрачной подмены не получилось
            var bclBack = (CompatSubject)new BclXmlSerializer(typeof(CompatSubject))
                .Deserialize(new StringReader(xml));
            AssertSameSubjectValues(bclBack);

            var back = (CompatSubject)serializer.Deserialize(new StringReader(xml));
            AssertSameSubject(back);
        }

        [Fact]
        public void Serialize_ThroughXmlWriter_ProducesWellFormedDocument_Test()
        {
            BclXmlSerializer serializer = new XmlSerializer(typeof(CompatSubject));

            var sb = new StringBuilder();
            using (var xw = XmlWriter.Create(sb))
            {
                serializer.Serialize(xw, CreateSubject());
            }

            //если бы фасад написал второе объявление <?xml?> или незакрытый тег,
            //разбор бы упал
            var document = new XmlDocument();
            document.LoadXml(sb.ToString());

            Assert.Equal("CompatSubject", document.DocumentElement.Name);
        }

        /// <summary>
        /// XmlSerDe пишет пространства имён фиксированными литералами, поэтому
        /// чужой <see cref="System.Xml.Serialization.XmlSerializerNamespaces"/>
        /// учесть не может. Правильная реакция - отдать вызов штатному
        /// сериализатору целиком, а не выдать документ без запрошенных объявлений.
        /// </summary>
        [Fact]
        public void CustomNamespaces_GoThroughSystemXml_Test()
        {
            BclXmlSerializer serializer = new XmlSerializer(typeof(CompatSubject));

            var namespaces = new System.Xml.Serialization.XmlSerializerNamespaces();
            namespaces.Add("t", "urn:test");

            var sb = new StringBuilder();
            using (var xw = XmlWriter.Create(sb))
            {
                serializer.Serialize(xw, CreateSubject(), namespaces);
            }

            Assert.Contains("urn:test", sb.ToString());
        }

        [Fact]
        public void Deserialize_FromSpan_Test()
        {
            var serializer = new XmlSerializer(typeof(CompatSubject));
            var xml = serializer.SerializeToString(CreateSubject());

            var back = (CompatSubject)serializer.Deserialize(xml.AsSpan());

            AssertSameSubject(back);
        }

        /// <summary>
        /// Кодировка в объявлении - свойство стока, а не константа: BCL берёт её
        /// из <see cref="TextWriter.Encoding"/>. Быстрый путь писал utf-8 куда угодно,
        /// и документ, записанный в utf-16 с объявлением «utf-8», строгий читатель
        /// вправе не принять - пролог противоречит байтам.
        ///
        /// Ожидание снимается с BCL прямо здесь: сверять литералы с литералами
        /// значило бы проверять собственное представление о BCL.
        /// </summary>
        [Fact]
        public void XmlDeclaration_FollowsTheSinkEncoding_Test()
        {
            var serializer = new XmlSerializer(typeof(CompatSubject));
            Assert.True(serializer.IsAccelerated, "иначе тест проверяет фолбэк, а не быстрый путь");

            var bcl = new BclXmlSerializer(typeof(CompatSubject));
            var subject = CreateSubject();

            //StringWriter объявляет utf-16 - именно тот случай, который раньше врал
            var ourString = new StringWriter();
            serializer.Serialize(ourString, subject);
            var bclString = new StringWriter();
            bcl.Serialize(bclString, subject);

            Assert.Equal(Declaration(bclString.ToString()), Declaration(ourString.ToString()));
            Assert.Contains("utf-16", Declaration(ourString.ToString()));

            foreach (var encoding in new Encoding[]
                {
                    new UTF8Encoding(false),
                    Encoding.Unicode,
                    Encoding.BigEndianUnicode,
                    Encoding.ASCII,
                })
            {
                Assert.Equal(
                    Declaration(WriteThrough(bcl, subject, encoding)),
                    Declaration(WriteThrough(serializer, subject, encoding))
                    );
            }

            //у самодельного писателя свойство вправе вернуть null, и объявление
            //тогда пишется без кодировки вовсе - одинаково на всех таргетах (проверено)
            var ourNull = new NullEncodingWriter();
            serializer.Serialize(ourNull, subject);
            var bclNull = new NullEncodingWriter();
            bcl.Serialize(bclNull, subject);

            Assert.Equal("<?xml version=\"1.0\"?>", Declaration(bclNull.ToString()));
            Assert.Equal(Declaration(bclNull.ToString()), Declaration(ourNull.ToString()));

            //перегрузка со Stream кодировку не выбирает вовсе
            var ourStream = new MemoryStream();
            serializer.Serialize(ourStream, subject);
            var bclStream = new MemoryStream();
            bcl.Serialize(bclStream, subject);

            var ourStreamDeclaration = Declaration(Encoding.UTF8.GetString(ourStream.ToArray()));

#if NETFRAMEWORK
            //здесь мы сознательно расходимся: .NET Framework пишет в поток объявление
            //без кодировки вовсе, .NET Core - с utf-8 (снято прогоном обоих). Оба
            //документа корректны - utf-8 и есть умолчание по спецификации, - но
            //равняемся мы на .NET Core, как и во всём остальном
            Assert.Equal("<?xml version=\"1.0\"?>", Declaration(Encoding.UTF8.GetString(bclStream.ToArray())));
            Assert.Equal("<?xml version=\"1.0\" encoding=\"utf-8\"?>", ourStreamDeclaration);
#else
            Assert.Equal(
                Declaration(Encoding.UTF8.GetString(bclStream.ToArray())),
                ourStreamDeclaration
                );
#endif
        }

        private sealed class NullEncodingWriter : StringWriter
        {
            public override Encoding Encoding => null;
        }

        private sealed class Utf8StringWriter : StringWriter
        {
            public override Encoding Encoding => Encoding.UTF8;
        }

        /// <summary>
        /// <see cref="XmlSerDe.Compat.XmlSerializer.SerializeToString"/> - метод
        /// собственный, стока у него нет, и utf-8 здесь единственный осмысленный
        /// ответ про строку, которую сейчас запишут в файл. Важно другое: ответ
        /// обязан быть один и тот же независимо от того, ускорен тип или нет.
        /// </summary>
        [Fact]
        public void SerializeToString_DeclaresUtf8_WhicheverPathItTook_Test()
        {
            var accelerated = new XmlSerializer(typeof(CompatSubject));
            var fallback = new XmlSerializer(typeof(UnacceleratedStruct));

            Assert.True(accelerated.IsAccelerated);
            Assert.False(fallback.IsAccelerated);

            Assert.Equal(
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>",
                Declaration(accelerated.SerializeToString(CreateSubject()))
                );
            Assert.Equal(
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>",
                Declaration(fallback.SerializeToString(new UnacceleratedStruct { Number = 1, }))
                );

            //и отказ от объявления обязан работать на обоих путях, а не только
            //на быстром
            Assert.StartsWith("<CompatSubject", accelerated.SerializeToString(CreateSubject(), false));
            Assert.StartsWith("<UnacceleratedStruct", fallback.SerializeToString(new UnacceleratedStruct(), false));
        }

        private static string WriteThrough(BclXmlSerializer serializer, object subject, Encoding encoding)
        {
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream, encoding);
            serializer.Serialize(writer, subject);
            writer.Flush();

            return encoding.GetString(stream.ToArray());
        }

        private static string WriteThrough(XmlSerializer serializer, object subject, Encoding encoding)
        {
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream, encoding);
            serializer.Serialize(writer, subject);
            writer.Flush();

            return encoding.GetString(stream.ToArray());
        }

        private static string Declaration(string xml)
        {
            var trimmed = xml.TrimStart('﻿');
            var end = trimmed.IndexOf("?>", StringComparison.Ordinal);

            return end < 0 ? "" : trimmed.Substring(0, end + 2);
        }

        /// <summary>
        /// Вопрос «твой ли это документ» сводится к имени корневого элемента, и
        /// ускоренному типу оно известно из реестра. Раньше ответ строился штатным
        /// сериализатором - то есть разбором типа рефлексией целиком ради одного
        /// сравнения строк.
        ///
        /// Все ожидания снимаются с BCL здесь же, включая неочевидное: тип
        /// с <c>[XmlRoot("purchase")]</c> отзывается на <c>&lt;purchase&gt;</c>
        /// и не отзывается на собственное имя.
        /// </summary>
        [Fact]
        public void CanDeserialize_AnswersLikeSystemXml_Test()
        {
            var serializer = new XmlSerializer(typeof(CompatSubject));
            var bcl = new BclXmlSerializer(typeof(CompatSubject));

            Assert.True(serializer.IsAccelerated, "иначе тест проверяет фолбэк, а не реестр");

            foreach (var xml in new[]
                {
                    "<CompatSubject />",
                    "<CompatSubject><Number>1</Number></CompatSubject>",
                    "<?xml version=\"1.0\"?><CompatSubject />",
                    "  <!-- c --> <CompatSubject />",
                    "<Alien />",
                    "<CompatSubject xmlns=\"urn:test\" />",
                })
            {
                Assert.Equal(Can(bcl, xml), Can(serializer, xml));
            }

            var renamed = new XmlSerializer(typeof(CompatRenamedRoot));
            var bclRenamed = new BclXmlSerializer(typeof(CompatRenamedRoot));

            Assert.True(renamed.IsAccelerated, "[XmlRoot] с одним именем ускорению не мешает");

            foreach (var xml in new[] { "<purchase />", "<CompatRenamedRoot />", })
            {
                Assert.Equal(Can(bclRenamed, xml), Can(renamed, xml));
            }

            //читатель после вопроса обязан остаться на корне: и BCL, и мы делаем
            //MoveToContent, поэтому следом идущий разбор работает
            using (var reader = XmlReader.Create(new StringReader("<CompatSubject><Number>42</Number></CompatSubject>")))
            {
                Assert.True(serializer.CanDeserialize(reader));

                var back = (CompatSubject)serializer.Deserialize(reader);
                Assert.Equal(42, back.Number);
            }
        }

        private static bool Can(BclXmlSerializer serializer, string xml)
        {
            using var reader = XmlReader.Create(new StringReader(xml));

            return serializer.CanDeserialize(reader);
        }

        private static bool Can(XmlSerializer serializer, string xml)
        {
            using var reader = XmlReader.Create(new StringReader(xml));

            return serializer.CanDeserialize(reader);
        }

        /// <summary>
        /// События разбора быстрый путь поднять не может в принципе: у
        /// <see cref="XmlElementEventArgs"/> и его соседей нет ни одного публичного
        /// конструктора, а метод, которым базовый класс передаёт читателю список
        /// подписчиков, внутренний. Значит подписка обязана означать «этот экземпляр
        /// разбирает штатным сериализатором целиком» - молчать там, где потребитель
        /// попросил сообщить, нельзя.
        ///
        /// Ожидания снова снимаются с BCL: сверяется и имя элемента, и список
        /// ожидавшихся, который мы не составили бы сами.
        /// </summary>
        [Fact]
        public void UnknownElementSubscription_FallsBackAndFires_Test()
        {
            var xml = "<CompatSubject><Number>42</Number><Nope>x</Nope></CompatSubject>";

            var expected = new List<string>();
            var bcl = new BclXmlSerializer(typeof(CompatSubject));
            bcl.UnknownElement += (s, e) => expected.Add($"{e.Element.Name}|{e.ExpectedElements}");
            bcl.Deserialize(new StringReader(xml));

            var serializer = new XmlSerializer(typeof(CompatSubject));
            Assert.True(serializer.IsAccelerated);
            Assert.True(serializer.IsDeserializationAccelerated, "пока никто не подписан, разбор быстрый");

            var actual = new List<string>();
            XmlElementEventHandler handler = (s, e) => actual.Add($"{e.Element.Name}|{e.ExpectedElements}");
            serializer.UnknownElement += handler;

            Assert.True(serializer.IsAccelerated, "тип по-прежнему ускорен - изменился путь разбора, а не тип");
            Assert.False(serializer.IsDeserializationAccelerated, "подписка уводит разбор штатным путём");

            var back = (CompatSubject)serializer.Deserialize(new StringReader(xml));
            Assert.Equal(42, back.Number);

            Assert.NotEmpty(expected);
            Assert.Equal(expected, actual);

            //и span-перегрузка тоже: подписка не должна зависеть от того, какой
            //перегрузкой её позвали
            actual.Clear();
            serializer.Deserialize(xml.AsSpan());
            Assert.Equal(expected, actual);

            //отписка возвращает ускорение
            serializer.UnknownElement -= handler;
            Assert.True(serializer.IsDeserializationAccelerated);

            actual.Clear();
            var fast = (CompatSubject)serializer.Deserialize(xml.AsSpan());
            Assert.Equal(42, fast.Number);
            Assert.Empty(actual);
        }

        /// <summary>
        /// Подписка после того, как штатный сериализатор уже построен: обработчик
        /// обязан достаться <b>тому самому</b> экземпляру, а не следующему.
        /// </summary>
        [Fact]
        public void SubscriptionAfterFallbackWasBuilt_StillFires_Test()
        {
            var serializer = new XmlSerializer(typeof(CompatSubject));

            //первый разбор строит фолбэк: подписка ещё не сделана, но тип не важен -
            //важно, что экземпляр уже существует
            serializer.UnknownNode += (s, e) => { };
            serializer.Deserialize(new StringReader("<CompatSubject />"));

            var seen = new List<string>();
            serializer.UnknownElement += (s, e) => seen.Add(e.Element.Name);

            serializer.Deserialize(new StringReader("<CompatSubject><Nope /></CompatSubject>"));

            Assert.Equal(new[] { "Nope", }, seen);
        }

        /// <summary>
        /// Остальные три события той же природы. <c>UnknownNode</c> BCL поднимает
        /// и на неизвестном атрибуте, и на неизвестном элементе - повторить это
        /// самим было бы отдельной работой, а фолбэк делает это даром.
        /// </summary>
        [Fact]
        public void OtherDeserializationEvents_FallBackToo_Test()
        {
            var xml = "<CompatSubject zzz=\"1\"><Nope /></CompatSubject>";

            var expected = new List<string>();
            var bcl = new BclXmlSerializer(typeof(CompatSubject));
            bcl.UnknownNode += (s, e) => expected.Add($"node:{e.Name}:{e.NodeType}");
            bcl.UnknownAttribute += (s, e) => expected.Add($"attr:{e.Attr.Name}");
            bcl.Deserialize(new StringReader(xml));

            var actual = new List<string>();
            var serializer = new XmlSerializer(typeof(CompatSubject));
            serializer.UnknownNode += (s, e) => actual.Add($"node:{e.Name}:{e.NodeType}");
            serializer.UnknownAttribute += (s, e) => actual.Add($"attr:{e.Attr.Name}");

            Assert.False(serializer.IsDeserializationAccelerated);

            serializer.Deserialize(new StringReader(xml));

            Assert.NotEmpty(expected);
            Assert.Equal(expected, actual);
        }

        /// <summary>
        /// Сериализации подписки не касаются вовсе: событий про запись у
        /// <see cref="BclXmlSerializer"/> нет, и терять на них скорость не за что.
        /// </summary>
        [Fact]
        public void Subscription_DoesNotSlowDownSerialization_Test()
        {
            var serializer = new XmlSerializer(typeof(CompatSubject));
            var before = serializer.SerializeToString(CreateSubject());

            serializer.UnknownElement += (s, e) => { };

            Assert.True(serializer.IsAccelerated);
            Assert.Equal(before, serializer.SerializeToString(CreateSubject()));
        }

        /// <summary>
        /// Список подписчиков, переданный прямо в вызов, - вход в обход подписки
        /// на самом сериализаторе, и до правки он молча уходил в быстрый путь:
        /// перегрузка не виртуальная, базовый класс про фасад не знает и доводил
        /// её до контракта предгенерированных сборок.
        /// </summary>
        [Fact]
        public void DeserializeWithEventsArgument_FallsBackAndFires_Test()
        {
            var xml = "<CompatSubject><Number>42</Number><Nope>x</Nope></CompatSubject>";

            var expected = new List<string>();
            var bclEvents = new XmlDeserializationEvents
            {
                OnUnknownElement = (s, e) => expected.Add($"{e.Element.Name}|{e.ExpectedElements}"),
            };

            using (var reader = XmlReader.Create(new StringReader(xml)))
            {
                new BclXmlSerializer(typeof(CompatSubject)).Deserialize(reader, bclEvents);
            }

            var actual = new List<string>();
            var events = new XmlDeserializationEvents
            {
                OnUnknownElement = (s, e) => actual.Add($"{e.Element.Name}|{e.ExpectedElements}"),
            };

            var serializer = new XmlSerializer(typeof(CompatSubject));
            Assert.True(serializer.IsAccelerated, "иначе тест проверяет фолбэк, а не подмену пути");

            CompatSubject back;
            using (var reader = XmlReader.Create(new StringReader(xml)))
            {
                back = (CompatSubject)serializer.Deserialize(reader, events);
            }

            Assert.Equal(42, back.Number);
            Assert.NotEmpty(expected);
            Assert.Equal(expected, actual);

            //пустой список подписчиков - это не подписка: быстрый путь остаётся
            using (var reader = XmlReader.Create(new StringReader(xml)))
            {
                var fast = (CompatSubject)serializer.Deserialize(reader, new XmlDeserializationEvents());
                Assert.Equal(42, fast.Number);
            }

            Assert.True(serializer.IsDeserializationAccelerated, "вызов с событиями не должен гасить ускорение навсегда");
        }

        /// <summary>
        /// SOAP-кодирования XmlSerDe не умеет, и притвориться нечем: штатный
        /// сериализатор, собранный не через <c>SoapReflectionImporter</c>, на непустом
        /// <c>encodingStyle</c> бросает. Быстрый путь молча написал бы обычный
        /// документ - то есть не ту форму, о которой просили.
        /// </summary>
        [Fact]
        public void EncodingStyle_ThrowsLikeSystemXml_Test()
        {
            const string soap = "http://schemas.xmlsoap.org/soap/encoding/";

            var serializer = new XmlSerializer(typeof(CompatSubject));
            var bcl = new BclXmlSerializer(typeof(CompatSubject));
            var subject = CreateSubject();

            var expected = Assert.Throws<InvalidOperationException>(() => Write(bcl, subject, soap));
            var thrown = Assert.Throws<InvalidOperationException>(() => Write(serializer, subject, soap));

            Assert.Equal(expected.Message, thrown.Message);

            var xml = serializer.SerializeToString(subject, appendXmlDeclaration: false);

            var expectedRead = Assert.Throws<InvalidOperationException>(
                () => Read(bcl, xml, soap)
                );
            var thrownRead = Assert.Throws<InvalidOperationException>(
                () => Read(serializer, xml, soap)
                );

            Assert.Equal(expectedRead.Message, thrownRead.Message);

            //null encodingStyle - обычный вызов, он обязан работать
            Assert.NotNull(Read(serializer, xml, null));

            //то, от чего перекрытие не спасает, - и это зафиксировано, а не забыто:
            //через ссылку базового типа перегрузка не наша (у базового класса она
            //не виртуальная), и фасад молча пишет обычный документ там, где BCL
            //бросает. Проверено прогоном; закрыть нечем, кроме как перестать быть
            //drop-in, поэтому дыра описана в README
            BclXmlSerializer asBase = serializer;
            Assert.Null(Record.Exception(() => Write(asBase, subject, soap)));
        }

        private static void Write(BclXmlSerializer serializer, object subject, string encodingStyle)
        {
            var sb = new StringBuilder();
            using var writer = XmlWriter.Create(sb);
            serializer.Serialize(writer, subject, null, encodingStyle);
        }

        private static void Write(XmlSerializer serializer, object subject, string encodingStyle)
        {
            var sb = new StringBuilder();
            using var writer = XmlWriter.Create(sb);
            serializer.Serialize(writer, subject, null, encodingStyle);
        }

        private static object Read(BclXmlSerializer serializer, string xml, string encodingStyle)
        {
            using var reader = XmlReader.Create(new StringReader(xml));
            return serializer.Deserialize(reader, encodingStyle);
        }

        private static object Read(XmlSerializer serializer, string xml, string encodingStyle)
        {
            using var reader = XmlReader.Create(new StringReader(xml));
            return serializer.Deserialize(reader, encodingStyle);
        }

        /// <summary>
        /// Фабричный метод штатного сериализатора: без перекрытия он возвращал
        /// предгенерированные сериализаторы BCL, то есть подмена одной строкой
        /// теряла ускорение везде, где проект пользуется именно им.
        ///
        /// Тип <see cref="CompatFromTypesSubject"/> нигде больше не назван -
        /// значит проверяется и то, что генератор видит эту точку вызова.
        /// </summary>
        [Fact]
        public void FromTypes_ReturnsAcceleratedFacades_Test()
        {
            var serializers = XmlSerializer.FromTypes(
                new[] { typeof(CompatFromTypesSubject), typeof(UnacceleratedStruct), }
                );

            Assert.Equal(2, serializers.Length);

            var accelerated = Assert.IsType<XmlSerializer>(serializers[0]);
            Assert.True(accelerated.IsAccelerated, "тип назван в FromTypes, значит генератор обязан был его увидеть");

            //неподдержанный тип тоже приходит фасадом - просто без ускорения
            var fallback = Assert.IsType<XmlSerializer>(serializers[1]);
            Assert.False(fallback.IsAccelerated);

            var xml = accelerated.SerializeToString(new CompatFromTypesSubject { Number = 5, });
            var back = (CompatFromTypesSubject)accelerated.Deserialize(xml.AsSpan());
            Assert.Equal(5, back.Number);

            //документ обязан читаться штатным сериализатором
            Assert.Equal(
                5,
                ((CompatFromTypesSubject)new BclXmlSerializer(typeof(CompatFromTypesSubject))
                    .Deserialize(new StringReader(xml))).Number
                );

            //null - это пустой массив, а не исключение (снято с BCL)
            Assert.Empty(BclXmlSerializer.FromTypes(null));
            Assert.Empty(XmlSerializer.FromTypes(null));
        }

        /// <summary>
        /// То же для фабрики. Незарегистрированный тип она отдаёт <b>базовой</b>
        /// фабрике, а не заворачивает в фасад: фабрика существует ради кэша, и
        /// терять его там, где фасад всё равно ничем не поможет, незачем.
        /// </summary>
        [Fact]
        public void Factory_ReturnsAcceleratedFacade_Test()
        {
            var factory = new XmlSerializerFactory();

            var created = factory.CreateSerializer(typeof(CompatFactorySubject));

            var accelerated = Assert.IsType<XmlSerializer>(created);
            Assert.True(accelerated.IsAccelerated, "тип назван в CreateSerializer, значит генератор обязан был его увидеть");

            var xml = accelerated.SerializeToString(new CompatFactorySubject { Number = 6, });
            Assert.Equal(
                6,
                ((CompatFactorySubject)new BclXmlSerializer(typeof(CompatFactorySubject))
                    .Deserialize(new StringReader(xml))).Number
                );

            //незарегистрированный тип - штатный сериализатор от базовой фабрики
            var plain = factory.CreateSerializer(typeof(UnacceleratedStruct));
            Assert.IsNotType<XmlSerializer>(plain);

            var writer = new StringWriter();
            plain.Serialize(writer, new UnacceleratedStruct { Number = 7, Name = "plain", });
            Assert.Contains("plain", writer.ToString());

            //перегрузка с непустым добавочным аргументом ускорения не обещает,
            //но работать обязана
            var namespaced = factory.CreateSerializer(typeof(CompatFactorySubject), "urn:test");
            var namespacedWriter = new StringWriter();
            namespaced.Serialize(namespacedWriter, new CompatFactorySubject { Number = 8, });
            Assert.Contains("urn:test", namespacedWriter.ToString());

            //а пустой добавочный аргумент - это тот же простой случай
            Assert.IsType<XmlSerializer>(factory.CreateSerializer(typeof(CompatFactorySubject), (string)null));
            Assert.IsType<XmlSerializer>(factory.CreateSerializer(typeof(CompatFactorySubject), Type.EmptyTypes));

            //и наша фабрика остаётся штатной фабрикой - её можно отдать чужому коду
            Assert.IsAssignableFrom<BclXmlSerializerFactory>(factory);
        }

        /// <summary>
        /// То же требование к битому документу, что и в
        /// <see cref="XmlSerDe.Tests.MalformedInputFixture"/>, но через фасад.
        /// Здесь оно проверяется иначе: фасад заворачивает <b>любую</b> ошибку
        /// в <see cref="InvalidOperationException"/>, поэтому сам тип наружного
        /// исключения ничего не доказывает - смотреть надо на причину внутри.
        ///
        /// Путь тоже другой: пролог фасад срезает своим кодом
        /// (<c>XmlPrologue</c>), и обрезанное объявление до основного разбора
        /// вообще не доходит.
        /// </summary>
        [Fact]
        public void MalformedDocument_NeverReportsBoundsError_Test()
        {
            var serializer = new XmlSerializer(typeof(CompatSubject));
            Assert.True(serializer.IsAccelerated, "иначе тест проверяет BCL, а не наш разбор");

            var valid = serializer.SerializeToString(CreateSubject());

            for (var length = 0; length <= valid.Length; length++)
            {
                AssertNoBoundsError(serializer, valid.Substring(0, length));
            }

            foreach (var broken in new[]
                {
                    "<?xml version=\"1.0\"",
                    "<?xml version=\"1.0\" encoding=\"utf-8\"<CompatSubject />",
                    "<?xml",
                    "<?xml ",
                    "<CompatSubject a:b></CompatSubject>",
                    "<CompatSubject a=></CompatSubject>",
                    "<CompatSubject xmlns:p3></CompatSubject>",
                })
            {
                AssertNoBoundsError(serializer, broken);
            }
        }

        private static void AssertNoBoundsError(XmlSerializer serializer, string xml)
        {
            try
            {
                serializer.Deserialize(xml.AsSpan());
            }
            catch (Exception e)
            {
                var inner = e;
                while (inner is not null)
                {
                    Assert.False(
                        inner is IndexOutOfRangeException
                            || inner is ArgumentOutOfRangeException
                            || inner is NullReferenceException,
                        $"{inner.GetType().Name} на входе [{xml.Length}] \"{xml}\""
                        );

                    inner = inner.InnerException;
                }
            }
        }

        private static void AssertSameSubject(CompatSubject actual)
        {
            AssertSameSubjectValues(actual);
            Assert.Equal(new List<int> { 1, 2, 3 }, actual.Values);
        }

        private static void AssertSameSubjectValues(CompatSubject actual)
        {
            Assert.Equal(42, actual.Number);
            Assert.Equal("compat", actual.Name);
        }

        [Fact]
        public void Facade_ReadsOptInXmlWithoutUserAttribute_Test()
        {
            var serializer = new XmlSerializer(typeof(CompatSubject));
            Assert.True(serializer.IsAccelerated, "тип зарегистрирован, значит должен обслуживаться быстрым путём");

            var withCData = (CompatSubject)serializer.Deserialize(
                "<CompatSubject><Number>1</Number><Name><![CDATA[hello]]></Name></CompatSubject>".AsSpan()
                );
            Assert.Equal("hello", withCData.Name);

            var withComment = (CompatSubject)serializer.Deserialize(
                "<CompatSubject><!-- c --><Number>2</Number><Name>y</Name></CompatSubject>".AsSpan()
                );
            Assert.Equal(2, withComment.Number);
            Assert.Equal("y", withComment.Name);

            var withQuote = (CompatSubject)serializer.Deserialize(
                "<CompatSubject><Nope attr=\"1>2\"/><Number>3</Number><Name>z</Name></CompatSubject>".AsSpan()
                );
            Assert.Equal(3, withQuote.Number);

            var holder = new XmlSerializer(typeof(CompatHolder));
            Assert.True(holder.IsAccelerated);

            var p3 = (CompatHolder)holder.Deserialize(
                ("<CompatHolder><Polymorphic xmlns:p3=\"http://www.w3.org/2001/XMLSchema-instance\" p3:type=\"CompatDerived\"><BaseNumber>1</BaseNumber><DerivedName>d</DerivedName></Polymorphic><Child n='7'><Title>t</Title></Child></CompatHolder>").AsSpan()
                );
            var derived = Assert.IsType<CompatDerived>(p3.Polymorphic);
            Assert.Equal("d", derived.DerivedName);
            Assert.Equal(7, p3.Child.Number);
            Assert.Equal("t", p3.Child.Title);

            Assert.Throws<XmlSerDe.Common.XmlDocumentException>(
                () => XmlSerDe.Tests.XmlSerializerDeserializer2.Deserialize(
                    XmlSerDe.Components.Injector.DefaultInjector.Instance,
                    "<XmlObject2><IntProperty>1</IntProperty><StringProperty><![CDATA[x]]></StringProperty></XmlObject2>".AsSpan(),
                    out XmlSerDe.Tests.XmlObject2 _
                    )
                );
        }
    }
}
