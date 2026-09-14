#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace XmlSerDe.Tests.Compat
{
    /// <summary>
    /// Отказ фасада проверяется здесь, на уровне самого генератора, а не по
    /// поведению собранного кода: <see cref="CompatFixture"/> видит только «тип
    /// не ускорен», а вопрос «почему» - это диагностика, и она либо есть, либо нет.
    ///
    /// Дисциплина, которую здесь и караулят: <b>отказ должен быть отказом</b>.
    /// Неподдержанный тип обязан уйти штатному сериализатору целиком и с внятной
    /// причиной, а не получить почти правильный сгенерированный код.
    /// </summary>
    public class CompatGeneratorFixture
    {
        private const string Preamble = @"
using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using XmlSerializer = XmlSerDe.Compat.XmlSerializer;
";

        [Fact]
        public void SupportedType_IsAccelerated_Test()
        {
            var result = Run(Preamble + @"
public class Ok
{
    public int Number { get; set; }
    public string Name { get; set; }
}

public static class Entry
{
    public static object Make() => new XmlSerializer(typeof(Ok));
}
");

            Assert.Empty(result.CompatRefusals);
            Assert.Contains("typeof(global::Ok)", result.Registration);
            Assert.Contains("PooledCharExhauster", result.Registration);
            Assert.Contains("LengthEstimatorExhauster", result.Registration);
            Assert.Contains("Utf8StreamExhauster", result.Registration);
            Assert.DoesNotContain("StringBuilderExhauster", result.Registration);
        }

        /// <summary>
        /// Фасад не подключён к проекту вовсе - генератор про совместимость
        /// не порождает ни строки. Это и есть «не используешь - не платишь»,
        /// проверенное, а не обещанное.
        /// </summary>
        [Fact]
        public void WithoutFacade_NothingIsGenerated_Test()
        {
            var result = Run(@"
using System.Xml.Serialization;

public class Ok
{
    public int Number { get; set; }
}

public static class Entry
{
    public static object Make() => new XmlSerializer(typeof(Ok));
}
");

            Assert.Null(result.Registration);
            Assert.Empty(result.CompatRefusals);
        }

        /// <summary>
        /// <c>typeof</c> обязан быть литеральным: по переменной тип на этапе
        /// сборки неизвестен, и такой вызов вообще не попадает в поле зрения.
        /// </summary>
        [Fact]
        public void NonLiteralTypeof_IsNotACallSite_Test()
        {
            var result = Run(Preamble + @"
public class Ok
{
    public int Number { get; set; }
}

public static class Entry
{
    public static object Make(Type t) => new XmlSerializer(t);
}
");

            Assert.Null(result.Registration);
            Assert.Empty(result.CompatRefusals);
        }

        [Fact]
        public void UnsupportedMember_RefusesWholeType_Test()
        {
            var result = Run(Preamble + @"
public class Bad
{
    public HashSet<int> Set { get; set; }
}

public static class Entry
{
    public static object Make() => new XmlSerializer(typeof(Bad));
}
");

            Assert.Null(result.Registration);

            var refusal = Assert.Single(result.CompatRefusals);
            Assert.Equal(DiagnosticSeverity.Info, refusal.Severity);
            Assert.Contains("global::Bad", refusal.GetMessage());
            Assert.Contains("Set", refusal.GetMessage());
        }

        /// <summary>
        /// Отказ одного типа не должен лишать ускорения остальные: они друг
        /// о друге ничего не знают.
        /// </summary>
        [Fact]
        public void RefusalOfOneRoot_LeavesOthersAccelerated_Test()
        {
            var result = Run(Preamble + @"
public class Ok
{
    public int Number { get; set; }
}

public class Bad
{
    public HashSet<int> Set { get; set; }
}

public static class Entry
{
    public static object MakeOk() => new XmlSerializer(typeof(Ok));
    public static object MakeBad() => new XmlSerializer(typeof(Bad));
}
");

            Assert.Single(result.CompatRefusals);
            Assert.Contains("typeof(global::Ok)", result.Registration);
            Assert.DoesNotContain("typeof(global::Bad)", result.Registration);
        }

        [Fact]
        public void AbstractRoot_IsRefused_Test()
        {
            var result = Run(Preamble + @"
[XmlInclude(typeof(Concrete))]
public abstract class Root
{
    public int Number { get; set; }
}

public class Concrete : Root
{
    public string Name { get; set; }
}

public static class Entry
{
    public static object Make() => new XmlSerializer(typeof(Root));
}
");

            Assert.Null(result.Registration);
            Assert.Contains("abstract root", Assert.Single(result.CompatRefusals).GetMessage());
        }

        [Fact]
        public void NestedCollection_IsRefused_Test()
        {
            var result = Run(Preamble + @"
public class Bad
{
    public List<List<int>> Matrix { get; set; }
}

public static class Entry
{
    public static object Make() => new XmlSerializer(typeof(Bad));
}
");

            Assert.Null(result.Registration);
            Assert.Contains("nested collections", Assert.Single(result.CompatRefusals).GetMessage());
        }

        [Fact]
        public void NoParameterlessConstructor_IsRefused_Test()
        {
            var result = Run(Preamble + @"
public class Bad
{
    public Bad(int number) { Number = number; }

    public int Number { get; set; }
}

public static class Entry
{
    public static object Make() => new XmlSerializer(typeof(Bad));
}
");

            Assert.Null(result.Registration);
            Assert.Contains("parameterless constructor", Assert.Single(result.CompatRefusals).GetMessage());
        }

        /// <summary>
        /// Отдельная порода отказов: не «генератор не умеет такой тип», а «генератор
        /// молча игнорирует такой атрибут». Она опаснее - неподдержанный тип роняет
        /// генерацию, а непонятый атрибут выдаёт валидный код и <b>другой</b> документ,
        /// причём тип отчитывается ускоренным.
        ///
        /// Хуже всех здесь пространство имён: документ без объявления штатный
        /// сериализатор не прочитает вовсе, то есть молчание стоило бы не косметики,
        /// а совместимости.
        /// </summary>
        [Theory]
        [InlineData("[XmlRoot(Namespace = \"urn:x\")] public class Bad { public int Number { get; set; } }", "namespaces")]
        [InlineData("[XmlType(Namespace = \"urn:x\")] public class Bad { public int Number { get; set; } }", "namespaces")]
        [InlineData("public class Bad { [XmlElement(Namespace = \"urn:x\")] public int Number { get; set; } }", "namespaces")]
        [InlineData("public class Bad { [XmlAttribute(Namespace = \"urn:x\")] public int Number { get; set; } }", "namespaces")]
        [InlineData("public class Bad { [XmlArray(Namespace = \"urn:x\")] public List<int> Values { get; set; } }", "namespaces")]
        [InlineData("public class Bad { [XmlAnyElement] public object Any { get; set; } }", "XmlAnyElement")]
        [InlineData("public class Bad { [XmlAnyAttribute] public System.Xml.XmlAttribute[] Any { get; set; } }", "XmlAnyAttribute")]
        [InlineData("public class Bad { [XmlChoiceIdentifier(\"Kind\")] public object Value { get; set; } public int Kind { get; set; } }", "XmlChoiceIdentifier")]
        [InlineData("public class Bad { [XmlNamespaceDeclarations] public XmlSerializerNamespaces Ns { get; set; } }", "XmlNamespaceDeclarations")]
        [InlineData("public class Bad { [XmlElement(\"a\", typeof(int))] public object Value { get; set; } }", "type-driven")]
        [InlineData("public class Bad { [XmlElement(\"a\")] [XmlElement(\"b\")] public object Value { get; set; } }", "type-driven")]
        //DataType задаёт лексическую форму значения: с ним BCL пишет DateTime как
        //"2020-01-02", а byte[] как "01FF" вместо base64 (снято прогоном)
        [InlineData("public class Bad { [XmlElement(DataType = \"date\")] public DateTime Day { get; set; } }", "DataType")]
        [InlineData("public class Bad { [XmlAttribute(DataType = \"date\")] public DateTime Day { get; set; } }", "DataType")]
        [InlineData("public class Bad { [XmlElement(DataType = \"hexBinary\")] public byte[] Data { get; set; } }", "DataType")]
        [InlineData("public class Bad { [XmlArrayItem(DataType = \"date\")] public List<DateTime> Days { get; set; } }", "DataType")]
        [InlineData("public class Bad { [XmlText(DataType = \"date\")] public DateTime Day { get; set; } }", "DataType")]
        //[Flags] хуже всех остальных: документ выходит такой, который штатный
        //сериализатор прочитать не может вовсе - он пишет "Read Write", а
        //Enum.ToString() даёт "Read, Write" (проверено в обе стороны)
        [InlineData("[Flags] public enum Access { None = 0, Read = 1, Write = 2 } public class Bad { public Access Rights { get; set; } }", "[Flags]")]
        [InlineData("[Flags] public enum Access { None = 0, Read = 1, Write = 2 } public class Bad { public List<Access> Rights { get; set; } }", "[Flags]")]
        public void SilentlyIgnoredAttribute_RefusesWholeType_Test(string declaration, string because)
        {
            var result = Run(Preamble + declaration + @"

public static class Entry
{
    public static object Make() => new XmlSerializer(typeof(Bad));
}
");

            Assert.Null(result.Registration);
            Assert.Contains(because, Assert.Single(result.CompatRefusals).GetMessage());
        }

        /// <summary>
        /// Форму документа такой тип решает сам, в своих <c>ReadXml</c>/<c>WriteXml</c>,
        /// а генератор обошёл бы его члены и написал совсем другое.
        /// </summary>
        [Fact]
        public void XmlSerializable_IsRefused_Test()
        {
            var result = Run(Preamble + @"
using System.Xml;
using System.Xml.Schema;

public class Bad : System.Xml.Serialization.IXmlSerializable
{
    public int Number { get; set; }

    public XmlSchema GetSchema() => null;
    public void ReadXml(XmlReader reader) { }
    public void WriteXml(XmlWriter writer) { }
}

public static class Entry
{
    public static object Make() => new XmlSerializer(typeof(Bad));
}
");

            Assert.Null(result.Registration);
            Assert.Contains("IXmlSerializable", Assert.Single(result.CompatRefusals).GetMessage());
        }

        /// <summary>
        /// Отказ обязан срабатывать и вглубь графа, а не только на корне: атрибут
        /// на ребёнке ломает документ ровно так же.
        /// </summary>
        [Fact]
        public void UnsupportedAttributeOnChild_RefusesRoot_Test()
        {
            var result = Run(Preamble + @"
public class Root
{
    public Child Child { get; set; }
}

[XmlType(Namespace = ""urn:x"")]
public class Child
{
    public int Number { get; set; }
}

public static class Entry
{
    public static object Make() => new XmlSerializer(typeof(Root));
}
");

            Assert.Null(result.Registration);
            Assert.Contains("global::Child", Assert.Single(result.CompatRefusals).GetMessage());
        }

        /// <summary>
        /// Обратная сторона: имя без пространства имён - это ровно то, ради чего
        /// разбор штатных атрибутов и делался, и отказывать здесь не за что.
        /// </summary>
        [Fact]
        public void PlainNames_StayAccelerated_Test()
        {
            var result = Run(Preamble + @"
[XmlRoot(""root"")]
[XmlType(""bad"")]
public class Ok
{
    [XmlElement(""n"")]
    public int Number { get; set; }

    [XmlAttribute(""id"")]
    public int Id { get; set; }

    [XmlArray(""values"")]
    [XmlArrayItem(""value"")]
    public List<int> Values { get; set; }
}

public static class Entry
{
    public static object Make() => new XmlSerializer(typeof(Ok));
}
");

            Assert.Empty(result.CompatRefusals);
            Assert.Contains("typeof(global::Ok)", result.Registration);
        }

        /// <summary>
        /// Обычное перечисление отказом не является: отказывает только <c>[Flags]</c>.
        /// Без этой проверки правка «отказаться от флагов» тихо унесла бы с собой
        /// все перечисления вообще.
        /// </summary>
        [Fact]
        public void PlainEnum_StaysAccelerated_Test()
        {
            var result = Run(Preamble + @"
public enum Kind { First = 1, Second = 2 }

public class Ok
{
    public Kind Kind { get; set; }
    public List<Kind> Kinds { get; set; }
}

public static class Entry
{
    public static object Make() => new XmlSerializer(typeof(Ok));
}
");

            Assert.Empty(result.CompatRefusals);
            Assert.Contains("typeof(global::Ok)", result.Registration);
        }

        #region состав членов: то, что мы пропускаем, а BCL пишет (и наоборот)

        /// <summary>
        /// Полифилы под net472: <c>required</c> и <c>init</c> компилятор ищет по
        /// полному имени типа, а не по сборке, поэтому объявить их в самом
        /// исходнике достаточно.
        ///
        /// Под net8/net10 эти типы в corlib уже есть, и объявлять их второй раз
        /// нельзя: ссылки тестовая сборка берёт у себя самой, обе объявления
        /// оказываются видимы одновременно, и <c>[SetsRequiredMembers]</c>
        /// перестаёт связываться. Поэтому полифилы подставляются только там,
        /// где их правда нет.
        /// </summary>
        private static string ModernMemberPolyfills =>
            typeof(object).Assembly.GetType("System.Runtime.CompilerServices.RequiredMemberAttribute") is null
                ? ModernMemberPolyfillSource
                : "";

        private const string ModernMemberPolyfillSource = @"
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
    internal sealed class RequiredMemberAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
    internal sealed class CompilerFeatureRequiredAttribute : Attribute
    {
        public CompilerFeatureRequiredAttribute(string featureName) { }
    }
}
namespace System.Diagnostics.CodeAnalysis
{
    [AttributeUsage(AttributeTargets.Constructor)]
    internal sealed class SetsRequiredMembersAttribute : Attribute { }
}
";

        /// <summary>
        /// Худший из возможных исходов для drop-in, и он не про документ, а про
        /// сборку: <c>required</c> - проверка компилятора, рефлексию она не касается,
        /// поэтому штатный сериализатор такой тип обслуживает как ни в чём не бывало
        /// (проверено прогоном в обе стороны), а сгенерированный <c>new T()</c>
        /// не компилируется вовсе - CS9035. То есть добавление одного
        /// <c>global using</c> превращало собирающийся проект в несобирающийся,
        /// с ошибкой в коде, которого потребитель не писал.
        ///
        /// Отказ нужен даже там, где сам член прекрасно сериализуется, и даже там,
        /// где он помечен <c>[XmlIgnore]</c>: требование задать его стоит
        /// на конструкторе, а не на члене.
        /// </summary>
        [Theory]
        [InlineData("public class Bad { public int Z { get; set; } public required int Y { get; set; } }")]
        [InlineData("public class Bad { public int Z { get; set; } public required int Y { get; init; } }")]
        [InlineData("public class Bad { public int Z { get; set; } [XmlIgnore] public required int Y { get; set; } }")]
        [InlineData("public class Bad { public int Z { get; set; } public required int Y; }")]
        [InlineData("public class BadBase { public required int Y { get; set; } } public class Bad : BadBase { public int Z { get; set; } }")]
        public void RequiredMember_RefusesWholeType_Test(string declaration)
        {
            var result = Run(Preamble + ModernMemberPolyfills + declaration + @"

public static class Entry
{
    public static object Make() => new XmlSerializer(typeof(Bad));
}
");

            Assert.Null(result.Registration);

            var message = Assert.Single(result.CompatRefusals).GetMessage();
            Assert.Contains("required", message);
            Assert.Contains("Y", message);
        }

        /// <summary>
        /// Обратная сторона: конструктор без параметров, помеченный
        /// <c>[SetsRequiredMembers]</c>, обещает компилятору, что задал всё сам, -
        /// <c>new T()</c> становится законным, и отказывать не за что.
        /// </summary>
        [Fact]
        public void RequiredMember_WithSetsRequiredMembers_StaysAccelerated_Test()
        {
            var result = Run(Preamble + ModernMemberPolyfills + @"
public class Ok
{
    //полное имя, а не using: полифилы выше уже объявили пространства имён,
    //и using после них - CS1529
    [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
    public Ok() { }

    public int Z { get; set; }
    public required int Y { get; set; }
}

public static class Entry
{
    public static object Make() => new XmlSerializer(typeof(Ok));
}
");

            Assert.Empty(result.CompatRefusals);
            Assert.Contains("typeof(global::Ok)", result.Registration);
        }

        /// <summary>
        /// Член, который System.Xml.Serialization пишет, а наш отбор выбрасывает.
        /// Отказ здесь был структурно невозможен: обходчик перебирал уже отобранные
        /// члены, и то, что отбор кого-то выбросил, до него не доходило вовсе -
        /// код выходил валидный, тип отчитывался ускоренным, а документ получался
        /// другой, с потерей данных в обе стороны.
        ///
        /// Каждая строка снята прогоном рядом с System.Xml.Serialization:
        /// <c>init</c>-свойство он пишет и читает, свойство без сеттера типа
        /// <c>Collection&lt;T&gt;</c> и наследника <c>List&lt;T&gt;</c> наполняет
        /// через <c>Add</c>, <c>readonly</c>-поле такого же типа - тоже.
        /// </summary>
        [Theory]
        [InlineData("public class Bad { public int Z { get; set; } public int Y { get; init; } }", "init-only")]
        [InlineData("public class Bad { public int Z { get; set; } public System.Collections.ObjectModel.Collection<int> Y { get; } = new(); }", "Add")]
        [InlineData("public class MyList : List<int> { } public class Bad { public int Z { get; set; } public MyList Y { get; } = new(); }", "Add")]
        [InlineData("public class MyList : List<int> { } public class Bad { public int Z { get; set; } public readonly MyList Y = new(); }", "Add")]
        public void MemberSerializedByBclButSkippedHere_RefusesWholeType_Test(string declaration, string because)
        {
            var result = Run(Preamble + ModernMemberPolyfills + declaration + @"

public static class Entry
{
    public static object Make() => new XmlSerializer(typeof(Bad));
}
");

            Assert.Null(result.Registration);

            var message = Assert.Single(result.CompatRefusals).GetMessage();
            Assert.Contains("Y", message);
            Assert.Contains(because, message);
        }

        /// <summary>
        /// Обратное расхождение: член, который пишем мы, а System.Xml.Serialization
        /// не видит, - и потому в документе оказывается лишний элемент.
        ///
        /// Остался один случай: свойство без геттера. BCL его не пишет, потому что
        /// значение брать неоткуда, а наш отбор его пропускал - и сгенерированный
        /// код потом не компилировался бы вовсе.
        ///
        /// <c>internal</c> отсюда ушёл: его теперь не берёт и наш отбор - см.
        /// <see cref="InternalMember_IsSkippedLikeTheBclDoes_StaysAccelerated_Test"/>.
        /// </summary>
        [Theory]
        [InlineData("public class Bad { public int Z { get; set; } private int _y; public int Y { set { _y = value; } } }")]
        public void MemberSkippedByBclButWrittenHere_RefusesWholeType_Test(string declaration)
        {
            var result = Run(Preamble + declaration + @"

public static class Entry
{
    public static object Make() => new XmlSerializer(typeof(Bad));
}
");

            Assert.Null(result.Registration);

            var message = Assert.Single(result.CompatRefusals).GetMessage();
            Assert.Contains("Y", message);
            Assert.Contains("skips this member", message);
        }

        /// <summary>
        /// <c>internal</c>, <c>protected internal</c> и поле - всё это
        /// System.Xml.Serialization не видит, потому что берёт члены через
        /// <c>BindingFlags.Public</c> (снято прогоном). Наш отбор теперь тоже не
        /// видит, значит расхождения нет и отказываться не от чего: тип ускоряется.
        ///
        /// Тест стоит здесь именно как предохранитель от лечения не того места.
        /// Пока отбор брал internal, ровно эти три формы давали отказ - и всякий
        /// POCO с одним internal-свойством терял ускорение из-за особенности
        /// нативного пути, к drop-in отношения не имеющей.
        /// </summary>
        [Theory]
        [InlineData("public class Ok { public int Z { get; set; } internal int Y { get; set; } }")]
        [InlineData("public class Ok { public int Z { get; set; } internal int Y; }")]
        [InlineData("public class Ok { public int Z { get; set; } protected internal int Y { get; set; } }")]
        public void InternalMember_IsSkippedLikeTheBclDoes_StaysAccelerated_Test(string declaration)
        {
            var result = Run(Preamble + declaration + @"

public static class Entry
{
    public static object Make() => new XmlSerializer(typeof(Ok));
}
");

            Assert.Empty(result.CompatRefusals);
            Assert.NotNull(result.Registration);
            Assert.Contains("typeof(global::Ok)", result.Registration);
        }

        /// <summary>
        /// Третья порода: не расхождение, а отказ самого System.Xml.Serialization.
        /// Непубличный сеттер он не прощает - «Cannot deserialize type ... because
        /// it contains property ... which has no public setter» летит уже из
        /// конструктора <c>XmlSerializer</c> (снято прогоном на private, protected
        /// и internal set). Ускорять здесь нечего, а сгенерированный код ещё и
        /// не скомпилировался бы (CS0272); фолбэк воспроизводит исключение BCL
        /// слово в слово.
        /// </summary>
        [Theory]
        [InlineData("public class Bad { public int Z { get; set; } public int Y { get; private set; } }", "no public setter")]
        [InlineData("public class Bad { public int Z { get; set; } public int Y { get; protected set; } }", "no public setter")]
        [InlineData("public class Bad { public int Z { get; set; } public int Y { get; internal set; } }", "no public setter")]
        [InlineData("public class Bad { public int Z { get; set; } public int Y { private get; set; } }", "no public getter")]
        [InlineData("public class Bad { public int Z { get; set; } public Dictionary<string, int> Y { get; } = new(); }", "IDictionary")]
        public void BclRefusesTheTypeItself_RefusesWholeType_Test(string declaration, string because)
        {
            var result = Run(Preamble + declaration + @"

public static class Entry
{
    public static object Make() => new XmlSerializer(typeof(Bad));
}
");

            Assert.Null(result.Registration);

            var message = Assert.Single(result.CompatRefusals).GetMessage();
            Assert.Contains("Y", message);
            Assert.Contains(because, message);
        }

        /// <summary>
        /// Главный предохранитель этой правки. Сверка состава членов обязана молчать
        /// там, где наш пропуск <b>совпадает</b> с пропуском System.Xml.Serialization:
        /// отказ по такому случаю - потеря ускорения ни за что, и он унёс бы с собой
        /// почти всякий настоящий POCO.
        ///
        /// Каждая строка снята прогоном: свойство без сеттера типа строки, массива,
        /// числа и сложного типа BCL не пишет; <c>readonly</c>-поле не-коллекции -
        /// тоже; <c>readonly</c>-поле и свойство без сеттера типа
        /// <c>List&lt;T&gt;</c> он наполняет через <c>Add</c> - и мы так же;
        /// <c>[XmlIgnore]</c>, private, protected, const и static пропускают оба.
        /// </summary>
        [Fact]
        public void SkipsThatMatchBcl_StayAccelerated_Test()
        {
            var result = Run(Preamble + @"
public class Child { public int Q { get; set; } }

public class Ok
{
    public int Number { get; set; }

    public string Str { get; } = ""s"";
    public int[] Arr { get; } = new int[] { 1 };
    public int Num { get; } = 2;
    public Child Cmp { get; } = new Child();

    public readonly int RoNum = 3;
    public readonly string RoStr = ""r"";
    public readonly int[] RoArr = new int[] { 4 };

    public List<int> FilledProperty { get; } = new List<int>();
    public readonly List<int> FilledField = new List<int>();

    [XmlIgnore] public int Ignored { get; set; }
    private int _private;
    protected int Protected;
    public const int Konst = 5;
    public static int Stat = 6;
}

public static class Entry
{
    public static object Make() => new XmlSerializer(typeof(Ok));
}
");

            Assert.Empty(result.CompatRefusals);
            Assert.Contains("typeof(global::Ok)", result.Registration);
        }

        #endregion

        /// <summary>
        /// Точка вызова - не только конструктор. <c>FromTypes</c> называет несколько
        /// типов сразу, и все они обязаны попасть в реестр: иначе подмена одной
        /// строкой теряет ускорение везде, где проект пользуется фабричным методом,
        /// причём молча.
        /// </summary>
        [Fact]
        public void FromTypesCallSite_RegistersEveryNamedType_Test()
        {
            var result = Run(Preamble + @"
public class First { public int Number { get; set; } }
public class Second { public string Name { get; set; } }

public static class Entry
{
    public static object Make() => XmlSerializer.FromTypes(new[] { typeof(First), typeof(Second), });
}
");

            Assert.Empty(result.CompatRefusals);
            Assert.Contains("typeof(global::First)", result.Registration);
            Assert.Contains("typeof(global::Second)", result.Registration);
        }

        [Fact]
        public void FromTypesCallSite_WithExplicitArrayType_Test()
        {
            var result = Run(Preamble + @"
public class First { public int Number { get; set; } }

public static class Entry
{
    public static object Make() => XmlSerializer.FromTypes(new Type[] { typeof(First), });
}
");

            Assert.Contains("typeof(global::First)", result.Registration);
        }

        /// <summary>
        /// Массив, приехавший переменной, на этапе сборки не раскрывается - как и
        /// <c>typeof</c> в переменной. Это не отказ (отказывать не от чего: типа мы
        /// не знаем), а просто отсутствие точки вызова.
        /// </summary>
        [Fact]
        public void FromTypesCallSite_WithVariableArray_IsNotACallSite_Test()
        {
            var result = Run(Preamble + @"
public class First { public int Number { get; set; } }

public static class Entry
{
    public static readonly Type[] Types = new[] { typeof(First), };

    public static object Make() => XmlSerializer.FromTypes(Types);
}
");

            Assert.Null(result.Registration);
            Assert.Empty(result.CompatRefusals);
        }

        [Fact]
        public void FactoryCallSite_IsRegistered_Test()
        {
            var result = Run(Preamble + @"
using XmlSerializerFactory = XmlSerDe.Compat.XmlSerializerFactory;

public class First { public int Number { get; set; } }

public static class Entry
{
    public static object Make() => new XmlSerializerFactory().CreateSerializer(typeof(First));
}
");

            Assert.Empty(result.CompatRefusals);
            Assert.Contains("typeof(global::First)", result.Registration);
        }

        /// <summary>
        /// Штатная фабрика - не наша: её вызов ускорять нечем, и точкой вызова он
        /// не является. Иначе мы регистрировали бы типы, которые фасад в глаза
        /// не увидит.
        /// </summary>
        [Fact]
        public void BclFactoryCallSite_IsIgnored_Test()
        {
            var result = Run(Preamble + @"
public class First { public int Number { get; set; } }

public static class Entry
{
    public static object Make() => new XmlSerializerFactory().CreateSerializer(typeof(First));
}
");

            Assert.Null(result.Registration);
            Assert.Empty(result.CompatRefusals);
        }

        /// <summary>
        /// Строгий режим не меняет решения - он меняет громкость: проект, который
        /// на ускорение рассчитывает, узнаёт об отказе ошибкой сборки, а не
        /// строчкой в подробном логе.
        /// </summary>
        [Theory]
        [InlineData(null, DiagnosticSeverity.Info)]
        [InlineData("false", DiagnosticSeverity.Info)]
        [InlineData("true", DiagnosticSeverity.Warning)]
        [InlineData("warning", DiagnosticSeverity.Warning)]
        [InlineData("error", DiagnosticSeverity.Error)]
        public void StrictMode_ChangesSeverityOnly_Test(string strict, DiagnosticSeverity expected)
        {
            var result = Run(Preamble + @"
public class Bad
{
    public HashSet<int> Set { get; set; }
}

public static class Entry
{
    public static object Make() => new XmlSerializer(typeof(Bad));
}
", strict);

            Assert.Null(result.Registration);
            Assert.Equal(expected, Assert.Single(result.CompatRefusals).Severity);
        }

        #region прогон генератора

        private sealed class GeneratorRun
        {
            public string Registration { get; set; }
            public List<Diagnostic> CompatRefusals { get; set; }
        }

        private static GeneratorRun Run(string source, string strict = null)
        {
            var compilation = CSharpCompilation.Create(
                "CompatGeneratorFixtureAssembly",
                new[] { CSharpSyntaxTree.ParseText(source), },
                GeneratorHarness.References(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                );

            var driver = CSharpGeneratorDriver.Create(
                new[] { new global::XmlSerDe.Generator.XmlDeserializeGenerator().AsSourceGenerator(), },
                optionsProvider: new GeneratorHarness.StrictOptionsProvider(strict)
                );

            var result = driver
                .RunGenerators(compilation)
                .GetRunResult();

            var registration = result.GeneratedTrees
                .FirstOrDefault(t => t.FilePath.EndsWith("XmlSerDe.Compat.Registration.g.cs", StringComparison.Ordinal));

            return new GeneratorRun
            {
                Registration = registration?.ToString(),
                CompatRefusals = result.Diagnostics
                    .Where(d => d.Id == "XMLSERDE001")
                    .ToList(),
            };
        }

        #endregion
    }
}
