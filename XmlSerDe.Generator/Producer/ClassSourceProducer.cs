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
        public const string HeadlessSerializeMethodName = "SerializeBody";

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

            if (ssi.IsRoot)
            {
                GenerateRootSerializeMethod(subject, exhaustType);
            }

            GenerateSerializeMethod(subject, ssi.Deriveds, exhaustType, true);
            GenerateSerializeMethod(subject, ssi.Deriveds, exhaustType, false);
        }

        private readonly void GenerateRootSerializeMethod(
            INamedTypeSymbol subject,
            INamedTypeSymbol exhaustType
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

            {{HeadSerializeMethodName}}(exh, obj);
        }

""");
        }

        private readonly void GenerateSerializeMethod(
            INamedTypeSymbol subject,
            List<INamedTypeSymbol> deriveds,
            INamedTypeSymbol exhaustType,
            bool withHeadMethod
            )
        {
            var methodName = withHeadMethod ? HeadSerializeMethodName : HeadlessSerializeMethodName;
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
                    exh.{{nameof(IExhauster.Append)}}(@"<{{subject.Name}} xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xsi:type=""{{derived.Name}}"">");
                    {{HeadlessSerializeMethodName}}(exh, dobj);
                    exh.{{nameof(IExhauster.Append)}}("</{{subject.Name}}>");
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
            else
            {
                if (withHeadMethod)
                {

                    _sb.AppendLine($$"""

            exh.{{nameof(IExhauster.Append)}}("<{{subject.Name}}>");

""");
                }

                var members = GetMembersOrderByInheritance(subject);
                if (members.Count > 0)
                {
                    GenerateSerializeMembers(methodName, members);
                }

                if (withHeadMethod)
                {
                    _sb.AppendLine($$"""

            exh.{{nameof(IExhauster.Append)}}("</{{subject.Name}}>");

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

            var canBeNull = !memberType.IsValueType;
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


            if (BuiltinSourceProducer.TryGetBuiltin(_compilation, memberType.Symbol, out _))
            {
                _sb.AppendLine($$"""
                exh.{{nameof(IExhauster.Append)}}("<{{member.Name}}>");
                {{BuiltinSerializeHeadlessFullMethodName}}(exh, obj.{{member.Name}});
                exh.{{nameof(IExhauster.Append)}}("</{{member.Name}}>");

""");

            }
            else if (memberType.IsEnum)
            {
                var gses = GenerateSerializeEnum(member.Name, member.Name);
                _sb.AppendLine(gses);
            }
            //TODO other collections?
            else if (memberType.IsCollection(out var collectionItemType))
            {
                var countOrLength = memberType.DetermineCountOrLength();

                var scms = GenerateSerializeCollectionMember(member, (INamedTypeSymbol)collectionItemType!);

                _sb.AppendLine($$"""
                exh.{{nameof(IExhauster.Append)}}("<{{member.Name}}>");
                for(var index = 0; index < obj.{{member.Name}}.{{countOrLength}}; index++)
                {
{{scms}}
                }
                exh.{{nameof(IExhauster.Append)}}("</{{member.Name}}>");
""");

            }
            else
            {
                var subjectFound = SerializationInfoCollection.TryGetSubject(memberType.Symbol, out var ssi);
                if (subjectFound && ssi.Deriveds.Count > 0)
                {
                    foreach (var derived in ssi.Deriveds)
                    {
                        _sb.AppendLine($$"""

                {
                    if(obj.{{member.Name}} is {{derived.ToGlobalDisplayString()}} dobj)
                    {
                        exh.{{nameof(IExhauster.Append)}}(@"<{{member.Name}} xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xsi:type=""{{derived.Name}}"">");
                        {{HeadlessSerializeMethodName}}(exh, dobj);
                        exh.{{nameof(IExhauster.Append)}}("</{{member.Name}}>");
                    }
                }

""");
                    }
                }
                else
                {
                    _sb.AppendLine($$"""
                exh.{{nameof(IExhauster.Append)}}(@"<{{member.Name}}>");
                {{HeadlessSerializeMethodName}}(exh, obj.{{member.Name}});
                exh.{{nameof(IExhauster.Append)}}("</{{member.Name}}>");
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
            if (BuiltinSourceProducer.TryGetBuiltin(_compilation, listItemType, out _))
            {
                return $"                    {BuiltinSerializeHeadFullMethodName}(exh, obj.{member.Name}[index]);";
            }
            else if (listItemType.EnumUnderlyingType != null)
            {
                var indexVarName = "index";

                var gses = GenerateSerializeEnum(
                    listItemType.Name,
                    $"{member.Name}[{indexVarName}]"
                    );

                return gses;
            }
            else
            {
                return $"                {HeadSerializeMethodName}(exh, obj.{member.Name}[index]);";
            }
        }

        private readonly string GenerateSerializeEnum(string enumTypeName, string enumPropertyName)
        {
            return $$"""
                exh.{{nameof(IExhauster.Append)}}("<{{enumTypeName}}>");
                exh.{{nameof(IExhauster.Append)}}(obj.{{enumPropertyName}}.ToString());
                exh.{{nameof(IExhauster.Append)}}("</{{enumTypeName}}>");
""";
        }


        #endregion

        #region deserialize

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

            GenerateDeserializeMethod(injectorType, subject, ssi.Deriveds, ssi.FactoryInvocation, true);
            GenerateDeserializeMethod(injectorType, subject, ssi.Deriveds, ssi.FactoryInvocation, false);

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

        private readonly void GenerateDeserializeMethod(
            INamedTypeSymbol injectorType,
            INamedTypeSymbol subject,
            List<INamedTypeSymbol> derived,
            string? factoryInvocation,
            bool withHeadMethod
            )
        {
            var roscharVarName = withHeadMethod ? "fullNode" : "internals";
            var methodName = withHeadMethod ? HeadDeserializeMethodName : HeadlessDeserializeMethodName;

            var ssGlobalName = subject.ToGlobalDisplayString();

            _sb.AppendLine($$"""
        private static void {{methodName}}(ref {{typeof(XmlDeserializeSettings).FullName}} settings, {{injectorType.ToGlobalDisplayString()}} inj, roschar {{roscharVarName}}, roschar xmlnsAttributeName, out {{ssGlobalName}} result)
        {
""");

            if (withHeadMethod)
            {
                _sb.AppendLine($$"""
            var xmlNode = new {{typeof(XmlNode2).FullName}}(settings, {{roscharVarName}}, xmlnsAttributeName);
            var xmlNodePreciseType = xmlNode.{{nameof(XmlNode2.GetPreciseNodeType)}}();
            if(xmlNodePreciseType.IsEmpty)
            {
                xmlNodePreciseType = xmlNode.{{nameof(XmlNode2.DeclaredNodeType)}};
            }

            if(!xmlNodePreciseType.SequenceEqual(nameof({{ssGlobalName}}).AsSpan()))
            {
""");

                GenerateDeserializeDispatch(injectorType, derived);

                _sb.AppendLine($$"""

                throw new InvalidOperationException("(1) Unknown type " + xmlNodePreciseType.ToString());
            }

""");
            }

            if (subject.IsAbstract)
            {
                _sb.AppendLine($$"""
            throw new InvalidOperationException("Cannot instanciate abstract class {{ssGlobalName}}");
""");
            }
            else
            {
                if (withHeadMethod)
                {
                    _sb.AppendLine($$"""
            var internals = xmlNode.{{nameof(XmlNode2.Internals)}};
""");
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

                var members = GetMembersOrderByInheritance(subject);
                if (members.Count > 0)
                {
                    GenerateDeserializeMembers(
                        members,
                        withHeadMethod
                        );
                }
            }

            _sb.AppendLine($$"""
        }
""");
        }


        private readonly void GenerateDeserializeDispatch(
            INamedTypeSymbol injectorType,
            List<INamedTypeSymbol> derived
            )
        {
            _sb.AppendLine($$"""
                var xmlNodeInternals = xmlNode.{{nameof(XmlNode2.Internals)}};
""");

            foreach (var d in derived)
            {
                var classAndMethodName = DetermineClassName(d) + "." + HeadlessDeserializeMethodName;

                _sb.AppendLine($$"""
                //{{nameof(GenerateDeserializeDispatch)}}
                if (xmlNodePreciseType.SequenceEqual(nameof({{d.Name}}).AsSpan()))
                {
                    {{classAndMethodName}}(ref settings, inj, xmlNodeInternals, xmlNode.{{nameof(XmlNode2.XmlnsAttributeName)}}, out {{d.ToGlobalDisplayString()}} iresult);
                    result = iresult;
                    return;
                }
""");
            }
        }


        private readonly void GenerateDeserializeDispatch2(
            List<INamedTypeSymbol> derived,
            string memberName
            )
        {
            foreach (var d in derived)
            {
                var classAndMethodName = DetermineClassName(d) + "." + HeadlessDeserializeMethodName;

                _sb.AppendLine($$"""
                        //{{nameof(GenerateDeserializeDispatch2)}}
                        if (childPreciseType.SequenceEqual(nameof({{d.Name}}).AsSpan()))
                        {
                            {{classAndMethodName}}(ref settings, inj, childInternals, child.XmlnsAttributeName, out {{d.ToGlobalDisplayString()}} iresult);
                            result.{{memberName}} = iresult;
                        }
""");
            }
        }


        private readonly void GenerateDeserializeMembers(
            List<ISymbol> members,
            bool withHeadMethod
            )
        {
            var xmlnsAttributeNameVarName = withHeadMethod
                ? $"xmlNode.{nameof(XmlNode2.XmlnsAttributeName)}"
                : $"xmlnsAttributeName";

            _sb.AppendLine($$"""
            if(!internals.IsEmpty)
            {

""");
            foreach (var member in FilterMembers(members))
            {
                _sb.AppendLine($$"""
                var {{member.Name}}Span = "{{member.Name}}".AsSpan();
""");
                
            }

            _sb.AppendLine($$"""
                {{typeof(XmlNode2).FullName}} child = new();
                while(true)
                {
                    {{typeof(XmlNode2).FullName}}.{{nameof(XmlNode2.GetFirst)}}(ref settings, internals, {{xmlnsAttributeNameVarName}} /*1*/, ref child);
                    if(child.IsEmpty)
                    {
                        break;
                    }
                    var childDeclaredNodeType = child.{{nameof(XmlNode2.DeclaredNodeType)}};

""");

            var memberIndex = 0;
            foreach (var member in FilterMembers(members))
            {
                var memberType = ParseMember(member);

                GenerateDeserializeMember(memberIndex, member, memberType);
                memberIndex++;
            }

            _sb.AppendLine($$"""

                    internals = internals.Slice(child.FullNode.Length);
                    if(internals.IsEmpty)
                    {
                        break;
                    }
                    var lcl = {{nameof(XmlNode2)}}.{{nameof(XmlNode2.GetLeadingCommentLengthIfExists)}}(settings.{{nameof(XmlDeserializeSettings.ContainsXmlComments)}}, internals);
                    if(lcl > 0)
                    {
                        internals = internals.Slice(lcl);
                        if(internals.IsEmpty)
                        {
                            break;
                        }
                    }
                }
            }
""");
        }

        private readonly void GenerateDeserializeMember(
            int index,
            ISymbol member,
            TypeSymbol memberType
            )
        {
            var elseif = index > 0 ? "else " : "";

            if(BuiltinSourceProducer.TryGetBuiltin(_compilation, memberType.Symbol, out _))
            {
                _sb.AppendLine($$"""
                    //{{memberType.ToGlobalDisplayString()}}  {{member.Name}}
                    {{elseif}}if(childDeclaredNodeType.SequenceEqual({{member.Name}}Span))
                    {
                        inj.{{nameof(IInjector.ParseBody)}}(child.{{nameof(XmlNode2.Internals)}}, out {{memberType.GlobalName}} injr);
                        result.{{member.Name}} = injr;
                    }
""");
            }
            else if (memberType.IsEnum)
            {
                var fullParserInvocation = GenerateEnumParseStatement(
                    memberType,
                    $"child.{nameof(XmlNode2.Internals)}"
                    );

                _sb.AppendLine($$"""
                    //Enum
                    {{elseif}}if(childDeclaredNodeType.SequenceEqual({{member.Name}}Span))
                    {
                        result.{{member.Name}} = {{fullParserInvocation}};
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

                _sb.AppendLine($$"""
                    //List<T>
                    {{elseif}}if(childDeclaredNodeType.SequenceEqual({{member.Name}}Span))
                    {
                        var {{poolVarName}} = new global::System.Collections.Generic.List<{{listItemType.ToGlobalDisplayString()}}>();

                        var childInternals = child.{{nameof(XmlNode2.Internals)}};
                        {{typeof(XmlNode2).FullName}} {{child2VarName}} = new();
                        while(true)
                        {
                            {{typeof(XmlNode2).FullName}}.{{nameof(XmlNode2.GetFirst)}}(ref settings, childInternals, child.{{nameof(XmlNode2.XmlnsAttributeName)}}, ref {{child2VarName}});
                            if({{child2VarName}}.{{nameof(XmlNode2.IsEmpty)}})
                            {
                                break;
                            }

                            {{listItemParseStatement}}
                            {{poolVarName}}.Add({{listItemParseResultVarName}});

                            childInternals = childInternals.Slice(
                                {{child2VarName}}.{{nameof(XmlNode2.FullNode)}}.Length
                                );
                            if(childInternals.IsEmpty)
                            {
                                break;
                            }
                        }

                        {{assignStatement}}
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

                if (subject.Deriveds.Count > 0)
                {
                    //тут могут быть вариации
                    //генерируем метод с проверками наследников

                    var classAndMethodName = DetermineClassName(memberType.Symbol) + "." + HeadlessDeserializeMethodName;

                    _sb.AppendLine($$"""
                    //custom type (with custom types dispatching)
                    var childPreciseType = child.{{nameof(XmlNode2.GetPreciseNodeType)}}();
                    {{elseif}}if(!childPreciseType.IsEmpty)
                    {
                        var childInternals = child.{{nameof(XmlNode2.Internals)}};

""");


                    GenerateDeserializeDispatch2(subject.Deriveds, member.Name);

                    _sb.AppendLine($$"""
                    }
                    else
                    {
                        if(childDeclaredNodeType.SequenceEqual({{member.Name}}Span))
                        {
                            var childInternals = child.{{nameof(XmlNode2.Internals)}};
                            {{classAndMethodName}}(ref settings, inj, childInternals, child.{{nameof(XmlNode2.XmlnsAttributeName)}}, out {{memberType.ToGlobalDisplayString()}} iresult);
                            result.{{member.Name}} = iresult;
                        }
                    }
""");
                }
                else
                {
                    //здесь всё четко, нет никаких вариаций типов и соотв. не должно быть типа в дочерней ноде

                    var methodName = HeadlessDeserializeMethodName;
                    var classAndMethodName = DetermineClassName(memberType.Symbol) + "." + methodName;

                    _sb.AppendLine($$"""
                    //custom type (no custom type dispatch)
                    {{elseif}}if(childDeclaredNodeType.SequenceEqual({{member.Name}}Span))
                    {
                        var childInternals = child.{{nameof(XmlNode2.Internals)}};
                        {{classAndMethodName}}(ref settings, inj, childInternals, child.{{nameof(XmlNode2.XmlnsAttributeName)}}, out {{memberType.ToGlobalDisplayString()}} iresult);
                        result.{{member.Name}} = iresult;
                    }
""");

                }

            }
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
                return $"result.{memberName} = {poolVarName}.ToArray();";
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
                var listItemVarName = $"{child2VarName}.{nameof(XmlNode2.Internals)}";
                var enumParseStatement = GenerateEnumParseStatement(
                    listItemType,
                    listItemVarName
                    );
                return $"var {listItemParseResultVarName} = {enumParseStatement};";
            }
            else
            {
                var listItemVarName = $"{child2VarName}.{nameof(XmlNode2.FullNode)}";
                var classAndMethodName = DetermineClassName(listItemType.Symbol) + "." + HeadDeserializeMethodName;

                return $@"{classAndMethodName}(ref settings, inj, {listItemVarName}, {child2VarName}.{nameof(XmlNode2.XmlnsAttributeName)}, out {listItemType.ToGlobalDisplayString()} {listItemParseResultVarName});";
            }
        }

        private readonly string GenerateEnumParseStatement(
            TypeSymbol memberType,
            string varName
            )
        {
            var fullParserInvocation =
                $@"({memberType.ToGlobalDisplayString()})Enum.Parse(typeof({memberType.ToGlobalDisplayString()}), {varName})"
                ;

            return fullParserInvocation;
        }

        #endregion

        private readonly IEnumerable<ISymbol> FilterMembers(
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
                    XmlDeserializeGenerator.DerivedSubjectAttributeFullName,
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
                else if (fsa == XmlDeserializeGenerator.InjectorAttributeFullName)
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
                else
                {
                    var ca0 = attribute.ConstructorArguments[0];
                    if (ca0.Kind != TypedConstantKind.Type)
                    {
                        throw new InvalidOperationException("Something wrong with attributes 8");
                    }

                    var ca1 = attribute.ConstructorArguments[1];
                    if (ca1.Kind != TypedConstantKind.Type)
                    {
                        throw new InvalidOperationException("Something wrong with attributes 9");
                    }

                    var type = (INamedTypeSymbol)ca0.Value!;
                    var typegn = type.ToGlobalDisplayString();
                    var derived = (INamedTypeSymbol)ca1.Value!;

                    if (derived.IsAbstract)
                    {
                        throw new InvalidOperationException(
                            $"{derived.ToGlobalDisplayString()} must be non abstract class"
                            );
                    }

                    sinfos[typegn].AddDerived(derived);
                }
            }

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

        private static List<ISymbol> GetMembersOrderByInheritance(
            INamedTypeSymbol serializationSubject
            )
        {
            var result = new List<ISymbol>();

            var symbol = serializationSubject;
            while(symbol.BaseType != null)
            {
                result.AddRange(
                    from m in symbol.GetMembers()
                    where m.Kind.In(SymbolKind.Property, SymbolKind.Field) && !m.IsStatic
                    select m
                    );

                symbol = symbol.BaseType;
            }

            return result;
        }
    }
}
#endif
