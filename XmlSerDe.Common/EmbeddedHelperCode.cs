using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Xml.Linq;
using roschar = System.ReadOnlySpan<char>;

namespace XmlSerDe.Common
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class XmlExhausterAttribute : Attribute
    {
        public readonly Type ExhausterType;

        public XmlExhausterAttribute(Type exhausterType)
        {
            if (exhausterType is null)
            {
                throw new ArgumentNullException(nameof(exhausterType));
            }

            ExhausterType = exhausterType;
        }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class XmlFactoryAttribute : Attribute
    {
        public readonly Type SubjectType;
        public readonly string InvocationStatement;

        public XmlFactoryAttribute(Type subjectType, string invocationStatement)
        {
            if (subjectType is null)
            {
                throw new ArgumentNullException(nameof(subjectType));
            }
            if (invocationStatement is null)
            {
                throw new ArgumentNullException(nameof(invocationStatement));
            }

            SubjectType = subjectType;
            InvocationStatement = invocationStatement;
        }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class XmlInjectorAttribute : Attribute
    {
        public readonly Type InjectorType;

        public XmlInjectorAttribute(Type injectorType)
        {
            if (injectorType is null)
            {
                throw new ArgumentNullException(nameof(injectorType));
            }

            InjectorType = injectorType;
        }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class XmlSubjectAttribute : Attribute
    {
        public readonly Type SubjectType;
        public readonly bool IsRoot;

        public XmlSubjectAttribute(Type subjectType, bool isRoot)
        {
            if (subjectType is null)
            {
                throw new ArgumentNullException(nameof(subjectType));
            }

            SubjectType = subjectType;
            IsRoot = isRoot;
        }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class XmlDerivedSubjectAttribute : Attribute
    {
        public readonly Type SubjectType;
        public readonly Type DerivedType;

        public XmlDerivedSubjectAttribute(Type subjectType, Type derivedType)
        {
            if (subjectType is null)
            {
                throw new ArgumentNullException(nameof(subjectType));
            }

            if (derivedType is null)
            {
                throw new ArgumentNullException(nameof(derivedType));
            }

            SubjectType = subjectType;
            DerivedType = derivedType;
        }
    }


    public readonly ref struct XmlNode2
    {
        public static readonly string CDataHead = "<![CDATA[";
        public static readonly string CDataTail = "]]>";

        /// <summary>
        /// Struct is empty.
        /// </summary>
        public readonly bool IsEmpty;

        /// <summary>
        /// Full node span (including spaces).
        /// </summary>
        public readonly roschar FullNode;

        /// <summary>
        /// Вся голова этой ноды. Для
        /// <SomeNode a:b="c" > </SomeNode>
        /// возвращает
        /// <SomeNode a:b="c" >
        /// (а также пробелы перед нодой).
        /// </summary>
        public readonly roschar FullHead;

        /// <summary>
        /// Тип этой ноды. Для
        /// <SomeNode a:b="c" > </SomeNode>
        /// возвращает
        /// SomeNode
        /// </summary>
        public readonly roschar DeclaredNodeType;

        /// <summary>
        /// Количество пробелов (и переносов строк) до начала головы в FullHead
        /// </summary>
        public readonly int FullHeadPrefixLength;
        
        /// <summary>
        /// Закрытая нода <SomeNode /> (без отдельного закрывающего тега)
        /// </summary>
        public readonly bool IsBodyless;

        /// <summary>
        /// Internals of this node (including spaces, without leading comment if exists).
        /// </summary>
        public readonly roschar Internals;

        /// <summary>
        /// Attribute name for xmlns.
        /// </summary>
        public readonly roschar XmlnsAttributeName;

        public XmlNode2()
        {
            IsEmpty = true;
            FullNode = roschar.Empty;
            FullHead = roschar.Empty;
            DeclaredNodeType = roschar.Empty;
            FullHeadPrefixLength = 0;
            IsBodyless = true;
            Internals = roschar.Empty;
            XmlnsAttributeName = roschar.Empty;
        }

        public XmlNode2(
            XmlDeserializeSettings settings,
            roschar fullNode,
            roschar xmlnsAttributeName
            )
        {
            IsEmpty = fullNode.IsEmpty;
            FullNode = fullNode;

            if (fullNode.IsEmpty)
            {
                FullHead = roschar.Empty;
                DeclaredNodeType = roschar.Empty;
                FullHeadPrefixLength = 0;
                IsBodyless = true;
                Internals = roschar.Empty;
                return;
            }

            var trimmed = fullNode.TrimStart();
            if (trimmed.IsEmpty)
            {
                FullHead = roschar.Empty;
                DeclaredNodeType = roschar.Empty;
                FullHeadPrefixLength = 0;
                IsBodyless = true;
                Internals = roschar.Empty;
                return;
            }

            FullHeadPrefixLength = fullNode.Length - trimmed.Length;
            ScanHead_Core(fullNode, trimmed, out var fullHeadLength, out DeclaredNodeType, out IsBodyless);
            FullHead = fullNode.Slice(0, fullHeadLength);
            Internals = GetInternalsOf(settings.ContainsXmlComments);

            if (xmlnsAttributeName.IsEmpty)
            {
                XmlnsAttributeName = GetXmlnsAttributeName();
            }
            else
            {
                XmlnsAttributeName = xmlnsAttributeName;
            }
        }

        /// <summary>
        /// Конструктор для случая, когда голова ноды уже разобрана вызывающей стороной
        /// (см. <see cref="GetFirstLength"/>). Разбирать её повторно незачем: результат
        /// заведомо тот же, потому что fullNode - это префикс того же самого спана,
        /// а голова целиком помещается внутри этого префикса.
        /// </summary>
        private XmlNode2(
            XmlDeserializeSettings settings,
            roschar fullNode,
            roschar xmlnsAttributeName,
            int fullHeadPrefixLength,
            int fullHeadLength,
            int declaredNodeTypeLength,
            bool isBodyless
            )
        {
            IsEmpty = false;
            FullNode = fullNode;
            FullHeadPrefixLength = fullHeadPrefixLength;
            FullHead = fullNode.Slice(0, fullHeadLength);
            DeclaredNodeType = fullNode.Slice(fullHeadPrefixLength + 1, declaredNodeTypeLength);
            IsBodyless = isBodyless;
            Internals = GetInternalsOf(settings.ContainsXmlComments);

            if (xmlnsAttributeName.IsEmpty)
            {
                XmlnsAttributeName = GetXmlnsAttributeName();
            }
            else
            {
                XmlnsAttributeName = xmlnsAttributeName;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private readonly roschar GetXmlnsAttributeName()
        {
            //ищем xmlns
            XmlScan.ParseAttribute(
                FullHead,
                FullHeadPrefixLength + DeclaredNodeType.Length + 1,
                XmlScan.XmlnsSpan,
                roschar.Empty,
                XmlScan.XmlnsHttpSpan,
                out var parsedAttribute
                );

            //may be empty
            return parsedAttribute.Name;
        }

        /// <summary>
        /// for
        /// <BaseType xmlns:p3="http://www.w3.org/2001/XMLSchema-instance" p3:type="ChildType"></BaseType>
        /// it's a
        /// ChildType
        /// If xmlns and type does not exists, then roschar.Empty
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly roschar GetPreciseNodeType()
        {
            if(XmlnsAttributeName.IsEmpty)
            {
                return roschar.Empty;
            }

            //ищем точный тип
            XmlScan.ParseAttribute(
                FullHead,
                FullHeadPrefixLength + DeclaredNodeType.Length + 1,
                XmlnsAttributeName,
                XmlScan.TypeSpan,
                roschar.Empty,
                out var parsedAttribute
                );

            //may be empty
            return parsedAttribute.Value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void GetFirst(
            ref XmlDeserializeSettings settings,
            roschar nodes,
            roschar xmlnsAttributeName,
            ref XmlNode2 result
            )
        {
            var length = GetFirstLength(
                ref settings,
                nodes,
                out var fullHeadPrefixLength,
                out var fullHeadLength,
                out var nodeTypeLength,
                out var isBodyLess
                );
            if (length == 0)
            {
                result = new XmlNode2();
                return;
            }

            result = new XmlNode2(
                settings,
                nodes.Slice(0, length),
                xmlnsAttributeName,
                fullHeadPrefixLength,
                fullHeadLength,
                nodeTypeLength,
                isBodyLess
                );
        }

        /// <summary>
        /// Возвращает длину первой ноды в переданном спане и заодно отдаёт наружу всё,
        /// что о её голове удалось узнать по дороге: без этого вызывающая сторона
        /// разбирала бы ту же голову второй раз уже в конструкторе.
        /// Рекурсивным вызовам для дочерних нод эти сведения не нужны, они их отбрасывают.
        ///
        /// Имя ноды отдаётся длиной, а не спаном, намеренно: спан, полученный через out
        /// из метода с параметром ref, компилятор обязан считать потенциально ссылающимся
        /// на этот ref, и сохранить такой спан в поле ref struct уже нельзя (CS8352).
        /// Начало имени вызывающей стороне и так известно - это fullHeadPrefixLength + 1.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetFirstLength(
            ref XmlDeserializeSettings settings,
            roschar nodes,
            out int fullHeadPrefixLength,
            out int fullHeadLength,
            out int nodeTypeLength,
            out bool isBodyLess
            )
        {
            fullHeadPrefixLength = 0;
            fullHeadLength = 0;
            nodeTypeLength = 0;
            isBodyLess = true;

            if (nodes.IsEmpty)
            {
                return 0;
            }
            var trimmed = nodes.TrimStart();
            if (trimmed.IsEmpty)
            {
                return 0;
            }
            fullHeadPrefixLength = nodes.Length - trimmed.Length;
            ScanHead_Core(nodes, trimmed, out fullHeadLength, out var nodeType, out isBodyLess);
            if (fullHeadLength == 0)
            {
                return 0;
            }
            if (isBodyLess)
            {
                return 0;
            }

            nodeTypeLength = nodeType.Length;

            //теперь сканируем до закрывающего тега
            var index = fullHeadLength;
            while (true)
            {
                var sliced = nodes.Slice(index);
                var iof = sliced.IndexOf('<');
                index += iof;

                //возможно, это закрывающий тег, или открывающий дочерней ноды
                //или это камент
                var ch1 = nodes[index + 1];
                if (ch1 == '/')
                {
                    //это закрывающий тег, надо сравнить его с нашим
                    var eq = MemoryExtensions.SequenceEqual(
                        nodeType,
                        nodes.Slice(index + 2, nodeTypeLength)
                        );
                    if (!eq)
                    {
                        //не наша нода, битый XML?
                        throw new InvalidOperationException($"Something wrong with node {nodeType.ToString()}");
                    }
                    //сравним последний символ
                    if (nodes[index + 2 + nodeTypeLength] != '>')
                    {
                        throw new InvalidOperationException($"Something wrong with node {nodeType.ToString()}");
                    }

                    //успех, нашли закрывающий тег
                    var resultCandidate = index + 2 + nodeTypeLength + 1;

                    //учтем возможный комментарий
                    var rcspan = nodes.Slice(resultCandidate);
                    resultCandidate += GetLeadingCommentLengthIfExists(settings.ContainsXmlComments, rcspan);
                    return resultCandidate;
                }
                else if(settings.ContainsCDataBlocks && sliced.StartsWith(CDataHead.AsSpan()))
                {
                    //мы в блоке CDATA, ищем его хвост
                    var cdeIndex = sliced.IndexOf(CDataTail.AsSpan());
                    var cDataShift = cdeIndex + CDataTail.Length;
                    index += cDataShift;
                    continue;
                }
                else
                {
                    //это открывающий тег дочерней ноды (возможно, с ведущим комментарием)

                    //учтем этот возможный комментарий
                    var innerNodes = nodes.Slice(index);
                    var lcl = GetLeadingCommentLengthIfExists(settings.ContainsXmlComments, innerNodes);
                    if(lcl > 0)
                    {
                        innerNodes = innerNodes.Slice(lcl);
                    }

                    var childLength = GetFirstLength(ref settings, innerNodes, out _, out _, out _, out _);
                    //получили дочернюю ноду, пропускаем ее
                    index += lcl + childLength;
                }
            }
        }

        private readonly roschar GetInternalsOf(
            bool containsXmlComments
            )
        {
            if(FullHead.IsEmpty)
            {
                return roschar.Empty;
            }
            if (IsBodyless)
            {
                return roschar.Empty;
            }

            //теперь ищем </ с конца
            var cindex = FullNode.LastIndexOf("</".AsSpan());
            if (cindex < 0)
            {
                throw new InvalidOperationException("Closing tag not found.");
            }
            if (cindex + DeclaredNodeType.Length + 3 > FullNode.Length)
            {
                //выход на пределы строки
                throw new InvalidOperationException("Closing tag not found.");
            }

            var compareResult = MemoryExtensions.SequenceEqual(
                DeclaredNodeType,
                FullNode.Slice(cindex + 2, DeclaredNodeType.Length)
                );
            if (!compareResult)
            {
                throw new InvalidOperationException("Mismatched closing tag found for " + DeclaredNodeType.ToString());
            }
            if (FullNode[cindex + 2 + DeclaredNodeType.Length] != '>')
            {
                throw new InvalidOperationException("Broken closing tag found for " + DeclaredNodeType.ToString());
            }

            var startIndex = FullHead.Length;

            var sfn = FullNode.Slice(startIndex); //without head
            //detect and skip leading comment if exists
            startIndex += GetLeadingCommentLengthIfExists(containsXmlComments, sfn);

            return FullNode.Slice(startIndex, cindex - startIndex);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsXmlCommentExistsHeuristic(
            roschar span
            )
        {
            var startCommentSpan = "<!--".AsSpan();

            var foundIndex = MemoryExtensions.IndexOf(
                span,
                startCommentSpan
                );

            return foundIndex >= 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsCDataBlockExistsHeuristic(
            roschar span
            )
        {
            var startCommentSpan = XmlNode2.CDataHead.AsSpan();

            var foundIndex = MemoryExtensions.IndexOf(
                span,
                startCommentSpan
                );

            return foundIndex >= 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetLeadingCommentLengthIfExists(
            bool containsXmlComments,
            roschar span
            )
        {
            if (!containsXmlComments)
            {
                return 0;
            }

            var result = 0;

            while (true)
            {
                var startCommentIndex = MemoryExtensions.IndexOf(
                    span,
                    "<".AsSpan()
                    );
                if (startCommentIndex < 0)
                {
                    return result;
                }
                if (startCommentIndex + 6 >= span.Length)
                {
                    return result;
                }

                var startCommentSpan = "<!--".AsSpan();

                var isStartComment = MemoryExtensions.SequenceEqual(
                    span.Slice(startCommentIndex, startCommentSpan.Length),
                    startCommentSpan
                    );
                if (!isStartComment)
                {
                    return result;
                }

                //that's a comment!
                var endCommentSpan = "-->".AsSpan();

                //search for end comment index
                var endCommentIndex = MemoryExtensions.IndexOf(
                    span,
                    endCommentSpan
                    );
                if (endCommentIndex < 0)
                {
                    throw new InvalidOperationException("Mismatched closing tag found for COMMENT around " + span.ToString());
                }

                var portion = endCommentIndex + endCommentSpan.Length;
                span = span.Slice(portion);
                result += portion;
            }
        }

        /// <summary>
        /// Skips everything the XML 1.0 §2.8 prolog grammar allows after an optional
        /// XMLDecl: prolog ::= XMLDecl? Misc* (doctypedecl Misc*)?  where
        /// Misc ::= Comment | PI | S. Handles processing instructions with any target
        /// (not just the XML declaration itself) and a DOCTYPE declaration, including
        /// one with an internal subset containing its own '&gt;' characters.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static roschar SkipPrologMisc(
            bool containsXmlComments,
            roschar span
            )
        {
            while (true)
            {
                span = span.TrimStart();
                if (span.IsEmpty)
                {
                    return span;
                }

                var lcl = GetLeadingCommentLengthIfExists(containsXmlComments, span);
                if (lcl > 0)
                {
                    span = span.Slice(lcl);
                    continue;
                }

                if (span.StartsWith("<?".AsSpan()))
                {
                    var endIndex = span.IndexOf("?>".AsSpan());
                    if (endIndex < 0)
                    {
                        throw new InvalidOperationException("Malformed processing instruction in XML prolog.");
                    }

                    span = span.Slice(endIndex + 2);
                    continue;
                }

                var doctypeHead = "<!DOCTYPE".AsSpan();
                if (span.StartsWith(doctypeHead))
                {
                    span = SkipDoctypeDeclaration(span.Slice(doctypeHead.Length));
                    continue;
                }

                return span;
            }
        }

        /// <summary>
        /// Skips the remainder of a "&lt;!DOCTYPE" declaration, tracking '['/']' depth so a
        /// '&gt;' inside an internal subset (e.g. inside "&lt;!ELEMENT Foo (#PCDATA)&gt;") doesn't
        /// prematurely end the declaration.
        /// </summary>
        private static roschar SkipDoctypeDeclaration(roschar afterKeyword)
        {
            var depth = 0;
            for (var i = 0; i < afterKeyword.Length; i++)
            {
                var c = afterKeyword[i];
                if (c == '[')
                {
                    depth++;
                }
                else if (c == ']')
                {
                    depth--;
                }
                else if (c == '>' && depth <= 0)
                {
                    return afterKeyword.Slice(i + 1);
                }
            }

            throw new InvalidOperationException("Malformed DOCTYPE declaration in XML prolog.");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ScanHead_Core(
            roschar fullnode,
            roschar trimmed,
            out int fullHeadLength,
            out roschar nodeType,
            out bool isBodyLess
            )
        {
            var headSpaceCount = fullnode.Length - trimmed.Length;

            ////ищем конец имени ноды, учитывая CDATA
            //var cDataShift = 0;
            //repeatCData:
            //if (trimmed.StartsWith(_cDataHead.AsSpan()))
            //{
            //    //мы в блоке CDATA, ищем его хвост
            //    var cdeIndex = trimmed.IndexOf(_cDataTail.AsSpan());
            //    var cDataShift1 = cdeIndex + _cDataTail.Length;
            //    trimmed = trimmed.Slice(cDataShift1);
            //    cDataShift += cDataShift1;
            //    goto repeatCData;
            //}

            XmlScan.ScanHead(trimmed, out var endOfNameIndex, out var endOfHeadIndex);

            var nodeTypeLength = headSpaceCount + endOfNameIndex;
            var chm1 = trimmed[endOfHeadIndex - 1];
            isBodyLess = chm1 == '/';

            fullHeadLength = headSpaceCount + endOfHeadIndex + 1;
            nodeType = fullnode.Slice(headSpaceCount + 1, nodeTypeLength - headSpaceCount - 1);
        }

    }

    /// <summary>
    /// Settings for deserialization process.
    /// </summary>
    public readonly ref struct XmlDeserializeSettings
    {
        /// <summary>
        /// Heuristic: true if XML document likely contains a XML comments.
        /// If so, we need to spent CPU time for parsing them.
        /// </summary>
        public readonly bool ContainsXmlComments;

        /// <summary>
        /// Heuristic: true if XML document likely contains a CDATA blocks.
        /// If so, we need to spent CPU time for parsing them.
        /// </summary>
        public readonly bool ContainsCDataBlocks;

        public XmlDeserializeSettings(
            bool containsXmlComments,
            bool containsCDataBlocks
            )
        {
            ContainsXmlComments = containsXmlComments;
            ContainsCDataBlocks = containsCDataBlocks;
        }

    }

    public readonly ref struct AttributeProcessResult
    {
        public readonly ParsedAttribute Attribute;
        public readonly int TotalLength;

        public AttributeProcessResult()
        {
            Attribute = new ParsedAttribute();
            TotalLength = 0;
        }

        public AttributeProcessResult(ParsedAttribute attribute, int totalLength)
        {
            Attribute = attribute;
            TotalLength = totalLength;
        }
    }


    public readonly ref struct ParsedAttribute
    {
        public readonly roschar Prefix; //before :
        public readonly roschar Name; //after :
        public readonly roschar Value; //inside ""
        public readonly bool IsEmpty;

        public ParsedAttribute()
        {
            Prefix = roschar.Empty;
            Name = roschar.Empty;
            Value = roschar.Empty;
            IsEmpty = true;
        }

        public ParsedAttribute(roschar prefix, roschar name, roschar value)
        {
            Prefix = prefix;
            Name = name;
            Value = value;
            IsEmpty = prefix.IsEmpty && name.IsEmpty && value.IsEmpty;
        }
    }

    /// <summary>
    /// Guards against writing text that would make the produced XML not
    /// well-formed per the Char production of XML 1.0 §2.2:
    /// Char ::= #x9 | #xA | #xD | [#x20-#xD7FF] | [#xE000-#xFFFD] | [#x10000-#x10FFFF]
    /// There is no legal way to represent these characters in XML (even a
    /// numeric character reference must resolve to a legal Char), so the
    /// only spec-compliant option is to reject them outright.
    /// </summary>
    public static class XmlCharGuard
    {
        public static void EnsureValidXmlChars(roschar value)
        {
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];

                if (c == '\t' || c == '\n' || c == '\r')
                {
                    continue;
                }

                if (c < 0x20)
                {
                    ThrowIllegalChar(c, i);
                }

                if (c >= 0xD800 && c <= 0xDFFF)
                {
                    if (char.IsHighSurrogate(c) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
                    {
                        //valid surrogate pair, represents U+10000-U+10FFFF
                        i++;
                        continue;
                    }

                    ThrowIllegalChar(c, i);
                }

                var codePoint = (int)c;
                if (codePoint == 0xFFFE || codePoint == 0xFFFF)
                {
                    ThrowIllegalChar(c, i);
                }
            }
        }

        private static void ThrowIllegalChar(char c, int index)
        {
            throw new ArgumentException(
                $"String contains character U+{(int)c:X4} at position {index}, which is not a legal XML 1.0 character (XML 1.0 §2.2)."
                );
        }
    }

}
