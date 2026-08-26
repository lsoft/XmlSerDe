#nullable disable

using System;
using System.Collections.Generic;
using XmlSerDe.Tests.Huge.Subject;

namespace XmlSerDe.Tests.Huge
{
    /// <summary>
    /// Собирает объектный граф HUGE-документа. XML из него не строит: сериализация
    /// - отдельный шаг, и в бенчмарке он должен произойти до измеряемого метода,
    /// а не внутри него.
    /// </summary>
    public static class HugeDocumentBuilder
    {
        public const int TargetXmlLength = 100 * 1024 * 1024;

        public static readonly Guid DocumentId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

        public static readonly DateTime CreatedUtc =
            new DateTime(2024, 6, 15, 14, 30, 45, DateTimeKind.Utc).AddTicks(1234567);

        private static readonly Guid SampleGuid = Guid.Parse("12345678-1234-5678-9abc-def012345678");

        private static readonly DateTime UtcSample =
            new DateTime(2024, 6, 15, 14, 30, 45, DateTimeKind.Utc).AddTicks(1234567);

        private static readonly DateTime LocalSample =
            new DateTime(2024, 6, 15, 14, 30, 45, DateTimeKind.Local).AddTicks(1234567);

        private static readonly DateTime UnspecifiedSample =
            new DateTime(2024, 6, 15, 14, 30, 45, DateTimeKind.Unspecified).AddTicks(1234567);

        private static readonly byte[] SampleBlob = new byte[] { 1, 2, 250, 3, 4, 5, 6, 7 };

        private static readonly string[] LongTexts = BuildLongTexts();

        public static HugeDocument Build(int recordCount)
        {
            if (recordCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(recordCount));
            }

            var records = new List<HugeRecord>(recordCount);
            for (var i = 0; i < recordCount; i++)
            {
                records.Add(CreateRecord(i));
            }

            return new HugeDocument
            {
                Id = DocumentId,
                Title = "huge-document",
                CreatedUtc = CreatedUtc,
                Records = records
            };
        }

        /// <summary>
        /// Подбирает число записей так, чтобы сериализованный XML оказался около
        /// <paramref name="targetCharCount"/> символов. Зонд из 32 записей покрывает
        /// все восемь шаблонов заполнения, поэтому средняя длина репрезентативна.
        /// </summary>
        public static HugeDocument BuildForTargetLength(
            int targetCharCount,
            Func<HugeDocument, string> serialize)
        {
            if (targetCharCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(targetCharCount));
            }
            if (serialize is null)
            {
                throw new ArgumentNullException(nameof(serialize));
            }

            const int ProbeCount = 32;
            var probeXml = serialize(Build(ProbeCount));
            var average = Math.Max(1, probeXml.Length / ProbeCount);
            var count = Math.Max(ProbeCount, (targetCharCount + average - 1) / average);
            return Build(count);
        }

        public static HugeRecord CreateRecord(int index)
        {
            var pattern = index % 8;
            var record = new HugeRecord
            {
                Index = index,
                Kind = (HugeKind)(index % 4),
                EnumValue = (HugeKind)(index % 4),
                RenamedKind = (index % 2) == 0 ? HugeRenamedKind.Zero : HugeRenamedKind.One,
                Scalars = CreateScalars(index, pattern),
                Nullables = CreateNullables(pattern),
                Strings = CreateStrings(index, pattern),
                Tree = CreateTree(index),
                UtcTime = UtcSample,
                LocalTime = LocalSample,
                UnspecifiedTime = UnspecifiedSample,
                FieldNumber = index,
                FieldLabel = "field-" + index,
                Ignored = 123,
                Score = pattern == 3 ? double.NaN : index + 0.5,
                Defaulted = pattern == 0 ? 0 : 7,
                Ordered = new HugeOrdered { First = index, Second = -index }
            };

            FillCollections(record, index, pattern);
            FillPolymorphism(record, index, pattern);
            FillBinary(record, pattern);
            FillDurations(record, pattern);
            FillOptionalAndNested(record, index, pattern);

            return record;
        }

        private static HugeScalars CreateScalars(int index, int pattern)
        {
            if (pattern == 4)
            {
                return new HugeScalars
                {
                    BoolMember = false,
                    SByteMember = sbyte.MinValue,
                    ByteMember = byte.MaxValue,
                    ShortMember = short.MinValue,
                    UShortMember = ushort.MaxValue,
                    IntMember = int.MinValue,
                    UIntMember = uint.MaxValue,
                    LongMember = long.MinValue,
                    ULongMember = ulong.MaxValue,
                    DecimalMember = 1234567890123456789012345.67m,
                    FloatMember = float.Epsilon,
                    DoubleMember = 1.0 / 3.0,
                    CharMember = 'я',
                    StringMember = "extremes-" + index,
                    GuidMember = SampleGuid,
                    DateTimeMember = UtcSample,
                    TimeSpanMember = TimeSpan.FromDays(-1)
                };
            }

            if (pattern == 3)
            {
                return new HugeScalars
                {
                    BoolMember = true,
                    SByteMember = -42,
                    ByteMember = 200,
                    ShortMember = -1234,
                    UShortMember = 65000,
                    IntMember = -987654,
                    UIntMember = 4000000000,
                    LongMember = -922337203685477580,
                    ULongMember = 18446744073709551615,
                    DecimalMember = 99.99m,
                    FloatMember = float.NaN,
                    DoubleMember = double.PositiveInfinity,
                    CharMember = 'A',
                    StringMember = "specials-" + index,
                    GuidMember = SampleGuid,
                    DateTimeMember = UnspecifiedSample,
                    TimeSpanMember = TimeSpan.Zero
                };
            }

            return new HugeScalars
            {
                BoolMember = (index % 2) == 0,
                SByteMember = (sbyte)(index % 100 - 50),
                ByteMember = (byte)(index % 256),
                ShortMember = (short)index,
                UShortMember = (ushort)index,
                IntMember = index,
                UIntMember = (uint)index,
                LongMember = index,
                ULongMember = (ulong)index,
                DecimalMember = index + 0.25m,
                FloatMember = index + 1.5f,
                DoubleMember = pattern == 5 ? double.NegativeInfinity : index + 0.125,
                CharMember = (char)('A' + (index % 26)),
                StringMember = "scalar-" + index,
                GuidMember = SampleGuid,
                DateTimeMember = UtcSample,
                TimeSpanMember = new TimeSpan(1, 2, 3, 4, 5)
            };
        }

        private static HugeNullables CreateNullables(int pattern)
        {
            if (pattern == 1)
            {
                return new HugeNullables();
            }

            var filled = new HugeNullables
            {
                BoolMember = false,
                SByteMember = -7,
                ByteMember = 9,
                ShortMember = -11,
                UShortMember = 13,
                IntMember = -7,
                UIntMember = 15,
                LongMember = -17,
                ULongMember = 19,
                DecimalMember = 99.99m,
                FloatMember = 2.25f,
                DoubleMember = 2.25,
                CharMember = 'Q',
                GuidMember = SampleGuid,
                DateTimeMember = UtcSample,
                TimeSpanMember = TimeSpan.FromHours(3)
            };

            if (pattern == 2)
            {
                filled.FloatMember = null;
                filled.DoubleMember = null;
                filled.TimeSpanMember = null;
                filled.DateTimeMember = null;
                filled.GuidMember = null;
            }

            return filled;
        }

        private static HugeStrings CreateStrings(int index, int pattern)
        {
            if (pattern == 1)
            {
                return new HugeStrings
                {
                    Ordinary = null,
                    Empty = null,
                    Null = null,
                    NeedsEscaping = null,
                    LeadingAndTrailingSpaces = null,
                    WithNewLines = null,
                    LongText = null
                };
            }

            if (pattern == 2)
            {
                return new HugeStrings
                {
                    Ordinary = "",
                    Empty = "",
                    Null = null,
                    NeedsEscaping = "",
                    LeadingAndTrailingSpaces = "",
                    WithNewLines = "",
                    LongText = ""
                };
            }

            return new HugeStrings
            {
                Ordinary = "ordinary-" + index,
                Empty = "",
                Null = pattern == 6 ? null : "present-" + index,
                NeedsEscaping = "a<b>c&d\"e'f",
                LeadingAndTrailingSpaces = "  padded  ",
                WithNewLines = "line1\nline2",
                LongText = LongTexts[index % LongTexts.Length]
            };
        }

        private static HugeNode CreateTree(int index)
        {
            var depth = 1 + (index % 10);
            return CreateChain(depth, index, 0);
        }

        private static HugeNode CreateChain(int remaining, int index, int level)
        {
            var node = new HugeNode
            {
                Depth = level,
                Label = "L" + level + "-" + index
            };

            if (remaining <= 1)
            {
                node.Payload = "leaf-" + index;
                return node;
            }

            node.Child = CreateChain(remaining - 1, index, level + 1);

            if (level == remaining / 2 && (index % 3) == 0)
            {
                node.Branches = new List<HugeNode>
                {
                    CreateChain(Math.Min(3, remaining - 1), index, level + 1),
                    new HugeNode
                    {
                        Depth = level + 1,
                        Label = "branch-" + index,
                        Payload = "side-" + index
                    }
                };
            }

            return node;
        }

        private static void FillCollections(HugeRecord record, int index, int pattern)
        {
            if (pattern == 1)
            {
                record.Numbers = null;
                record.NumberArray = null;
                record.Labels = null;
                record.LabelArray = null;
                record.Children = null;
                record.ChildArray = null;
                record.EmptyOrNullList = null;
                record.EmptyOrNullArray = null;
                record.Enums = null;
                return;
            }

            if (pattern == 2)
            {
                record.Numbers = new List<int>();
                record.NumberArray = Array.Empty<int>();
                record.Labels = new List<string>();
                record.LabelArray = Array.Empty<string>();
                record.Children = new List<HugeChild>();
                record.ChildArray = Array.Empty<HugeChild>();
                record.EmptyOrNullList = new List<int>();
                record.EmptyOrNullArray = Array.Empty<int>();
                record.Enums = new List<HugeKind>();
                return;
            }

            record.Numbers = new List<int> { index, index + 1, index + 2 };
            record.NumberArray = new[] { index, -index, 42 };
            record.Labels = new List<string> { "a-" + index, "b-" + index };
            record.LabelArray = new[] { "x-" + index, "y-" + index };
            record.Children = new List<HugeChild>
            {
                CreateChild(index, 0, nested: true),
                CreateChild(index, 1, nested: false)
            };
            record.ChildArray = new[]
            {
                CreateChild(index, 2, nested: false)
            };
            record.EmptyOrNullList = (index % 2) == 0 ? new List<int>() : new List<int> { 1 };
            record.EmptyOrNullArray = (index % 2) == 0 ? Array.Empty<int>() : new[] { 2 };
            record.Enums = new List<HugeKind> { HugeKind.Alpha, HugeKind.Gamma };
        }

        private static HugeChild CreateChild(int index, int slot, bool nested)
        {
            return new HugeChild
            {
                Id = index * 10 + slot,
                Name = "child-" + index + "-" + slot,
                Number = slot,
                Nested = nested
                    ? new HugeChild
                    {
                        Id = index * 10 + slot + 50,
                        Name = "nested-" + index,
                        Number = slot + 10
                    }
                    : null
            };
        }

        private static void FillPolymorphism(HugeRecord record, int index, int pattern)
        {
            if (pattern == 1)
            {
                record.Shape = null;
                record.Shapes = null;
                record.MaybeBase = null;
                record.Derived = null;
                return;
            }

            if ((index % 2) == 0)
            {
                record.Shape = new HugeCircle { Color = "red", Radius = 1.5 + index };
            }
            else
            {
                record.Shape = new HugeRectangle { Color = "blue", Width = 2, Height = 3 + index };
            }

            record.Shapes = new List<HugeShape>
            {
                new HugeCircle { Color = "green", Radius = 0.5 },
                new HugeRectangle { Color = "yellow", Width = 4, Height = 5 }
            };

            if (pattern == 6)
            {
                record.MaybeBase = new HugeBase { BaseNumber = index, BaseName = "base-" + index };
            }
            else
            {
                record.MaybeBase = new HugeDerived
                {
                    BaseNumber = index,
                    BaseName = "base-" + index,
                    DerivedNumber = index * 2,
                    DerivedName = "derived-" + index
                };
            }

            record.Derived = new HugeDerived
            {
                BaseNumber = 100 + index,
                BaseName = "own-base-" + index,
                DerivedNumber = 200 + index,
                DerivedName = "own-derived-" + index
            };
        }

        private static void FillBinary(HugeRecord record, int pattern)
        {
            if (pattern == 1)
            {
                record.Blob = null;
                record.EmptyBlob = null;
                record.ByteList = null;
                record.WrappedBytes = null;
                return;
            }

            if (pattern == 2)
            {
                record.Blob = Array.Empty<byte>();
                record.EmptyBlob = Array.Empty<byte>();
                record.ByteList = new List<byte>();
                record.WrappedBytes = Array.Empty<byte>();
                return;
            }

            record.Blob = SampleBlob;
            record.EmptyBlob = Array.Empty<byte>();
            record.ByteList = new List<byte> { 1, 2, 3 };
            record.WrappedBytes = new byte[] { 9, 8, 7 };
        }

        private static void FillDurations(HugeRecord record, int pattern)
        {
            if (pattern == 1)
            {
                record.Duration = TimeSpan.Zero;
                record.OptionalDuration = null;
                return;
            }

            if (pattern == 4)
            {
                record.Duration = TimeSpan.FromDays(-1);
                record.OptionalDuration = TimeSpan.FromHours(3);
                return;
            }

            record.Duration = new TimeSpan(1, 2, 3, 4, 5);
            record.OptionalDuration = pattern == 2 ? (TimeSpan?)null : TimeSpan.FromMinutes(9);
        }

        private static void FillOptionalAndNested(HugeRecord record, int index, int pattern)
        {
            record.OptionalNumberSpecified = pattern != 1;
            record.OptionalNumber = pattern == 1 ? 999 : 40 + index;
            record.MissingChild = pattern == 1 || pattern == 2
                ? null
                : CreateChild(index, 9, nested: false);
            record.EmptyMarker = pattern == 1 ? null : new HugeEmpty();
            record.TextPayload = pattern == 1
                ? null
                : new HugeTextPayload
                {
                    Attribute = "attr-" + index,
                    Text = pattern == 3 ? "a<b>&c" : "text-" + index
                };
        }

        private static string[] BuildLongTexts()
        {
            var chunk = "lorem ipsum dolor sit amet <not-a-tag> & more; ";
            var texts = new string[6];
            for (var i = 0; i < texts.Length; i++)
            {
                var repeats = 8 + i * 4;
                var buffer = new char[chunk.Length * repeats];
                for (var r = 0; r < repeats; r++)
                {
                    chunk.CopyTo(0, buffer, r * chunk.Length, chunk.Length);
                }
                texts[i] = new string(buffer);
            }

            return texts;
        }
    }
}
