#if NETSTANDARD
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;
using XmlSerDe.Generator.Helper;
using System.Xml.Serialization;
using XmlSerDe;
using XmlSerDe.Internal;
using System.Reflection;

namespace XmlSerDe.Generator.Producer
{
    public struct ClassSourceProducer
    {

        /// <summary>
        /// Сток и инжектор обязаны наследоваться от базового класса, а не просто
        /// реализовывать интерфейс. Разница не косметическая: у интерфейса новый
        /// член ломает всех, кто его реализовал, а у класса приезжает virtual'ом
        /// с телом по умолчанию и никого не задевает. Интерфейсы остались - их
        /// реализуют сами базы, - но контрактом для генератора служат базы.
        /// </summary>
        public const string ExhausterBaseFullName = "XmlSerDe.ExhausterBase";

        public const string InjectorBaseFullName = "XmlSerDe.InjectorBase";

        public const string HeadDeserializeMethodName = "Deserialize";
        public const string HeadSerializeMethodName = "Serialize";
        public const string HeadlessDeserializeMethodName = "DeserializeBody";
        public const string HeadedDeserializeMethodName = "DeserializeHeaded";
        public const string HeadlessSerializeMethodName = "SerializeBody";

        /// <summary>
        /// Члены с <see cref="System.Xml.Serialization.XmlAttributeAttribute"/> живут
        /// не в теле, а в голове элемента, и потому не могут разбираться теми же
        /// методами, что и тело: пишутся они между именем тега и '&gt;', а читаются
        /// из <see cref="XmlHead.FullHead"/>. Оба метода генерируются только для типов,
        /// у которых атрибутные члены действительно есть - тип без них получает ровно
        /// тот же код, что и раньше, включая голову одним литералом.
        /// </summary>
        public const string SerializeAttributesMethodName = "SerializeAttributes";
        public const string DeserializeAttributesMethodName = "DeserializeAttributes";

        /// <summary>
        /// Корень с собственным именем (<see cref="System.Xml.Serialization.XmlRootAttribute"/>)
        /// получает отдельный метод: <see cref="HeadSerializeMethodName"/> пишет тот же тип
        /// под его обычным именем и нужен для элементов коллекций, а совместить оба имени
        /// в одном методе нельзя. Генерируется только когда имена действительно разошлись.
        /// </summary>
        public const string RootElementSerializeMethodName = "SerializeRootElement";

        public static readonly string BuiltinFullClassName = "global::" + BuiltinSourceProducer.BuiltinCodeHelperNamespace + "." + BuiltinSourceProducer.BuiltinCodeHelperClassName;
        public static readonly string BuiltinSerializeHeadFullMethodName = BuiltinFullClassName + "." + HeadSerializeMethodName;
        public static readonly string BuiltinSerializeHeadlessFullMethodName = BuiltinFullClassName + "." + HeadlessSerializeMethodName;

        private readonly Compilation _compilation;

        /// <summary>
        /// Класс, объявленный пользователем, - или null, если весь класс порождает
        /// генератор и объявления в исходниках нет вовсе (фасад совместимости).
        /// Нужен ровно для одного: перенести в сгенерированный файл список using'ов
        /// того файла, где класс написан. Всё остальное про целевой класс живёт
        /// в трёх строках ниже, и им безразлично, откуда они взялись.
        /// </summary>
        private readonly INamedTypeSymbol? _deSubject;

        private readonly string? _targetNamespace;
        private readonly string _targetDeclarationName;
        private readonly string _targetGlobalName;

        /// <summary>
        /// using'и, которые надо дописать сверх снятых с пользовательского файла.
        /// </summary>
        private readonly IReadOnlyList<string> _extraUsings;

        public readonly SerializationInfoCollection SerializationInfoCollection;

        /// <summary>
        /// Primitives this host emits. Assembled once from <c>[XmlFeatures]</c>
        /// (or <see cref="XmlFeature.SystemXmlCompatible"/> for compat).
        /// Generate methods only read it — see docs/opt-in-xml-features.md §4.5.
        /// </summary>
        private readonly HostFeatureBinding _binding;

        /// <summary>
        /// Какие нарушения well-formedness этот хост ловит. Собран один раз из
        /// <c>[XmlGuards]</c> (или из <see cref="XmlGuard.SystemXmlCompatible"/>
        /// для compat) вместе с набором фич: три точки, где оси встречаются,
        /// разрешаются внутри <see cref="HostGuardBinding.From"/>, а не здесь.
        /// Generate-методы его только читают - docs/opt-in-xml-guards.md §5.0.
        /// </summary>
        private readonly HostGuardBinding _guards;

        private StringBuilder _sb;

        public ClassSourceProducer(
            Compilation compilation,
            INamedTypeSymbol deSubject
            )
        {
            if (compilation is null)
            {
                throw new ArgumentNullException(nameof(compilation));
            }

            if (deSubject is null)
            {
                throw new ArgumentNullException(nameof(deSubject));
            }

            _compilation = compilation;
            _deSubject = deSubject;
            _targetNamespace = deSubject.ContainingNamespace.IsGlobalNamespace
                ? null
                : deSubject.ContainingNamespace.ToFullDisplayString();
            _targetDeclarationName = deSubject.ToReflectionFormat(false);
            _targetGlobalName = deSubject.ToGlobalDisplayString();
            _extraUsings = new List<string>();

            _sb = new StringBuilder();

            SerializationInfoCollection = ParseAttributes(compilation, _deSubject);

            var features = ReadXmlFeatures(_deSubject);
            _binding = HostFeatureBinding.From(features);
            _guards = HostGuardBinding.From(ReadXmlGuards(_deSubject), features);
        }

        /// <summary>
        /// Тот же producer, но состав сериализуемых типов приходит готовым, а не
        /// вычитывается из атрибутов на классе: класса в исходниках может не быть
        /// вовсе. Так работает фасад совместимости - там состав считается обходом
        /// графа от типа, названного в <c>new XmlSerializer(typeof(T))</c>.
        /// </summary>
        public ClassSourceProducer(
            Compilation compilation,
            string? targetNamespace,
            string targetClassName,
            IReadOnlyList<string> extraUsings,
            SerializationInfoCollection serializationInfoCollection
            )
        {
            if (compilation is null)
            {
                throw new ArgumentNullException(nameof(compilation));
            }
            if (string.IsNullOrEmpty(targetClassName))
            {
                throw new ArgumentException($"'{nameof(targetClassName)}' cannot be null or empty.", nameof(targetClassName));
            }
            if (extraUsings is null)
            {
                throw new ArgumentNullException(nameof(extraUsings));
            }

            _compilation = compilation;
            _deSubject = null;
            _targetNamespace = string.IsNullOrEmpty(targetNamespace) ? null : targetNamespace;
            _targetDeclarationName = targetClassName;
            _targetGlobalName = _targetNamespace is null
                ? "global::" + targetClassName
                : "global::" + _targetNamespace + "." + targetClassName;
            _extraUsings = extraUsings;

            _sb = new StringBuilder();

            SerializationInfoCollection = serializationInfoCollection;

            //фасад включает себе оба полных набора сам: пользователь не пишет
            //ни [XmlFeatures], ни [XmlGuards], а native-хост в той же сборке
            //ничего из этого не наследует
            _binding = HostFeatureBinding.From(XmlFeature.SystemXmlCompatible);
            _guards = HostGuardBinding.From(XmlGuard.SystemXmlCompatible, XmlFeature.SystemXmlCompatible);
        }

        public string GenerateClass(
            )
        {
            _sb.Clear();

            GenerateUsings();
            _sb.AppendLine("using roschar = System.ReadOnlySpan<char>;");

            if (_targetNamespace is not null)
            {
                _sb.AppendLine($@"
namespace {_targetNamespace}");
            }

            _sb.AppendLine($$"""
{
    public partial class {{_targetDeclarationName}}
    {

""");

            GenerateCutXmlHeadMethod();

            foreach (var exhaustType in this.SerializationInfoCollection.ExhaustList)
            {
                GenerateSerializeMethods(exhaustType);
            }
            foreach (var injectorType in this.SerializationInfoCollection.InjectorList)
            {
                GenerateDeserializeMethods(injectorType);
            }

            _sb.AppendLine($$"""
    }
}
""");

            return _sb.ToString();
        }

        /// <summary>
        /// Обёртка над общим <c>BuiltinCodeHelper.CutXmlHead</c>, подставляющая
        /// фичи именно этого хоста (docs/opt-in-xml-features.md §5.6). Без неё
        /// пользователю пришлось бы помнить набор своих флагов и передавать его
        /// руками, а безаргументный общий helper молча снимал бы пролог по
        /// default-правилам даже у хоста с <c>Doctype</c>.
        /// </summary>
        private readonly void GenerateCutXmlHeadMethod()
        {
            var helper =
                $"global::{BuiltinSourceProducer.BuiltinCodeHelperNamespace}"
                + $".{BuiltinSourceProducer.BuiltinCodeHelperClassName}"
                + $".{BuiltinSourceProducer.CutXmlHeadMethodName}";

            _sb.AppendLine($$"""
        /// <summary>
        /// Снимает XML-декларацию и пролог по фичам этого сериализатора.
        /// </summary>
        public static roschar {{BuiltinSourceProducer.CutXmlHeadMethodName}}(roschar xml)
        {
            return {{helper}}({{_binding.CutXmlHeadInvocation("xml")}});
        }

""");
        }


        #region serialize

        private readonly void GenerateSerializeMethods(
            INamedTypeSymbol exhaustType
            )
        {
            _sb.AppendLine($$"""
#region serialize

""");
            foreach (var ssi in SerializationInfoCollection.Infos)
            {
                GenerateSerializeMethod(
                    ssi,
                    exhaustType
                    );
            }

            _sb.AppendLine($$"""

#endregion
""");
        }

        private readonly void GenerateSerializeMethod(
            SerializationInfo ssi,
            INamedTypeSymbol exhaustType
            )
        {
            var subject = ssi.Subject;

            var typeName = subject.GetXmlTypeName();
            var rootName = subject.GetXmlRootName();

            GenerateSerializeAttributesMethod(subject, exhaustType);

            GenerateSerializeMethod(subject, ssi.Deriveds, exhaustType, HeadSerializeMethodName, typeName);
            GenerateSerializeMethod(subject, ssi.Deriveds, exhaustType, HeadlessSerializeMethodName, null);

            if (ssi.IsRoot)
            {
                var rootNameDiffers = rootName != typeName;
                if (rootNameDiffers)
                {
                    GenerateSerializeMethod(subject, ssi.Deriveds, exhaustType, RootElementSerializeMethodName, rootName);
                }

                GenerateRootSerializeMethod(
                    subject,
                    exhaustType,
                    rootNameDiffers ? RootElementSerializeMethodName : HeadSerializeMethodName
                    );
            }
        }

        private readonly void GenerateRootSerializeMethod(
            INamedTypeSymbol subject,
            INamedTypeSymbol exhaustType,
            string rootMethodName
            )
        {
            var ssGlobalName = subject.ToGlobalDisplayString();
            var exhaustTypeGlobalName = exhaustType.ToGlobalDisplayString();

            _sb.AppendLine($$"""
        public static void {{HeadSerializeMethodName}}({{exhaustTypeGlobalName}} exh, {{ssGlobalName}} obj, bool appendXmlHead)
        {
            if(appendXmlHead)
            {
                global::{{BuiltinSourceProducer.BuiltinCodeHelperNamespace}}.{{BuiltinSourceProducer.BuiltinCodeHelperClassName}}.{{BuiltinSourceProducer.AppendXmlHeadMethodName}}(exh);
            }

            {{rootMethodName}}(exh, obj);
        }

""");
        }

        /// <summary>
        /// <paramref name="elementName"/> - имя элемента, в который заворачивается тип;
        /// null означает "без головы вовсе", то есть <see cref="HeadlessSerializeMethodName"/>.
        /// </summary>
        private readonly void GenerateSerializeMethod(
            INamedTypeSymbol subject,
            List<INamedTypeSymbol> deriveds,
            INamedTypeSymbol exhaustType,
            string methodName,
            string? elementName
            )
        {
            var withHeadMethod = elementName is not null;
            var ssGlobalName = subject.ToGlobalDisplayString();
            var exhaustTypeGlobalName = exhaustType.ToGlobalDisplayString();

            _sb.AppendLine($$"""
        private static void {{methodName}}({{exhaustTypeGlobalName}} exh, {{ssGlobalName}} obj)
        {
""");

            if(!subject.IsValueType)
            {
                _sb.AppendLine($$"""
            if(obj is null)
            {
                return;
            }

""");
            }

            //цепочка проверок на наследников, а следом - тело самого типа.
            //Раньше эти два случая были взаимоисключающими (if/else), и у не-абстрактной
            //базы с наследниками экземпляр самой базы не подходил ни под одну проверку
            //и уходил в пустоту: ни исключения, ни предупреждения, просто пропавший объект.
            if (deriveds.Count > 0)
            {
                foreach (var derived in deriveds)
                {
                    if (withHeadMethod)
                    {
                        _sb.AppendLine($$"""

            {
                if(obj is {{derived.ToGlobalDisplayString()}} dobj)
                {
""");
                        GenerateElementHead(
                            "                    ",
                            elementName!,
                            XsiTypeAttributes(derived),
                            derived,
                            "dobj"
                            );
                        _sb.AppendLine($$"""
                    {{HeadlessSerializeMethodName}}(exh, dobj);
                    exh.{{nameof(IExhauster.Append)}}("</{{elementName}}>");
                    return;
                }
            }
""");
                    }
                    else
                    {
                        _sb.AppendLine($$"""

            {
                if(obj is {{derived.ToGlobalDisplayString()}} dobj)
                {
                    {{HeadlessSerializeMethodName}}(exh, dobj);
                    return;
                }
            }
""");
                    }
                }
            }

            //абстрактный тип экземпляром быть не может, поэтому после цепочки наследников
            //писать нечего; во всех остальных случаях дальше идёт тело самого типа
            if (deriveds.Count == 0 || !subject.IsAbstract)
            {
                if (withHeadMethod)
                {
                    _sb.AppendLine();
                    GenerateElementHead(
                        "            ",
                        elementName!,
                        "",
                        subject,
                        "obj"
                        );
                    _sb.AppendLine();
                }

                GenerateSerializeMembers(subject);

                if (withHeadMethod)
                {
                    _sb.AppendLine($$"""

            exh.{{nameof(IExhauster.Append)}}("</{{elementName}}>");

""");
                }
            }

            _sb.AppendLine($$"""
        }

""");
        }

        private readonly void GenerateSerializeMembers(
            INamedTypeSymbol subject
            )
        {
            SplitMembers(subject, out var elements, out _, out var text);

            //атрибутные члены сюда не попадают вовсе: их место - голова, и пишет
            //их SerializeAttributes ещё до того, как начнётся тело
            foreach (var member in elements)
            {
                GenerateSerializeMember(member);
            }

            if (text is not null)
            {
                GenerateSerializeTextMember(text);
            }
        }

        /// <summary>
        /// Член с <see cref="XmlTextAttribute"/>: тело владельца целиком, без
        /// собственного тега. Ни nil, ни пустого элемента здесь быть не может -
        /// писать их некуда, - поэтому null просто не пишется, и владелец
        /// оказывается пустым. Ровно так же поступает System.Xml.Serialization.
        /// </summary>
        private readonly void GenerateSerializeTextMember(
            ISymbol member
            )
        {
            var memberType = ParseMember(member);

            _sb.AppendLine($$"""
            //{{typeof(XmlTextAttribute).Name}} {{memberType.ToGlobalDisplayString()}} {{member.Name}}
""");

            var presenceGuard = GetPresenceGuard(member);
            if (presenceGuard is not null)
            {
                _sb.AppendLine($$"""
            if({{presenceGuard}})
            {
""");
            }

            var valueStatement = GenerateSimpleContentStatement(member, memberType, "                ", XmlPlacement.Text);

            if (!memberType.IsValueType)
            {
                _sb.AppendLine($$"""
            if(obj.{{member.CsName()}} is not null)
            {
                {{valueStatement}}
            }
""");
            }
            else
            {
                _sb.AppendLine($$"""
            {{valueStatement}}
""");
            }

            if (presenceGuard is not null)
            {
                _sb.AppendLine($$"""
            }
""");
            }
        }

        /// <summary>
        /// Атрибутные члены одного типа - одним методом, чтобы каждое из шести мест,
        /// где пишется голова элемента, обошлось одним вызовом. Порядок - тот же
        /// base-first, что и у элементов: так их пишет и System.Xml.Serialization.
        /// </summary>
        private readonly void GenerateSerializeAttributesMethod(
            INamedTypeSymbol subject,
            INamedTypeSymbol exhaustType
            )
        {
            SplitMembers(subject, out _, out var attributes, out _);
            if (attributes.Count == 0)
            {
                return;
            }

            _sb.AppendLine($$"""
        private static void {{SerializeAttributesMethodName}}({{exhaustType.ToGlobalDisplayString()}} exh, {{subject.ToGlobalDisplayString()}} obj)
        {
""");

            foreach (var member in attributes)
            {
                var memberType = ParseMember(member);
                var attributeName = member.GetXmlAttributeName();

                _sb.AppendLine($$"""
            //{{memberType.ToGlobalDisplayString()}} {{member.Name}}
""");

                var guards = new List<string>();
                var presenceGuard = GetPresenceGuard(member);
                if (presenceGuard is not null)
                {
                    guards.Add(presenceGuard);
                }
                if (!memberType.IsValueType)
                {
                    //null-строку BCL не пишет вовсе: атрибута с "отсутствующим"
                    //значением в XML не бывает, а пустая строка - это уже не null
                    guards.Add($"obj.{member.CsName()} is not null");
                }

                var open = guards.Count > 0
                    ? $"            if({string.Join(" && ", guards)})\r\n            {{"
                    : "            {";

                var valueStatement = GenerateSimpleContentStatement(member, memberType, "                ", XmlPlacement.Attribute);

                _sb.AppendLine($$"""
{{open}}
                exh.{{nameof(IExhauster.Append)}}(" {{attributeName}}=\"");
                {{valueStatement}}
                exh.{{nameof(IExhauster.Append)}}("\"");
            }
""");
            }

            _sb.AppendLine($$"""
        }

""");
        }

        /// <summary>
        /// Голова элемента. У типа без атрибутных членов это по-прежнему один
        /// литерал целиком - тот же самый, что генератор писал всегда.
        /// </summary>
        private readonly void GenerateElementHead(
            string indent,
            string elementName,
            string extraAttributes,
            ITypeSymbol? attributeOwner,
            string objExpression
            )
        {
            _sb.AppendLine(
                GenerateElementHeadLines(indent, elementName, extraAttributes, attributeOwner, objExpression)
                );
        }

        private readonly string GenerateElementHeadLines(
            string indent,
            string elementName,
            string extraAttributes,
            ITypeSymbol? attributeOwner,
            string objExpression
            )
        {
            if (!HasXmlAttributeMembers(attributeOwner))
            {
                return $"{indent}exh.{nameof(IExhauster.Append)}({ToCsharpLiteral("<" + elementName + extraAttributes + ">")});";
            }

            return
                $"{indent}exh.{nameof(IExhauster.Append)}({ToCsharpLiteral("<" + elementName + extraAttributes)});\r\n"
                + $"{indent}{SerializeAttributesMethodName}(exh, {objExpression});\r\n"
                + $"{indent}exh.{nameof(IExhauster.Append)}(\">\");";
        }

        /// <summary>
        /// Одна лексема без всякой разметки вокруг: так пишется и значение атрибута,
        /// и текст тела. Тегов здесь нет ни у того, ни у другого, поэтому обычный
        /// путь через <see cref="GenerateSerializeEnum"/> не годится.
        ///
        /// Экранирование у этих двух мест разное, и разойтись они могут только на
        /// строке: у всех прочих builtin-типов лексическая форма - цифры, буквы и
        /// знаки, которых нет ни среди разметки, ни среди пробельных.
        /// </summary>
        private readonly string GenerateSimpleContentStatement(
            ISymbol member,
            TypeSymbol memberType,
            string indent,
            XmlPlacement placement
            )
        {
            if (IsBase64Binary(member, memberType))
            {
                //одна и та же лексема годится обоим местам: экранировать в base64 нечего
                return $"exh.{nameof(IExhauster.AppendBase64)}(obj.{member.CsName()});";
            }

            if (placement == XmlPlacement.Attribute && memberType.Symbol.SpecialType == SpecialType.System_String)
            {
                return _binding.AppendAttributeEncodedStatement("exh", $"obj.{member.CsName()}");
            }

            if (memberType.Symbol.SpecialType == SpecialType.System_String)
            {
                return _binding.AppendEncodedStatement("exh", $"obj.{member.CsName()}");
            }

            if (!memberType.IsEnum)
            {
                return $"{BuiltinSerializeHeadlessFullMethodName}(exh, obj.{member.CsName()});";
            }

            var expression = GenerateEnumToStringExpression(
                (INamedTypeSymbol)memberType.Symbol,
                $"obj.{member.CsName()}",
                indent
                );

            return $"exh.{nameof(IExhauster.Append)}({expression});";
        }

        private static string XsiTypeAttributes(ITypeSymbol derived)
        {
            return $" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xsi:type=\"{derived.GetXmlTypeName()}\"";
        }

        /// <summary>
        /// Кавычки внутри бывают только у xsi:type, поэтому вербатим-литерал
        /// появляется ровно там же, где появлялся раньше.
        /// </summary>
        private static string ToCsharpLiteral(string text)
        {
            if (text.IndexOf('"') < 0)
            {
                return "\"" + text + "\"";
            }

            return "@\"" + text.Replace("\"", "\"\"") + "\"";
        }

        private readonly bool HasXmlAttributeMembers(ITypeSymbol? type)
        {
            if (type is not INamedTypeSymbol named)
            {
                return false;
            }

            SplitMembers(named, out _, out var attributes, out _);
            return attributes.Count > 0;
        }

        /// <summary>
        /// Пишется ли член одной лексемой base64Binary вместо коллекции элементов.
        ///
        /// Правило замерено на System.Xml.Serialization и оказалось узким: оно про
        /// сам <c>byte[]</c>, а не про байт и не про коллекцию байтов. <c>List&lt;byte&gt;</c>
        /// остаётся коллекцией <c>&lt;unsignedByte&gt;</c>, и <c>byte[]</c> с явной
        /// обёрткой <see cref="XmlArrayAttribute"/> - тоже: обёртка возвращает массиву
        /// вид обычной коллекции. Поэтому проверка стоит здесь, на паре "член + его тип",
        /// а не среди builtin'ов, где о члене ничего не известно.
        /// </summary>
        private readonly bool IsBase64Binary(
            ISymbol member,
            TypeSymbol memberType
            )
        {
            if (!memberType.IsArray(out var itemType))
            {
                return false;
            }
            if (itemType!.SpecialType != SpecialType.System_Byte)
            {
                return false;
            }

            return !member.HasXmlArrayAttribute();
        }

        private readonly void GenerateSerializeMember(
            ISymbol member
            )
        {
            var memberType = ParseMember(member);
            _sb.AppendLine($$"""
            //{{memberType.ToGlobalDisplayString()}} {{member.Name}}
""");

            var elementName = GetMemberElementName(member);

            var presenceGuard = GetPresenceGuard(member);
            if (presenceGuard is not null)
            {
                _sb.AppendLine($$"""
            if({{presenceGuard}})
            {
""");
            }

            var canBeNull = !memberType.IsValueType || memberType.IsNullableValueType;
            if (canBeNull)
            {
                _sb.AppendLine($$"""
            if(obj.{{member.CsName()}} is not null)
            {
""");
            }
            else
            {
                _sb.AppendLine($$"""
            {
""");
            }


            if (BuiltinSourceProducer.TryGetBuiltin(_compilation, memberType.Symbol, out _))
            {
                if (memberType.Symbol.SpecialType == SpecialType.System_String)
                {
                    _sb.AppendLine($$"""
                exh.{{nameof(IExhauster.Append)}}("<{{elementName}}>");
                {{_binding.AppendEncodedStatement("exh", $"obj.{member.CsName()}")}}
                exh.{{nameof(IExhauster.Append)}}("</{{elementName}}>");

""");
                }
                else
                {
                    _sb.AppendLine($$"""
                exh.{{nameof(IExhauster.Append)}}("<{{elementName}}>");
                {{BuiltinSerializeHeadlessFullMethodName}}(exh, obj.{{member.CsName()}});
                exh.{{nameof(IExhauster.Append)}}("</{{elementName}}>");

""");
                }

            }
            else if (memberType.IsEnum)
            {
                var gses = GenerateSerializeEnum((INamedTypeSymbol)memberType.Symbol, elementName, member.CsName());
                _sb.AppendLine(gses);
            }
            else if (IsBase64Binary(member, memberType))
            {
                //одна лексема в теле собственного элемента - ровно как у строки,
                //только без экранирования: в алфавите base64 разметки нет
                _sb.AppendLine($$"""
                exh.{{nameof(IExhauster.Append)}}("<{{elementName}}>");
                exh.{{nameof(IExhauster.AppendBase64)}}(obj.{{member.CsName()}});
                exh.{{nameof(IExhauster.Append)}}("</{{elementName}}>");
""");
            }
            //TODO other collections?
            else if (memberType.IsCollection(out var collectionItemType))
            {
                var countOrLength = memberType.DetermineCountOrLength();

                var scms = GenerateSerializeCollectionMember(member, (INamedTypeSymbol)collectionItemType!);

                _sb.AppendLine($$"""
                exh.{{nameof(IExhauster.Append)}}("<{{elementName}}>");
                for(var index = 0; index < obj.{{member.CsName()}}.{{countOrLength}}; index++)
                {
{{scms}}
                }
                exh.{{nameof(IExhauster.Append)}}("</{{elementName}}>");
""");

            }
            else
            {
                var subjectFound = SerializationInfoCollection.TryGetSubject(memberType.Symbol, out var ssi);
                if (subjectFound && ssi.Deriveds.Count > 0)
                {
                    //цепочка else if, а не набор независимых if: во-первых, ниже за ней идёт
                    //ветка на сам базовый тип, во-вторых, из двух наследников, состоящих в
                    //родстве между собой, независимые проверки сработали бы обе и написали
                    //бы член дважды. Переменная в каждой ветке своя: у else if общая область
                    //видимости, и одно имя на всю цепочку не скомпилировалось бы.
                    var derivedIndex = 0;
                    foreach (var derived in ssi.Deriveds)
                    {
                        var elseif = derivedIndex == 0 ? "" : "else ";

                        _sb.AppendLine($$"""
                {{elseif}}if(obj.{{member.CsName()}} is {{derived.ToGlobalDisplayString()}} dobj{{derivedIndex}})
                {
""");
                        GenerateElementHead(
                            "                    ",
                            elementName,
                            XsiTypeAttributes(derived),
                            derived,
                            $"dobj{derivedIndex}"
                            );
                        _sb.AppendLine($$"""
                    {{HeadlessSerializeMethodName}}(exh, dobj{{derivedIndex}});
                    exh.{{nameof(IExhauster.Append)}}("</{{elementName}}>");
                }
""");
                        derivedIndex++;
                    }

                    if (!memberType.IsAbstract)
                    {
                        //экземпляр самой базы: ни одна проверка на наследника не сработала.
                        //Без этой ветки такой член уходил в пустоту молча
                        _sb.AppendLine($$"""
                else
                {
""");
                        GenerateElementHead(
                            "                    ",
                            elementName,
                            "",
                            memberType.Symbol,
                            $"obj.{member.CsName()}"
                            );
                        _sb.AppendLine($$"""
                    {{HeadlessSerializeMethodName}}(exh, obj.{{member.CsName()}});
                    exh.{{nameof(IExhauster.Append)}}("</{{elementName}}>");
                }
""");
                    }
                }
                else
                {
                    GenerateElementHead(
                        "                ",
                        elementName,
                        "",
                        memberType.Symbol,
                        $"obj.{member.CsName()}"
                        );
                    _sb.AppendLine($$"""
                {{HeadlessSerializeMethodName}}(exh, obj.{{member.CsName()}});
                exh.{{nameof(IExhauster.Append)}}("</{{elementName}}>");
""");
                }
            }

            _sb.AppendLine($$"""
            }
""");

            GenerateSerializeNil(memberType, elementName);

            if (presenceGuard is not null)
            {
                _sb.AppendLine($$"""
            }
""");
            }
        }

        /// <summary>
        /// Условие, при котором член вообще попадает в документ, - или null, если
        /// член пишется всегда (тогда и в сгенерированном коде не появляется ничего:
        /// тип без этих атрибутов получает ровно тот же код, что и раньше).
        ///
        /// Оба условия складываются через &amp;&amp;, потому что оба могут стоять на
        /// одном члене и оба означают "не писать".
        /// </summary>
        private readonly string? GetPresenceGuard(ISymbol member)
        {
            var guards = new List<string>();

            var specified = member.GetSpecifiedCompanion();
            if (specified is not null)
            {
                guards.Add($"obj.{specified.CsName()}");
            }

            //умолчание несопоставимого с членом типа охраны не даёт вовсе: у BCL
            //такое значение не совпадает никогда, и член пишется всегда - см.
            //XmlPresenceHelper.TryGetComparableLiteral
            if (member.TryGetDefaultValue(out var defaultValue)
                && defaultValue.TryGetComparableLiteral(
                    _compilation,
                    ParseMember(_compilation, member).Symbol,
                    out var literal
                    ))
            {
                guards.Add($"obj.{member.CsName()} != {literal}");
            }

            if (guards.Count == 0)
            {
                return null;
            }

            return string.Join(" && ", guards);
        }

        /// <summary>
        /// Пустой элемент с <c>xsi:nil="true"</c> для члена, оказавшегося null.
        ///
        /// Пишется только для <see cref="Nullable{T}"/>, и это не упрощение, а
        /// ровно то, что делает System.Xml.Serialization: null-строку, null-ссылку
        /// на сложный тип и null-коллекцию он молча опускает, а nil ставит только
        /// там, где иначе значение было бы неотличимо от default(T). Проверено
        /// прогоном BCL по типу со всеми пятью видами null сразу.
        ///
        /// xmlns:xsi объявляется прямо на элементе - там же, где его объявляет
        /// запись xsi:type: корня у безголового метода под рукой нет, а объявление
        /// на самом элементе столь же законно и читается обеими сторонами.
        /// </summary>
        private readonly void GenerateSerializeNil(
            TypeSymbol memberType,
            string elementName
            )
        {
            if (!memberType.IsNullableValueType)
            {
                return;
            }

            _sb.AppendLine($$"""
            else
            {
                exh.{{nameof(IExhauster.Append)}}(@"<{{elementName}} xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xsi:nil=""true"" />");
            }
""");
        }

        private readonly string GenerateSerializeCollectionMember(
            ISymbol member,
            INamedTypeSymbol listItemType
            )
        {
            //без XmlArrayItem элемент называется по своему типу - это и есть умолчание
            //System.Xml.Serialization, и именно так генератор писал его всегда
            var itemName = member.GetXmlArrayItemName();

            if (listItemType.EnumUnderlyingType != null)
            {
                return GenerateSerializeEnum(
                    listItemType,
                    itemName ?? listItemType.GetXmlTypeName(),
                    $"{member.CsName()}[index]"
                    );
            }

            if (itemName is null)
            {
                if (BuiltinSourceProducer.TryGetBuiltin(_compilation, listItemType, out var itemBuiltin))
                {
                    if (listItemType.SpecialType == SpecialType.System_String)
                    {
                        //голова пишется здесь, потому что тело выбирает binding
                        //(checked/unchecked encode); имя элемента при этом берётся
                        //из той же таблицы builtin'ов, что и у общего пути
                        var itemElementName = itemBuiltin.XmlTypeName;

                        return
                            "                    exh." + nameof(IExhauster.Append) + "(\"<" + itemElementName + ">\");\r\n"
                            + "                    " + _binding.AppendEncodedStatement("exh", $"obj.{member.CsName()}[index]") + "\r\n"
                            + "                    exh." + nameof(IExhauster.Append) + "(\"</" + itemElementName + ">\");";
                    }

                    return $"                    {BuiltinSerializeHeadFullMethodName}(exh, obj.{member.CsName()}[index]);";
                }

                return $"                {HeadSerializeMethodName}(exh, obj.{member.CsName()}[index]);";
            }

            //имя задано вручную, поэтому голову пишем здесь, а телом занимается
            //безголовый метод
            if (!BuiltinSourceProducer.TryGetBuiltin(_compilation, listItemType, out _)
                && SerializationInfoCollection.TryGetSubject(listItemType, out var itemSubject)
                && itemSubject.Deriveds.Count > 0)
            {
                //у безголового метода нет места для xsi:type, а выдать элемент без него
                //значило бы молча потерять тип наследника
                throw new InvalidOperationException(
                    $"{typeof(XmlArrayItemAttribute).Name} on {member.Name} is not supported:"
                    + $" {listItemType.ToGlobalDisplayString()} has declared derived types,"
                    + $" and a manually named item element leaves no place for xsi:type"
                    );
            }

            var isBuiltin = BuiltinSourceProducer.TryGetBuiltin(_compilation, listItemType, out _);

            string headlessInvocation;
            if (isBuiltin && listItemType.SpecialType == SpecialType.System_String)
            {
                headlessInvocation = _binding.AppendEncodedStatement("exh", $"obj.{member.CsName()}[index]");
            }
            else if (isBuiltin)
            {
                headlessInvocation = $"{BuiltinSerializeHeadlessFullMethodName}(exh, obj.{member.CsName()}[index]);";
            }
            else
            {
                headlessInvocation = $"{HeadlessSerializeMethodName}(exh, obj.{member.CsName()}[index]);";
            }

            var head = GenerateElementHeadLines(
                "                    ",
                itemName,
                "",
                isBuiltin ? null : listItemType,
                $"obj.{member.CsName()}[index]"
                );

            return $$"""
{{head}}
                    {{headlessInvocation}}
                    exh.{{nameof(IExhauster.Append)}}("</{{itemName}}>");
""";
        }

        /// <summary>
        /// Emits a serialize snippet for an enum-typed value. Uses a switch over
        /// the declared members (each mapped to its literal name) instead of
        /// obj.Prop.ToString(), which avoids Enum.ToString()'s reflection lookup
        /// and always-allocates-a-new-string behavior. Falls back to ToString()
        /// in the switch's default arm for values with no matching declared
        /// member (undefined numeric values, [Flags] combinations), which keeps
        /// output identical to before for those cases.
        /// </summary>
        private readonly string GenerateSerializeEnum(
            INamedTypeSymbol enumType,
            string tagName,
            string enumPropertyExpression
            )
        {
            var expression = GenerateEnumToStringExpression(
                enumType,
                "enumValueToAppend",
                "                    "
                );

            return $$"""
                {
                    var enumValueToAppend = obj.{{enumPropertyExpression}};
                    exh.{{nameof(IExhauster.Append)}}("<{{tagName}}>");
                    exh.{{nameof(IExhauster.Append)}}({{expression}});
                    exh.{{nameof(IExhauster.Append)}}("</{{tagName}}>");
                }
""";
        }

        /// <summary>
        /// Выражение <c>значение switch { ... }</c>, отдающее имя члена перечисления
        /// строкой. Жило внутри <see cref="GenerateSerializeEnum"/>, пока перечисление
        /// могло попасть только в собственный тег; у члена в атрибуте и у члена в тексте
        /// тегов нет, а switch нужен ровно тот же.
        /// </summary>
        private readonly string GenerateEnumToStringExpression(
            INamedTypeSymbol enumType,
            string enumValueExpression,
            string indent
            )
        {
            var switchArms = GenerateEnumToStringSwitchArms(enumType, indent + "    ");

            return
                $"{enumValueExpression} switch\r\n"
                + $"{indent}{{\r\n"
                + switchArms
                + $"{indent}    _ => {enumValueExpression}.ToString(),\r\n"
                + $"{indent}}}";
        }

        private readonly string GenerateEnumToStringSwitchArms(INamedTypeSymbol enumType, string indent)
        {
            var enumGlobalName = enumType.ToGlobalDisplayString();
            var seenValues = new HashSet<object>();
            var sb = new StringBuilder();

            foreach (var field in enumType.GetMembers().OfType<IFieldSymbol>())
            {
                if (!field.HasConstantValue || field.ConstantValue is null)
                {
                    continue;
                }
                if (!seenValues.Add(field.ConstantValue))
                {
                    //another member already declared with the same underlying value; skip to avoid a duplicate switch arm
                    continue;
                }

                sb.AppendLine($"""{indent}{enumGlobalName}.{field.Name} => "{field.GetXmlEnumName()}",""");
            }

            return sb.ToString();
        }


        #endregion

        #region deserialize

        private static readonly string XmlHeadFullName = typeof(XmlHead).FullName;
        private static readonly string XmlScanFullName = typeof(XmlScan).FullName;
        private static readonly string XmlBase64FullName = typeof(XmlBase64).FullName;
        //FullName у открытого обобщённого типа несёт хвост арности
        //("...PooledArrayBuilder`1"), которого в исходном коде быть не должно
        private static readonly string PooledArrayBuilderFullName =
            typeof(PooledArrayBuilder<>).FullName.Split('`')[0];

        private readonly void GenerateDeserializeMethods(
            INamedTypeSymbol injectorType
            )
        {
            _sb.AppendLine($$"""
#region deserialize

""");
            foreach (var ssi in SerializationInfoCollection.Infos)
            {
                GenerateDeserializeMethod(
                    injectorType,
                    ssi
                    );
            }

            _sb.AppendLine($$"""

#endregion
""");
        }

        private readonly void GenerateDeserializeMethod(
            INamedTypeSymbol injectorType,
            SerializationInfo ssi
            )
        {
            var subject = ssi.Subject;

            GenerateDeserializeAttributesMethod(injectorType, subject);
            GenerateDeserializeHeadMethod(injectorType, subject);
            GenerateDeserializeHeadedMethod(injectorType, subject, ssi.Deriveds, ssi.IsRoot);
            GenerateDeserializeBodyMethod(injectorType, subject, ssi.FactoryInvocation);

            if(ssi.IsRoot)
            {
                GenerateRootDeserializeMethod(injectorType, subject);
            }
        }

        private readonly void GenerateRootDeserializeMethod(
            INamedTypeSymbol injectorType,
            INamedTypeSymbol subject
            )
        {
            var ssGlobalName = subject.ToGlobalDisplayString();

            var invocation = $"{HeadDeserializeMethodName}(inj, xmlFullNode, roschar.Empty, out result);";

            _sb.AppendLine($$"""
        public static void {{HeadDeserializeMethodName}}({{injectorType.ToGlobalDisplayString()}} inj, roschar xmlFullNode, out {{ssGlobalName}} result)
        {
{{_guards.RootBodyStatement("            ", invocation)}}
        }
""");
        }

        /// <summary>
        /// Разбор атрибутных членов. Голова к этому моменту уже прочитана, так что
        /// работы здесь ровно столько, сколько атрибутов объявлено: каждый ищется
        /// в голове по имени.
        ///
        /// Отсутствующий атрибут не трогает член вовсе - тот остаётся с тем значением,
        /// с которым его создал конструктор. Так же ведёт себя и BCL (проверено):
        /// "атрибута не было" и "атрибут был пустой" - разные случаи, и второй
        /// действительно присваивает пустую лексему.
        /// </summary>
        private readonly void GenerateDeserializeAttributesMethod(
            INamedTypeSymbol injectorType,
            INamedTypeSymbol subject
            )
        {
            if (subject.IsAbstract)
            {
                //экземпляра такого типа не бывает, присваивать нечему
                return;
            }

            SplitMembers(subject, out _, out var attributes, out _);
            if (attributes.Count == 0)
            {
                return;
            }

            _sb.AppendLine($$"""
        private static void {{DeserializeAttributesMethodName}}({{injectorType.ToGlobalDisplayString()}} inj, ref {{XmlHeadFullName}} xmlNode, {{subject.ToGlobalDisplayString()}} result)
        {
            if(!xmlNode.{{nameof(XmlHead.HasAttributes)}})
            {
                return;
            }

            var attributeSearchStart = xmlNode.{{nameof(XmlHead.DeclaredNodeType)}}.Length + 1;
""");

            if (attributes.Count > 1)
            {
                GenerateDeserializeAttributesLoop(attributes);
            }
            else
            {
                GenerateDeserializeAttributesLookup(attributes[0]);
            }

            _sb.AppendLine($$"""
        }

""");
        }

        /// <summary>
        /// Один <c>[XmlAttribute]</c>-член: адресный поиск по имени. Слияние
        /// здесь не окупается - поиск останавливается на найденном атрибуте,
        /// а обход перечислением дошёл бы до конца головы.
        /// </summary>
        private readonly void GenerateDeserializeAttributesLookup(ISymbol member)
        {
            var memberType = ParseMember(member);
            var valueExpression = $"{member.Name}Attribute.{nameof(ParsedAttribute.Value)}";

            _sb.AppendLine($$"""
            //{{memberType.ToGlobalDisplayString()}} {{member.Name}}
            {{XmlScanFullName}}.{{nameof(XmlScan.ParseAttribute)}}(xmlNode.{{nameof(XmlHead.FullHead)}}, attributeSearchStart, roschar.Empty, "{{member.GetXmlAttributeName()}}".AsSpan(), roschar.Empty, out var {{member.Name}}Attribute);
            if(!{{member.Name}}Attribute.{{nameof(ParsedAttribute.IsEmpty)}})
            {
                {{GenerateAttributeAssignment(member, memberType, valueExpression, "                ")}}
            }
""");
        }

        /// <summary>
        /// Два и больше <c>[XmlAttribute]</c>-члена: голова разбирается один
        /// раз, и каждый разобранный атрибут раздаётся по имени.
        ///
        /// Прежняя форма звала <c>ParseAttribute</c> на каждый член, и каждый
        /// раз с начала головы: чтобы дойти до третьего атрибута, надо разобрать
        /// первые два - они и разбирались заново. На типе из трёх атрибутных
        /// членов это стоило порядка сорока процентов времени разбора
        /// (README, «Cost of the <c>xsi:type</c> lookup»).
        ///
        /// Флаг <c>{Member}Found</c> сохраняет прежнее «побеждает первое
        /// совпадение»: <c>id="1" id="2"</c> - не well-formed XML, его ловит
        /// <see cref="XmlGuard.UniqueAttributes"/>, но хост без стража обязан
        /// вести себя как раньше, а не начать брать последнее.
        /// </summary>
        private readonly void GenerateDeserializeAttributesLoop(List<ISymbol> attributes)
        {
            foreach (var member in attributes)
            {
                _sb.AppendLine($$"""
            var {{member.Name}}Found = false;
""");
            }

            _sb.AppendLine($$"""
            while({{XmlScanFullName}}.{{nameof(XmlScan.NextAttribute)}}(xmlNode.{{nameof(XmlHead.FullHead)}}, ref attributeSearchStart, out var attribute))
            {
""");

            foreach (var member in attributes)
            {
                var memberType = ParseMember(member);
                var valueExpression = $"{member.Name}Value";

                //строковому члену значение нужно строкой, и декодер отдаёт её сразу:
                //через спан строка на значении со ссылкой строилась бы дважды
                var isString = memberType.Symbol.SpecialType == SpecialType.System_String
                    && !memberType.IsEnum
                    && !IsBase64Binary(member, memberType);
                var decode = isString
                    ? nameof(XmlScan.DecodeAttributeValueToString)
                    : nameof(XmlScan.DecodeAttributeValue);

                _sb.AppendLine($$"""
                //{{memberType.ToGlobalDisplayString()}} {{member.Name}}
                if(!{{member.Name}}Found && attribute.{{nameof(ParsedAttribute.Name)}}.SequenceEqual("{{member.GetXmlAttributeName()}}".AsSpan()))
                {
                    {{member.Name}}Found = true;
                    var {{valueExpression}} = {{XmlScanFullName}}.{{decode}}(attribute.{{nameof(ParsedAttribute.Value)}});
                    {{GenerateAttributeAssignment(member, memberType, valueExpression, "                    ", isString)}}
                    continue;
                }
""");
            }

            _sb.AppendLine($$"""
            }
""");
        }

        /// <summary>
        /// Присвоение члену уже добытого значения атрибута. Общее для обеих
        /// форм разбора: отличается только откуда взялся
        /// <paramref name="valueExpression"/>.
        /// </summary>
        private readonly string GenerateAttributeAssignment(
            ISymbol member,
            TypeSymbol memberType,
            string valueExpression,
            string bodyIndent,
            bool valueIsString = false
            )
        {
            if (valueIsString)
            {
                //значение уже строка (DecodeAttributeValueToString): страж смотрит
                //на её спан, присваивание - без второго ToString()
                return _guards.CheckSpanStatementBefore(bodyIndent, valueExpression + ".AsSpan()")
                    + $"result.{member.CsName()} = {valueExpression};";
            }

            if (memberType.IsEnum)
            {
                return $"result.{member.CsName()} = {GenerateEnumParseStatement(memberType, valueExpression)};";
            }

            if (IsBase64Binary(member, memberType))
            {
                return $"result.{member.CsName()} = {XmlBase64FullName}.{nameof(XmlBase64.Decode)}({valueExpression});";
            }

            if (memberType.Symbol.SpecialType == SpecialType.System_String)
            {
                //разбирать уже нечего: значение приходит после нормализации
                //§3.3.3 и раскрытия ссылок, а ParseBody - это декодер текста
                //тела, и второй проход по уже раскрытому значению спотыкался бы
                //о литеральный '<', пришедший из &lt;
                return _guards.CheckSpanStatementBefore(bodyIndent, valueExpression)
                    + $"result.{member.CsName()} = {valueExpression}.ToString();";
            }

            return $"inj.{nameof(IInjector.ParseBody)}({valueExpression}, out {memberType.GlobalName} {member.Name}Parsed);\r\n"
                + bodyIndent + $"result.{member.CsName()} = {member.Name}Parsed;";
        }

        /// <summary>
        /// Вызов разбора атрибутов - или пустая строка, если у типа их нет.
        /// </summary>
        private readonly string GenerateDeserializeAttributesInvocation(
            string indent,
            ITypeSymbol type,
            string headVarName,
            string resultExpression
            )
        {
            //у абстрактного типа метода нет (см. GenerateDeserializeAttributesMethod):
            //его атрибутные члены присваивает перегрузка наследника, в которую
            //диспетчеризация уже завела
            if (type.IsAbstract || !HasXmlAttributeMembers(type))
            {
                return "";
            }

            var className = DetermineClassName(type);

            return $"\r\n{indent}{className}.{DeserializeAttributesMethodName}(inj, ref {headVarName}, {resultExpression});";
        }

        /// <summary>
        /// Точка входа "курсор стоит на открывающем теге, голова ещё не прочитана".
        /// Читает голову и передаёт дальше; сколько съедено, вызывающему не нужно.
        /// </summary>
        private readonly void GenerateDeserializeHeadMethod(
            INamedTypeSymbol injectorType,
            INamedTypeSymbol subject
            )
        {
            var ssGlobalName = subject.ToGlobalDisplayString();

            _sb.AppendLine($$"""
        private static void {{HeadDeserializeMethodName}}({{injectorType.ToGlobalDisplayString()}} inj, roschar fullNode, roschar xmlnsAttributeName, out {{ssGlobalName}} result)
        {
            {{XmlHeadFullName}} xmlNode = new();
            {{XmlScanFullName}}.{{_binding.ReadHead}}({{_binding.ReadHeadInvocation("fullNode", "xmlnsAttributeName", "xmlNode")}});{{_guards.AfterReadHeadStatement("            ", "xmlNode")}}{{_guards.RootHeadStatement("            ", "xmlNode")}}
            var xmlNodeBody = xmlNode.{{nameof(XmlHead.IsBodyless)}} ? roschar.Empty : fullNode.Slice(xmlNode.{{nameof(XmlHead.TotalLength)}});
            {{HeadedDeserializeMethodName}}(inj, ref xmlNode, xmlNodeBody, out result, {{_guards.RootBodyConsumedArgument}});{{_guards.RootTailStatement("            ", "fullNode", "xmlNode")}}
        }
""");
        }

        /// <summary>
        /// Точка входа "голова уже прочитана". Проверяет тип, при необходимости
        /// диспетчеризует по xsi:type и сообщает, сколько тела съедено вместе с
        /// закрывающим тегом.
        /// </summary>
        private readonly void GenerateDeserializeHeadedMethod(
            INamedTypeSymbol injectorType,
            INamedTypeSymbol subject,
            List<INamedTypeSymbol> derived,
            bool isRoot
            )
        {
            var ssGlobalName = subject.ToGlobalDisplayString();

            var typeName = subject.GetXmlTypeName();
            var rootName = subject.GetXmlRootName();

            //корень с собственным именем приходит сюда же, что и тот же тип внутри
            //чужого документа, поэтому подходят оба имени. Второе сравнение появляется
            //в коде, только когда имена действительно разошлись
            var nameCheck = $"!xmlNodePreciseType.SequenceEqual(\"{typeName}\".AsSpan())";
            if (isRoot && rootName != typeName)
            {
                nameCheck += $"\r\n                && !xmlNodePreciseType.SequenceEqual(\"{rootName}\".AsSpan())";
            }

            _sb.AppendLine($$"""
        private static void {{HeadedDeserializeMethodName}}({{injectorType.ToGlobalDisplayString()}} inj, ref {{XmlHeadFullName}} xmlNode, roschar body, out {{ssGlobalName}} result, out int bodyConsumed)
        {
            var xmlNodePreciseType = xmlNode.{{_binding.PreciseNodeTypeAccessor(derived.Count > 0)}}();
            if(xmlNodePreciseType.IsEmpty)
            {
                xmlNodePreciseType = xmlNode.{{nameof(XmlHead.DeclaredNodeType)}};
            }

            if({{nameCheck}})
            {
""");

            GenerateDeserializeDispatch(injectorType, derived);

            _sb.AppendLine($$"""

                throw new InvalidOperationException("(1) Unknown type " + xmlNodePreciseType.ToString());
            }

            {{HeadlessDeserializeMethodName}}(inj, body, xmlNode.{{nameof(XmlHead.XmlnsAttributeName)}}{{_guards.ExpectedEndNameArgument("xmlNode")}}, out result, out bodyConsumed);{{GenerateDeserializeAttributesInvocation("            ", subject, "xmlNode", "result")}}
        }
""");
        }

        private readonly void GenerateDeserializeDispatch(
            INamedTypeSymbol injectorType,
            List<INamedTypeSymbol> derived
            )
        {
            foreach (var d in derived)
            {
                var classAndMethodName = DetermineClassName(d) + "." + HeadlessDeserializeMethodName;

                _sb.AppendLine($$"""
                //{{nameof(GenerateDeserializeDispatch)}}
                if (xmlNodePreciseType.SequenceEqual("{{d.GetXmlTypeName()}}".AsSpan()))
                {
                    {{classAndMethodName}}(inj, body, xmlNode.{{nameof(XmlHead.XmlnsAttributeName)}}{{_guards.ExpectedEndNameArgument("xmlNode")}}, out {{d.ToGlobalDisplayString()}} iresult, out bodyConsumed);{{GenerateDeserializeAttributesInvocation("                    ", d, "xmlNode", "iresult")}}
                    result = iresult;
                    return;
                }
""");
            }
        }

        /// <summary>
        /// Разбор тела. Курсор идёт вперёд по детям; сколько занял очередной
        /// ребёнок, сообщает его собственный разбор - заранее это никто не считает.
        /// Цикл останавливается на своём закрывающем теге, его длина входит в
        /// bodyConsumed.
        /// </summary>
        private readonly void GenerateDeserializeBodyMethod(
            INamedTypeSymbol injectorType,
            INamedTypeSymbol subject,
            string? factoryInvocation
            )
        {
            var ssGlobalName = subject.ToGlobalDisplayString();

            _sb.AppendLine($$"""
        private static void {{HeadlessDeserializeMethodName}}({{injectorType.ToGlobalDisplayString()}} inj, roschar body, roschar xmlnsAttributeName{{_guards.ExpectedEndNameParameter}}, out {{ssGlobalName}} result, out int bodyConsumed)
        {
""");

            if (subject.IsAbstract)
            {
                _sb.AppendLine($$"""
            throw new InvalidOperationException("Cannot instanciate abstract class {{ssGlobalName}}");
        }
""");
                return;
            }

            if (!string.IsNullOrEmpty(factoryInvocation))
            {
                _sb.AppendLine($$"""
            result = {{factoryInvocation}};
""");
            }
            else
            {
                //проверка стоит ровно здесь, у места, которое её и нарушает: с фабрикой
                //никакого new T() не пишется, и required-член ничему не мешает
                if (TryFindUnassignableRequiredMember(subject, out var requiredMember))
                {
                    throw new GenerationRefusedException(
                        $"{subject.ToFullDisplayString()}.{requiredMember.Name} is a required member:"
                        + $" the generated deserializer creates the type with new {subject.Name}()"
                        + $" and cannot satisfy it (CS9035)."
                        + $" Drop 'required', or add a parameterless constructor marked with [SetsRequiredMembers],"
                        + $" or supply the instance with [XmlFactory]"
                        );
                }

                _sb.AppendLine($$"""
            result = new {{ssGlobalName}}();
""");
            }

            GenerateDeserializeMembers(subject);

            _sb.AppendLine($$"""
        }
""");
        }

        private readonly void GenerateDeserializeMembers(
            INamedTypeSymbol subject
            )
        {
            //атрибутные члены разбираются из головы, а сюда приходит одно тело -
            //искать их среди детей нечего
            SplitMembers(subject, out var members, out _, out var text);

            if (text is not null)
            {
                GenerateDeserializeTextMember(text);
                return;
            }

            foreach (var member in members)
            {
                _sb.AppendLine($$"""
            var {{member.Name}}Span = "{{GetMemberElementName(member)}}".AsSpan();
""");
            }

            _sb.AppendLine($$"""
            bodyConsumed = 0;
            var cursor = body;
            {{XmlHeadFullName}} child = new();
            while(true)
            {
                {{XmlScanFullName}}.{{_binding.ReadHead}}({{_binding.ReadHeadInvocation("cursor", "xmlnsAttributeName", "child")}});{{_guards.AfterReadHeadStatement("                ", "child")}}
                if(child.{{nameof(XmlHead.IsEmpty)}})
                {{{_guards.EndOfInputStatement("                    ", "!expectedEndName.IsEmpty")}}
                    break;
                }
                if(child.{{nameof(XmlHead.IsEndTag)}})
                {
                    //наш собственный закрывающий тег - тело кончилось{{_guards.EndTagStatement("                    ", "child", "expectedEndName")}}
                    bodyConsumed += child.{{nameof(XmlHead.TotalLength)}};
                    break;
                }
                var childDeclaredNodeType = child.{{nameof(XmlHead.DeclaredNodeType)}};
                var childBody = child.{{nameof(XmlHead.IsBodyless)}} ? roschar.Empty : cursor.Slice(child.{{nameof(XmlHead.TotalLength)}});
                int childConsumed;

""");

            //закрытая нода <Foo/> раньше отсекалась здесь, до разбора имени, и член
            //не получал ничего. Но <Foo/> - это не "члена не было", а "значение пустое":
            //именно так System.Xml.Serialization пишет и пустую строку, и пустую
            //коллекцию, так что оба читались как null. Теперь имя сверяется всегда,
            //а что делать с пустым телом, решает ветка конкретного члена: строке и
            //коллекции есть что присвоить, числу - нечего.
            var isFirstMember = true;
            foreach (var member in members)
            {
                var memberType = ParseMember(member);

                GenerateDeserializeMember(member, memberType, isFirstMember);
                isFirstMember = false;
            }

            //у типа без единого разбираемого члена цепочки нет вовсе, и хвост
            //не может начинаться с else
            var skipStatement =
                $"childConsumed = child.{nameof(XmlHead.IsBodyless)}"
                + $" ? 0"
                + $" : {XmlScanFullName}.{_binding.SkipBody}({_binding.SkipBodyInvocation("childBody", "false")});";

            if (isFirstMember)
            {
                _sb.AppendLine($$"""
                //у этого типа нет разбираемых членов - любому элементу достаточно
                //досчитать баланс тегов
                {{skipStatement}}
""");
            }
            else
            {
                _sb.AppendLine($$"""
                else
                {
                    //этому элементу не соответствует ни один член - разбирать нечего,
                    //достаточно досчитать баланс тегов
                    {{skipStatement}}
                }
""");
            }

            _sb.AppendLine($$"""

                var childStep = child.{{nameof(XmlHead.TotalLength)}} + childConsumed;
                bodyConsumed += childStep;
                cursor = cursor.Slice(childStep);
            }
""");
        }

        /// <summary>
        /// Член с <see cref="XmlTextAttribute"/> забирает тело целиком, поэтому
        /// цикла по детям здесь нет вовсе: детей у такого типа и не бывает
        /// (см. <see cref="SplitMembers"/>).
        ///
        /// Пустое тело - <c>&lt;Foo/&gt;</c> и <c>&lt;Foo&gt;&lt;/Foo&gt;</c> -
        /// члена не касается: у BCL оба случая оставляют его null, а не пустой
        /// строкой (проверено), и это ровно противоположно тому, как тот же BCL
        /// читает пустой элемент обычного строкового члена.
        /// </summary>
        private readonly void GenerateDeserializeTextMember(
            ISymbol member
            )
        {
            var memberType = ParseMember(member);

            string assignment;
            if (memberType.IsEnum)
            {
                assignment = $"result.{member.CsName()} = {GenerateEnumParseStatement(memberType, "bodyText")};";
            }
            else if (IsBase64Binary(member, memberType))
            {
                assignment = $"result.{member.CsName()} = {XmlBase64FullName}.{nameof(XmlBase64.Decode)}(bodyText);";
            }
            else
            {
                var isString = memberType.Symbol.SpecialType == SpecialType.System_String;

                assignment =
                    _binding.ParseBodyStatement(
                        "bodyText",
                        isString,
                        memberType.GlobalName,
                        "bodyParsed"
                        )
                    + _guards.CheckStringStatement("                ", isString, "bodyParsed")
                    + $"\r\n                result.{member.CsName()} = bodyParsed;";
            }

            _sb.AppendLine($$"""
            //{{typeof(XmlTextAttribute).Name}} {{memberType.ToGlobalDisplayString()}} {{member.Name}}
            {{XmlScanFullName}}.{{_binding.ReadTextBody}}({{_binding.ReadTextBodyInvocation("body", "body.IsEmpty", _guards.TextBodyExpectedName("roschar.Empty"), "bodyText", "bodyConsumed")}});
            if(!bodyText.IsEmpty)
            {
                {{assignment}}
            }
""");
        }

        private readonly void GenerateDeserializeMember(
            ISymbol member,
            TypeSymbol memberType,
            bool isFirstMember
            )
        {
            var elseif = isFirstMember ? "" : "else ";

            //спутник XxxSpecified взводится по факту встречи элемента, а не по итогу
            //разбора его тела: смысл флага - "член в документе был", и BCL ставит его
            //так же. Ветка, в которую он попадает, у каждого типа члена своя, поэтому
            //он подставляется первой же строкой в тело - до любых её собственных условий
            var specified = member.GetSpecifiedCompanion();
            var specifiedAssignment = specified is null
                ? ""
                : $"result.{specified.CsName()} = true;";

            //пустое тело осмысленно не для всякого типа. У строки пустое лексическое
            //представление есть, и <Foo/> - это "", а не отсутствие значения. У числа,
            //Guid, DateTime и перечисления его нет: разбор пустоты закончился бы
            //исключением, поэтому такой элемент по-прежнему пропускается.
            var isString = SymbolEqualityComparer.Default.Equals(
                memberType.Symbol,
                _compilation.String()
                );

            if(BuiltinSourceProducer.TryGetBuiltin(_compilation, memberType.Symbol, out _))
            {
                if (isString)
                {
                    _sb.AppendLine($$"""
                    //{{memberType.ToGlobalDisplayString()}}  {{member.Name}}
                    {{elseif}}if(childDeclaredNodeType.SequenceEqual({{member.Name}}Span))
                    {
                        {{specifiedAssignment}}
                        if(child.{{nameof(XmlHead.IsBodyless)}} && child.{{_binding.IsNil}}())
                        {
                            //<Foo xsi:nil="true"/> - значения нет, член остаётся null
                            childConsumed = 0;
                        }
                        else
                        {
                            //ReadTextBody на закрытой ноде отдаёт пустой текст и consumed = 0,
                            //так что <Foo/> здесь превращается в пустую строку
                            {{GenerateReadTextBodyStatement("childBody", "child", "childText", "childConsumed")}}
                            {{_binding.ParseBodyStatement("childText", true, memberType.GlobalName, "injr")}}{{_guards.CheckStringStatement("                            ", true, "injr")}}
                            result.{{member.CsName()}} = injr;
                        }
                    }
""");
                }
                else
                {
                    _sb.AppendLine($$"""
                    //{{memberType.ToGlobalDisplayString()}}  {{member.Name}}
                    {{elseif}}if(childDeclaredNodeType.SequenceEqual({{member.Name}}Span))
                    {
                        {{specifiedAssignment}}
                        if(child.{{nameof(XmlHead.IsBodyless)}})
                        {
                            childConsumed = 0;
                        }
                        else
                        {
                            {{GenerateReadTextBodyStatement("childBody", "child", "childText", "childConsumed")}}
                            inj.{{nameof(IInjector.ParseBody)}}(childText, out {{memberType.GlobalName}} injr);
                            result.{{member.CsName()}} = injr;
                        }
                    }
""");
                }
            }
            else if (memberType.IsEnum)
            {
                var fullParserInvocation = GenerateEnumParseStatement(
                    memberType,
                    "childText"
                    );

                _sb.AppendLine($$"""
                    //Enum
                    {{elseif}}if(childDeclaredNodeType.SequenceEqual({{member.Name}}Span))
                    {
                        {{specifiedAssignment}}
                        if(child.{{nameof(XmlHead.IsBodyless)}})
                        {
                            childConsumed = 0;
                        }
                        else
                        {
                            {{GenerateReadTextBodyStatement("childBody", "child", "childText", "childConsumed")}}
                            result.{{member.CsName()}} = {{fullParserInvocation}};
                        }
                    }
""");

            }
            else if (IsBase64Binary(member, memberType))
            {
                _sb.AppendLine($$"""
                    //base64Binary {{memberType.ToGlobalDisplayString()}}  {{member.Name}}
                    {{elseif}}if(childDeclaredNodeType.SequenceEqual({{member.Name}}Span))
                    {
                        {{specifiedAssignment}}
                        if(child.{{nameof(XmlHead.IsBodyless)}} && child.{{_binding.IsNil}}())
                        {
                            //<Foo xsi:nil="true"/> - массива нет вовсе, а не пустой массив
                            childConsumed = 0;
                        }
                        else
                        {
                            //пустое тело - это byte[0], а не null: и <Foo/>, и <Foo></Foo>
                            //BCL читает именно в пустой массив (проверено)
                            {{GenerateReadTextBodyStatement("childBody", "child", "childText", "childConsumed")}}
                            result.{{member.CsName()}} = {{XmlBase64FullName}}.{{nameof(XmlBase64.Decode)}}(childText);
                        }
                    }
""");
            }
            //TODO other collections?
            else if (memberType.IsCollection(out var collectionItemType))
            {
                var listItemType = new TypeSymbol(
                    _compilation,
                    collectionItemType!
                    );

                const string child2VarName = "child2";
                const string listItemParseResultVarName = "iresult";

                var listItemParseStatement = GenerateListItemParseStatement(
                    child2VarName,
                    listItemType,
                    listItemParseResultVarName,
                    member.GetXmlArrayItemName()
                    );

                var poolVarName = "pool";

                var assignStatement = GenerateAssignStatement(
                    member,
                    memberType,
                    poolVarName
                    );

                var poolDeclarationStatement = GeneratePoolDeclarationStatement(
                    member,
                    memberType,
                    listItemType,
                    poolVarName
                    );

                _sb.AppendLine($$"""
                    //List<T>
                    {{elseif}}if(childDeclaredNodeType.SequenceEqual({{member.Name}}Span))
                    {
                        {{specifiedAssignment}}
                        if(child.{{nameof(XmlHead.IsBodyless)}} && child.{{_binding.IsNil}}())
                        {
                            //<Foo xsi:nil="true"/> - коллекции нет вовсе, а не пустая коллекция
                            childConsumed = 0;
                        }
                        else
                        {
                        {{poolDeclarationStatement}}

                        var itemCursor = childBody;
                        var itemConsumed = 0;
                        {{XmlHeadFullName}} {{child2VarName}} = new();
                        while(true)
                        {
                            {{XmlScanFullName}}.{{_binding.ReadHead}}({{_binding.ReadHeadInvocation("itemCursor", $"child.{nameof(XmlHead.XmlnsAttributeName)}", child2VarName)}});{{_guards.AfterReadHeadStatement("                            ", child2VarName)}}
                            if({{child2VarName}}.{{nameof(XmlHead.IsEmpty)}})
                            {{{_guards.EndOfInputStatement("                                ", "!child." + nameof(XmlHead.IsBodyless))}}
                                break;
                            }
                            if({{child2VarName}}.{{nameof(XmlHead.IsEndTag)}})
                            {{{_guards.EndTagStatement("                                ", child2VarName, "childDeclaredNodeType")}}
                                itemConsumed += {{child2VarName}}.{{nameof(XmlHead.TotalLength)}};
                                break;
                            }
                            var child2Step = {{child2VarName}}.{{nameof(XmlHead.TotalLength)}};
                            if({{child2VarName}}.{{nameof(XmlHead.IsBodyless)}} && {{child2VarName}}.{{_binding.IsNil}}())
                            {
                                //<string xsi:nil="true"/> - элемент есть, а значения нет
                                {{poolVarName}}.Add(default!);
                            }
                            else
                            {
                                //<string/> и <Point/> - это элементы с пустым телом, а не их
                                //отсутствие: System.Xml.Serialization так пишет пустую строку
                                //и объект без записанных членов. Раньше закрытая нода
                                //пропускалась, и коллекция читалась короче, чем была
                                var child2Body = {{child2VarName}}.{{nameof(XmlHead.IsBodyless)}} ? roschar.Empty : itemCursor.Slice({{child2VarName}}.{{nameof(XmlHead.TotalLength)}});
                                int child2Consumed;
                                {{listItemParseStatement}}
                                {{poolVarName}}.Add({{listItemParseResultVarName}});
                                child2Step += child2Consumed;
                            }

                            itemConsumed += child2Step;
                            itemCursor = itemCursor.Slice(child2Step);
                        }

                        childConsumed = itemConsumed;
                        {{assignStatement}}
                        }
                    }
""");
            }
            else
            {
                //здесь, вероятно, какой-то другой тип

                if (!SerializationInfoCollection.TryGetSubject(memberType.Symbol, out var subject))
                {
                    throw new InvalidOperationException($"(2) Unknown type {memberType.ToGlobalDisplayString()}");
                }

                var classAndMethodName = DetermineClassName(memberType.Symbol) + "." + HeadlessDeserializeMethodName;

                if (subject.Deriveds.Count > 0)
                {
                    //тут могут быть вариации, диспетчеризуем по xsi:type.
                    //Ключом остаётся имя члена: раньше эта ветка срабатывала на любого
                    //ребёнка с xsi:type независимо от имени, и объявление переменной
                    //перед else if не компилировалось, если член был не первым.
                    _sb.AppendLine($$"""
                    //custom type (with custom types dispatching)
                    {{elseif}}if(childDeclaredNodeType.SequenceEqual({{member.Name}}Span))
                    {
                        {{specifiedAssignment}}
                        var childPreciseType = child.{{_binding.GetPreciseNodeType}}();
""");

                    GenerateDeserializeDispatch2(subject.Deriveds, member.CsName());

                    _sb.AppendLine($$"""
                        else
                        {
                            {{classAndMethodName}}(inj, childBody, child.{{nameof(XmlHead.XmlnsAttributeName)}}{{_guards.ExpectedEndNameArgumentRaw("child", "childDeclaredNodeType")}}, out {{memberType.ToGlobalDisplayString()}} iresult, out childConsumed);{{GenerateDeserializeAttributesInvocation("                            ", memberType.Symbol, "child", "iresult")}}
                            result.{{member.CsName()}} = iresult;
                        }
                    }
""");
                }
                else
                {
                    //здесь всё четко, нет никаких вариаций типов и соотв. не должно быть типа в дочерней ноде

                    _sb.AppendLine($$"""
                    //custom type (no custom type dispatch)
                    {{elseif}}if(childDeclaredNodeType.SequenceEqual({{member.Name}}Span))
                    {
                        {{specifiedAssignment}}
                        {{classAndMethodName}}(inj, childBody, child.{{nameof(XmlHead.XmlnsAttributeName)}}{{_guards.ExpectedEndNameArgumentRaw("child", "childDeclaredNodeType")}}, out {{memberType.ToGlobalDisplayString()}} iresult, out childConsumed);{{GenerateDeserializeAttributesInvocation("                        ", memberType.Symbol, "child", "iresult")}}
                        result.{{member.CsName()}} = iresult;
                    }
""");

                }

            }
        }

        private readonly void GenerateDeserializeDispatch2(
            List<INamedTypeSymbol> derived,
            string memberName
            )
        {
            var first = true;
            foreach (var d in derived)
            {
                var classAndMethodName = DetermineClassName(d) + "." + HeadlessDeserializeMethodName;
                var elseif = first ? "" : "else ";
                first = false;

                _sb.AppendLine($$"""
                        //{{nameof(GenerateDeserializeDispatch2)}}
                        {{elseif}}if (childPreciseType.SequenceEqual("{{d.GetXmlTypeName()}}".AsSpan()))
                        {
                            {{classAndMethodName}}(inj, childBody, child.XmlnsAttributeName{{_guards.ExpectedEndNameArgumentRaw("child", "childDeclaredNodeType")}}, out {{d.ToGlobalDisplayString()}} iresult, out childConsumed);{{GenerateDeserializeAttributesInvocation("                            ", d, "child", "iresult")}}
                            result.{{memberName}} = iresult;
                        }
""");
            }
        }

        private readonly string GenerateReadTextBodyStatement(
            string bodyVarName,
            string headVarName,
            string textVarName,
            string consumedVarName
            )
        {
            return
                $"{XmlScanFullName}.{_binding.ReadTextBody}("
                + _binding.ReadTextBodyInvocation(
                    bodyVarName,
                    $"{headVarName}.{nameof(XmlHead.IsBodyless)}",
                    $"{headVarName}.{nameof(XmlHead.DeclaredNodeType)}",
                    textVarName,
                    consumedVarName
                    )
                + ");";
        }

        /// <summary>
        /// Член типа <c>List&lt;T&gt;</c> с сеттером и <c>T[]</c> копятся в
        /// <see cref="PooledArrayBuilder{T}"/>: промежуточные буферы из пула,
        /// на выходе один массив нужной длины (у списка - его внутренний).
        /// Без сеттера наполняется экземпляр, который создал конструктор.
        /// </summary>
        private readonly string GeneratePoolDeclarationStatement(
            ISymbol member,
            TypeSymbol memberType,
            TypeSymbol listItemType,
            string poolVarName
            )
        {
            if (memberType.IsList(out _))
            {
                if (HasNoSetter(member))
                {
                    //наполняется тот экземпляр, что создал конструктор. Не создал -
                    //наполнять нечего, и член остаётся null: BCL в этом случае
                    //поступает так же, а не создаёт список за пользователя
                    return
                        $"var {poolVarName} = result.{member.CsName()}"
                        + $" ?? new global::System.Collections.Generic.List<{listItemType.ToGlobalDisplayString()}>();";
                }

                return $"var {poolVarName} = new {PooledArrayBuilderFullName}<{listItemType.ToGlobalDisplayString()}>();";
            }
            else if (memberType.IsArray(out _))
            {
                return $"var {poolVarName} = new {PooledArrayBuilderFullName}<{listItemType.ToGlobalDisplayString()}>();";
            }

            throw new InvalidOperationException($"Unknown type {memberType.ToGlobalDisplayString()}");
        }

        private readonly string GenerateAssignStatement(
            ISymbol member,
            TypeSymbol memberType,
            string poolVarName
            )
        {
            if (memberType.IsList(out _))
            {
                if (HasNoSetter(member))
                {
                    //пул и есть сам член - присваивать нечего и нечем
                    return "";
                }

                return $"result.{member.CsName()} = {poolVarName}.{nameof(PooledArrayBuilder<int>.ToListAndRelease)}();";
            }
            else if (memberType.IsArray(out _))
            {
                return $"result.{member.CsName()} = {poolVarName}.{nameof(PooledArrayBuilder<int>.ToArrayAndRelease)}();";
            }

            throw new InvalidOperationException($"Unknown type {memberType.ToGlobalDisplayString()}");
        }

        private static bool HasNoSetter(ISymbol member)
        {
            return member switch
            {
                IPropertySymbol property => property.SetMethod is null || property.SetMethod.IsInitOnly,
                IFieldSymbol field => field.IsReadOnly,
                _ => false,
            };
        }

        private readonly string GenerateListItemParseStatement(
            string child2VarName,
            TypeSymbol listItemType,
            string listItemParseResultVarName,
            string? itemName
            )
        {
            if (listItemType.IsEnum)
            {
                var enumParseStatement = GenerateEnumParseStatement(
                    listItemType,
                    "child2Text"
                    );

                return
                    GenerateReadTextBodyStatement("child2Body", child2VarName, "child2Text", "child2Consumed")
                    + $"\r\n                            var {listItemParseResultVarName} = {enumParseStatement};";
            }
            else if (BuiltinSourceProducer.TryGetBuiltin(_compilation, listItemType.Symbol, out _))
            {
                var isString = listItemType.Symbol.SpecialType == SpecialType.System_String;

                return
                    GenerateReadTextBodyStatement("child2Body", child2VarName, "child2Text", "child2Consumed")
                    + "\r\n                            "
                    + _binding.ParseBodyStatement(
                        "child2Text",
                        isString,
                        listItemType.GlobalName,
                        listItemParseResultVarName
                        )
                    + _guards.CheckStringStatement("                            ", isString, listItemParseResultVarName);
            }
            else if (itemName is not null)
            {
                //элемент назван вручную через XmlArrayItem: головной метод сверяет имя
                //с именем типа и такой элемент отверг бы. Наследников у типа быть не
                //может - запись с ними и ручным именем отвергается, - поэтому
                //диспетчеризации по xsi:type здесь нет, только сверка имени
                var headless = DetermineClassName(listItemType.Symbol) + "." + HeadlessDeserializeMethodName;
                var itemGlobalName = listItemType.ToGlobalDisplayString();

                return $$"""
if(!{{child2VarName}}.{{nameof(XmlHead.DeclaredNodeType)}}.SequenceEqual("{{itemName}}".AsSpan()))
                                {
                                    throw new InvalidOperationException("(3) Unknown type " + {{child2VarName}}.{{nameof(XmlHead.DeclaredNodeType)}}.ToString());
                                }
                                {{headless}}(inj, child2Body, {{child2VarName}}.{{nameof(XmlHead.XmlnsAttributeName)}}{{_guards.ExpectedEndNameArgument(child2VarName)}}, out {{itemGlobalName}} {{listItemParseResultVarName}}, out child2Consumed);{{GenerateDeserializeAttributesInvocation("                                ", listItemType.Symbol, child2VarName, listItemParseResultVarName)}}
""";
            }
            else
            {
                var classAndMethodName = DetermineClassName(listItemType.Symbol) + "." + HeadedDeserializeMethodName;

                return $@"{classAndMethodName}(inj, ref {child2VarName}, child2Body, out {listItemType.ToGlobalDisplayString()} {listItemParseResultVarName}, out child2Consumed);";
            }
        }

        /// <summary>
        /// Emits a parse expression for an enum-typed value. Uses a chain of
        /// span SequenceEqual comparisons against each declared member's literal
        /// name instead of (T)Enum.Parse(typeof(T), span): Enum.Parse returns
        /// object, so the parsed value is boxed on every single call, whereas
        /// comparing against literal names and returning the enum constant
        /// directly never boxes. Falls back to Enum.Parse for anything that
        /// doesn't match a declared name (keeps error behavior identical for
        /// malformed/unknown input).
        ///
        /// The fallback materializes the span into a string, because
        /// Enum.Parse(Type, ReadOnlySpan&lt;char&gt;) does not exist on
        /// netstandard2.0 and the generated code has to compile under every
        /// target framework the consumer may pick. The extra allocation is
        /// confined to a path that already boxes and that any declared name
        /// never reaches.
        /// </summary>
        private readonly string GenerateEnumParseStatement(
            TypeSymbol memberType,
            string varName
            )
        {
            var enumType = (INamedTypeSymbol)memberType.Symbol;
            var enumGlobalName = memberType.ToGlobalDisplayString();

            var sb = new StringBuilder();
            sb.Append('(');

            foreach (var field in enumType.GetMembers().OfType<IFieldSymbol>())
            {
                if (!field.HasConstantValue)
                {
                    continue;
                }

                sb.Append(varName);
                sb.Append(".SequenceEqual(\"");
                sb.Append(field.GetXmlEnumName());
                sb.Append("\".AsSpan()) ? ");
                sb.Append(enumGlobalName);
                sb.Append('.');
                sb.Append(field.Name);
                sb.Append(" : ");
            }

            sb.Append('(');
            sb.Append(enumGlobalName);
            sb.Append(")Enum.Parse(typeof(");
            sb.Append(enumGlobalName);
            sb.Append("), ");
            sb.Append(varName);
            sb.Append(".ToString()))");

            return sb.ToString();
        }

        #endregion

        /// <summary>
        /// Члены, которые вообще участвуют в обмене, в том порядке, в котором они
        /// попадут в документ.
        /// </summary>
        private readonly List<ISymbol> FilterMembers(
            List<ISymbol> members
            ) => FilterMembers(_compilation, members);

        /// <summary>
        /// Тот же состав членов, что увидит генерация, - но без самого продюсера.
        /// Обходчику графа фасада нужен ровно он: разойдись эти два перечисления,
        /// фасад зарегистрировал бы тип, чей граф обошёл не полностью.
        /// </summary>
        public static List<ISymbol> SelectSerializableMembers(
            Compilation compilation,
            INamedTypeSymbol type
            )
        {
            return FilterMembers(compilation, GetMembersBaseFirst(type));
        }

        /// <summary>
        /// Все члены-кандидаты - до всякого отбора, в том же порядке и с тем же
        /// разрешением скрытых через <c>new</c>, что и у
        /// <see cref="SelectSerializableMembers"/>.
        ///
        /// Нужно это одному месту - обходчику графа фасада: чтобы сверить наш отбор
        /// с отбором System.Xml.Serialization, обоим нужен один и тот же исходный
        /// список. Свой такой же список обходчик строить не может: разойдись они,
        /// сверка сравнивала бы не то с тем.
        /// </summary>
        public static List<ISymbol> SelectCandidateMembers(
            INamedTypeSymbol type
            )
        {
            return GetMembersBaseFirst(type);
        }

        /// <summary>
        /// Член, помеченный <c>required</c>, которого сгенерированный <c>new T()</c>
        /// задать не может.
        ///
        /// <c>required</c> - проверка компилятора, а не рефлексии: штатный
        /// <see cref="System.Xml.Serialization.XmlSerializer"/> такой тип
        /// сериализует и читает как ни в чём не бывало (проверено прогоном), а вот
        /// сгенерированный код на <c>new T()</c> не компилируется вовсе - CS9035.
        /// Причём неважно, участвует ли сам член в обмене: <c>[XmlIgnore]</c> на нём
        /// ошибку не снимает, требование задать его стоит на конструкторе.
        ///
        /// Единственное исключение - конструктор без параметров, помеченный
        /// <c>[SetsRequiredMembers]</c>: он обещает компилятору, что задал всё сам,
        /// и <c>new T()</c> становится законным.
        /// </summary>
        public static bool TryFindUnassignableRequiredMember(
            INamedTypeSymbol type,
            out ISymbol member
            )
        {
            member = null!;

            if (HasSetsRequiredMembersParameterlessConstructor(type))
            {
                return false;
            }

            //база первой: имя в диагностике не должно прыгать от того, с какой
            //стороны графа мы в этот тип пришли
            var layers = new List<INamedTypeSymbol>();
            for (var current = type; current is not null && current.BaseType is not null; current = current.BaseType)
            {
                layers.Add(current);
            }
            layers.Reverse();

            foreach (var layer in layers)
            {
                foreach (var candidate in layer.GetMembers())
                {
                    if (candidate is IPropertySymbol property && property.IsRequired)
                    {
                        member = property;
                        return true;
                    }
                    if (candidate is IFieldSymbol field && field.IsRequired)
                    {
                        member = field;
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool HasSetsRequiredMembersParameterlessConstructor(
            INamedTypeSymbol type
            )
        {
            foreach (var constructor in type.InstanceConstructors)
            {
                if (constructor.Parameters.Length != 0)
                {
                    continue;
                }

                foreach (var attribute in constructor.GetAttributes())
                {
                    if (attribute.AttributeClass?.ToFullDisplayString() == SetsRequiredMembersAttributeFullName)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private const string SetsRequiredMembersAttributeFullName =
            "System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute";

        private static List<ISymbol> FilterMembers(
            Compilation compilation,
            List<ISymbol> members
            )
        {
            var result = new List<ISymbol>(SelectMembers(compilation, members));

            //Order задаёт порядок элементов явно. Сортировка устойчивая, поэтому
            //члены без Order остаются там же, где были объявлены, а список, в котором
            //Order не поставил никто, не трогается вовсе
            if (result.Exists(m => m.GetXmlOrder() >= 0))
            {
                result = result
                    .OrderBy(m => m.GetXmlOrder() >= 0 ? m.GetXmlOrder() : int.MaxValue)
                    .ToList();
            }

            return result;
        }

        /// <summary>
        /// Свойство без сеттера, которое всё-таки участвует в обмене: разобранные
        /// элементы уходят в <c>Add</c> уже существующего экземпляра, присваивать
        /// ничего не нужно. Так же поступает и System.Xml.Serialization.
        ///
        /// Только <see cref="List{T}"/>, и это не упрощение: массив без сеттера BCL
        /// не сериализует вовсе - наполнить его через Add нельзя, а заменить нечем.
        /// Строку и сложный тип без сеттера он тоже пропускает. Проверено прогоном
        /// по типу со всеми четырьмя случаями сразу.
        /// </summary>
        private static bool IsFillableWithoutSetter(Compilation compilation, ISymbol member)
        {
            return ParseMember(compilation, member).IsList(out _);
        }

        /// <summary>
        /// Члены типа, разложенные по месту внутри элемента. Считается одинаково
        /// для записи и для чтения: разойдись эти два разложения - разошлись бы
        /// и написанное с прочитанным.
        /// </summary>
        private readonly void SplitMembers(
            INamedTypeSymbol type,
            out List<ISymbol> elements,
            out List<ISymbol> attributes,
            out ISymbol? text
            )
        {
            elements = new List<ISymbol>();
            attributes = new List<ISymbol>();
            text = null;

            foreach (var member in FilterMembers(GetMembersBaseFirst(type)))
            {
                switch (member.GetXmlPlacement())
                {
                    case XmlPlacement.Attribute:
                        CheckSimpleContentMember(member, typeof(XmlAttributeAttribute));
                        attributes.Add(member);
                        break;
                    case XmlPlacement.Text:
                        CheckSimpleContentMember(member, typeof(XmlTextAttribute));
                        if (text is not null)
                        {
                            throw new InvalidOperationException(
                                $"{type.Name} declares more than one {typeof(XmlTextAttribute).Name} member"
                                + $" ({text.Name} and {member.Name}): an element has only one body"
                                );
                        }
                        text = member;
                        break;
                    default:
                        elements.Add(member);
                        break;
                }
            }

            if (text is not null && elements.Count > 0)
            {
                //смешанное содержимое System.Xml.Serialization пишет и читает, а здесь
                //тело - это либо текст, либо дети, и промолчать значило бы выдать документ,
                //в котором текста просто нет
                throw new InvalidOperationException(
                    $"{type.Name} mixes a {typeof(XmlTextAttribute).Name} member ({text.Name})"
                    + $" with element members ({string.Join(", ", elements.Select(m => m.Name))}):"
                    + $" mixed content is not supported"
                    );
            }
        }

        /// <summary>
        /// И атрибут, и текст - это одна лексема без всякой разметки внутри, поэтому
        /// членам обоих видов доступны ровно те типы, у которых такая лексема есть.
        /// System.Xml.Serialization отказывается ровно так же и теми же словами
        /// ("XmlAttribute/XmlText cannot be used to encode complex types"), причём
        /// <see cref="Nullable{T}"/> он тоже считает сложным типом - проверено.
        /// </summary>
        private readonly void CheckSimpleContentMember(
            ISymbol member,
            Type attributeType
            )
        {
            var memberType = ParseMember(member);
            if (memberType.IsEnum)
            {
                return;
            }
            if (!memberType.IsNullableValueType
                && BuiltinSourceProducer.TryGetBuiltin(_compilation, memberType.Symbol, out _))
            {
                return;
            }
            if (IsBase64Binary(member, memberType))
            {
                //массив, но лексема у него всё-таки есть - одна строка base64.
                //BCL пропускает byte[] в атрибут и в текст ровно по той же причине
                return;
            }

            throw new InvalidOperationException(
                $"{attributeType.Name} on {member.Name} is not supported:"
                + $" {memberType.ToGlobalDisplayString()} has no plain lexical form"
                );
        }

        private static IEnumerable<ISymbol> SelectMembers(
            Compilation compilation,
            List<ISymbol> members
            )
        {
            foreach (var member in members)
            {
                //берётся только публичный член - ровно то, что делает
                //System.Xml.Serialization своим BindingFlags.Public (снято прогоном:
                //internal и protected internal он не видит так же, как private).
                //
                //Раньше здесь отсеивались только private и protected, и internal-член
                //уезжал в документ, которого у BCL на том же типе нет. В drop-in это
                //к тому же стоило ускорения целиком: сверка состава членов видела
                //лишний элемент и отказывалась от типа.
                if (member.DeclaredAccessibility != Accessibility.Public)
                {
                    continue;
                }
                if (member is IPropertySymbol property)
                {
                    //init-сеттер из сгенерированного кода недоступен так же, как его
                    //отсутствие: присвоить можно только в инициализаторе объекта
                    if ((property.SetMethod == null || property.SetMethod.IsInitOnly) && !IsFillableWithoutSetter(compilation, member))
                    {
                        continue;
                    }
                }
                else if (member is IFieldSymbol fieldSymbol)
                {
                    //readonly-поле System.Xml.Serialization пропускает, кроме коллекции,
                    //которую наполняет через Add уже существующего экземпляра
                    if (fieldSymbol.IsReadOnly && !IsFillableWithoutSetter(compilation, member))
                    {
                        continue;
                    }
                }
                else
                {
                    continue;
                }

                //XmlIgnore проверяется на любом члене, а не только на свойстве:
                //System.Xml.Serialization читает его и на поле тоже
                var ignoreAttribute = member.GetAttributes().FirstOrDefault(
                    a => a.AttributeClass != null && a.AttributeClass.ToFullDisplayString() == typeof(XmlIgnoreAttribute).FullName
                    );
                if (ignoreAttribute != null)
                {
                    continue;
                }

                yield return member;
            }
        }

        /// <summary>
        /// Имя элемента, под которым член попадает в документ. Считается один раз
        /// и одинаково для обеих сторон - иначе записанное и прочитанное разошлись бы.
        /// </summary>
        private readonly string GetMemberElementName(ISymbol member)
        {
            var memberType = ParseMember(member);

            //byte[] без явной обёртки коллекцией не считается: в документе это одна
            //лексема, и имя ему задаёт XmlElement, как всякому простому члену
            if (!memberType.IsCollection(out _) || IsBase64Binary(member, memberType))
            {
                return member.GetXmlElementName();
            }

            if (member.HasXmlElementAttribute())
            {
                //на коллекции XmlElement означает совсем другую форму - элементы
                //без обёртки вовсе, - и промолчать здесь значило бы выдать документ,
                //не совпадающий с тем, что написано на самом члене.
                //
                //Отказ, а не падение: причина названа словами вместе с именем типа
                //и члена, и стектрейс к ней ничего не добавляет - см. комментарий
                //у GenerationRefusedException
                throw new GenerationRefusedException(
                    $"{member.ContainingType.ToFullDisplayString()}.{member.Name}:"
                    + $" {typeof(XmlElementAttribute).Name} on a collection member"
                    + $" is not supported: it declares items without a wrapping element."
                    + $" Use {typeof(XmlArrayAttribute).Name} to rename the wrapper"
                    );
            }

            return member.GetXmlArrayName();
        }

        private readonly string DetermineClassName(ITypeSymbol type)
        {
            if (BuiltinSourceProducer.TryGetBuiltin(_compilation, type, out _))
            {
                return BuiltinSourceProducer.BuiltinCodeHelperNamespace + "." + BuiltinSourceProducer.BuiltinCodeHelperClassName;
            }
            else
            {
                return _targetGlobalName;
            }
        }

        private readonly void GenerateUsings()
        {
            var set = new HashSet<string>(_extraUsings);

            if (_deSubject is not null)
            {
                var foundUsings = new List<UsingDirectiveSyntax>();

                GrabUsings(_deSubject, ref foundUsings);

                set.UnionWith(
                    foundUsings.ConvertAll(u => u.WithoutTrivia().ToFullString())
                    );
            }

            foreach(var unit in set)
            {
                _sb.AppendLine(unit);
            }
        }

        private static void GrabUsings(
            INamedTypeSymbol symbol,
            ref List<UsingDirectiveSyntax> foundUsings
            )
        {
            foreach (var dsr in symbol.DeclaringSyntaxReferences)
            {
                var syntax = dsr.GetSyntax();

                var cus = syntax.Up<CompilationUnitSyntax>();
                if (cus == null)
                {
                    continue;
                }

                var fu = cus
                    .DescendantNodes()
                    .OfType<UsingDirectiveSyntax>()
                    .ToList();

                foundUsings.AddRange(fu);
            }
        }

        private readonly TypeSymbol ParseMember(ISymbol member) => ParseMember(_compilation, member);

        public static TypeSymbol ParseMember(Compilation compilation, ISymbol member)
        {
            if (member is IPropertySymbol property)
            {
                return new TypeSymbol(
                    compilation,
                    property.Type
                    );
            }
            else if (member is IFieldSymbol field)
            {
                return new TypeSymbol(
                    compilation,
                    field.Type
                    );
            }
            else
            {
                throw new NotImplementedException($"Unknown member type: {member.GetType().Name}");
            }
        }

        public readonly struct TypeSymbol
        {
            private readonly Compilation _compilation;

            public readonly ITypeSymbol Symbol;
            public readonly string GlobalName;

            public bool IsEnum
            {
                get
                {
                    if (Symbol is INamedTypeSymbol nts)
                    {
                        return nts.EnumUnderlyingType != null;
                    }
                    if (Symbol is IArrayTypeSymbol ats)
                    {
                        return false; //TODO: arrays of enums are possible!
                    }

                    return false;
                }
            }

            public ITypeSymbol? CollectionItemType
            {
                get
                {
                    if (Symbol is INamedTypeSymbol nts)
                    {
                        if (nts.TypeArguments.Length == 0)
                        {
                            return null;
                        }

                        if (nts.TypeArguments.Length > 1)
                        {
                            //Dictionary<K,V> и прочие - не коллекция элементов одного типа.
                            //Раньше здесь бросалось исключение, и свойство без сеттера
                            //такого типа роняло генератор целиком
                            return null;
                        }

                        return nts.TypeArguments[0];
                    }
                    if (Symbol is IArrayTypeSymbol ats)
                    {
                        return ats.ElementType;
                    }

                    return null;
                }
            }

            public readonly bool IsValueType
            {
                get
                {
                    return
                        Symbol.IsValueType;
                }
            }

            public readonly bool IsNullableValueType
            {
                get
                {
                    return
                        Symbol is INamedTypeSymbol nts
                        && nts.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
                }
            }

            public readonly bool IsAbstract
            {
                get
                {
                    return Symbol.IsAbstract;
                }
            }

            public TypeSymbol(
                Compilation compilation,
                ITypeSymbol symbol
                )
            {
                if (compilation is null)
                {
                    throw new ArgumentNullException(nameof(compilation));
                }

                if (symbol is null)
                {
                    throw new ArgumentNullException(nameof(symbol));
                }

                _compilation = compilation;
                Symbol = symbol;
                GlobalName = symbol is IArrayTypeSymbol ats
                    ? $"{ats.ElementType.ToGlobalDisplayString()}[]"
                    : symbol.ToGlobalDisplayString();
            }

            public readonly bool IsCollection(out ITypeSymbol? collectionItemType)
            {
                return
                    IsList(out collectionItemType)
                    || IsArray(out collectionItemType)
                    ;
            }

            public readonly bool IsArray(out ITypeSymbol? collectionItemType)
            {
                collectionItemType = CollectionItemType;

                return
                    collectionItemType is not null
                    && SymbolEqualityComparer.Default.Equals(Symbol, _compilation.Array(new[] { collectionItemType }))
                    ;
            }

            public readonly bool IsList(out ITypeSymbol? collectionItemType)
            {
                collectionItemType = CollectionItemType;

                return
                    collectionItemType is not null
                    && SymbolEqualityComparer.Default.Equals(Symbol, _compilation.List(new[] { collectionItemType }))
                    ;
            }

            public readonly string DetermineCountOrLength()
            {
                if (IsArray(out _))
                {
                    return "Length";
                }

                return "Count";
            }

            public readonly string ToGlobalDisplayString() => GlobalName;
        }

        private static XmlFeature ReadXmlFeatures(
            INamedTypeSymbol deSubject
            )
        {
            return (XmlFeature)ReadFlagsAttribute(
                deSubject,
                XmlDeserializeGenerator.FeaturesAttributeFullName
                );
        }

        private static XmlGuard ReadXmlGuards(
            INamedTypeSymbol deSubject
            )
        {
            return (XmlGuard)ReadFlagsAttribute(
                deSubject,
                XmlDeserializeGenerator.GuardsAttributeFullName
                );
        }

        /// <summary>
        /// OR всех значений одного <c>[Flags]</c>-атрибута на классе-хосте.
        /// Обе оси (<see cref="XmlFeature"/> и <see cref="XmlGuard"/>) читаются
        /// одинаково и складываются одинаково: несколько атрибутов на классе -
        /// это объединение, а не последний победивший.
        /// </summary>
        private static int ReadFlagsAttribute(
            INamedTypeSymbol deSubject,
            string fullName
            )
        {
            var flags = 0;

            foreach (var attribute in deSubject.GetAttributes())
            {
                var attrSymbol = attribute.AttributeClass;
                if (attrSymbol is null)
                {
                    continue;
                }
                if (attrSymbol.ToFullDisplayString() != fullName)
                {
                    continue;
                }
                if (attribute.ConstructorArguments.Length == 0)
                {
                    continue;
                }

                var arg = attribute.ConstructorArguments[0];
                if (arg.Value is int i)
                {
                    flags |= i;
                }
                else if (arg.Value is long l)
                {
                    flags |= (int)l;
                }
            }

            return flags;
        }

        private static SerializationInfoCollection ParseAttributes(
            Compilation compilation,
            INamedTypeSymbol deSubject
            )
        {
            if (compilation is null)
            {
                throw new ArgumentNullException(nameof(compilation));
            }

            if (deSubject is null)
            {
                throw new ArgumentNullException(nameof(deSubject));
            }

            var exhaustList = new List<INamedTypeSymbol>();
            var injectorList = new List<INamedTypeSymbol>();
            var sinfos = new Dictionary<string, SerializationInfo>();

            foreach (var attribute in deSubject.GetAttributes())
            {
                var attrSymbol = attribute.AttributeClass;
                if (attrSymbol is null)
                {
                    continue;
                }
                var fsa = attrSymbol.ToFullDisplayString();
                if (fsa.NotIn(
                    XmlDeserializeGenerator.SubjectAttributeFullName,
                    XmlDeserializeGenerator.FactoryAttributeFullName,
                    XmlDeserializeGenerator.ExhausterAttributeFullName,
                    XmlDeserializeGenerator.InjectorAttributeFullName))
                {
                    continue;
                }
                if (attribute.ConstructorArguments.Length == 0)
                {
                    continue;
                }

                //_sb.AppendLine("//fsa: " + fsa);
                if (fsa == XmlDeserializeGenerator.SubjectAttributeFullName)
                {
                    var ca0 = attribute.ConstructorArguments[0];
                    if (ca0.Kind != TypedConstantKind.Type)
                    {
                        throw new InvalidOperationException("Something wrong with attributes 1");
                    }
                    var type = (INamedTypeSymbol)ca0.Value!;
                    var typegn = type.ToGlobalDisplayString();
                    if (sinfos.ContainsKey(typegn))
                    {
                        throw new InvalidOperationException($"Type is already contains in the attribute list: {typegn}");
                    }

                    var ca1 = attribute.ConstructorArguments[1];
                    if (ca1.Kind != TypedConstantKind.Primitive)
                    {
                        throw new InvalidOperationException("Something wrong with attributes 2");
                    }
                    var isRoot = (bool)ca1.Value!;

                    var ssi = new SerializationInfo(
                        type,
                        isRoot
                        );
                    sinfos[typegn] = ssi;
                }
                else if (fsa == XmlDeserializeGenerator.FactoryAttributeFullName)
                {
                    var ca0 = attribute.ConstructorArguments[0];
                    if (ca0.Kind != TypedConstantKind.Type)
                    {
                        throw new InvalidOperationException("Something wrong with attributes 3");
                    }
                    var type = (INamedTypeSymbol)ca0.Value!;
                    var typegn = type.ToGlobalDisplayString();
                    if (!sinfos.ContainsKey(typegn))
                    {
                        throw new InvalidOperationException($"Type is not contains in the attribute list: {typegn}");
                    }

                    var ca1 = attribute.ConstructorArguments[1];
                    if (ca1.Kind != TypedConstantKind.Primitive)
                    {
                        throw new InvalidOperationException("Something wrong with attributes 4");
                    }

                    var factoryInvocation = (string)ca1.Value!;
                    sinfos[typegn] = sinfos[typegn].WithFactoryInvocation(factoryInvocation);
                }
                else if (fsa == XmlDeserializeGenerator.ExhausterAttributeFullName)
                {
                    var ca0 = attribute.ConstructorArguments[0];
                    if (ca0.Kind != TypedConstantKind.Type)
                    {
                        throw new InvalidOperationException("Something wrong with attributes 5");
                    }
                    var type = (INamedTypeSymbol)ca0.Value!;
                    var typegn = type.ToGlobalDisplayString();
                    if (!IsDerivedFrom(type, ExhausterBaseFullName))
                    {
                        throw new InvalidOperationException($"Type {typegn} must be derived from {ExhausterBaseFullName}. Implementing XmlSerDe.IExhauster is not enough: only a base class lets a new member arrive with a default implementation instead of breaking every implementer.");
                    }

                    exhaustList.Add(type);
                }
                else
                {
                    var ca0 = attribute.ConstructorArguments[0];
                    if (ca0.Kind != TypedConstantKind.Type)
                    {
                        throw new InvalidOperationException("Something wrong with attributes 6");
                    }
                    var type = (INamedTypeSymbol)ca0.Value!;
                    var typegn = type.ToGlobalDisplayString();
                    if (!IsDerivedFrom(type, InjectorBaseFullName))
                    {
                        throw new InvalidOperationException($"Type {typegn} must be derived from {InjectorBaseFullName} (XmlSerDe.DefaultInjector already is). Implementing XmlSerDe.IInjector is not enough: only a base class lets a new member arrive with a default implementation instead of breaking every implementer.");
                    }

                    injectorList.Add(type);
                }
            }

            ExpandXmlIncludes(sinfos);

            //add default exhauster if no one specified
            if (exhaustList.Count == 0)
            {
                exhaustList.Add(compilation.StringBuilderExhauster());
            }

            //add default injector if no one specified
            if (injectorList.Count == 0)
            {
                injectorList.Add(compilation.DefaultInjector());
            }

            return new SerializationInfoCollection(
                exhaustList,
                injectorList,
                sinfos.Values.ToList()
                );
        }

        /// <summary>
        /// Есть ли <paramref name="baseFullName"/> в цепочке базовых классов.
        /// Именно в цепочке, а не среди интерфейсов: наследник наследника тоже
        /// годится, и именно так пользователь и пишет - <c>MyInjector :
        /// DefaultInjector</c>.
        /// </summary>
        public static bool IsDerivedFrom(INamedTypeSymbol type, string baseFullName)
        {
            for (var current = type.BaseType; current is not null; current = current.BaseType)
            {
                if (current.ToFullDisplayString() == baseFullName)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// <see cref="XmlIncludeAttribute"/> на самом типе - единственный способ
        /// объявить наследников: своего атрибута для этого больше нет, потому что
        /// штатный делает ровно то же самое и его к тому же понимает
        /// System.Xml.Serialization.
        ///
        /// Наследник регистрируется здесь же как отдельный субъект: без этого для
        /// него не сгенерировалось бы ни одного метода и объявлять его пришлось бы
        /// всё равно вручную. Обход рекурсивный - наследник вправе объявлять уже
        /// своих наследников.
        /// </summary>
        public static void ExpandXmlIncludes(
            Dictionary<string, SerializationInfo> sinfos
            )
        {
            //прямые объявления: тип -> кого он включает. Собираются отдельно от
            //наследников, потому что наследники базы - это замыкание, а не первый слой
            var direct = new Dictionary<string, List<INamedTypeSymbol>>(StringComparer.Ordinal);
            var queue = new Queue<SerializationInfo>(sinfos.Values);

            while (queue.Count > 0)
            {
                var ssi = queue.Dequeue();
                var ssign = ssi.Subject.ToGlobalDisplayString();
                if (direct.ContainsKey(ssign))
                {
                    continue;
                }

                var includes = new List<INamedTypeSymbol>();
                direct[ssign] = includes;

                foreach (var included in ssi.Subject.GetXmlIncludes())
                {
                    var includedgn = included.ToGlobalDisplayString();

                    if (!sinfos.ContainsKey(includedgn))
                    {
                        var includedInfo = new SerializationInfo(included, false);
                        sinfos[includedgn] = includedInfo;
                        queue.Enqueue(includedInfo);
                    }

                    if (includes.Any(d => SymbolEqualityComparer.Default.Equals(d, included)))
                    {
                        //тот же XmlInclude объявлен на типе дважды
                        continue;
                    }

                    includes.Add(included);
                }
            }

            //наследники базы - транзитивно: наследник вправе объявлять уже своих
            //наследников, и лист должен быть виден базе, иначе он писался бы под
            //xsi:type промежуточного типа, а читался как база. Порядок - от самого
            //дальнего потомка к ближнему: проверка obj is Mid на экземпляре Leaf
            //сработала бы первой и потеряла его
            foreach (var ssi in sinfos.Values)
            {
                var closure = new List<INamedTypeSymbol>();
                var seen = new HashSet<string>(StringComparer.Ordinal) { ssi.Subject.ToGlobalDisplayString(), };
                var stack = new Stack<INamedTypeSymbol>();
                stack.Push(ssi.Subject);

                while (stack.Count > 0)
                {
                    var current = stack.Pop();
                    if (!direct.TryGetValue(current.ToGlobalDisplayString(), out var includes))
                    {
                        continue;
                    }

                    foreach (var included in includes)
                    {
                        if (seen.Add(included.ToGlobalDisplayString()))
                        {
                            closure.Add(included);
                            stack.Push(included);
                        }
                    }
                }

                var subject = ssi.Subject;
                var ordered = closure
                    //абстрактный тип экземпляром быть не может, диспетчеризовать в него нечего
                    .Where(d => !d.IsAbstract)
                    //OrderByDescending устойчива: на одной глубине порядок объявления сохраняется
                    .OrderByDescending(d => InheritanceDepth(d, subject))
                    .ToList();

                ssi.Deriveds.Clear();
                ssi.Deriveds.AddRange(ordered);
            }

            CheckXsiTypeNames(sinfos);
        }

        /// <summary>
        /// Сколько шагов по базовым типам от <paramref name="type"/> до
        /// <paramref name="root"/>; 0, если <paramref name="root"/> среди предков нет.
        /// </summary>
        private static int InheritanceDepth(INamedTypeSymbol type, INamedTypeSymbol root)
        {
            var depth = 0;
            for (var current = type.BaseType; current is not null; current = current.BaseType)
            {
                depth++;
                if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, root.OriginalDefinition))
                {
                    return depth;
                }
            }

            return 0;
        }

        /// <summary>
        /// xsi:type - это простое имя типа, и два наследника с одним именем из
        /// разных пространств имён писались бы одинаково, а читались как первый.
        /// System.Xml.Serialization отказывает при построении сериализатора;
        /// здесь отказ - ошибка генератора.
        /// </summary>
        private static void CheckXsiTypeNames(Dictionary<string, SerializationInfo> sinfos)
        {
            foreach (var ssi in sinfos.Values)
            {
                var names = new Dictionary<string, INamedTypeSymbol>(StringComparer.Ordinal)
                {
                    [ssi.Subject.GetXmlTypeName()] = ssi.Subject,
                };

                foreach (var derived in ssi.Deriveds)
                {
                    var name = derived.GetXmlTypeName();
                    if (names.TryGetValue(name, out var other))
                    {
                        throw new InvalidOperationException(
                            $"xsi:type \"{name}\" is ambiguous: {other.ToGlobalDisplayString()} and {derived.ToGlobalDisplayString()}"
                            + $" would both be written under it. Give one of them its own name via [{typeof(XmlTypeAttribute).Name}(TypeName = ...)]"
                            );
                    }

                    names[name] = derived;
                }
            }
        }

        /// <summary>
        /// Члены типа, начиная с самых дальних предков и заканчивая собственными.
        /// Именно в таком порядке пишет их System.Xml.Serialization, и совпадение
        /// формата на любом типе с наследованием держится на этом порядке.
        ///
        /// Обход идёт от типа к базе, потому что дойти до предков иначе нельзя,
        /// а результат разворачивается по слоям: внутри одного типа объявленный
        /// порядок членов сохраняется, переставляются только сами слои.
        /// (Прежнее имя метода - GetMembersOrderByInheritance - обещало этот порядок,
        /// но отдавало обратный.)
        /// </summary>
        private static List<ISymbol> GetMembersBaseFirst(
            INamedTypeSymbol serializationSubject
            )
        {
            var layers = new List<List<ISymbol>>();

            var symbol = serializationSubject;
            while(symbol.BaseType != null)
            {
                layers.Add(
                    (from m in symbol.GetMembers()
                    where m.Kind.In(SymbolKind.Property, SymbolKind.Field) && !m.IsStatic
                    select m).ToList()
                    );

                symbol = symbol.BaseType;
            }

            layers.Reverse();

            //член, скрытый через new, берётся из наследника и остаётся на месте
            //базового: так его пишет System.Xml.Serialization. Без этого оба попадали
            //в список, и локальная переменная объявлялась дважды (CS0128)
            var result = new List<ISymbol>();
            var slots = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var layer in layers)
            {
                foreach (var member in layer)
                {
                    if (slots.TryGetValue(member.Name, out var slot))
                    {
                        result[slot] = member;
                        continue;
                    }

                    slots[member.Name] = result.Count;
                    result.Add(member);
                }
            }

            return result;
        }
    }
}
#endif
