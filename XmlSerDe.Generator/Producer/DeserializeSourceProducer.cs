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

        private readonly BuiltinCollection _builtins;

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

            _builtins = new BuiltinCollection(
                    new List<Builtin>
                    {
                        new Builtin("global::System.DateTime.Parse({0})", _compilation.DateTime(), false),
                        new Builtin("global::System.Boolean.Parse({0})", _compilation.Bool(), false),
                        new Builtin("global::System.Net.WebUtility.HtmlDecode({0})", _compilation.String(), true),

                        new Builtin("global::System.SByte.Parse({0}, provider: global::System.Globalization.CultureInfo.InvariantCulture)", _compilation.SByte(), false),
                        new Builtin("global::System.Byte.Parse({0}, provider: global::System.Globalization.CultureInfo.InvariantCulture)", _compilation.Byte(), false),

                        new Builtin("global::System.UInt16.Parse({0}, provider: global::System.Globalization.CultureInfo.InvariantCulture)", _compilation.UInt16(), false),
                        new Builtin("global::System.Int16.Parse({0}, provider: global::System.Globalization.CultureInfo.InvariantCulture)", _compilation.Int16(), false),

                        new Builtin("global::System.UInt32.Parse({0}, provider: global::System.Globalization.CultureInfo.InvariantCulture)", _compilation.UInt32(), false),
                        new Builtin("global::System.Int32.Parse({0}, provider: global::System.Globalization.CultureInfo.InvariantCulture)", _compilation.Int32(), false),

                        new Builtin("global::System.UInt64.Parse({0}, provider: global::System.Globalization.CultureInfo.InvariantCulture)", _compilation.UInt64(), false),
                        new Builtin("global::System.Int64.Parse({0}, provider: global::System.Globalization.CultureInfo.InvariantCulture)", _compilation.Int64(), false),
                        //TODO other builtin branches
                    }
                    );


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

            GenerateBuiltinMethods();

            _sb.AppendLine($$"""
    }
}
""");

            return _sb.ToString();
        }

        private void GenerateBuiltinMethods()
        {
            _sb.AppendLine($$"""

        public static void {{HeadDeserializeMethodName}}(roschar fullNode, out string result)
        {
            var xmlNode = new {{typeof(XmlNode2).FullName}}(fullNode);
            
            var xmlNodeDeclareType = xmlNode.GetDeclaredNodeType();
            if(!xmlNodeDeclareType.SequenceEqual("string".AsSpan()))
            {
                throw new InvalidOperationException("Unknown type " + xmlNodeDeclareType.ToString() + " (should be a string)");
            }

            result = System.Net.WebUtility.HtmlDecode(
                xmlNode.{{nameof(XmlNode2.Internals)}}.ToString()
                );
        }
""");

            var scopeds = new List<(string, string)>();
            scopeds.Add(("dateTime", "global::System.DateTime"));
            scopeds.Add(("boolean", "global::System.Boolean"));
            scopeds.Add(("unsignedByte", "global::System.Byte"));
            
            scopeds.Add(("byte", "global::System.SByte"));
            scopeds.Add(("unsignedShort", "global::System.UInt16"));
            
            scopeds.Add(("short", "global::System.Int16"));
            scopeds.Add(("unsignedInt", "global::System.UInt32"));
            
            scopeds.Add(("int", "global::System.Int32"));
            scopeds.Add(("unsignedLong", "global::System.UInt64"));
            
            scopeds.Add(("long", "global::System.Int64"));
            scopeds.Add(("decimal", "global::System.Decimal"));
            //TODO other builtin branches

            foreach (var scoped in scopeds)
            {
                _sb.AppendLine($$"""
        public static void {{HeadDeserializeMethodName}}(roschar fullNode, out {{scoped.Item2}} result)
        {
            var xmlNode = new {{typeof(XmlNode2).FullName}}(fullNode);
            var xmlNodePreciseType = xmlNode.GetPreciseNodeType();

            if(!xmlNodePreciseType.SequenceEqual("{{scoped.Item1}}".AsSpan()))
            {
                throw new InvalidOperationException("Unknown type " + xmlNodePreciseType.ToString());
            }

            result = {{scoped.Item2}}.Parse(
                xmlNode.{{nameof(XmlNode2.Internals)}}.ToString()
                );
        }
""");

            }
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
                if (fsa.NotIn(XmlDeserializeGenerator.RootAttributeFullName, XmlDeserializeGenerator.SubjectAttributeFullName, XmlDeserializeGenerator.DerivedSubjectAttributeFullName))
                {
                    continue;
                }
                if (attribute.ConstructorArguments.Length == 0)
                {
                    continue;
                }

                if (fsa == XmlDeserializeGenerator.RootAttributeFullName)
                {

                    var ca0 = attribute.ConstructorArguments[0];
                    if (ca0.Kind != TypedConstantKind.Type)
                    {
                        throw new InvalidOperationException("Something wrong with attributes 0");
                    }

                    var type = (INamedTypeSymbol)ca0.Value!;
                    var typegn = type.ToGlobalDisplayString();
                    if (result.ContainsKey(typegn))
                    {
                        throw new InvalidOperationException($"Type is already contains in the attribute list: {typegn}");
                    }

                    var ssi = new SerializationSubjectInfo(
                        true,
                        type
                        );
                    result[typegn] = ssi;
                }
                else if (fsa == XmlDeserializeGenerator.SubjectAttributeFullName)
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
                        false,
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
            //var additionalArgument = withHeadMethod ? "" : $"{nameof(ScanHeadResult)} head, ";

            var ssGlobalName = subject.ToGlobalDisplayString();

            _sb.AppendLine($$"""
        {{privatePublic}} static void {{methodName}}(roschar {{roscharVarName}}, out {{ssGlobalName}} result)
        {
""");

            if (withHeadMethod)
            {
                _sb.AppendLine($$"""
            var xmlNode = new {{typeof(XmlNode2).FullName}}({{roscharVarName}});
            var xmlNodePreciseType = xmlNode.GetPreciseNodeType();
            if(xmlNodePreciseType.IsEmpty)
            {
                xmlNodePreciseType = xmlNode.GetDeclaredNodeType();
            }

            if(!xmlNodePreciseType.SequenceEqual(nameof({{ssGlobalName}}).AsSpan()))
            {
""");

                GenerateDispatch(derived);

                _sb.AppendLine($$"""

                throw new InvalidOperationException("Unknown type " + xmlNodePreciseType.ToString());
            }

""");
            }

            if(subject.IsAbstract)
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
                _sb.AppendLine($$"""
                //{{nameof(GenerateDispatch)}}
                if (xmlNodePreciseType.SequenceEqual(nameof({{d.Name}}).AsSpan()))
                {
                    {{HeadlessDeserializeMethodName}}(xmlNodeInternals, out {{d.ToGlobalDisplayString()}} iresult);
                    result = iresult;
                    return;
                }
""");
            }
        }


        private readonly void GenerateDispatch2(string memberName, SerializationSubjectInfo subject)
        {
            foreach (var d in subject.Derived)
            {
                _sb.AppendLine($$"""
                        //{{nameof(GenerateDispatch2)}}
                        if (childPreciseType.SequenceEqual(nameof({{d.Name}}).AsSpan()))
                        {
                            {{HeadlessDeserializeMethodName}}(childInternals, out {{d.ToGlobalDisplayString()}} iresult);
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
                {{typeof(XmlNode2).FullName}} child;
                while(true)
                {
                    child = {{typeof(XmlNode2).FullName}}.{{nameof(XmlNode2.GetFirst)}}(internals);
                    if(child.IsEmpty)
                    {
                        break;
                    }
                    var childDeclaredNodeType = child.{{nameof(XmlNode2.GetDeclaredNodeType)}}();

""");

            var memberIndex = 0;
            foreach(var member in members)
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

        private readonly void GenerateMember(
            int index,
            ISymbol member,
            INamedTypeSymbol memberType
            )
        {
            var elseif = index > 0 ? "else " : "";

            var processed = false;
            foreach(var v in _builtins.Builtins)
            {
                var converterClause = v.ConverterClause;
                var symbol = v.Symbol;
                var needToStringClause = v.NeedToStringClause;

                if (SymbolEqualityComparer.Default.Equals(memberType, symbol))
                {
                    var toStringSuffix = needToStringClause ? ".ToString()" : "";
                    var finalClause = string.Format(
                        converterClause,
                        $"child.{nameof(XmlNode2.Internals)}{toStringSuffix}"
                        );

                    _sb.AppendLine($$"""
                    //{{symbol.ToFullDisplayString()}}
                    {{elseif}}if(childDeclaredNodeType.SequenceEqual("{{member.Name}}".AsSpan()))
                    {
                        result.{{member.Name}} = {{finalClause}};
                    }
""");
                    processed = true;
                    break;
                }

            }

            if (!processed)
            {
                if (memberType.EnumUnderlyingType != null)
                {
                    _sb.AppendLine($$"""
                    //Enum
                    {{elseif}}if(childDeclaredNodeType.SequenceEqual("{{member.Name}}".AsSpan()))
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
                    _sb.AppendLine($$"""
                    //List<T>
                    {{elseif}}if(childDeclaredNodeType.SequenceEqual("{{member.Name}}".AsSpan()))
                    {
                        if(result.{{member.Name}} == default)
                        {
                            result.{{member.Name}} = new {{memberType.ToGlobalDisplayString()}}();
                        }

                        var childInternals = child.{{nameof(XmlNode2.Internals)}};
                        {{typeof(XmlNode2).FullName}} child2;
                        while(true)
                        {
                            child2 = {{typeof(XmlNode2).FullName}}.{{nameof(XmlNode2.GetFirst)}}(childInternals);
                            if(child2.{{nameof(XmlNode2.IsEmpty)}})
                            {
                                break;
                            }

                            {{HeadDeserializeMethodName}}(child2.{{nameof(XmlNode2.FullNode)}}, out {{memberType.TypeArguments[0].ToGlobalDisplayString()}} iresult);
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
                        throw new InvalidOperationException($"Unknown type {memberType.ToGlobalDisplayString()}");
                    }

                    if (memberType.IsAbstract || subject.Derived.Count > 0)
                    {
                        //тут могут быть вариации
                        //генерируем метод с проверками наследников

                        _sb.AppendLine($$"""
                    //custom type
                    var childPreciseType = child.GetPreciseNodeType();
                    {{elseif}}if(!childPreciseType.IsEmpty)
                    {
                        var childInternals = child.{{nameof(XmlNode2.Internals)}};

""");


                        GenerateDispatch2(member.Name, subject);

                        _sb.AppendLine($$"""
                    }
                    else
                    {
                        if(childDeclaredNodeType.SequenceEqual("{{member.Name}}".AsSpan()))
                        {
                            var childInternals = child.{{nameof(XmlNode2.Internals)}};
                            {{HeadlessDeserializeMethodName}}(childInternals, out {{memberType.ToGlobalDisplayString()}} iresult);
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

                        _sb.AppendLine($$"""
                    //custom type
                    {{elseif}}if(childDeclaredNodeType.SequenceEqual("{{member.Name}}".AsSpan()))
                    {
                        var childInternals = child.{{nameof(XmlNode2.Internals)}};
                        {{methodName}}(childInternals, out {{memberType.ToGlobalDisplayString()}} iresult);
                        result.{{member.Name}} = iresult;
                    }
""");

                    }

                }
            }
        }

        private void GenerateUsings()
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

        private void GrabUsings(
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
