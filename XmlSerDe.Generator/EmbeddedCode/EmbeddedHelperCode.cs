using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Xml.Linq;
using roschar = System.ReadOnlySpan<char>;

namespace XmlSerDe.Generator.EmbeddedCode
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class XmlRootAttribute : Attribute
    {
        public readonly Type RootType;

        public XmlRootAttribute(Type rootType)
        {
            if (rootType is null)
            {
                throw new ArgumentNullException(nameof(rootType));
            }

            RootType = rootType;
        }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class XmlSubjectAttribute : Attribute
    {
        public readonly Type SubjectType;

        public XmlSubjectAttribute(Type subjectType)
        {
            if (subjectType is null)
            {
                throw new ArgumentNullException(nameof(subjectType));
            }

            SubjectType = subjectType;
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
        private readonly roschar _xmlnsHttp = "http://www.w3.org/2001/XMLSchema-instance".AsSpan();
        private readonly roschar _typeHttp = "type".AsSpan();

        /// <summary>
        /// Struct is empty.
        /// </summary>
        public readonly bool IsEmpty;

        /// <summary>
        /// Full node span (including spaces).
        /// </summary>
        public readonly roschar FullNode;

        /// <summary>
        /// Head of this node.
        /// </summary>
        public readonly ScanHeadResult Head;

        /// <summary>
        /// Internals of this node (including spaces).
        /// </summary>
        public readonly roschar Internals;

        public XmlNode2()
        {
            IsEmpty = true;
            FullNode = roschar.Empty;
            Head = new ScanHeadResult();
            Internals = roschar.Empty;
        }

        public XmlNode2(
            roschar fullNode
            )
        {
            IsEmpty = fullNode.IsEmpty;
            FullNode = fullNode;
            Head = ScanHead(fullNode);
            Internals = GetInternalsOf(fullNode, Head);
        }

        /// <summary>
        /// Get first child of its node.
        /// </summary>
        public readonly void GetFirstChild(ref XmlNode2 result)
        {
            GetFirst(Internals, ref result);
        }

        /// <summary>
        /// for
        /// <BaseType xmlns:p3="http://www.w3.org/2001/XMLSchema-instance" p3:type="ChildType"></BaseType>
        /// it's a
        /// BaseType
        /// </summary>
        public readonly roschar GetDeclaredNodeType()
        {
            return Head.NodeType;
        }

        /// <summary>
        /// for
        /// <BaseType xmlns:p3="http://www.w3.org/2001/XMLSchema-instance" p3:type="ChildType"></BaseType>
        /// it's a
        /// ChildType
        /// If xmlns and type does not exists, then roschar.Empty
        /// </summary>
        public readonly roschar GetPreciseNodeType()
        {
            ParsedAttribute parsedAttribute = new();

            //ищем xmlns
            ParseAttribute(
                Head.FullHead,
                Head.PrefixLength + Head.NodeType.Length + 1,
                "xmlns".AsSpan(),
                roschar.Empty,
                _xmlnsHttp,//"http://www.w3.org/2001/XMLSchema-instance".AsSpan(),
                ref parsedAttribute
                );
            if(parsedAttribute.IsEmpty)
            {
                return roschar.Empty;
            }

            //ищем тип
            ParseAttribute(
                Head.FullHead,
                Head.PrefixLength + Head.NodeType.Length + 1,
                parsedAttribute.Name,
                _typeHttp,//"type".AsSpan(),
                roschar.Empty,
                ref parsedAttribute
                );
            if(parsedAttribute.IsEmpty)
            {
                return roschar.Empty;
            }

            return parsedAttribute.Value;
        }

        public static void GetFirst(
            roschar nodes,
            ref XmlNode2 result
            )
        {
            var length = GetFirstLength(nodes);
            if (length == 0)
            {
                result = new XmlNode2();
                return;
            }

            result = new XmlNode2(nodes.Slice(0, length));
        }

        //{REMOVE THIS COMMENT}[MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private static int GetFirstLength(
            roschar nodes
            )
        {
            var shr = ScanHead(nodes);
            if (shr.IsEmpty)
            {
                return 0;
            }
            if (shr.IsBodyless)
            {
                return 0;
            }

            //теперь сканируем до закрывающего тега
            var index = shr.FullHead.Length;
            while (true)
            {
                var sliced = nodes.Slice(index);
                var iof = sliced.IndexOf('<');
                index += iof;

                //возможно, это закрывающий тег, или открывающий дочерней ноды
                //TODO: или это камент, пока не поддерживается
                var ch1 = nodes[index + 1];
                if (ch1 == '/')
                {
                    //это закрывающий тег, надо сравнить его с нашим
                    var eq = MemoryExtensions.SequenceEqual(
                        shr.NodeType,
                        nodes.Slice(index + 2, shr.NodeType.Length)
                        );
                    if (!eq)
                    {
                        //не наша нода, битый XML?
                        throw new InvalidOperationException($"Something wrong with node {shr.NodeType.ToString()}");
                    }
                    //сравним последний символ
                    if (nodes[index + 2 + shr.NodeType.Length] != '>')
                    {
                        throw new InvalidOperationException($"Something wrong with node {shr.NodeType.ToString()}");
                    }

                    //успех, нашли закрывающий тег
                    return index + 2 + shr.NodeType.Length + 1;
                }
                else
                {
                    //это открывающий тег дочерней ноды
                    var innerNodes = nodes.Slice(index);
                    var childLength = GetFirstLength(innerNodes);
                    //получили дочернюю ноду, пропускаем ее
                    index += childLength;
                }
            }
        }

        //public static XmlNode2 GetFirst(
        //    roschar nodes
        //    )
        //{
        //    var shr = ScanHead(nodes);
        //    if (shr.IsEmpty)
        //    {
        //        return new XmlNode2();
        //    }
        //    if (shr.IsBodyless)
        //    {
        //        return new XmlNode2();
        //    }

        //    //теперь сканируем до закрывающего тега
        //    var index = shr.FullHead.Length;
        //    while (true)
        //    {
        //        var ch0 = nodes[index];
        //        if (ch0 == '<')
        //        {
        //            //возможно, это закрывающий тег, или открывающий дочерней ноды
        //            //TODO: или это камент, пока не поддерживается
        //            var ch1 = nodes[index + 1];
        //            if (ch1 == '/')
        //            {
        //                //это закрывающий тег, надо сравнить его с нашим
        //                var compareResult = MemoryExtensions.SequenceCompareTo(
        //                    shr.NodeType,
        //                    nodes.Slice(index + 2, shr.NodeType.Length)
        //                    );
        //                if (compareResult != 0)
        //                {
        //                    //не наша нода, битый XML?
        //                    throw new InvalidOperationException($"Something wrong with node {shr.NodeType.ToString()}");
        //                }
        //                //сравним последний символ
        //                if (nodes[index + 2 + shr.NodeType.Length] != '>')
        //                {
        //                    throw new InvalidOperationException($"Something wrong with node {shr.NodeType.ToString()}");
        //                }

        //                //успех, нашли закрывающий тег
        //                return new XmlNode2(nodes.Slice(0, index + 2 + shr.NodeType.Length + 1));
        //            }
        //            else
        //            {
        //                //это открывающий тег дочерней ноды
        //                var innerNodes = nodes.Slice(index);
        //                var child = GetFirst(innerNodes);
        //                //получили дочернюю ноду, пропускаем ее
        //                index += child.FullNode.Length;
        //            }
        //        }
        //        else
        //        {
        //            index++;
        //        }
        //    }
        //}

        private static roschar GetInternalsOf(
            roschar fullNode,
            ScanHeadResult head
            )
        {
            if(head.IsEmpty)
            {
                return roschar.Empty;
            }
            if (head.IsBodyless)
            {
                return roschar.Empty;
            }

            //теперь ищем </ с конца
            var cindex = fullNode.LastIndexOf("</".AsSpan());
            if (cindex < 0)
            {
                throw new InvalidOperationException("Closing tag not found.");
            }
            if (cindex + head.NodeType.Length + 3 > fullNode.Length)
            {
                //выход на пределы строки
                throw new InvalidOperationException("Closing tag not found.");
            }

            var compareResult = MemoryExtensions.SequenceCompareTo(
                head.NodeType,
                fullNode.Slice(cindex + 2, head.NodeType.Length)
                );
            if (compareResult != 0)
            {
                throw new InvalidOperationException("Mismatched closing tag found.");
            }
            if (fullNode[cindex + 2 + head.NodeType.Length] != '>')
            {
                throw new InvalidOperationException("Broken closing tag found.");
            }

            return fullNode.Slice(head.FullHead.Length, cindex - head.FullHead.Length);
        }

        private static ScanHeadResult ScanHead(roschar fullnode)
        {
            if (fullnode.IsEmpty)
            {
                return new ScanHeadResult();
            }

            var trimmed = fullnode.TrimStart();
            if (trimmed.IsEmpty)
            {
                return new ScanHeadResult();
            }

            var firstNonSpace = fullnode.Length - trimmed.Length;

            //ищем конец имени ноды
            var endOfNameIndex = trimmed.IndexOfAny(
                '/', '>', ' '
                );
            var nodeTypeLength = firstNonSpace + endOfNameIndex;

            //ищем конец головы
            var endOfHeadIndex = trimmed.IndexOf(
                '>'
                );
            var chm1 = trimmed[endOfHeadIndex - 1];
            var isBodyLess = chm1 == '/';

            return new ScanHeadResult(
                fullnode.Slice(0, firstNonSpace + endOfHeadIndex + 1),
                fullnode.Slice(firstNonSpace + 1, nodeTypeLength - firstNonSpace - 1),
                firstNonSpace,
                isBodyLess
                );
        }

        //private static ScanHeadResult ScanHead(roschar fullnode)
        //{
        //    if(fullnode.IsEmpty)
        //    {
        //        return new ScanHeadResult();
        //    }

        //    var index = 0;

        //    //пропускаем пробелы
        //    var firstNonSpace = 0;
        //    while (true)
        //    {
        //        if(index == fullnode.Length)
        //        {
        //            return new ScanHeadResult();
        //        }

        //        var ch = fullnode[index];
        //        if (!char.IsWhiteSpace(ch))
        //        {
        //            firstNonSpace = index;
        //            break;
        //        }
        //        index++;
        //    }

        //    //ищем конец имени ноды
        //    var nodeTypeLength = 0;
        //    while (true)
        //    {
        //        var ch = fullnode[index];
        //        if(ch == '/' || ch == '>')
        //        {
        //            nodeTypeLength = index;
        //            break;
        //        }
        //        if (char.IsWhiteSpace(ch))
        //        {
        //            nodeTypeLength = index;
        //            index++;
        //            break;
        //        }
        //        index++;
        //    }

        //    //ищем конец головы
        //    while (true)
        //    {
        //        var ch = fullnode[index];
        //        if(ch == '>')
        //        {
        //            var chm1 = fullnode[index - 1];
        //            var isBodyLess = chm1 == '/';
        //            //var isBodyLessIndex = isBodyLess ? 1 : 0;

        //            return new ScanHeadResult(
        //                fullnode.Slice(firstNonSpace, index + 1),
        //                fullnode.Slice(firstNonSpace + 1, nodeTypeLength - firstNonSpace - 1 /*- isBodyLessIndex*/),
        //                isBodyLess
        //                );
        //        }
        //        index++;
        //    }
        //}


        private static void ParseAttribute(
            roschar internalsOfHead,
            int index,
            roschar requiredPrefix,
            roschar requiredName,
            roschar requiredValue,
            ref ParsedAttribute result
            )
        {
            AttributeProcessResult apr = new();
            while (true)
            {
                ParseFirstFoundAttribute(internalsOfHead, index, ref apr);
                if (apr.Attribute.IsEmpty)
                {
                    result = apr.Attribute;
                    return;
                }

                if (requiredPrefix.IsEmpty || requiredPrefix.SequenceEqual(apr.Attribute.Prefix))
                {
                    if (requiredName.IsEmpty || requiredName.SequenceEqual(apr.Attribute.Name))
                    {
                        if (requiredValue.IsEmpty || requiredValue.SequenceEqual(apr.Attribute.Value))
                        {
                            //нашли что нужно
                            result = apr.Attribute;
                            return;
                        }
                    }
                }

                index += apr.TotalLength;
            }
        }

        private static void ParseFirstFoundAttribute(
            roschar internalsOfHead,
            int iindex,
            ref AttributeProcessResult result
            )
        {
            var trimmed = internalsOfHead.Slice(iindex).TrimStart();
            var trimmedLength = internalsOfHead.Length - trimmed.Length;

            var iofa0 = trimmed.IndexOfAny("/>:".AsSpan());
            var c = trimmed[iofa0];
            if (c == '/' || c == '>')
            {
                //нода закрылась, атрибутов нету
                result = new AttributeProcessResult();
                return;
            }

            var prefix = internalsOfHead.Slice(trimmedLength, iofa0);
            //var prefix = trimmed.Slice(trimmedLength, iofa0 - trimmedLength);
            //var afterPrefixIndex = trimmedLength + iofa0 + 1;
            trimmed = trimmed.Slice(iofa0 + 1);

            var iofa1 = trimmed.IndexOfAny("=".AsSpan());
            var name = trimmed.Slice(0, iofa1);
            //var afterNameIndex = afterPrefixIndex + iofa1;
            trimmed = trimmed.Slice(iofa1 + 1);

            var iofa2 = trimmed.IndexOfAny("\"".AsSpan());
            //найдено начало значения
            //var startValueIndex = afterNameIndex + iofa2 + 1;
            trimmed = trimmed.Slice(iofa2 + 1);

            var iofa3 = trimmed.IndexOfAny("\"".AsSpan());
            //найден конец значения
            var value = trimmed.Slice(0, iofa3);
            //var endValueIndex = startValueIndex + iofa3;

            result = new AttributeProcessResult(
                new ParsedAttribute(prefix, name, value),
                trimmedLength + iofa0 + iofa1 + iofa2 + iofa3 + 4 - iindex
                );
        }

        //private static AttributeProcessResult ParseFirstFoundAttribute(
        //    roschar internalsOfHead,
        //    int iindex
        //    )
        //{
        //    var index = iindex;

        //    //пропускаем пробелы
        //    while (char.IsWhiteSpace(internalsOfHead[index]))
        //    {
        //        index++;
        //    }

        //    var startIndex = index;

        //    bool foundFirstQuote = false;
        //    var prefix = roschar.Empty; //before :
        //    var name = roschar.Empty; //after :
        //    var value = roschar.Empty; //inside ""

        //    while (true)
        //    {
        //        var c = internalsOfHead[index];

        //        if ((c == '/' || c == '>') && !foundFirstQuote)
        //        {
        //            //нода закрылась, атрибутов нету
        //            return new AttributeProcessResult();
        //        }
        //        else if (c == ':' && !foundFirstQuote)
        //        {
        //            prefix = internalsOfHead.Slice(startIndex, index - startIndex);
        //            startIndex = index + 1;
        //        }
        //        else if (c == '=' && !foundFirstQuote)
        //        {
        //            name = internalsOfHead.Slice(startIndex, index - startIndex);
        //            startIndex = index + 1;
        //        }
        //        else if (c == '"' && !foundFirstQuote)
        //        {
        //            //найдено начало значения
        //            startIndex = index + 1;
        //            foundFirstQuote = true;
        //        }
        //        else if (c == '"' && foundFirstQuote)
        //        {
        //            //найден конец значения
        //            value = internalsOfHead.Slice(startIndex, index - startIndex);
        //        }

        //        index++;

        //        if (!value.IsEmpty)
        //        {
        //            break;
        //        }
        //    }

        //    return new AttributeProcessResult(
        //        new ParsedAttribute(prefix, name, value),
        //        index - iindex
        //        );
        //}

    }

    public readonly ref struct ScanHeadResult
    {
        public readonly bool IsEmpty;
        public readonly roschar FullHead;
        public readonly roschar NodeType;

        /// <summary>
        /// Количество пробелов (и переносов строк) до начала головы в FullHead
        /// </summary>
        public readonly int PrefixLength;
        public readonly bool IsBodyless;

        public ScanHeadResult()
        {
            IsEmpty = true;
            FullHead = roschar.Empty;
            NodeType = roschar.Empty;
            PrefixLength = 0;
            IsBodyless = true;
        }

        public ScanHeadResult(roschar fullHead, roschar nodeType, int prefixLength, bool isBodyless)
        {
            IsEmpty = FullHead.IsEmpty && nodeType.IsEmpty;
            FullHead = fullHead;
            NodeType = nodeType;
            PrefixLength = prefixLength;
            IsBodyless = isBodyless;
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

}
