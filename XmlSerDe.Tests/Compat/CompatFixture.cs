#nullable disable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using Xunit;
using BclXmlSerializer = System.Xml.Serialization.XmlSerializer;
using XmlSerializer = XmlSerDe.Compat.XmlSerializer;

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

        [Fact]
        public void CanDeserialize_AnswersLikeSystemXml_Test()
        {
            var serializer = new XmlSerializer(typeof(CompatSubject));

            using (var matching = XmlReader.Create(new StringReader("<CompatSubject />")))
            {
                Assert.True(serializer.CanDeserialize(matching));
            }
            using (var foreign = XmlReader.Create(new StringReader("<Alien />")))
            {
                Assert.False(serializer.CanDeserialize(foreign));
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
    }
}
