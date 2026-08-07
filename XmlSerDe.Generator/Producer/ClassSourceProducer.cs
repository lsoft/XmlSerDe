#if NETSTANDARD
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;
using XmlSerDe.Generator.Helper;
using System.Xml.Serialization;
using XmlSerDe.Common;
using System.Reflection;

namespace XmlSerDe.Generator.Producer
{
    public struct ClassSourceProducer
    {

        public const string HeadDeserializeMethodName = "Deserialize";
        public const string HeadSerializeMethodName = "Serialize";
        public const string HeadlessDeserializeMethodName = "DeserializeBody";
        public const string HeadedDeserializeMethodName = "DeserializeHeaded";
        public const string HeadlessSerializeMethodName = "SerializeBody";

        /// <summary>
        /// Корень с собственным именем (<see cref="System.Xml.Serialization.XmlRootAttribute"/>)
        /// получает отдельный метод: <see cref="HeadSerializeMethodName"/> пишет тот же тип
        /// под его обычным именем и нужен для элементов коллекций, а совместить оба имени
        /// в одном методе нельзя. Генерируется только когда имена действительно разошлись.
        /// </summary>
        public const string RootElementSerializeMethodName = "SerializeRootElement";

        public static readonly string BuiltinFullClassName = "global::" + typeof(BuiltinSourceProducer).Namespace + "." + BuiltinSourceProducer.BuiltinCodeHelperClassName;
        public static readonly string BuiltinSerializeHeadFullMethodName = BuiltinFullClassName + "." + HeadSerializeMethodName;
        public static readonly string BuiltinSerializeHeadlessFullMethodName = BuiltinFullClassName + "." + HeadlessSerializeMethodName;

        private readonly Compilation _compilation;
        private readonly INamedTypeSymbol _deSubject;
        private readonly string _deSubjectGlobalType;
        private readonly string _deSubjectReflectionFormat1;
        public readonly SerializationInfoCollection SerializationInfoCollection;

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
            _deSubjectGlobalType = _deSubject.ToGlobalDisplayString();
            _deSubjectReflectionFormat1 = _deSubject.ToReflectionFormat(false);

            _sb = new StringBuilder();

            SerializationInfoCollection = ParseAttributes(compilation, _deSubject);
        }

        public string GenerateClass(
            )
        {
            _sb.Clear();

            GenerateUsings();
            _sb.AppendLine("using roschar = System.ReadOnlySpan<char>;");

            if (!_deSubject.ContainingNamespace.IsGlobalNamespace)
            {
                _sb.AppendLine($@"
namespace {_deSubject.ContainingNamespace.ToFullDisplayString()}");
            }

            _sb.AppendLine($$"""
{
    public partial class {{_deSubjectReflectionFormat1}}
    {

""");

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
                global::{{typeof(BuiltinSourceProducer).Namespace}}.{{BuiltinSourceProducer.BuiltinCodeHelperClassName}}.{{BuiltinSourceProducer.AppendXmlHeadMethodName}}(exh);
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
                    exh.{{nameof(IExhauster.Append)}}(@"<{{elementName}} xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xsi:type=""{{derived.GetXmlTypeName()}}"">");
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

                    _sb.AppendLine($$"""

            exh.{{nameof(IExhauster.Append)}}("<{{elementName}}>");

""");
                }

                var members = GetMembersBaseFirst(subject);
                if (members.Count > 0)
                {
                    GenerateSerializeMembers(methodName, members);
                }

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
            string methodName,
            List<ISymbol> members
            )
        {
            foreach (var member in FilterMembers(members))
            {
                GenerateSerializeMember(methodName, member);
            }
        }

        private readonly void GenerateSerializeMember(
            string methodName,
            ISymbol member
            )
        {
            var memberType = ParseMember(member);
            _sb.AppendLine($$"""
            //{{memberType.ToGlobalDisplayString()}} {{member.Name}}
""");

            var canBeNull = !memberType.IsValueType || memberType.IsNullableValueType;
            if (canBeNull)
            {
                _sb.AppendLine($$"""
            if(obj.{{member.Name}} is not null)
            {
""");
            }
            else
            {
                _sb.AppendLine($$"""
            {
""");
            }


            var elementName = GetMemberElementName(member);

            if (BuiltinSourceProducer.TryGetBuiltin(_compilation, memberType.Symbol, out _))
            {
                _sb.AppendLine($$"""
                exh.{{nameof(IExhauster.Append)}}("<{{elementName}}>");
                {{BuiltinSerializeHeadlessFullMethodName}}(exh, obj.{{member.Name}});
                exh.{{nameof(IExhauster.Append)}}("</{{elementName}}>");

""");

            }
            else if (memberType.IsEnum)
            {
                var gses = GenerateSerializeEnum((INamedTypeSymbol)memberType.Symbol, elementName, member.Name);
                _sb.AppendLine(gses);
            }
            //TODO other collections?
            else if (memberType.IsCollection(out var collectionItemType))
            {
                var countOrLength = memberType.DetermineCountOrLength();

                var scms = GenerateSerializeCollectionMember(member, (INamedTypeSymbol)collectionItemType!);

                _sb.AppendLine($$"""
                exh.{{nameof(IExhauster.Append)}}("<{{elementName}}>");
                for(var index = 0; index < obj.{{member.Name}}.{{countOrLength}}; index++)
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
                {{elseif}}if(obj.{{member.Name}} is {{derived.ToGlobalDisplayString()}} dobj{{derivedIndex}})
                {
                    exh.{{nameof(IExhauster.Append)}}(@"<{{elementName}} xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xsi:type=""{{derived.GetXmlTypeName()}}"">");
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
                    exh.{{nameof(IExhauster.Append)}}(@"<{{elementName}}>");
                    {{HeadlessSerializeMethodName}}(exh, obj.{{member.Name}});
                    exh.{{nameof(IExhauster.Append)}}("</{{elementName}}>");
                }
""");
                    }
                }
                else
                {
                    _sb.AppendLine($$"""
                exh.{{nameof(IExhauster.Append)}}(@"<{{elementName}}>");
                {{HeadlessSerializeMethodName}}(exh, obj.{{member.Name}});
                exh.{{nameof(IExhauster.Append)}}("</{{elementName}}>");
""");
                }
            }

            _sb.AppendLine($$"""
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
                    $"{member.Name}[index]"
                    );
            }

            if (itemName is null)
            {
                if (BuiltinSourceProducer.TryGetBuiltin(_compilation, listItemType, out _))
                {
                    return $"                    {BuiltinSerializeHeadFullMethodName}(exh, obj.{member.Name}[index]);";
                }

                return $"                {HeadSerializeMethodName}(exh, obj.{member.Name}[index]);";
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

            var headlessInvocation = BuiltinSourceProducer.TryGetBuiltin(_compilation, listItemType, out _)
                ? $"{BuiltinSerializeHeadlessFullMethodName}(exh, obj.{member.Name}[index]);"
                : $"{HeadlessSerializeMethodName}(exh, obj.{member.Name}[index]);";

            return $$"""
                    exh.{{nameof(IExhauster.Append)}}("<{{itemName}}>");
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
            var switchArms = GenerateEnumToStringSwitchArms(enumType);

            return $$"""
                {
                    var enumValueToAppend = obj.{{enumPropertyExpression}};
                    exh.{{nameof(IExhauster.Append)}}("<{{tagName}}>");
                    exh.{{nameof(IExhauster.Append)}}(enumValueToAppend switch
                    {
{{switchArms}}                        _ => enumValueToAppend.ToString(),
                    });
                    exh.{{nameof(IExhauster.Append)}}("</{{tagName}}>");
                }
""";
        }

        private readonly string GenerateEnumToStringSwitchArms(INamedTypeSymbol enumType)
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

                sb.AppendLine($"""                        {enumGlobalName}.{field.Name} => "{field.GetXmlEnumName()}",""");
            }

            return sb.ToString();
        }


        #endregion

        #region deserialize

        private static readonly string XmlHeadFullName = typeof(XmlHead).FullName;
        private static readonly string XmlScanFullName = typeof(XmlScan).FullName;

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

            _sb.AppendLine($$"""
        public static void {{HeadDeserializeMethodName}}({{injectorType.ToGlobalDisplayString()}} inj, roschar xmlFullNode, out {{ssGlobalName}} result)
        {
            var settings = new {{typeof(XmlDeserializeSettings).FullName}}(
                {{typeof(XmlNode2).FullName}}.{{nameof(XmlNode2.IsXmlCommentExistsHeuristic)}}(xmlFullNode),
                {{typeof(XmlNode2).FullName}}.{{nameof(XmlNode2.IsCDataBlockExistsHeuristic)}}(xmlFullNode)
                );
            {{HeadDeserializeMethodName}}(ref settings, inj, xmlFullNode, roschar.Empty, out result);
        }
""");
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
        private static void {{HeadDeserializeMethodName}}(ref {{typeof(XmlDeserializeSettings).FullName}} settings, {{injectorType.ToGlobalDisplayString()}} inj, roschar fullNode, roschar xmlnsAttributeName, out {{ssGlobalName}} result)
        {
            {{XmlHeadFullName}} xmlNode = new();
            {{XmlScanFullName}}.{{nameof(XmlScan.ReadHead)}}(settings.{{nameof(XmlDeserializeSettings.ContainsXmlComments)}}, settings.{{nameof(XmlDeserializeSettings.ContainsCDataBlocks)}}, fullNode, xmlnsAttributeName, ref xmlNode);
            var xmlNodeBody = xmlNode.{{nameof(XmlHead.IsBodyless)}} ? roschar.Empty : fullNode.Slice(xmlNode.{{nameof(XmlHead.TotalLength)}});
            {{HeadedDeserializeMethodName}}(ref settings, inj, ref xmlNode, xmlNodeBody, out result, out _);
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
        private static void {{HeadedDeserializeMethodName}}(ref {{typeof(XmlDeserializeSettings).FullName}} settings, {{injectorType.ToGlobalDisplayString()}} inj, ref {{XmlHeadFullName}} xmlNode, roschar body, out {{ssGlobalName}} result, out int bodyConsumed)
        {
            var xmlNodePreciseType = xmlNode.{{nameof(XmlHead.GetPreciseNodeType)}}();
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

            {{HeadlessDeserializeMethodName}}(ref settings, inj, body, xmlNode.{{nameof(XmlHead.XmlnsAttributeName)}}, out result, out bodyConsumed);
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
                    {{classAndMethodName}}(ref settings, inj, body, xmlNode.{{nameof(XmlHead.XmlnsAttributeName)}}, out {{d.ToGlobalDisplayString()}} iresult, out bodyConsumed);
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
        private static void {{HeadlessDeserializeMethodName}}(ref {{typeof(XmlDeserializeSettings).FullName}} settings, {{injectorType.ToGlobalDisplayString()}} inj, roschar body, roschar xmlnsAttributeName, out {{ssGlobalName}} result, out int bodyConsumed)
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
                _sb.AppendLine($$"""
            result = new {{ssGlobalName}}();
""");
            }

            var members = GetMembersBaseFirst(subject);

            GenerateDeserializeMembers(members);

            _sb.AppendLine($$"""
        }
""");
        }

        private readonly void GenerateDeserializeMembers(
            List<ISymbol> members
            )
        {
            foreach (var member in FilterMembers(members))
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
                {{XmlScanFullName}}.{{nameof(XmlScan.ReadHead)}}(settings.{{nameof(XmlDeserializeSettings.ContainsXmlComments)}}, settings.{{nameof(XmlDeserializeSettings.ContainsCDataBlocks)}}, cursor, xmlnsAttributeName, ref child);
                if(child.{{nameof(XmlHead.IsEmpty)}})
                {
                    break;
                }
                if(child.{{nameof(XmlHead.IsEndTag)}})
                {
                    //наш собственный закрывающий тег - тело кончилось
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
            foreach (var member in FilterMembers(members))
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
                + $" : {XmlScanFullName}.{nameof(XmlScan.SkipBody)}(childBody, false);";

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

        private readonly void GenerateDeserializeMember(
            ISymbol member,
            TypeSymbol memberType,
            bool isFirstMember
            )
        {
            var elseif = isFirstMember ? "" : "else ";

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
                        if(child.{{nameof(XmlHead.IsBodyless)}} && child.{{nameof(XmlHead.IsNil)}}())
                        {
                            //<Foo xsi:nil="true"/> - значения нет, член остаётся null
                            childConsumed = 0;
                        }
                        else
                        {
                            //ReadTextBody на закрытой ноде отдаёт пустой текст и consumed = 0,
                            //так что <Foo/> здесь превращается в пустую строку
                            {{GenerateReadTextBodyStatement("childBody", "child", "childText", "childConsumed")}}
                            inj.{{nameof(IInjector.ParseBody)}}(childText, out {{memberType.GlobalName}} injr);
                            result.{{member.Name}} = injr;
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
                        if(child.{{nameof(XmlHead.IsBodyless)}})
                        {
                            childConsumed = 0;
                        }
                        else
                        {
                            {{GenerateReadTextBodyStatement("childBody", "child", "childText", "childConsumed")}}
                            inj.{{nameof(IInjector.ParseBody)}}(childText, out {{memberType.GlobalName}} injr);
                            result.{{member.Name}} = injr;
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
                        if(child.{{nameof(XmlHead.IsBodyless)}})
                        {
                            childConsumed = 0;
                        }
                        else
                        {
                            {{GenerateReadTextBodyStatement("childBody", "child", "childText", "childConsumed")}}
                            result.{{member.Name}} = {{fullParserInvocation}};
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
                    listItemParseResultVarName
                    );

                var poolVarName = "pool";

                var assignStatement = GenerateAssignStatement(
                    memberType,
                    member.Name,
                    poolVarName
                    );

                var poolDeclarationStatement = GeneratePoolDeclarationStatement(
                    memberType,
                    listItemType,
                    poolVarName
                    );

                _sb.AppendLine($$"""
                    //List<T>
                    {{elseif}}if(childDeclaredNodeType.SequenceEqual({{member.Name}}Span))
                    {
                        if(child.{{nameof(XmlHead.IsBodyless)}} && child.{{nameof(XmlHead.IsNil)}}())
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
                            {{XmlScanFullName}}.{{nameof(XmlScan.ReadHead)}}(settings.{{nameof(XmlDeserializeSettings.ContainsXmlComments)}}, settings.{{nameof(XmlDeserializeSettings.ContainsCDataBlocks)}}, itemCursor, child.{{nameof(XmlHead.XmlnsAttributeName)}}, ref {{child2VarName}});
                            if({{child2VarName}}.{{nameof(XmlHead.IsEmpty)}})
                            {
                                break;
                            }
                            if({{child2VarName}}.{{nameof(XmlHead.IsEndTag)}})
                            {
                                itemConsumed += {{child2VarName}}.{{nameof(XmlHead.TotalLength)}};
                                break;
                            }
                            var child2Step = {{child2VarName}}.{{nameof(XmlHead.TotalLength)}};
                            if(!{{child2VarName}}.{{nameof(XmlHead.IsBodyless)}})
                            {
                                var child2Body = itemCursor.Slice({{child2VarName}}.{{nameof(XmlHead.TotalLength)}});
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
                        var childPreciseType = child.{{nameof(XmlHead.GetPreciseNodeType)}}();
""");

                    GenerateDeserializeDispatch2(subject.Deriveds, member.Name);

                    _sb.AppendLine($$"""
                        else
                        {
                            {{classAndMethodName}}(ref settings, inj, childBody, child.{{nameof(XmlHead.XmlnsAttributeName)}}, out {{memberType.ToGlobalDisplayString()}} iresult, out childConsumed);
                            result.{{member.Name}} = iresult;
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
                        {{classAndMethodName}}(ref settings, inj, childBody, child.{{nameof(XmlHead.XmlnsAttributeName)}}, out {{memberType.ToGlobalDisplayString()}} iresult, out childConsumed);
                        result.{{member.Name}} = iresult;
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
                            {{classAndMethodName}}(ref settings, inj, childBody, child.XmlnsAttributeName, out {{d.ToGlobalDisplayString()}} iresult, out childConsumed);
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
                $"{XmlScanFullName}.{nameof(XmlScan.ReadTextBody)}("
                + $"settings.{nameof(XmlDeserializeSettings.ContainsXmlComments)}, "
                + $"settings.{nameof(XmlDeserializeSettings.ContainsCDataBlocks)}, "
                + $"{bodyVarName}, "
                + $"{headVarName}.{nameof(XmlHead.IsBodyless)}, "
                + $"{headVarName}.{nameof(XmlHead.DeclaredNodeType)}, "
                + $"out var {textVarName}, "
                + $"out {consumedVarName});";
        }

        /// <summary>
        /// Член типа <c>List&lt;T&gt;</c> отдаётся вызывающему как есть, поэтому
        /// накапливать его больше не во что - список и есть результат.
        /// А вот <c>T[]</c> всё равно придётся копировать в массив точного
        /// размера, так что промежуточный список - чистые потери: его буферы
        /// берутся из пула (см. <see cref="PooledArrayBuilder{T}"/>).
        /// </summary>
        private readonly string GeneratePoolDeclarationStatement(
            TypeSymbol memberType,
            TypeSymbol listItemType,
            string poolVarName
            )
        {
            if (memberType.IsList(out _))
            {
                return $"var {poolVarName} = new global::System.Collections.Generic.List<{listItemType.ToGlobalDisplayString()}>();";
            }
            else if (memberType.IsArray(out _))
            {
                return $"var {poolVarName} = new {PooledArrayBuilderFullName}<{listItemType.ToGlobalDisplayString()}>();";
            }

            throw new InvalidOperationException($"Unknown type {memberType.ToGlobalDisplayString()}");
        }

        private readonly string GenerateAssignStatement(
            TypeSymbol memberType,
            string memberName,
            string poolVarName
            )
        {
            if (memberType.IsList(out _))
            {
                return $"result.{memberName} = {poolVarName};";
            }
            else if (memberType.IsArray(out _))
            {
                return $"result.{memberName} = {poolVarName}.{nameof(PooledArrayBuilder<int>.ToArrayAndRelease)}();";
            }

            throw new InvalidOperationException($"Unknown type {memberType.ToGlobalDisplayString()}");
        }

        private readonly string GenerateListItemParseStatement(
            string child2VarName,
            TypeSymbol listItemType,
            string listItemParseResultVarName
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
                return
                    GenerateReadTextBodyStatement("child2Body", child2VarName, "child2Text", "child2Consumed")
                    + $"\r\n                            inj.{nameof(IInjector.ParseBody)}(child2Text, out {listItemType.GlobalName} {listItemParseResultVarName});";
            }
            else
            {
                var classAndMethodName = DetermineClassName(listItemType.Symbol) + "." + HeadedDeserializeMethodName;

                return $@"{classAndMethodName}(ref settings, inj, ref {child2VarName}, child2Body, out {listItemType.ToGlobalDisplayString()} {listItemParseResultVarName}, out child2Consumed);";
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
            )
        {
            var result = new List<ISymbol>(SelectMembers(members));

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

        private readonly IEnumerable<ISymbol> SelectMembers(
            List<ISymbol> members
            )
        {
            foreach (var member in members)
            {
                if (member.DeclaredAccessibility.In(Accessibility.Private, Accessibility.Protected)) //TODO: what about other Accessibilities?
                {
                    continue;
                }
                if (member is IPropertySymbol property)
                {
                    if (property.SetMethod == null)
                    {
                        continue;
                    }

                    var propertyAttributes = property.GetAttributes();
                    var ignoreAttribute = propertyAttributes.FirstOrDefault(a => a.AttributeClass != null && a.AttributeClass.ToFullDisplayString() == typeof(XmlIgnoreAttribute).FullName);
                    if (ignoreAttribute != null)
                    {
                        continue;
                    }
                }
                else if (member is IFieldSymbol fieldSymbol)
                {
                    //nothing to do
                }
                else
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

            if (!memberType.IsCollection(out _))
            {
                return member.GetXmlElementName();
            }

            if (member.HasXmlElementAttribute())
            {
                //на коллекции XmlElement означает совсем другую форму - элементы
                //без обёртки вовсе, - и промолчать здесь значило бы выдать документ,
                //не совпадающий с тем, что написано на самом члене
                throw new InvalidOperationException(
                    $"{typeof(XmlElementAttribute).Name} on collection member {member.Name}"
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
                return typeof(BuiltinSourceProducer).Namespace + "." + BuiltinSourceProducer.BuiltinCodeHelperClassName;
            }
            else
            {
                return _deSubject.ToGlobalDisplayString();
            }
        }

        private readonly void GenerateUsings()
        {
            var foundUsings = new List<UsingDirectiveSyntax>();

            GrabUsings(_deSubject, ref foundUsings);

            var set = new HashSet<string>(
                foundUsings.ConvertAll(u => u.WithoutTrivia().ToFullString())
                );

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

        private readonly TypeSymbol ParseMember(ISymbol member)
        {
            if (member is IPropertySymbol property)
            {
                return new TypeSymbol(
                    _compilation,
                    property.Type
                    );
            }
            else if (member is IFieldSymbol field)
            {
                return new TypeSymbol(
                    _compilation,
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
                            throw new NotSupportedException($"{nts.ToGlobalDisplayString()} does not support");
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
                    if (type.AllInterfaces.All(i => i.ToFullDisplayString() != "XmlSerDe.Common.IExhauster"))
                    {
                        throw new InvalidOperationException($"Type {typegn} must be derived from XmlSerDe.Common.IExhauster interface");
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
                    if (type.AllInterfaces.All(i => i.ToFullDisplayString() != "XmlSerDe.Common.IInjector"))
                    {
                        throw new InvalidOperationException($"Type {typegn} must be derived from XmlSerDe.Common.IInjector interface");
                    }

                    injectorList.Add(type);
                }
            }

            ExpandXmlIncludes(sinfos);

            //add default exhauster if no one specified
            if (exhaustList.Count == 0)
            {
                exhaustList.Add(compilation.DefaultStringBuilderExhauster());
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
        private static void ExpandXmlIncludes(
            Dictionary<string, SerializationInfo> sinfos
            )
        {
            var queue = new Queue<SerializationInfo>(sinfos.Values);

            while (queue.Count > 0)
            {
                var ssi = queue.Dequeue();

                foreach (var included in ssi.Subject.GetXmlIncludes())
                {
                    var includedgn = included.ToGlobalDisplayString();

                    if (!sinfos.ContainsKey(includedgn))
                    {
                        var includedInfo = new SerializationInfo(included, false);
                        sinfos[includedgn] = includedInfo;
                        queue.Enqueue(includedInfo);
                    }

                    if (included.IsAbstract)
                    {
                        //абстрактный тип экземпляром быть не может, диспетчеризовать в него нечего
                        continue;
                    }
                    if (ssi.Deriveds.Any(d => SymbolEqualityComparer.Default.Equals(d, included)))
                    {
                        //тот же XmlInclude объявлен на типе дважды
                        continue;
                    }

                    ssi.AddDerived(included);
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

            var result = new List<ISymbol>();
            foreach (var layer in layers)
            {
                result.AddRange(layer);
            }

            return result;
        }
    }
}
#endif
