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
    public struct DeserializeSourceProducer
    {
        public const string HeadDeserializeMethodName = "Deserialize";
        public const string HeadlessDeserializeMethodName = "DeserializeBody";

        private readonly Compilation _compilation;
        private readonly INamedTypeSymbol _deSubject;
        private readonly string _deSubjectGlobalType;
        private readonly string _deSubjectReflectionFormat1;
        private readonly SerializationSubjectInfoCollection _ssic;

        private StringBuilder _sb;
        private Random _rnd;

        public DeserializeSourceProducer(
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
            _ssic = CreateSubjects();

            _sb = null!; //просто заткнуть проверку, 2 строками ниже оно инициализируется
            _rnd = null!; //просто заткнуть проверку, строкой ниже оно инициализируется
            Prepare();
        }

        /// <summary>
        /// Генерация десериализатора
        /// </summary>
        public string GenerateDeserializer(
            )
        {

            Prepare();

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

            foreach (var ssi in _ssic.Infos)
            {
                GenerateMethod(
                    ssi
                    );
            }

            _sb.AppendLine($$"""
    }
}
""");

            return _sb.ToString();
        }

        private SerializationSubjectInfoCollection CreateSubjects()
        {
            var result = new Dictionary<string, SerializationSubjectInfo>();

            foreach (var attribute in _deSubject.GetAttributes())
            {
                var attrSymbol = attribute.AttributeClass;
                if (attrSymbol is null)
                {
                    continue;
                }
                var fsa = attrSymbol.ToFullDisplayString();
                if (fsa.NotIn(XmlDeserializeGenerator.SubjectAttributeFullName, XmlDeserializeGenerator.DerivedSubjectAttributeFullName))
                {
                    continue;
                }
                if (attribute.ConstructorArguments.Length == 0)
                {
                    continue;
                }

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
                    var ssi = new SerializationSubjectInfo(
                        type
                        );
                    result[typegn] = ssi;
                }
                else
                {
                    var ca0 = attribute.ConstructorArguments[0];
                    if (ca0.Kind != TypedConstantKind.Type)
                    {
                        throw new InvalidOperationException("Something wrong with attributes 2");
                    }

                    var ca1 = attribute.ConstructorArguments[1];
                    if (ca1.Kind != TypedConstantKind.Type)
                    {
                        throw new InvalidOperationException("Something wrong with attributes 3");
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

        private void GenerateMethod(
            SerializationSubjectInfo ssi
            )
        {
            var subject = ssi.Subject;

            GenerateMethod(subject, ssi.Derived, true);
            GenerateMethod(subject, ssi.Derived, false);
        }

        private void GenerateMethod(
            INamedTypeSymbol subject,
            List<INamedTypeSymbol> derived,
            bool withHeadMethod
            )
        {
            var roscharVarName = withHeadMethod ? "fullNode" : "internals";
            var privatePublic = withHeadMethod ? "public" : "private";
            var methodName = withHeadMethod ? HeadDeserializeMethodName : HeadlessDeserializeMethodName;

            var ssGlobalName = subject.ToGlobalDisplayString();

            _sb.AppendLine($$"""
        {{privatePublic}} static void {{methodName}}(roschar {{roscharVarName}}, out {{ssGlobalName}} result)
        {
""");

            if (withHeadMethod)
            {
                _sb.AppendLine($$"""
            var xmlNode = new {{typeof(XmlNode2).FullName}}({{roscharVarName}});
            var xmlNodePreciseType = xmlNode.{{nameof(XmlNode2.GetPreciseNodeType)}}();
            if(xmlNodePreciseType.IsEmpty)
            {
                xmlNodePreciseType = xmlNode.{{nameof(XmlNode2.DeclaredNodeType)}};
            }

            if(!xmlNodePreciseType.SequenceEqual(nameof({{ssGlobalName}}).AsSpan()))
            {
""");

                GenerateDispatch(derived);

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

                _sb.AppendLine($$"""
            result = new {{ssGlobalName}}();
""");

                var members = GetMembersOrderByInheritance(subject);
                if (members.Count > 0)
                {
                    GenerateMembers(members, withHeadMethod);
                }
            }

            _sb.AppendLine($$"""
        }
""");
        }


        private readonly void GenerateDispatch(
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
                //{{nameof(GenerateDispatch)}}
                if (xmlNodePreciseType.SequenceEqual(nameof({{d.Name}}).AsSpan()))
                {
                    {{classAndMethodName}}(xmlNodeInternals, out {{d.ToGlobalDisplayString()}} iresult);
                    result = iresult;
                    return;
                }
""");
            }
        }


        private readonly void GenerateDispatch2(
            List<INamedTypeSymbol> derived,
            string memberName
            )
        {
            foreach (var d in derived)
            {
                var classAndMethodName = DetermineClassName(d) + "." + HeadlessDeserializeMethodName;

                _sb.AppendLine($$"""
                        //{{nameof(GenerateDispatch2)}}
                        if (childPreciseType.SequenceEqual(nameof({{d.Name}}).AsSpan()))
                        {
                            {{classAndMethodName}}(childInternals, out {{d.ToGlobalDisplayString()}} iresult);
                            result.{{memberName}} = iresult;
                        }
""");
            }
        }


        private void GenerateMembers(List<ISymbol> members, bool withHeadMethod)
        {
            _sb.AppendLine($$"""
            if(!internals.IsEmpty)
            {

""");
            foreach (var member in EnumerateMembers(members))
            {
                _sb.AppendLine($$"""
                var {{member.Name}}Span = "{{member.Name}}".AsSpan();
""");
                
            }

            _sb.AppendLine($$"""
                {{typeof(XmlNode2).FullName}} child = new();
                while(true)
                {
                    {{typeof(XmlNode2).FullName}}.{{nameof(XmlNode2.GetFirst)}}(internals, ref child);
                    if(child.IsEmpty)
                    {
                        break;
                    }
                    var childDeclaredNodeType = child.{{nameof(XmlNode2.DeclaredNodeType)}};

""");

            var memberIndex = 0;
            foreach (var member in EnumerateMembers(members))
            {
                var memberType = ParseMember(member);

                GenerateMember(memberIndex, member, memberType);
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

        private readonly IEnumerable<ISymbol> EnumerateMembers(
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

                yield return member;
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

        private readonly void GenerateMember(
            int index,
            ISymbol member,
            INamedTypeSymbol memberType
            )
        {
            var elseif = index > 0 ? "else " : "";

            if(BuiltinSourceProducer.TryGetBuiltin(_compilation, memberType, out var builtin))
            {
                var finalClause = string.Format(
                    builtin.ConverterClause,
                    $"child.{nameof(XmlNode2.Internals)}"
                    );

                _sb.AppendLine($$"""
                    //{{memberType.ToFullDisplayString()}}
                    {{elseif}}if(childDeclaredNodeType.SequenceEqual({{member.Name}}Span))
                    {
                        result.{{member.Name}} = {{finalClause}};
                    }
""");
            }
            else if (memberType.EnumUnderlyingType != null)
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
                            {{typeof(XmlNode2).FullName}}.{{nameof(XmlNode2.GetFirst)}}(childInternals, ref child2);
                            if(child2.{{nameof(XmlNode2.IsEmpty)}})
                            {
                                break;
                            }

                            {{classAndMethodName}}(child2.{{nameof(XmlNode2.FullNode)}}, out {{listItemType.ToGlobalDisplayString()}} iresult);
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


                    GenerateDispatch2(subject.Derived, member.Name);

                    _sb.AppendLine($$"""
                    }
                    else
                    {
                        if(childDeclaredNodeType.SequenceEqual({{member.Name}}Span))
                        {
                            var childInternals = child.{{nameof(XmlNode2.Internals)}};
                            {{classAndMethodName}}(childInternals, out {{memberType.ToGlobalDisplayString()}} iresult);
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
                        {{classAndMethodName}}(childInternals, out {{memberType.ToGlobalDisplayString()}} iresult);
                        result.{{member.Name}} = iresult;
                    }
""");

                }

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


        private void Prepare()
        {
            _sb = new();
            _rnd = new(123);
        }

        private string RandomSuffix()
        {
            return _rnd.Next(int.MaxValue).ToString();
        }
    }
}
#endif
