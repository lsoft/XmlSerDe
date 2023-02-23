﻿#if NETSTANDARD
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;
using XmlSerDe.Generator.Helper;
using System.Xml.Serialization;
using XmlSerDe.Common;

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
        private delegate {{_deSubjectGlobalType}} DeserializeMethodDelegate(roschar fullNode);

""");

            foreach (var exhaustType in this.SerializationInfoCollection.ExhaustList)
            {
                GenerateSerializeMethods(exhaustType);
            }
            GenerateDeserializeMethods();

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
        public static void {{HeadSerializeMethodName}}({{exhaustTypeGlobalName}} sb, {{ssGlobalName}} obj, bool appendXmlHead)
        {
            if(appendXmlHead)
            {
                global::{{typeof(BuiltinSourceProducer).Namespace}}.{{BuiltinSourceProducer.BuiltinCodeHelperClassName}}.{{BuiltinSourceProducer.AppendXmlHeadMethodName}}(sb);
            }

            {{HeadSerializeMethodName}}(sb, obj);
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
        private static void {{methodName}}({{exhaustTypeGlobalName}} sb, {{ssGlobalName}} obj)
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

            var isTypeUnknown = subject.IsAbstract;
            if (isTypeUnknown)
            {
                foreach (var derived in deriveds)
                {
                    if (withHeadMethod)
                    {
                        _sb.AppendLine($$"""

            {
                if(obj is {{derived.ToGlobalDisplayString()}} dobj)
                {
                    sb.{{nameof(IExhauster.Append)}}(@"<{{subject.Name}} xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xsi:type=""{{derived.Name}}"">");
                    {{HeadlessSerializeMethodName}}(sb, dobj);
                    sb.{{nameof(IExhauster.Append)}}("</{{subject.Name}}>");
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
                    {{HeadlessSerializeMethodName}}(sb, dobj);
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

            sb.{{nameof(IExhauster.Append)}}("<{{subject.Name}}>");

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

            sb.{{nameof(IExhauster.Append)}}("</{{subject.Name}}>");

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
            //{{memberType.ToFullDisplayString()}} {{member.Name}}
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


            if (BuiltinSourceProducer.TryGetBuiltin(_compilation, memberType, out var memberBuiltin))
            {
                _sb.AppendLine($$"""
                sb.{{nameof(IExhauster.Append)}}("<{{member.Name}}>");
                {{BuiltinSerializeHeadlessFullMethodName}}(sb, obj.{{member.Name}});
                sb.{{nameof(IExhauster.Append)}}("</{{member.Name}}>");

""");

            }
            else if (memberType.EnumUnderlyingType != null)
            {
                var gses = GenerateSerializeEnum(member.Name, member.Name);
                _sb.AppendLine(gses);
            }
            //TODO array and other collections
            else if (
                memberType.TypeArguments.Length > 0
                && (SymbolEqualityComparer.Default.Equals(memberType, _compilation.List(memberType.TypeArguments[0])))
                )
            {
                _sb.AppendLine($$"""
                sb.{{nameof(IExhauster.Append)}}("<{{member.Name}}>");

""");

                var listItemType = (INamedTypeSymbol)memberType.TypeArguments[0];

                if (BuiltinSourceProducer.TryGetBuiltin(_compilation, listItemType, out var listItemBuiltin))
                {
                    _sb.AppendLine($$"""

                for(var index = 0; index < obj.{{member.Name}}.Count; index++)
                {
                    {{BuiltinSerializeHeadFullMethodName}}(sb, obj.{{member.Name}}[index]);
                }
""");
                }
                else
                {
                    if (listItemType.EnumUnderlyingType != null)
                    {
                        var indexVarName = "index";

                        var gses = GenerateSerializeEnum(
                            listItemType.Name,
                            $"{member.Name}[{indexVarName}]"
                            );

                        _sb.AppendLine($$"""

                for(var {{indexVarName}} = 0; {{indexVarName}} < obj.{{member.Name}}.Count; {{indexVarName}}++)
                {
{{gses}}
                }
""");

                    }
                    else
                    {
                        _sb.AppendLine($$"""

                for(var index = 0; index < obj.{{member.Name}}.Count; index++)
                {
                    {{HeadSerializeMethodName}}(sb, obj.{{member.Name}}[index]);
                }
""");
                    }
                }

                _sb.AppendLine($$"""
                sb.{{nameof(IExhauster.Append)}}("</{{member.Name}}>");

""");

            }
            else
            {
                var subjectFound = SerializationInfoCollection.TryGetSubject(memberType, out var ssi);
                if (subjectFound && ssi.Deriveds.Count > 0)
                {
                    foreach (var derived in ssi.Deriveds)
                    {
                        _sb.AppendLine($$"""

                {
                    if(obj.{{member.Name}} is {{derived.ToGlobalDisplayString()}} dobj)
                    {
                        sb.{{nameof(IExhauster.Append)}}(@"<{{member.Name}} xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xsi:type=""{{derived.Name}}"">");
                        {{HeadlessSerializeMethodName}}(sb, dobj);
                        sb.{{nameof(IExhauster.Append)}}("</{{member.Name}}>");
                    }
                }

""");
                    }
                }
                else
                {
                    _sb.AppendLine($$"""
                sb.{{nameof(IExhauster.Append)}}(@"<{{member.Name}}>");
                {{HeadlessSerializeMethodName}}(sb, obj.{{member.Name}});
                sb.{{nameof(IExhauster.Append)}}("</{{member.Name}}>");
""");
                }
            }

            _sb.AppendLine($$"""
            }
""");

        }

        private readonly string GenerateSerializeEnum(string enumTypeName, string enumPropertyName)
        {
            return $$"""
                sb.{{nameof(IExhauster.Append)}}("<{{enumTypeName}}>");
                sb.{{nameof(IExhauster.Append)}}(obj.{{enumPropertyName}}.ToString());
                sb.{{nameof(IExhauster.Append)}}("</{{enumTypeName}}>");
""";
        }


        #endregion

        #region deserialize

        private readonly void GenerateDeserializeMethods()
        {
            _sb.AppendLine($$"""
#region deserialize

""");
            foreach (var ssi in SerializationInfoCollection.Infos)
            {
                GenerateDeserializeMethod(
                    ssi
                    );
            }

            _sb.AppendLine($$"""

#endregion
""");
        }

        private readonly void GenerateDeserializeMethod(
            SerializationInfo ssi
            )
        {
            var subject = ssi.Subject;

            GenerateDeserializeMethod(subject, ssi.Deriveds, ssi.FactoryInvocation, ssi.ParserInvocation, true);
            GenerateDeserializeMethod(subject, ssi.Deriveds, ssi.FactoryInvocation, ssi.ParserInvocation, false);

            if(ssi.IsRoot)
            {
                GenerateRootDeserializeMethod(subject);
            }
        }

        private readonly void GenerateRootDeserializeMethod(
            INamedTypeSymbol subject
            )
        {
            var ssGlobalName = subject.ToGlobalDisplayString();

            _sb.AppendLine($$"""
        public static void {{HeadDeserializeMethodName}}(roschar xmlFullNode, out {{ssGlobalName}} result)
        {
            {{HeadDeserializeMethodName}}(xmlFullNode, roschar.Empty, out result);
        }
""");
        }

        private readonly void GenerateDeserializeMethod(
            INamedTypeSymbol subject,
            List<INamedTypeSymbol> derived,
            string? factoryInvocation,
            string? parserInvocation,
            bool withHeadMethod
            )
        {
            var roscharVarName = withHeadMethod ? "fullNode" : "internals";
            var methodName = withHeadMethod ? HeadDeserializeMethodName : HeadlessDeserializeMethodName;

            var ssGlobalName = subject.ToGlobalDisplayString();

            _sb.AppendLine($$"""
        private static void {{methodName}}(roschar {{roscharVarName}}, roschar xmlnsAttributeName, out {{ssGlobalName}} result)
        {
""");

            if (withHeadMethod)
            {
                _sb.AppendLine($$"""
            var xmlNode = new {{typeof(XmlNode2).FullName}}({{roscharVarName}}, xmlnsAttributeName);
            var xmlNodePreciseType = xmlNode.{{nameof(XmlNode2.GetPreciseNodeType)}}();
            if(xmlNodePreciseType.IsEmpty)
            {
                xmlNodePreciseType = xmlNode.{{nameof(XmlNode2.DeclaredNodeType)}};
            }

            if(!xmlNodePreciseType.SequenceEqual(nameof({{ssGlobalName}}).AsSpan()))
            {
""");

                GenerateDeserializeDispatch(derived);

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
                        withHeadMethod,
                        parserInvocation
                        );
                }
            }

            _sb.AppendLine($$"""
        }
""");
        }


        private readonly void GenerateDeserializeDispatch(
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
                    {{classAndMethodName}}(xmlNodeInternals, xmlNode.{{nameof(XmlNode2.XmlnsAttributeName)}}, out {{d.ToGlobalDisplayString()}} iresult);
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
                            {{classAndMethodName}}(childInternals, child.XmlnsAttributeName, out {{d.ToGlobalDisplayString()}} iresult);
                            result.{{memberName}} = iresult;
                        }
""");
            }
        }


        private readonly void GenerateDeserializeMembers(
            List<ISymbol> members,
            bool withHeadMethod,
            string? parserInvocation
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
                    {{typeof(XmlNode2).FullName}}.{{nameof(XmlNode2.GetFirst)}}(internals, {{xmlnsAttributeNameVarName}} /*1*/, ref child);
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

                GenerateDeserializeMember(memberIndex, member, memberType, parserInvocation);
                memberIndex++;
            }

            _sb.AppendLine($$"""

                    internals = internals.Slice(child.FullNode.Length);
                    if(internals.IsEmpty)
                    {
                        break;
                    }
                }
            }
""");
        }

        private readonly void GenerateDeserializeMember(
            int index,
            ISymbol member,
            INamedTypeSymbol memberType,
            string? parserInvocation
            )
        {
            var elseif = index > 0 ? "else " : "";

            if(string.IsNullOrEmpty(parserInvocation) && BuiltinSourceProducer.TryGetBuiltin(_compilation, memberType, out var builtin))
            {
                var finalClause = string.Format(
                    builtin.ConverterClause,
                    $"child.{nameof(XmlNode2.Internals)}"
                    );

                _sb.AppendLine($$"""
                    //{{memberType.ToFullDisplayString()}}  {{member.Name}}
                    {{elseif}}if(childDeclaredNodeType.SequenceEqual({{member.Name}}Span))
                    {
                        result.{{member.Name}} = {{finalClause}};
                    }
""");
            }
            else if (memberType.EnumUnderlyingType != null)
            {
                var fullParserInvocation = GenerateEnumParseStatement(
                    memberType,
                    parserInvocation,
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
            //TODO array and other collections
            else if (
                memberType.TypeArguments.Length > 0
                && (SymbolEqualityComparer.Default.Equals(memberType, _compilation.List(memberType.TypeArguments[0])))
                )
            {
                var listItemType = (INamedTypeSymbol)memberType.TypeArguments[0];

                const string child2VarName = "child2";
                const string listItemParseResultVarName = "iresult";

                var listItemParseStatement = GenerateListItemParseStatement(
                    parserInvocation,
                    child2VarName,
                    listItemType,
                    listItemParseResultVarName
                    );

                _sb.AppendLine($$"""
                    //List<T>
                    {{elseif}}if(childDeclaredNodeType.SequenceEqual({{member.Name}}Span))
                    {
                        if(result.{{member.Name}} == default)
                        {
                            result.{{member.Name}} = new {{memberType.ToGlobalDisplayString()}}();
                        }

                        var childInternals = child.{{nameof(XmlNode2.Internals)}};
                        {{typeof(XmlNode2).FullName}} {{child2VarName}} = new();
                        while(true)
                        {
                            {{typeof(XmlNode2).FullName}}.{{nameof(XmlNode2.GetFirst)}}(childInternals, child.XmlnsAttributeName, ref {{child2VarName}});
                            if({{child2VarName}}.{{nameof(XmlNode2.IsEmpty)}})
                            {
                                break;
                            }

                            {{listItemParseStatement}}
                            result.{{member.Name}}.Add({{listItemParseResultVarName}});

                            childInternals = childInternals.Slice(
                                {{child2VarName}}.{{nameof(XmlNode2.FullNode)}}.Length
                                );
                            if(childInternals.IsEmpty)
                            {
                                break;
                            }
                        }
                    }
""");
            }
            else
            {
                //здесь, вероятно, какой-то другой тип

                if (!SerializationInfoCollection.TryGetSubject(memberType, out var subject))
                {
                    throw new InvalidOperationException($"(2) Unknown type {memberType.ToGlobalDisplayString()}");
                }

                if (memberType.IsAbstract || subject.Deriveds.Count > 0)
                {
                    //тут могут быть вариации
                    //генерируем метод с проверками наследников

                    var classAndMethodName = DetermineClassName(memberType) + "." + HeadlessDeserializeMethodName;

                    _sb.AppendLine($$"""
                    //custom type
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
                            {{classAndMethodName}}(childInternals, child.XmlnsAttributeName, out {{memberType.ToGlobalDisplayString()}} iresult);
                            result.{{member.Name}} = iresult;
                        }
                    }
""");
                }
                else
                {
                    //здесь всё четко, нет никаких вариаций типов и соотв. не должно быть типа в дочерней ноде

                    var withHeadBody = memberType.IsAbstract;
                    var methodName = withHeadBody ? HeadDeserializeMethodName : HeadlessDeserializeMethodName;
                    var classAndMethodName = DetermineClassName(memberType) + "." + methodName;

                    _sb.AppendLine($$"""
                    //custom type
                    {{elseif}}if(childDeclaredNodeType.SequenceEqual({{member.Name}}Span))
                    {
                        var childInternals = child.{{nameof(XmlNode2.Internals)}};
                        {{classAndMethodName}}(childInternals, child.XmlnsAttributeName, out {{memberType.ToGlobalDisplayString()}} iresult);
                        result.{{member.Name}} = iresult;
                    }
""");

                }

            }
        }

        private readonly string GenerateListItemParseStatement(
            string? parserInvocation,
            string child2VarName,
            INamedTypeSymbol listItemType,
            string listItemParseResultVarName
            )
        {
            if (listItemType.EnumUnderlyingType != null)
            {
                var listItemVarName = $"{child2VarName}.{nameof(XmlNode2.Internals)}";
                var enumParseStatement = GenerateEnumParseStatement(
                    listItemType,
                    parserInvocation,
                    listItemVarName
                    );
                return $"var {listItemParseResultVarName} = {enumParseStatement};";
            }
            else
            {
                var listItemVarName = $"{child2VarName}.{nameof(XmlNode2.FullNode)}";
                var classAndMethodName = DetermineClassName(listItemType) + "." + HeadDeserializeMethodName;

                return $@"{classAndMethodName}({listItemVarName}, {child2VarName}.{nameof(XmlNode2.XmlnsAttributeName)}, out {listItemType.ToGlobalDisplayString()} {listItemParseResultVarName});";
            }
        }

        private readonly string GenerateEnumParseStatement(
            INamedTypeSymbol memberType,
            string? parserInvocation,
            string varName
            )
        {
            if (!string.IsNullOrEmpty(parserInvocation))
            {
                var fullParserInvocation = string.Format(
                    parserInvocation,
                    varName
                    );

                return fullParserInvocation;
            }
            else
            {
                var fullParserInvocation =
                    $@"({memberType.ToGlobalDisplayString()})Enum.Parse(typeof({memberType.ToGlobalDisplayString()}), {varName})"
                    ;

                return fullParserInvocation;
            }
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

        private readonly string DetermineClassName(INamedTypeSymbol listItemType)
        {
            if (BuiltinSourceProducer.TryGetBuiltin(_compilation, listItemType, out _))
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

        private static INamedTypeSymbol ParseMember(ISymbol member)
        {
            INamedTypeSymbol memberType;
            if (member is IPropertySymbol property1)
            {
                memberType = (INamedTypeSymbol)property1.Type;
            }
            else if (member is IFieldSymbol field1)
            {
                memberType = (INamedTypeSymbol)field1.Type;
            }
            else
            {
                throw new NotImplementedException($"Unknown member type: {member.GetType().Name}");
            }

            return memberType;
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
                    XmlDeserializeGenerator.ParserAttributeFullName))
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
                else if (fsa == XmlDeserializeGenerator.ParserAttributeFullName)
                {
                    var ca0 = attribute.ConstructorArguments[0];
                    if (ca0.Kind != TypedConstantKind.Type)
                    {
                        throw new InvalidOperationException("Something wrong with attributes 6");
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
                        throw new InvalidOperationException("Something wrong with attributes 7");
                    }

                    var parserInvocation = (string)ca1.Value!;
                    sinfos[typegn] = sinfos[typegn].WithParserInvocation(parserInvocation);
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
                    sinfos[typegn].AddDerived(derived);
                }
            }

            //add default exhauster if no one specified
            if (exhaustList.Count == 0)
            {
                exhaustList.Add(compilation.DefaultStringBuilderExhauster());
            }

            return new SerializationInfoCollection(
                exhaustList,
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
