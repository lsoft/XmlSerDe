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
    public class XmlParserAttribute : Attribute
    {
        public readonly Type SubjectType;
        public readonly string ParserStatement;

        public XmlParserAttribute(Type subjectType, string parserStatement)
        {
            if (subjectType is null)
            {
                throw new ArgumentNullException(nameof(subjectType));
            }
            if (parserStatement is null)
            {
                throw new ArgumentNullException(nameof(parserStatement));
            }

            SubjectType = subjectType;
            ParserStatement = parserStatement;
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
        private readonly roschar _xmlnsSpan = "xmlns".AsSpan();
        private readonly roschar _xmlnsHttpSpan = "http://www.w3.org/2001/XMLSchema-instance".AsSpan();
        private readonly roschar _typeSpan = "type".AsSpan();

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
        /// Internals of this node (including spaces).
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
            Internals = GetInternalsOf();

            if (xmlnsAttributeName.IsEmpty)
            {
                XmlnsAttributeName = GetXmlnsAttributeName();
            }
            else
            {
                XmlnsAttributeName = xmlnsAttributeName;
            }
        }

        //{REMOVE THIS COMMENT}[MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private readonly roschar GetXmlnsAttributeName()
        {
            //ищем xmlns
            ParseAttribute(
                FullHead,
                FullHeadPrefixLength + DeclaredNodeType.Length + 1,
                _xmlnsSpan,
                roschar.Empty,
                _xmlnsHttpSpan,
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
        //{REMOVE THIS COMMENT}[MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public readonly roschar GetPreciseNodeType()
        {
            if(XmlnsAttributeName.IsEmpty)
            {
                return roschar.Empty;
            }

            //ищем точный тип
            ParseAttribute(
                FullHead,
                FullHeadPrefixLength + DeclaredNodeType.Length + 1,
                XmlnsAttributeName,
                _typeSpan,
                roschar.Empty,
                out var parsedAttribute
                );

            //may be empty
            return parsedAttribute.Value;
        }

        //{REMOVE THIS COMMENT}[MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static void GetFirst(
            roschar nodes,
            roschar xmlnsAttributeName,
            ref XmlNode2 result
            )
        {
            var length = GetFirstLength(nodes);
            if (length == 0)
            {
                result = new XmlNode2();
                return;
            }

            result = new XmlNode2(nodes.Slice(0, length), xmlnsAttributeName);
        }

        //{REMOVE THIS COMMENT}[MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private static int GetFirstLength(
            roschar nodes
            )
        {
            if (nodes.IsEmpty)
            {
                return 0;
            }
            var trimmed = nodes.TrimStart();
            if (trimmed.IsEmpty)
            {
                return 0;
            }
            ScanHead_Core(nodes, trimmed, out var fullHeadLength, out var nodeType, out var isBodyLess);
            if (fullHeadLength == 0)
            {
                return 0;
            }
            if (isBodyLess)
            {
                return 0;
            }

            var nodeTypeLength = nodeType.Length;

            //теперь сканируем до закрывающего тега
            var index = fullHeadLength;
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
                    return index + 2 + nodeTypeLength + 1;
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

        private readonly roschar GetInternalsOf(
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

            var compareResult = MemoryExtensions.SequenceCompareTo(
                DeclaredNodeType,
                FullNode.Slice(cindex + 2, DeclaredNodeType.Length)
                );
            if (compareResult != 0)
            {
                throw new InvalidOperationException("Mismatched closing tag found.");
            }
            if (FullNode[cindex + 2 + DeclaredNodeType.Length] != '>')
            {
                throw new InvalidOperationException("Broken closing tag found.");
            }

            return FullNode.Slice(FullHead.Length, cindex - FullHead.Length);
        }

        //{REMOVE THIS COMMENT}[MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private static void ScanHead_Core(
            roschar fullnode,
            roschar trimmed,
            out int fullHeadLength,
            out roschar nodeType,
            out bool isBodyLess
            )
        {
            var headSpaceCount = fullnode.Length - trimmed.Length;

            //ищем конец имени ноды
            var endOfNameIndex = trimmed.IndexOfAny(
                '/', '>', ' '
                );
            var nodeTypeLength = headSpaceCount + endOfNameIndex;

            //ищем конец головы
            var endOfHeadIndex = trimmed.IndexOf(
                '>'
                );
            var chm1 = trimmed[endOfHeadIndex - 1];
            isBodyLess = chm1 == '/';

            fullHeadLength = headSpaceCount + endOfHeadIndex + 1;
            //fullHead = fullnode.Slice(0, headSpaceCount + endOfHeadIndex + 1);
            nodeType = fullnode.Slice(headSpaceCount + 1, nodeTypeLength - headSpaceCount - 1);
        }

        private static void ParseAttribute(
            roschar internalsOfHead,
            int index,
            roschar requiredPrefix,
            roschar requiredName,
            roschar requiredValue,
            out ParsedAttribute result
            )
        {
            while (true)
            {
                ParseFirstFoundAttribute(internalsOfHead, index, out var apr);
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
            out AttributeProcessResult result
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
            trimmed = trimmed.Slice(iofa0 + 1);

            var iofa1 = trimmed.IndexOfAny("=".AsSpan());
            var name = trimmed.Slice(0, iofa1);
            trimmed = trimmed.Slice(iofa1 + 1);

            var iofa2 = trimmed.IndexOfAny("\"".AsSpan());
            //найдено начало значения
            trimmed = trimmed.Slice(iofa2 + 1);

            var iofa3 = trimmed.IndexOfAny("\"".AsSpan());
            //найден конец значения
            var value = trimmed.Slice(0, iofa3);

            result = new AttributeProcessResult(
                new ParsedAttribute(prefix, name, value),
                trimmedLength + iofa0 + iofa1 + iofa2 + iofa3 + 4 - iindex
                );
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
