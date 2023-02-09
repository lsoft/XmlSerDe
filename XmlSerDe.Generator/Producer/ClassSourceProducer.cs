#if NETSTANDARD
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;
using XmlSerDe.Generator.Helper;
using XmlSerDe.Generator.EmbeddedCode;
using System.Reflection;
using System.Xml.Linq;
using System.Xml.Serialization;
using System.Data;

namespace XmlSerDe.Generator.Producer
{
    public struct ClassSourceProducer
    {

        public const string HeadDeserializeMethodName = "Deserialize";
        public const string HeadSerializeMethodName = "Serialize";
        public const string HeadlessDeserializeMethodName = "DeserializeBody";
        public const string HeadlessSerializeMethodName = "SerializeBody";

        public static readonly string WriteStringToStreamFullMethodName = "global::" + typeof(BuiltinSourceProducer).Namespace + "." + BuiltinSourceProducer.BuiltinCodeParserClassName + "." + BuiltinSourceProducer.WriteStringToStreamMethodName;
        public static readonly string WriteEncodedStringToStreamFullMethodName = "global::" + typeof(BuiltinSourceProducer).Namespace + "." + BuiltinSourceProducer.BuiltinCodeParserClassName + "." + BuiltinSourceProducer.WriteEncodedStringToStreamMethodName;

        private readonly Compilation _compilation;
        private readonly INamedTypeSymbol _deSubject;
        private readonly string _deSubjectGlobalType;
        private readonly string _deSubjectReflectionFormat1;
        private readonly SerializationSubjectInfoCollection _ssic;

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

            _ssic = CreateSubjects(_deSubject);
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

            GenerateSerializeMethods();
            GenerateDeserializeMethods();

            _sb.AppendLine($$"""
    }
}
""");

            return _sb.ToString();
        }


        #region serialize

        private readonly void GenerateSerializeMethods()
        {
            _sb.AppendLine($$"""
#region serialize

""");
            foreach (var ssi in _ssic.Infos)
            {
                GenerateSerializeMethod(
                    ssi
                    );
            }

            _sb.AppendLine($$"""

#endregion
""");
        }

        private readonly void GenerateSerializeMethod(
            SerializationSubjectInfo ssi
            )
        {
            var subject = ssi.Subject;

            if (ssi.IsRoot)
            {
                GenerateRootSerializeMethod(subject);
            }

            GenerateSerializeMethod(subject, ssi.Derived, true);
            //GenerateSerializeMethod(subject, ssi.Derived, false);
        }

        private readonly void GenerateRootSerializeMethod(
            INamedTypeSymbol subject
            )
        {
            var ssGlobalName = subject.ToGlobalDisplayString();

            _sb.AppendLine($$"""
        public static void {{HeadSerializeMethodName}}(global::System.IO.Stream stream, {{ssGlobalName}} obj, bool appendXmlHead)
        {
            if(appendXmlHead)
            {
                global::{{typeof(BuiltinSourceProducer).Namespace}}.{{BuiltinSourceProducer.BuiltinCodeParserClassName}}.{{BuiltinSourceProducer.AppendXmlHeadMethodName}}(stream);
            }

            {{HeadSerializeMethodName}}(stream, obj);
        }

""");
        }

        private readonly void GenerateSerializeMethod(
            INamedTypeSymbol subject,
            List<INamedTypeSymbol> derived,
            bool withHeadMethod
            )
        {
            var methodName = withHeadMethod ? HeadSerializeMethodName : HeadlessSerializeMethodName;

            var ssGlobalName = subject.ToGlobalDisplayString();

            _sb.AppendLine($$"""
        private static void {{methodName}}(global::System.IO.Stream stream, {{ssGlobalName}} obj)
        {
""");


            _sb.AppendLine($$"""

            {{WriteStringToStreamFullMethodName}}(stream, "<{{subject.Name}}>");

""");

            var members = GetMembersOrderByInheritance(subject);
            if (members.Count > 0)
            {
                GenerateSerializeMembers(methodName, members);
            }

            _sb.AppendLine($$"""

            {{WriteStringToStreamFullMethodName}}(stream, "</{{subject.Name}}>");

""");

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
            {
                {{WriteStringToStreamFullMethodName}}(stream, "<{{member.Name}}>");

""");


            if (BuiltinSourceProducer.TryGetBuiltin(_compilation, memberType, out var builtin))
            {
                var toStringClause = string.Format(
                    builtin.ToStringClause,
                    "obj." + member.Name
                    );

                var serializeMethodName = builtin.IsNeedToGuardWhenEncoded
                    ? WriteEncodedStringToStreamFullMethodName
                    : WriteStringToStreamFullMethodName
                    ;

                _sb.AppendLine($$"""
                {{serializeMethodName}}(
                    stream,
                    {{toStringClause}}
                    );
""");


            }
            //TODO array and other collections
            else if (
                memberType.TypeArguments.Length > 0
                && (SymbolEqualityComparer.Default.Equals(memberType, _compilation.List(memberType.TypeArguments[0])))
                )
            {
                var listItemType = (INamedTypeSymbol)memberType.TypeArguments[0];
                //var classAndMethodName = DetermineClassName(listItemType) + "." + HeadDeserializeMethodName;

                if (BuiltinSourceProducer.TryGetBuiltin(_compilation, listItemType, out var listBuiltin))
                {
                    var toStringClause = string.Format(
                        listBuiltin.ToStringClause,
                        "obj." + member.Name + "[index]"
                        );

                    var serializeMethodName = listBuiltin.IsNeedToGuardWhenEncoded
                        ? WriteEncodedStringToStreamFullMethodName
                        : WriteStringToStreamFullMethodName
                        ;

                    _sb.AppendLine($$"""

                for(var index = 0; index < obj.{{member.Name}}.Count; index++)
                {
                    {{serializeMethodName}}(
                        stream,
                        {{toStringClause}}
                        );
                }
""");
                }
                else
                {
                    _sb.AppendLine($$"""

                for(var index = 0; index < obj.{{member.Name}}.Count; index++)
                {
                    {{methodName}}(stream, obj.{{member.Name}}[index]);
                }
""");
                }
            }
            else
            {
                _sb.AppendLine($$"""
                //{{methodName}}(stream, obj.{{member.Name}});
""");
            }


            _sb.AppendLine($$"""
                {{WriteStringToStreamFullMethodName}}(stream, "</{{member.Name}}>");
""");

            _sb.AppendLine($$"""
            }
""");

        }


        #endregion

        #region deserialize

        private readonly void GenerateDeserializeMethods()
        {
            _sb.AppendLine($$"""
#region deserialize

""");
            foreach (var ssi in _ssic.Infos)
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
            SerializationSubjectInfo ssi
            )
        {
            var subject = ssi.Subject;

            GenerateDeserializeMethod(subject, ssi.Derived, ssi.FactoryInvocation, ssi.ParserInvocation, true);
            GenerateDeserializeMethod(subject, ssi.Derived, ssi.FactoryInvocation, ssi.ParserInvocation, false);

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
                if (string.IsNullOrEmpty(parserInvocation))
                {
                    _sb.AppendLine($$"""
                    //Enum
                    {{elseif}}if(childDeclaredNodeType.SequenceEqual({{member.Name}}Span))
                    {
                        var child2 = child.{{nameof(XmlNode2.Internals)}};
                        result.{{member.Name}} = ({{memberType.ToGlobalDisplayString()}})Enum.Parse(typeof({{memberType.ToGlobalDisplayString()}}), child2);
                    }
""");
                }
                else
                {
                    var fullParserInvocation = string.Format(
                        parserInvocation,
                        "child2"
                        );

                    _sb.AppendLine($$"""
                    //Enum
                    {{elseif}}if(childDeclaredNodeType.SequenceEqual({{member.Name}}Span))
                    {
                        var child2 = child.{{nameof(XmlNode2.Internals)}};
                        result.{{member.Name}} = {{fullParserInvocation}};
                    }
""");
                }
            }
            //TODO array and other collections
            else if (
                memberType.TypeArguments.Length > 0
                && (SymbolEqualityComparer.Default.Equals(memberType, _compilation.List(memberType.TypeArguments[0])))
                )
            {
                var listItemType = (INamedTypeSymbol)memberType.TypeArguments[0];
                var classAndMethodName = DetermineClassName(listItemType) + "." + HeadDeserializeMethodName;

                _sb.AppendLine($$"""
                    //List<T>
                    {{elseif}}if(childDeclaredNodeType.SequenceEqual({{member.Name}}Span))
                    {
                        if(result.{{member.Name}} == default)
                        {
                            result.{{member.Name}} = new {{memberType.ToGlobalDisplayString()}}();
                        }

                        var childInternals = child.{{nameof(XmlNode2.Internals)}};
                        {{typeof(XmlNode2).FullName}} child2 = new();
                        while(true)
                        {
                            {{typeof(XmlNode2).FullName}}.{{nameof(XmlNode2.GetFirst)}}(childInternals, child.XmlnsAttributeName, ref child2);
                            if(child2.{{nameof(XmlNode2.IsEmpty)}})
                            {
                                break;
                            }

                            {{classAndMethodName}}(child2.{{nameof(XmlNode2.FullNode)}}, child2.XmlnsAttributeName, out {{listItemType.ToGlobalDisplayString()}} iresult);
                            result.{{member.Name}}.Add(iresult);

                            childInternals = childInternals.Slice(
                                child2.{{nameof(XmlNode2.FullNode)}}.Length
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

                if (!_ssic.TryGetSubject(memberType, out var subject))
                {
                    throw new InvalidOperationException($"(2) Unknown type {memberType.ToGlobalDisplayString()}");
                }

                if (memberType.IsAbstract || subject.Derived.Count > 0)
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


                    GenerateDeserializeDispatch2(subject.Derived, member.Name);

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
                return typeof(BuiltinSourceProducer).Namespace + "." + BuiltinSourceProducer.BuiltinCodeParserClassName;
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

        private static SerializationSubjectInfoCollection CreateSubjects(
            INamedTypeSymbol deSubject
            )
        {
            if (deSubject is null)
            {
                throw new ArgumentNullException(nameof(deSubject));
            }

            var result = new Dictionary<string, SerializationSubjectInfo>();

            foreach (var attribute in deSubject.GetAttributes())
            {
                var attrSymbol = attribute.AttributeClass;
                if (attrSymbol is null)
                {
                    continue;
                }
                var fsa = attrSymbol.ToFullDisplayString();
                if (fsa.NotIn(XmlDeserializeGenerator.SubjectAttributeFullName, XmlDeserializeGenerator.DerivedSubjectAttributeFullName, XmlDeserializeGenerator.FactoryAttributeFullName, XmlDeserializeGenerator.ParserAttributeFullName))
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
                    if (result.ContainsKey(typegn))
                    {
                        throw new InvalidOperationException($"Type is already contains in the attribute list: {typegn}");
                    }

                    var ca1 = attribute.ConstructorArguments[1];
                    if (ca1.Kind != TypedConstantKind.Primitive)
                    {
                        throw new InvalidOperationException("Something wrong with attributes 2");
                    }
                    var isRoot = (bool)ca1.Value!;

                    var ssi = new SerializationSubjectInfo(
                        type,
                        isRoot
                        );
                    result[typegn] = ssi;
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
                    if (!result.ContainsKey(typegn))
                    {
                        throw new InvalidOperationException($"Type is not contains in the attribute list: {typegn}");
                    }

                    var ca1 = attribute.ConstructorArguments[1];
                    if (ca1.Kind != TypedConstantKind.Primitive)
                    {
                        throw new InvalidOperationException("Something wrong with attributes 4");
                    }

                    var factoryInvocation = (string)ca1.Value!;
                    result[typegn] = result[typegn].WithFactoryInvocation(factoryInvocation);
                }
                else if (fsa == XmlDeserializeGenerator.ParserAttributeFullName)
                {
                    var ca0 = attribute.ConstructorArguments[0];
                    if (ca0.Kind != TypedConstantKind.Type)
                    {
                        throw new InvalidOperationException("Something wrong with attributes 5");
                    }
                    var type = (INamedTypeSymbol)ca0.Value!;
                    var typegn = type.ToGlobalDisplayString();
                    if (!result.ContainsKey(typegn))
                    {
                        throw new InvalidOperationException($"Type is not contains in the attribute list: {typegn}");
                    }

                    var ca1 = attribute.ConstructorArguments[1];
                    if (ca1.Kind != TypedConstantKind.Primitive)
                    {
                        throw new InvalidOperationException("Something wrong with attributes 6");
                    }

                    var parserInvocation = (string)ca1.Value!;
                    result[typegn] = result[typegn].WithParserInvocation(parserInvocation);
                }
                else
                {
                    var ca0 = attribute.ConstructorArguments[0];
                    if (ca0.Kind != TypedConstantKind.Type)
                    {
                        throw new InvalidOperationException("Something wrong with attributes 7");
                    }

                    var ca1 = attribute.ConstructorArguments[1];
                    if (ca1.Kind != TypedConstantKind.Type)
                    {
                        throw new InvalidOperationException("Something wrong with attributes 8");
                    }

                    var type = (INamedTypeSymbol)ca0.Value!;
                    var typegn = type.ToGlobalDisplayString();
                    var derived = (INamedTypeSymbol)ca1.Value!;
                    result[typegn].AddDerived(derived);
                }
            }

            return new SerializationSubjectInfoCollection(
                result.Values.ToList()
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
