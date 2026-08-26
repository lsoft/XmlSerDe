#nullable disable

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml.Serialization;
using XmlSerDe.Components.Exhauster;
using XmlSerDe.Components.Injector;
using XmlSerDe.Tests.Huge.Subject;
using Xunit;

namespace XmlSerDe.Tests.Huge
{
    /// <summary>
    /// Корректность HUGE-графа на маленьком документе. Сам 100-мегабайтный XML
    /// живёт только в performance-фикстуре: гонять его через <c>dotnet test</c>
    /// незачем, а форму всех веток покрывают 8 записей - по одной на шаблон.
    /// </summary>
    public class HugeFixture
    {
        public static readonly XmlSerializer SystemXmlSerializer = new XmlSerializer(
            typeof(HugeDocument),
            new[]
            {
                typeof(HugeCircle),
                typeof(HugeRectangle),
                typeof(HugeDerived)
            }
            );

        public const int CorrectnessRecordCount = 8;

        public static readonly HugeDocument SmallObject = HugeDocumentBuilder.Build(CorrectnessRecordCount);

        [MethodImpl(TestMethodImplOptions.AggressiveOptimization)]
        public static string Serialize_XmlSerDe(HugeDocument document)
        {
            var exhauster = new StringBuilderExhauster();
            HugeXmlSerializerDeserializer.Serialize(exhauster, document, false);
            return exhauster.ToString();
        }

        [MethodImpl(TestMethodImplOptions.AggressiveOptimization)]
        public static string Serialize_SystemXml(HugeDocument document)
        {
            using var ms = new MemoryStream();
            SystemXmlSerializer.Serialize(ms, document);
            return Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length);
        }

        [MethodImpl(TestMethodImplOptions.AggressiveOptimization)]
        public static HugeDocument Deserialize_SystemXml(string xml)
        {
            using (var reader = new StringReader(xml))
            {
                return (HugeDocument)SystemXmlSerializer.Deserialize(reader);
            }
        }

        [MethodImpl(TestMethodImplOptions.AggressiveOptimization)]
        public static HugeDocument Deserialize_XmlSerDe(ReadOnlySpan<char> xml)
        {
            HugeXmlSerializerDeserializer.Deserialize(DefaultInjector.Instance, xml, out HugeDocument result);
            return result;
        }

        [Fact]
        public void LengthEstimator_IsNotBelowSerializedLength()
        {
            var xmlLength = Serialize_XmlSerDe(SmallObject).Length;

            var accurate = new LengthEstimatorExhauster();
            HugeXmlSerializerDeserializer.Serialize(accurate, SmallObject, false);
            AssertEstimateAtLeast(accurate.EstimatedTotalLength, xmlLength, nameof(LengthEstimatorExhauster));
        }

        private static void AssertEstimateAtLeast(int estimated, int xmlLength, string name)
        {
            Assert.True(
                estimated >= xmlLength,
                name + " estimated " + estimated + " < actual " + xmlLength
                );
        }

        [Fact]
        public void Deserialize_XmlSerDe_MatchesTheBuiltObject()
        {
            var xml = Serialize_XmlSerDe(SmallObject);
            var deserialized = Deserialize_XmlSerDe(xml.AsSpan());

            HugeAssert.Equal(SmallObject, deserialized, skipDurations: false);
        }

        [Fact]
        public void Deserialize_XmlSerDe_MatchesSystemXml()
        {
            var xml = Serialize_XmlSerDe(SmallObject);

            HugeAssert.Equal(
                Deserialize_SystemXml(xml),
                Deserialize_XmlSerDe(xml.AsSpan()),
                skipDurations: SkipDurationsAgainstSystemXml,
                emptyListEqualsNull: true
                );
        }

        [Fact]
        public void Serialize_CrossRoundTrip_CheckForEquality()
        {
            var bySystemXml = Serialize_SystemXml(SmallObject);
            var byXmlSerDe = Serialize_XmlSerDe(SmallObject);

            HugeAssert.Equal(
                SmallObject,
                Deserialize_SystemXml(byXmlSerDe),
                skipDurations: SkipDurationsAgainstSystemXml,
                emptyListEqualsNull: true
                );
            HugeAssert.Equal(
                SmallObject,
                Deserialize_XmlSerDe(
                    global::XmlSerDe.Generator.Producer.BuiltinCodeHelper.CutXmlHead(
                        bySystemXml.AsSpan()
                        )
                    ),
                skipDurations: SkipDurationsAgainstSystemXml,
                emptyListEqualsNull: true
                );
        }

        /// <summary>
        /// На .NET Framework у <c>XmlSerializer</c> нет поддержки <see cref="TimeSpan"/>:
        /// член пишется пустым элементом и значение теряется. Сверять длительности
        /// с BCL имеет смысл только на Core.
        /// </summary>
        public static bool SkipDurationsAgainstSystemXml
        {
            get
            {
#if NETFRAMEWORK
                return true;
#else
                return false;
#endif
            }
        }
    }

    /// <summary>
    /// Снаружи фикстуры, чтобы performance-проект мог проверить то, что измеряет,
    /// не таща за собой xunit-факты.
    /// </summary>
    public static class HugeAssert
    {
        public static void Equal(
            HugeDocument expected,
            HugeDocument actual,
            bool skipDurations,
            bool emptyListEqualsNull = false)
        {
            Assert.NotNull(expected);
            Assert.NotNull(actual);
            Assert.Equal(expected.Id, actual.Id);
            Assert.Equal(expected.Title, actual.Title);
            Assert.Equal(expected.CreatedUtc, actual.CreatedUtc);
            Assert.Equal(expected.Records == null, actual.Records == null);
            if (expected.Records == null)
            {
                return;
            }

            Assert.Equal(expected.Records.Count, actual.Records.Count);
            for (var i = 0; i < expected.Records.Count; i++)
            {
                EqualRecord(
                    expected.Records[i],
                    actual.Records[i],
                    skipDurations,
                    emptyListEqualsNull,
                    "Records[" + i + "]"
                    );
            }
        }

        public static void EqualRecord(
            HugeRecord expected,
            HugeRecord actual,
            bool skipDurations,
            bool emptyListEqualsNull,
            string path)
        {
            Assert.NotNull(expected);
            Assert.NotNull(actual);
            Assert.True(expected.Index == actual.Index, path + ".Index");
            Assert.True(expected.Kind == actual.Kind, path + ".Kind");
            Assert.True(expected.EnumValue == actual.EnumValue, path + ".EnumValue");
            Assert.True(expected.RenamedKind == actual.RenamedKind, path + ".RenamedKind");
            Assert.True(expected.Score.Equals(actual.Score), path + ".Score");
            Assert.True(expected.Defaulted == actual.Defaulted, path + ".Defaulted");
            Assert.True(expected.FieldNumber == actual.FieldNumber, path + ".FieldNumber");
            Assert.True(expected.FieldLabel == actual.FieldLabel, path + ".FieldLabel");
            Assert.True(expected.UtcTime == actual.UtcTime, path + ".UtcTime");
            Assert.True(expected.LocalTime == actual.LocalTime, path + ".LocalTime");
            Assert.True(expected.UnspecifiedTime == actual.UnspecifiedTime, path + ".UnspecifiedTime");

            if (!skipDurations)
            {
                Assert.True(expected.Duration == actual.Duration, path + ".Duration");
                Assert.True(expected.OptionalDuration == actual.OptionalDuration, path + ".OptionalDuration");
            }

            var expectedOptional = expected.OptionalNumberSpecified ? expected.OptionalNumber : 0;
            Assert.True(expectedOptional == actual.OptionalNumber, path + ".OptionalNumber");

            EqualScalars(expected.Scalars, actual.Scalars, skipDurations, path + ".Scalars");
            EqualNullables(expected.Nullables, actual.Nullables, skipDurations, path + ".Nullables");
            EqualStrings(expected.Strings, actual.Strings, path + ".Strings");
            EqualTree(expected.Tree, actual.Tree, emptyListEqualsNull, path + ".Tree");
            EqualInts(expected.Numbers, actual.Numbers, emptyListEqualsNull, path + ".Numbers");
            EqualIntArray(expected.NumberArray, actual.NumberArray, path + ".NumberArray");
            EqualStrings(expected.Labels, actual.Labels, emptyListEqualsNull, path + ".Labels");
            EqualStringArray(expected.LabelArray, actual.LabelArray, path + ".LabelArray");
            EqualChildren(expected.Children, actual.Children, emptyListEqualsNull, path + ".Children");
            EqualChildArray(expected.ChildArray, actual.ChildArray, path + ".ChildArray");
            EqualInts(expected.EmptyOrNullList, actual.EmptyOrNullList, emptyListEqualsNull, path + ".EmptyOrNullList");
            EqualIntArray(expected.EmptyOrNullArray, actual.EmptyOrNullArray, path + ".EmptyOrNullArray");
            EqualShape(expected.Shape, actual.Shape, path + ".Shape");
            EqualShapes(expected.Shapes, actual.Shapes, emptyListEqualsNull, path + ".Shapes");
            EqualBase(expected.MaybeBase, actual.MaybeBase, path + ".MaybeBase");
            EqualDerived(expected.Derived, actual.Derived, path + ".Derived");
            EqualBytes(expected.Blob, actual.Blob, path + ".Blob");
            EqualBytes(expected.EmptyBlob, actual.EmptyBlob, path + ".EmptyBlob");
            EqualByteList(expected.ByteList, actual.ByteList, emptyListEqualsNull, path + ".ByteList");
            EqualBytes(expected.WrappedBytes, actual.WrappedBytes, path + ".WrappedBytes");
            EqualTextPayload(expected.TextPayload, actual.TextPayload, path + ".TextPayload");
            EqualOrdered(expected.Ordered, actual.Ordered, path + ".Ordered");
            EqualEmpty(expected.EmptyMarker, actual.EmptyMarker, path + ".EmptyMarker");
            EqualEnums(expected.Enums, actual.Enums, emptyListEqualsNull, path + ".Enums");
            EqualChild(expected.MissingChild, actual.MissingChild, path + ".MissingChild");
        }

        private static void EqualScalars(HugeScalars expected, HugeScalars actual, bool skipDurations, string path)
        {
            if (expected == null && actual == null)
            {
                return;
            }
            Assert.True(expected != null && actual != null, path);
            Assert.True(expected.BoolMember == actual.BoolMember, path + ".BoolMember");
            Assert.True(expected.SByteMember == actual.SByteMember, path + ".SByteMember");
            Assert.True(expected.ByteMember == actual.ByteMember, path + ".ByteMember");
            Assert.True(expected.ShortMember == actual.ShortMember, path + ".ShortMember");
            Assert.True(expected.UShortMember == actual.UShortMember, path + ".UShortMember");
            Assert.True(expected.IntMember == actual.IntMember, path + ".IntMember");
            Assert.True(expected.UIntMember == actual.UIntMember, path + ".UIntMember");
            Assert.True(expected.LongMember == actual.LongMember, path + ".LongMember");
            Assert.True(expected.ULongMember == actual.ULongMember, path + ".ULongMember");
            Assert.True(expected.DecimalMember == actual.DecimalMember, path + ".DecimalMember");
            Assert.True(expected.FloatMember.Equals(actual.FloatMember), path + ".FloatMember");
            Assert.True(expected.DoubleMember.Equals(actual.DoubleMember), path + ".DoubleMember");
            Assert.True(expected.CharMember == actual.CharMember, path + ".CharMember");
            Assert.True(expected.StringMember == actual.StringMember, path + ".StringMember");
            Assert.True(expected.GuidMember == actual.GuidMember, path + ".GuidMember");
            Assert.True(expected.DateTimeMember == actual.DateTimeMember, path + ".DateTimeMember");
            if (!skipDurations)
            {
                Assert.True(expected.TimeSpanMember == actual.TimeSpanMember, path + ".TimeSpanMember");
            }
        }

        private static void EqualNullables(HugeNullables expected, HugeNullables actual, bool skipDurations, string path)
        {
            if (expected == null && actual == null)
            {
                return;
            }
            Assert.True(expected != null && actual != null, path);
            Assert.True(expected.BoolMember == actual.BoolMember, path + ".BoolMember");
            Assert.True(expected.SByteMember == actual.SByteMember, path + ".SByteMember");
            Assert.True(expected.ByteMember == actual.ByteMember, path + ".ByteMember");
            Assert.True(expected.ShortMember == actual.ShortMember, path + ".ShortMember");
            Assert.True(expected.UShortMember == actual.UShortMember, path + ".UShortMember");
            Assert.True(expected.IntMember == actual.IntMember, path + ".IntMember");
            Assert.True(expected.UIntMember == actual.UIntMember, path + ".UIntMember");
            Assert.True(expected.LongMember == actual.LongMember, path + ".LongMember");
            Assert.True(expected.ULongMember == actual.ULongMember, path + ".ULongMember");
            Assert.True(expected.DecimalMember == actual.DecimalMember, path + ".DecimalMember");
            Assert.True(NullableEquals(expected.FloatMember, actual.FloatMember), path + ".FloatMember");
            Assert.True(NullableEquals(expected.DoubleMember, actual.DoubleMember), path + ".DoubleMember");
            Assert.True(expected.CharMember == actual.CharMember, path + ".CharMember");
            Assert.True(expected.GuidMember == actual.GuidMember, path + ".GuidMember");
            Assert.True(expected.DateTimeMember == actual.DateTimeMember, path + ".DateTimeMember");
            if (!skipDurations)
            {
                Assert.True(expected.TimeSpanMember == actual.TimeSpanMember, path + ".TimeSpanMember");
            }
        }

        private static bool NullableEquals(float? left, float? right)
        {
            if (left.HasValue != right.HasValue)
            {
                return false;
            }
            if (!left.HasValue)
            {
                return true;
            }
            return left.Value.Equals(right.Value);
        }

        private static bool NullableEquals(double? left, double? right)
        {
            if (left.HasValue != right.HasValue)
            {
                return false;
            }
            if (!left.HasValue)
            {
                return true;
            }
            return left.Value.Equals(right.Value);
        }

        private static void EqualStrings(HugeStrings expected, HugeStrings actual, string path)
        {
            if (expected == null && actual == null)
            {
                return;
            }
            Assert.True(expected != null && actual != null, path);
            Assert.True(SameText(expected.Ordinary, actual.Ordinary), path + ".Ordinary");
            Assert.True(SameText(expected.Empty, actual.Empty), path + ".Empty");
            Assert.True(SameText(expected.Null, actual.Null), path + ".Null");
            Assert.True(SameText(expected.NeedsEscaping, actual.NeedsEscaping), path + ".NeedsEscaping");
            Assert.True(SameText(expected.LeadingAndTrailingSpaces, actual.LeadingAndTrailingSpaces), path + ".LeadingAndTrailingSpaces");
            Assert.True(SameText(expected.WithNewLines, actual.WithNewLines), path + ".WithNewLines");
            Assert.True(SameText(expected.LongText, actual.LongText), path + ".LongText");
        }

        /// <summary>
        /// XML 1.0 §2.11: разборщик обязан свернуть <c>CR LF</c> и одиночный <c>CR</c>
        /// в <c>LF</c>. XmlSerDe читает байты как есть, BCL на Windows пишет <c>CR LF</c>,
        /// поэтому в сверке с его выводом перевод строки нормализуется.
        /// </summary>
        private static bool SameText(string expected, string actual)
        {
            if (expected == actual)
            {
                return true;
            }
            if (expected is null || actual is null)
            {
                return false;
            }
            return NormalizeNewlines(expected) == NormalizeNewlines(actual);
        }

        private static string NormalizeNewlines(string value)
        {
            return value.Replace("\r\n", "\n").Replace('\r', '\n');
        }

        private static bool ListAbsent<T>(List<T> expected, List<T> actual, bool emptyListEqualsNull)
        {
            if (expected == null && actual == null)
            {
                return true;
            }
            if (!emptyListEqualsNull)
            {
                return false;
            }
            return (expected == null || expected.Count == 0)
                && (actual == null || actual.Count == 0);
        }

        private static void EqualTree(HugeNode expected, HugeNode actual, bool emptyListEqualsNull, string path)
        {
            if (expected == null && actual == null)
            {
                return;
            }
            Assert.True(expected != null && actual != null, path);
            Assert.True(expected.Depth == actual.Depth, path + ".Depth");
            Assert.True(expected.Label == actual.Label, path + ".Label");
            Assert.True(expected.Payload == actual.Payload, path + ".Payload");
            EqualTree(expected.Child, actual.Child, emptyListEqualsNull, path + ".Child");
            if (ListAbsent(expected.Branches, actual.Branches, emptyListEqualsNull))
            {
                return;
            }
            Assert.True(expected.Branches != null && actual.Branches != null, path + ".Branches");
            Assert.True(expected.Branches.Count == actual.Branches.Count, path + ".Branches.Count");
            for (var i = 0; i < expected.Branches.Count; i++)
            {
                EqualTree(expected.Branches[i], actual.Branches[i], emptyListEqualsNull, path + ".Branches[" + i + "]");
            }
        }

        private static void EqualInts(List<int> expected, List<int> actual, bool emptyListEqualsNull, string path)
        {
            if (ListAbsent(expected, actual, emptyListEqualsNull))
            {
                return;
            }
            Assert.True(expected != null && actual != null, path);
            Assert.True(expected.Count == actual.Count, path + ".Count");
            for (var i = 0; i < expected.Count; i++)
            {
                Assert.True(expected[i] == actual[i], path + "[" + i + "]");
            }
        }

        private static void EqualIntArray(int[] expected, int[] actual, string path)
        {
            if (expected == null && actual == null)
            {
                return;
            }
            Assert.True(expected != null && actual != null, path);
            Assert.True(expected.Length == actual.Length, path + ".Length");
            for (var i = 0; i < expected.Length; i++)
            {
                Assert.True(expected[i] == actual[i], path + "[" + i + "]");
            }
        }

        private static void EqualStrings(List<string> expected, List<string> actual, bool emptyListEqualsNull, string path)
        {
            if (ListAbsent(expected, actual, emptyListEqualsNull))
            {
                return;
            }
            Assert.True(expected != null && actual != null, path);
            Assert.True(expected.Count == actual.Count, path + ".Count");
            for (var i = 0; i < expected.Count; i++)
            {
                Assert.True(expected[i] == actual[i], path + "[" + i + "]");
            }
        }

        private static void EqualStringArray(string[] expected, string[] actual, string path)
        {
            if (expected == null && actual == null)
            {
                return;
            }
            Assert.True(expected != null && actual != null, path);
            Assert.True(expected.Length == actual.Length, path + ".Length");
            for (var i = 0; i < expected.Length; i++)
            {
                Assert.True(expected[i] == actual[i], path + "[" + i + "]");
            }
        }

        private static void EqualChildren(List<HugeChild> expected, List<HugeChild> actual, bool emptyListEqualsNull, string path)
        {
            if (ListAbsent(expected, actual, emptyListEqualsNull))
            {
                return;
            }
            Assert.True(expected != null && actual != null, path);
            Assert.True(expected.Count == actual.Count, path + ".Count");
            for (var i = 0; i < expected.Count; i++)
            {
                EqualChild(expected[i], actual[i], path + "[" + i + "]");
            }
        }

        private static void EqualChildArray(HugeChild[] expected, HugeChild[] actual, string path)
        {
            if (expected == null && actual == null)
            {
                return;
            }
            Assert.True(expected != null && actual != null, path);
            Assert.True(expected.Length == actual.Length, path + ".Length");
            for (var i = 0; i < expected.Length; i++)
            {
                EqualChild(expected[i], actual[i], path + "[" + i + "]");
            }
        }

        private static void EqualChild(HugeChild expected, HugeChild actual, string path)
        {
            if (expected == null && actual == null)
            {
                return;
            }
            Assert.True(expected != null && actual != null, path);
            Assert.True(expected.Id == actual.Id, path + ".Id");
            Assert.True(expected.Name == actual.Name, path + ".Name");
            Assert.True(expected.Number == actual.Number, path + ".Number");
            EqualChild(expected.Nested, actual.Nested, path + ".Nested");
        }

        private static void EqualShape(HugeShape expected, HugeShape actual, string path)
        {
            if (expected == null && actual == null)
            {
                return;
            }
            Assert.True(expected != null && actual != null, path);
            Assert.True(expected.GetType() == actual.GetType(), path + ".Type");
            Assert.True(expected.Color == actual.Color, path + ".Color");
            if (expected is HugeCircle expectedCircle)
            {
                Assert.True(expectedCircle.Radius == ((HugeCircle)actual).Radius, path + ".Radius");
            }
            else if (expected is HugeRectangle expectedRectangle)
            {
                var actualRectangle = (HugeRectangle)actual;
                Assert.True(expectedRectangle.Width == actualRectangle.Width, path + ".Width");
                Assert.True(expectedRectangle.Height == actualRectangle.Height, path + ".Height");
            }
        }

        private static void EqualShapes(List<HugeShape> expected, List<HugeShape> actual, bool emptyListEqualsNull, string path)
        {
            if (ListAbsent(expected, actual, emptyListEqualsNull))
            {
                return;
            }
            Assert.True(expected != null && actual != null, path);
            Assert.True(expected.Count == actual.Count, path + ".Count");
            for (var i = 0; i < expected.Count; i++)
            {
                EqualShape(expected[i], actual[i], path + "[" + i + "]");
            }
        }

        private static void EqualBase(HugeBase expected, HugeBase actual, string path)
        {
            if (expected == null && actual == null)
            {
                return;
            }
            Assert.True(expected != null && actual != null, path);
            Assert.True(expected.GetType() == actual.GetType(), path + ".Type");
            Assert.True(expected.BaseNumber == actual.BaseNumber, path + ".BaseNumber");
            Assert.True(expected.BaseName == actual.BaseName, path + ".BaseName");
            if (expected is HugeDerived expectedDerived)
            {
                EqualDerived(expectedDerived, (HugeDerived)actual, path);
            }
        }

        private static void EqualDerived(HugeDerived expected, HugeDerived actual, string path)
        {
            if (expected == null && actual == null)
            {
                return;
            }
            Assert.True(expected != null && actual != null, path);
            Assert.True(expected.BaseNumber == actual.BaseNumber, path + ".BaseNumber");
            Assert.True(expected.BaseName == actual.BaseName, path + ".BaseName");
            Assert.True(expected.DerivedNumber == actual.DerivedNumber, path + ".DerivedNumber");
            Assert.True(expected.DerivedName == actual.DerivedName, path + ".DerivedName");
        }

        private static void EqualBytes(byte[] expected, byte[] actual, string path)
        {
            if (expected == null && actual == null)
            {
                return;
            }
            Assert.True(expected != null && actual != null, path);
            Assert.True(expected.Length == actual.Length, path + ".Length");
            for (var i = 0; i < expected.Length; i++)
            {
                Assert.True(expected[i] == actual[i], path + "[" + i + "]");
            }
        }

        private static void EqualByteList(List<byte> expected, List<byte> actual, bool emptyListEqualsNull, string path)
        {
            if (ListAbsent(expected, actual, emptyListEqualsNull))
            {
                return;
            }
            Assert.True(expected != null && actual != null, path);
            Assert.True(expected.Count == actual.Count, path + ".Count");
            for (var i = 0; i < expected.Count; i++)
            {
                Assert.True(expected[i] == actual[i], path + "[" + i + "]");
            }
        }

        private static void EqualTextPayload(HugeTextPayload expected, HugeTextPayload actual, string path)
        {
            if (expected == null && actual == null)
            {
                return;
            }
            Assert.True(expected != null && actual != null, path);
            Assert.True(SameText(expected.Attribute, actual.Attribute), path + ".Attribute");
            Assert.True(SameText(expected.Text, actual.Text), path + ".Text");
        }

        private static void EqualOrdered(HugeOrdered expected, HugeOrdered actual, string path)
        {
            if (expected == null && actual == null)
            {
                return;
            }
            Assert.True(expected != null && actual != null, path);
            Assert.True(expected.First == actual.First, path + ".First");
            Assert.True(expected.Second == actual.Second, path + ".Second");
        }

        private static void EqualEmpty(HugeEmpty expected, HugeEmpty actual, string path)
        {
            Assert.True((expected == null) == (actual == null), path);
        }

        private static void EqualEnums(List<HugeKind> expected, List<HugeKind> actual, bool emptyListEqualsNull, string path)
        {
            if (ListAbsent(expected, actual, emptyListEqualsNull))
            {
                return;
            }
            Assert.True(expected != null && actual != null, path);
            Assert.True(expected.Count == actual.Count, path + ".Count");
            for (var i = 0; i < expected.Count; i++)
            {
                Assert.True(expected[i] == actual[i], path + "[" + i + "]");
            }
        }
    }
}
