#nullable disable

using System;
using System.Collections.Generic;
using XmlSerDe.Components.Exhauster;
using XmlSerDe.Components.Injector;
using XmlSerDe.Tests.Interop.Subject;

namespace XmlSerDe.Tests.Interop
{
    /// <summary>
    /// Одна форма POCO под сверкой.
    /// </summary>
    public sealed class InteropCase
    {
        public InteropCase(string name, Func<InteropResult> run)
        {
            Name = name;
            Run = run;
        }

        public string Name { get; }

        public Func<InteropResult> Run { get; }
    }

    /// <summary>
    /// Корпус форм для дифференциальной сверки. Каждая форма заводится один раз здесь
    /// и используется и отчётом, и отдельным тестом на неё.
    /// </summary>
    public static class InteropCorpus
    {
        private static string Write<T>(T obj, Action<DefaultStringBuilderExhauster, T> serialize)
        {
            var exhauster = new DefaultStringBuilderExhauster();
            serialize(exhauster, obj);
            return exhauster.ToString();
        }

        private static InteropResult Check<T>(
            T obj,
            Action<DefaultStringBuilderExhauster, T> serialize,
            XmlSerDeReader<T> deserialize
            )
        {
            return InteropRunner.Verify(
                obj,
                o => Write(o, serialize),
                deserialize
                );
        }

        public static InteropResult Scalars() => Check(
            new ScalarsSubject
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
                DecimalMember = 1234567890123456789012345.67m,
                StringMember = "ordinary",
                GuidMember = Guid.Parse("12345678-1234-5678-9abc-def012345678"),
                DateTimeMember = new DateTime(2024, 6, 15, 14, 30, 45, DateTimeKind.Utc).AddTicks(1234567),
            },
            (e, o) => InteropSerializer.Serialize(e, o, false),
            (ReadOnlySpan<char> xml, out ScalarsSubject r) => InteropSerializer.Deserialize(DefaultInjector.Instance, xml, out r));

        public static InteropResult TrickyScalars() => Check(
            new TrickyScalarsSubject
            {
                FloatMember = 1.5f,
                DoubleMember = 1e300,
                //1/3 не представимо двоично: если запись потеряет хоть одну цифру,
                //обратное чтение даст другое число, и это будет видно
                ThirdMember = 1.0 / 3.0,
                NaNMember = float.NaN,
                PositiveInfinityMember = double.PositiveInfinity,
                NegativeInfinityMember = double.NegativeInfinity,
                CharMember = 'A',
                NonAsciiCharMember = 'я',
                FilledNullableDouble = 2.25,
                EmptyNullableDouble = null,
            },
            (e, o) => InteropSerializer.Serialize(e, o, false),
            (ReadOnlySpan<char> xml, out TrickyScalarsSubject r) => InteropSerializer.Deserialize(DefaultInjector.Instance, xml, out r));

        public static InteropResult Durations() => Check(
            new DurationSubject
            {
                Ordinary = new TimeSpan(1, 2, 3, 4, 5),
                Zero = TimeSpan.Zero,
                Negative = TimeSpan.FromDays(-1),
                Filled = TimeSpan.FromHours(3),
                Empty = null,
            },
            (e, o) => InteropSerializer.Serialize(e, o, false),
            (ReadOnlySpan<char> xml, out DurationSubject r) => InteropSerializer.Deserialize(DefaultInjector.Instance, xml, out r));

        public static InteropResult Nullables() => Check(
            new NullableSubject
            {
                FilledInt = -7,
                FilledBool = false,
                FilledDecimal = 99.99m,
                FilledGuid = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
                FilledDateTime = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            },
            (e, o) => InteropSerializer.Serialize(e, o, false),
            (ReadOnlySpan<char> xml, out NullableSubject r) => InteropSerializer.Deserialize(DefaultInjector.Instance, xml, out r));

        public static InteropResult Strings() => Check(
            new StringsSubject
            {
                Ordinary = "ordinary",
                Empty = "",
                Null = null,
                NeedsEscaping = "a<b>c&d\"e'f",
                LeadingAndTrailingSpaces = "  padded  ",
                WithNewLines = "line1\nline2",
            },
            (e, o) => InteropSerializer.Serialize(e, o, false),
            (ReadOnlySpan<char> xml, out StringsSubject r) => InteropSerializer.Deserialize(DefaultInjector.Instance, xml, out r));

        public static InteropResult DateTimeKinds() => Check(
            new DateTimeKindsSubject
            {
                Utc = new DateTime(2024, 6, 15, 14, 30, 45, DateTimeKind.Utc).AddTicks(1234567),
                Local = new DateTime(2024, 6, 15, 14, 30, 45, DateTimeKind.Local).AddTicks(1234567),
                Unspecified = new DateTime(2024, 6, 15, 14, 30, 45, DateTimeKind.Unspecified).AddTicks(1234567),
                WholeSecond = new DateTime(2024, 6, 15, 14, 30, 45, DateTimeKind.Utc),
            },
            (e, o) => InteropSerializer.Serialize(e, o, false),
            (ReadOnlySpan<char> xml, out DateTimeKindsSubject r) => InteropSerializer.Deserialize(DefaultInjector.Instance, xml, out r));

        public static InteropResult Fields() => Check(
            new FieldsSubject
            {
                IntField = 11,
                StringField = "field",
                IntProperty = 22,
            },
            (e, o) => InteropSerializer.Serialize(e, o, false),
            (ReadOnlySpan<char> xml, out FieldsSubject r) => InteropSerializer.Deserialize(DefaultInjector.Instance, xml, out r));

        public static InteropResult Empty() => Check(
            new EmptySubject(),
            (e, o) => InteropSerializer.Serialize(e, o, false),
            (ReadOnlySpan<char> xml, out EmptySubject r) => InteropSerializer.Deserialize(DefaultInjector.Instance, xml, out r));

        public static InteropResult Nested() => Check(
            new NestedSubject
            {
                Filled = new ChildSubject { Number = 1, Name = "child" },
                Empty = null,
                After = 5,
            },
            (e, o) => InteropSerializer.Serialize(e, o, false),
            (ReadOnlySpan<char> xml, out NestedSubject r) => InteropSerializer.Deserialize(DefaultInjector.Instance, xml, out r));

        public static InteropResult Lists() => Check(
            new ListSubject
            {
                Numbers = new List<int> { 1, 2, 3 },
                Strings = new List<string> { "a", "b" },
                Children = new List<ChildSubject>
                {
                    new ChildSubject { Number = 1, Name = "one" },
                    new ChildSubject { Number = 2, Name = "two" },
                },
            },
            (e, o) => InteropSerializer.Serialize(e, o, false),
            (ReadOnlySpan<char> xml, out ListSubject r) => InteropSerializer.Deserialize(DefaultInjector.Instance, xml, out r));

        public static InteropResult Arrays() => Check(
            new ArraySubject
            {
                Numbers = new[] { 1, 2, 3 },
                Strings = new[] { "a", "b" },
                Children = new[]
                {
                    new ChildSubject { Number = 1, Name = "one" },
                    new ChildSubject { Number = 2, Name = "two" },
                },
            },
            (e, o) => InteropSerializer.Serialize(e, o, false),
            (ReadOnlySpan<char> xml, out ArraySubject r) => InteropSerializer.Deserialize(DefaultInjector.Instance, xml, out r));

        public static InteropResult EmptyCollections() => Check(
            new EmptyCollectionsSubject
            {
                EmptyList = new List<int>(),
                NullList = null,
                EmptyArray = new int[0],
                NullArray = null,
            },
            (e, o) => InteropSerializer.Serialize(e, o, false),
            (ReadOnlySpan<char> xml, out EmptyCollectionsSubject r) => InteropSerializer.Deserialize(DefaultInjector.Instance, xml, out r));

        public static InteropResult GetOnlyCollection()
        {
            var subject = new GetOnlyCollectionSubject { Other = 7 };
            subject.Items.Add("a");
            subject.Items.Add("b");

            return Check(
                subject,
                (e, o) => InteropSerializer.Serialize(e, o, false),
                (ReadOnlySpan<char> xml, out GetOnlyCollectionSubject r) => InteropSerializer.Deserialize(DefaultInjector.Instance, xml, out r));
        }

        public static InteropResult Inheritance() => Check(
            new InheritanceDerived
            {
                BaseNumber = 1,
                BaseName = "base",
                DerivedNumber = 2,
                DerivedName = "derived",
            },
            (e, o) => InteropSerializer.Serialize(e, o, false),
            (ReadOnlySpan<char> xml, out InheritanceDerived r) => InteropSerializer.Deserialize(DefaultInjector.Instance, xml, out r));

        public static InteropResult Polymorphic() => Check(
            new PolyHolder
            {
                Item = new PolyDerived1 { Common = 1, First = "first" },
            },
            (e, o) => InteropSerializer.Serialize(e, o, false),
            (ReadOnlySpan<char> xml, out PolyHolder r) => InteropSerializer.Deserialize(DefaultInjector.Instance, xml, out r));

        public static InteropResult PolymorphicList() => Check(
            new PolyListHolder
            {
                Items = new List<PolyBase>
                {
                    new PolyDerived1 { Common = 1, First = "first" },
                    new PolyDerived2 { Common = 2, Second = 22 },
                },
            },
            (e, o) => InteropSerializer.Serialize(e, o, false),
            (ReadOnlySpan<char> xml, out PolyListHolder r) => InteropSerializer.Deserialize(DefaultInjector.Instance, xml, out r));

        public static InteropResult ConcreteBaseInstance() => Check(
            new ConcreteBaseHolder
            {
                Item = new ConcreteBase { Common = 1 },
            },
            (e, o) => InteropSerializer.Serialize(e, o, false),
            (ReadOnlySpan<char> xml, out ConcreteBaseHolder r) => InteropSerializer.Deserialize(DefaultInjector.Instance, xml, out r));

        public static InteropResult Enums() => Check(
            new EnumSubject
            {
                Value = InteropEnum.Two,
                Other = InteropEnum.Zero,
            },
            (e, o) => InteropSerializer.Serialize(e, o, false),
            (ReadOnlySpan<char> xml, out EnumSubject r) => InteropSerializer.Deserialize(DefaultInjector.Instance, xml, out r));

        public static InteropResult RenamedEnum() => Check(
            new RenamedEnumSubject
            {
                Value = Subject.RenamedEnum.One,
            },
            (e, o) => InteropSerializer.Serialize(e, o, false),
            (ReadOnlySpan<char> xml, out RenamedEnumSubject r) => InteropSerializer.Deserialize(DefaultInjector.Instance, xml, out r));

        public static InteropResult Ignore() => Check(
            new IgnoreSubject
            {
                Kept = 1,
                Skipped = 2,
            },
            (e, o) => InteropSerializer.Serialize(e, o, false),
            (ReadOnlySpan<char> xml, out IgnoreSubject r) => InteropSerializer.Deserialize(DefaultInjector.Instance, xml, out r));

        public static InteropResult RenamedElement() => Check(
            new RenamedElementSubject
            {
                Value = 1,
                Untouched = 2,
            },
            (e, o) => InteropSerializer.Serialize(e, o, false),
            (ReadOnlySpan<char> xml, out RenamedElementSubject r) => InteropSerializer.Deserialize(DefaultInjector.Instance, xml, out r));

        public static InteropResult XmlAttributeMember() => Check(
            new AttributeSubject
            {
                Id = 7,
                Payload = "payload",
            },
            (e, o) => InteropSerializer.Serialize(e, o, false),
            (ReadOnlySpan<char> xml, out AttributeSubject r) => InteropSerializer.Deserialize(DefaultInjector.Instance, xml, out r));

        public static InteropResult RenamedRoot() => Check(
            new RootRenamedSubject
            {
                Value = 1,
            },
            (e, o) => InteropSerializer.Serialize(e, o, false),
            (ReadOnlySpan<char> xml, out RootRenamedSubject r) => InteropSerializer.Deserialize(DefaultInjector.Instance, xml, out r));

        public static InteropResult RenamedType() => Check(
            new TypeRenamedSubject
            {
                Value = 1,
            },
            (e, o) => InteropSerializer.Serialize(e, o, false),
            (ReadOnlySpan<char> xml, out TypeRenamedSubject r) => InteropSerializer.Deserialize(DefaultInjector.Instance, xml, out r));

        public static InteropResult RenamedArray() => Check(
            new RenamedArraySubject
            {
                Values = new[] { 1, 2 },
            },
            (e, o) => InteropSerializer.Serialize(e, o, false),
            (ReadOnlySpan<char> xml, out RenamedArraySubject r) => InteropSerializer.Deserialize(DefaultInjector.Instance, xml, out r));

        public static InteropResult XmlTextMember() => Check(
            new TextSubject
            {
                Text = "text body",
            },
            (e, o) => InteropSerializer.Serialize(e, o, false),
            (ReadOnlySpan<char> xml, out TextSubject r) => InteropSerializer.Deserialize(DefaultInjector.Instance, xml, out r));

        public static InteropResult Specified() => Check(
            new SpecifiedSubject
            {
                Value = 5,
                ValueSpecified = false,
                Always = 6,
            },
            (e, o) => InteropSerializer.Serialize(e, o, false),
            (ReadOnlySpan<char> xml, out SpecifiedSubject r) => InteropSerializer.Deserialize(DefaultInjector.Instance, xml, out r));

        public static InteropResult DefaultValue() => Check(
            new DefaultValueSubject
            {
                Value = 42,
                Other = 1,
            },
            (e, o) => InteropSerializer.Serialize(e, o, false),
            (ReadOnlySpan<char> xml, out DefaultValueSubject r) => InteropSerializer.Deserialize(DefaultInjector.Instance, xml, out r));

        public static InteropResult Ordered() => Check(
            new OrderedSubject
            {
                First = 1,
                Second = 2,
            },
            (e, o) => InteropSerializer.Serialize(e, o, false),
            (ReadOnlySpan<char> xml, out OrderedSubject r) => InteropSerializer.Deserialize(DefaultInjector.Instance, xml, out r));

        public static IReadOnlyList<InteropCase> All => new[]
        {
            new InteropCase(nameof(Scalars), Scalars),
            new InteropCase(nameof(TrickyScalars), TrickyScalars),
            new InteropCase(nameof(Durations), Durations),
            new InteropCase(nameof(Nullables), Nullables),
            new InteropCase(nameof(Strings), Strings),
            new InteropCase(nameof(DateTimeKinds), DateTimeKinds),
            new InteropCase(nameof(Fields), Fields),
            new InteropCase(nameof(Empty), Empty),
            new InteropCase(nameof(Nested), Nested),
            new InteropCase(nameof(Lists), Lists),
            new InteropCase(nameof(Arrays), Arrays),
            new InteropCase(nameof(EmptyCollections), EmptyCollections),
            new InteropCase(nameof(GetOnlyCollection), GetOnlyCollection),
            new InteropCase(nameof(Inheritance), Inheritance),
            new InteropCase(nameof(Polymorphic), Polymorphic),
            new InteropCase(nameof(PolymorphicList), PolymorphicList),
            new InteropCase(nameof(ConcreteBaseInstance), ConcreteBaseInstance),
            new InteropCase(nameof(Enums), Enums),
            new InteropCase(nameof(RenamedEnum), RenamedEnum),
            new InteropCase(nameof(Ignore), Ignore),
            new InteropCase(nameof(RenamedElement), RenamedElement),
            new InteropCase(nameof(XmlAttributeMember), XmlAttributeMember),
            new InteropCase(nameof(RenamedRoot), RenamedRoot),
            new InteropCase(nameof(RenamedType), RenamedType),
            new InteropCase(nameof(RenamedArray), RenamedArray),
            new InteropCase(nameof(XmlTextMember), XmlTextMember),
            new InteropCase(nameof(Specified), Specified),
            new InteropCase(nameof(DefaultValue), DefaultValue),
            new InteropCase(nameof(Ordered), Ordered),
        };
    }
}
