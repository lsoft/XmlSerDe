using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Xml;
using XmlSerDe.Common;
using roschar = System.ReadOnlySpan<char>;

namespace XmlSerDe.Components.Injector
{
    public class DefaultInjector : IInjector
    {
        public static readonly DefaultInjector Instance = new DefaultInjector();

        /// <summary>
        /// На современных таргетах отдаёт спан как есть, и перегрузка
        /// <c>Parse(roschar, …)</c> выбирается без единой аллокации; на JIT это
        /// no-op, который инлайнится в ничто.
        ///
        /// В netstandard2.0 span-перегрузок Parse не существует вовсе (их нет ни в
        /// корлибе, ни в System.Memory), поэтому вход приходится материализовать в
        /// строку. Это единственное место, где различие между таргетами и живёт:
        /// все 24 вызова Parse ниже написаны одинаково.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET8_0_OR_GREATER
        private static roschar Parsable(roschar body) => body;
#else
        private static string Parsable(roschar body) => body.ToString();
#endif

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Parse(ref global::XmlSerDe.Common.XmlDeserializeSettings settings, roschar fullNode, roschar xmlnsAttributeName, out global::System.DateTime result)
        {
            var xmlNode = new global::XmlSerDe.Common.XmlNode2(settings, fullNode, xmlnsAttributeName);
            var xmlNodeDeclaredType = xmlNode.DeclaredNodeType;

            var returnType = "dateTime".AsSpan();
            if (!xmlNodeDeclaredType.SequenceEqual(returnType))
            {
                InvalidOperationException(returnType, xmlNodeDeclaredType);
            }

            ParseBody(xmlNode.Internals, out result);
        }
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Parse(ref global::XmlSerDe.Common.XmlDeserializeSettings settings, roschar fullNode, roschar xmlnsAttributeName, out global::System.DateTime? result)
        {
            var xmlNode = new global::XmlSerDe.Common.XmlNode2(settings, fullNode, xmlnsAttributeName);
            var xmlNodeDeclaredType = xmlNode.DeclaredNodeType;

            var returnType = "dateTime".AsSpan();
            if (!xmlNodeDeclaredType.SequenceEqual(returnType))
            {
                NullableInvalidOperationException(returnType, xmlNodeDeclaredType);
            }

            ParseBody(xmlNode.Internals, out result);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out global::System.DateTime result)
        {
            result = DateTime.Parse(Parsable(body), CultureInfo.InvariantCulture);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out global::System.DateTime? result)
        {
            result = DateTime.Parse(Parsable(body), CultureInfo.InvariantCulture);
        }



        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Parse(ref global::XmlSerDe.Common.XmlDeserializeSettings settings, roschar fullNode, roschar xmlnsAttributeName, out Guid result)
        {
            var xmlNode = new global::XmlSerDe.Common.XmlNode2(settings, fullNode, xmlnsAttributeName);
            var xmlNodeDeclaredType = xmlNode.DeclaredNodeType;

            var returnType = "guid".AsSpan();
            if (!xmlNodeDeclaredType.SequenceEqual(returnType))
            {
                InvalidOperationException(returnType, xmlNodeDeclaredType);
            }

            ParseBody(xmlNode.Internals, out result);
        }
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Parse(ref global::XmlSerDe.Common.XmlDeserializeSettings settings, roschar fullNode, roschar xmlnsAttributeName, out global::System.Guid? result)
        {
            var xmlNode = new global::XmlSerDe.Common.XmlNode2(settings, fullNode, xmlnsAttributeName);
            var xmlNodeDeclaredType = xmlNode.DeclaredNodeType;

            var returnType = "guid".AsSpan();
            if (!xmlNodeDeclaredType.SequenceEqual(returnType))
            {
                NullableInvalidOperationException(returnType, xmlNodeDeclaredType);
            }

            ParseBody(xmlNode.Internals, out result);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out global::System.Guid result)
        {
            result = global::System.Guid.Parse(Parsable(body));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out global::System.Guid? result)
        {
            result = global::System.Guid.Parse(Parsable(body));
        }


        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Parse(ref global::XmlSerDe.Common.XmlDeserializeSettings settings, roschar fullNode, roschar xmlnsAttributeName, out bool result)
        {
            var xmlNode = new global::XmlSerDe.Common.XmlNode2(settings, fullNode, xmlnsAttributeName);
            var xmlNodeDeclaredType = xmlNode.DeclaredNodeType;

            var returnType = "boolean".AsSpan();
            if (!xmlNodeDeclaredType.SequenceEqual(returnType))
            {
                InvalidOperationException(returnType, xmlNodeDeclaredType);
            }

            ParseBody(xmlNode.Internals, out result);
        }
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Parse(ref global::XmlSerDe.Common.XmlDeserializeSettings settings, roschar fullNode, roschar xmlnsAttributeName, out bool? result)
        {
            var xmlNode = new global::XmlSerDe.Common.XmlNode2(settings, fullNode, xmlnsAttributeName);
            var xmlNodeDeclaredType = xmlNode.DeclaredNodeType;

            var returnType = "boolean".AsSpan();
            if (!xmlNodeDeclaredType.SequenceEqual(returnType))
            {
                NullableInvalidOperationException(returnType, xmlNodeDeclaredType);
            }

            ParseBody(xmlNode.Internals, out result);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out bool result)
        {
            result = bool.Parse(Parsable(body));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out bool? result)
        {
            result = bool.Parse(Parsable(body));
        }


        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Parse(ref global::XmlSerDe.Common.XmlDeserializeSettings settings, roschar fullNode, roschar xmlnsAttributeName, out sbyte result)
        {
            var xmlNode = new global::XmlSerDe.Common.XmlNode2(settings, fullNode, xmlnsAttributeName);
            var xmlNodeDeclaredType = xmlNode.DeclaredNodeType;

            var returnType = "byte".AsSpan();
            if (!xmlNodeDeclaredType.SequenceEqual(returnType))
            {
                InvalidOperationException(returnType, xmlNodeDeclaredType);
            }

            ParseBody(xmlNode.Internals, out result);
        }
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Parse(ref global::XmlSerDe.Common.XmlDeserializeSettings settings, roschar fullNode, roschar xmlnsAttributeName, out sbyte? result)
        {
            var xmlNode = new global::XmlSerDe.Common.XmlNode2(settings, fullNode, xmlnsAttributeName);
            var xmlNodeDeclaredType = xmlNode.DeclaredNodeType;

            var returnType = "byte".AsSpan();
            if (!xmlNodeDeclaredType.SequenceEqual(returnType))
            {
                NullableInvalidOperationException(returnType, xmlNodeDeclaredType);
            }

            ParseBody(xmlNode.Internals, out result);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out sbyte result)
        {
            result = sbyte.Parse(Parsable(body), CultureInfo.InvariantCulture);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out sbyte? result)
        {
            result = sbyte.Parse(Parsable(body), CultureInfo.InvariantCulture);
        }


        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Parse(ref global::XmlSerDe.Common.XmlDeserializeSettings settings, roschar fullNode, roschar xmlnsAttributeName, out byte result)
        {
            var xmlNode = new global::XmlSerDe.Common.XmlNode2(settings, fullNode, xmlnsAttributeName);
            var xmlNodeDeclaredType = xmlNode.DeclaredNodeType;

            var returnType = "unsignedByte".AsSpan();
            if (!xmlNodeDeclaredType.SequenceEqual(returnType))
            {
                InvalidOperationException(returnType, xmlNodeDeclaredType);
            }

            ParseBody(xmlNode.Internals, out result);
        }
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Parse(ref global::XmlSerDe.Common.XmlDeserializeSettings settings, roschar fullNode, roschar xmlnsAttributeName, out byte? result)
        {
            var xmlNode = new global::XmlSerDe.Common.XmlNode2(settings, fullNode, xmlnsAttributeName);
            var xmlNodeDeclaredType = xmlNode.DeclaredNodeType;

            var returnType = "unsignedByte".AsSpan();
            if (!xmlNodeDeclaredType.SequenceEqual(returnType))
            {
                NullableInvalidOperationException(returnType, xmlNodeDeclaredType);
            }

            ParseBody(xmlNode.Internals, out result);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out byte result)
        {
            result = byte.Parse(Parsable(body), CultureInfo.InvariantCulture);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out byte? result)
        {
            result = byte.Parse(Parsable(body), CultureInfo.InvariantCulture);
        }


        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Parse(ref global::XmlSerDe.Common.XmlDeserializeSettings settings, roschar fullNode, roschar xmlnsAttributeName, out ushort result)
        {
            var xmlNode = new global::XmlSerDe.Common.XmlNode2(settings, fullNode, xmlnsAttributeName);
            var xmlNodeDeclaredType = xmlNode.DeclaredNodeType;

            var returnType = "unsignedShort".AsSpan();
            if (!xmlNodeDeclaredType.SequenceEqual(returnType))
            {
                InvalidOperationException(returnType, xmlNodeDeclaredType);
            }

            ParseBody(xmlNode.Internals, out result);
        }
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Parse(ref global::XmlSerDe.Common.XmlDeserializeSettings settings, roschar fullNode, roschar xmlnsAttributeName, out ushort? result)
        {
            var xmlNode = new global::XmlSerDe.Common.XmlNode2(settings, fullNode, xmlnsAttributeName);
            var xmlNodeDeclaredType = xmlNode.DeclaredNodeType;

            var returnType = "unsignedShort".AsSpan();
            if (!xmlNodeDeclaredType.SequenceEqual(returnType))
            {
                NullableInvalidOperationException(returnType, xmlNodeDeclaredType);
            }

            ParseBody(xmlNode.Internals, out result);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out ushort result)
        {
            result = ushort.Parse(Parsable(body), CultureInfo.InvariantCulture);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out ushort? result)
        {
            result = ushort.Parse(Parsable(body), CultureInfo.InvariantCulture);
        }


        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Parse(ref global::XmlSerDe.Common.XmlDeserializeSettings settings, roschar fullNode, roschar xmlnsAttributeName, out short result)
        {
            var xmlNode = new global::XmlSerDe.Common.XmlNode2(settings, fullNode, xmlnsAttributeName);
            var xmlNodeDeclaredType = xmlNode.DeclaredNodeType;

            var returnType = "short".AsSpan();
            if (!xmlNodeDeclaredType.SequenceEqual(returnType))
            {
                InvalidOperationException(returnType, xmlNodeDeclaredType);
            }

            ParseBody(xmlNode.Internals, out result);
        }
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Parse(ref global::XmlSerDe.Common.XmlDeserializeSettings settings, roschar fullNode, roschar xmlnsAttributeName, out short? result)
        {
            var xmlNode = new global::XmlSerDe.Common.XmlNode2(settings, fullNode, xmlnsAttributeName);
            var xmlNodeDeclaredType = xmlNode.DeclaredNodeType;

            var returnType = "short".AsSpan();
            if (!xmlNodeDeclaredType.SequenceEqual(returnType))
            {
                NullableInvalidOperationException(returnType, xmlNodeDeclaredType);
            }

            ParseBody(xmlNode.Internals, out result);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out short result)
        {
            result = short.Parse(Parsable(body), CultureInfo.InvariantCulture);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out short? result)
        {
            result = short.Parse(Parsable(body), CultureInfo.InvariantCulture);
        }


        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Parse(ref global::XmlSerDe.Common.XmlDeserializeSettings settings, roschar fullNode, roschar xmlnsAttributeName, out uint result)
        {
            var xmlNode = new global::XmlSerDe.Common.XmlNode2(settings, fullNode, xmlnsAttributeName);
            var xmlNodeDeclaredType = xmlNode.DeclaredNodeType;

            var returnType = "unsignedInt".AsSpan();
            if (!xmlNodeDeclaredType.SequenceEqual(returnType))
            {
                InvalidOperationException(returnType, xmlNodeDeclaredType);
            }

            ParseBody(xmlNode.Internals, out result);
        }
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Parse(ref global::XmlSerDe.Common.XmlDeserializeSettings settings, roschar fullNode, roschar xmlnsAttributeName, out uint? result)
        {
            var xmlNode = new global::XmlSerDe.Common.XmlNode2(settings, fullNode, xmlnsAttributeName);
            var xmlNodeDeclaredType = xmlNode.DeclaredNodeType;

            var returnType = "unsignedInt".AsSpan();
            if (!xmlNodeDeclaredType.SequenceEqual(returnType))
            {
                NullableInvalidOperationException(returnType, xmlNodeDeclaredType);
            }

            ParseBody(xmlNode.Internals, out result);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out uint result)
        {
            result = uint.Parse(Parsable(body), CultureInfo.InvariantCulture);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out uint? result)
        {
            result = uint.Parse(Parsable(body), CultureInfo.InvariantCulture);
        }


        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Parse(ref global::XmlSerDe.Common.XmlDeserializeSettings settings, roschar fullNode, roschar xmlnsAttributeName, out int result)
        {
            var xmlNode = new global::XmlSerDe.Common.XmlNode2(settings, fullNode, xmlnsAttributeName);
            var xmlNodeDeclaredType = xmlNode.DeclaredNodeType;

            var returnType = "int".AsSpan();
            if (!xmlNodeDeclaredType.SequenceEqual(returnType))
            {
                InvalidOperationException(returnType, xmlNodeDeclaredType);
            }

            ParseBody(xmlNode.Internals, out result);
        }
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Parse(ref global::XmlSerDe.Common.XmlDeserializeSettings settings, roschar fullNode, roschar xmlnsAttributeName, out int? result)
        {
            var xmlNode = new global::XmlSerDe.Common.XmlNode2(settings, fullNode, xmlnsAttributeName);
            var xmlNodeDeclaredType = xmlNode.DeclaredNodeType;

            var returnType = "int".AsSpan();
            if (!xmlNodeDeclaredType.SequenceEqual(returnType))
            {
                NullableInvalidOperationException(returnType, xmlNodeDeclaredType);
            }

            ParseBody(xmlNode.Internals, out result);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out int result)
        {
            result = int.Parse(Parsable(body), CultureInfo.InvariantCulture);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out int? result)
        {
            result = int.Parse(Parsable(body), CultureInfo.InvariantCulture);
        }


        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Parse(ref global::XmlSerDe.Common.XmlDeserializeSettings settings, roschar fullNode, roschar xmlnsAttributeName, out ulong result)
        {
            var xmlNode = new global::XmlSerDe.Common.XmlNode2(settings, fullNode, xmlnsAttributeName);
            var xmlNodeDeclaredType = xmlNode.DeclaredNodeType;

            var returnType = "unsignedLong".AsSpan();
            if (!xmlNodeDeclaredType.SequenceEqual(returnType))
            {
                InvalidOperationException(returnType, xmlNodeDeclaredType);
            }

            ParseBody(xmlNode.Internals, out result);
        }
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Parse(ref global::XmlSerDe.Common.XmlDeserializeSettings settings, roschar fullNode, roschar xmlnsAttributeName, out ulong? result)
        {
            var xmlNode = new global::XmlSerDe.Common.XmlNode2(settings, fullNode, xmlnsAttributeName);
            var xmlNodeDeclaredType = xmlNode.DeclaredNodeType;

            var returnType = "unsignedLong".AsSpan();
            if (!xmlNodeDeclaredType.SequenceEqual(returnType))
            {
                NullableInvalidOperationException(returnType, xmlNodeDeclaredType);
            }

            ParseBody(xmlNode.Internals, out result);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out ulong result)
        {
            result = ulong.Parse(Parsable(body), CultureInfo.InvariantCulture);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out ulong? result)
        {
            result = ulong.Parse(Parsable(body), CultureInfo.InvariantCulture);
        }


        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Parse(ref global::XmlSerDe.Common.XmlDeserializeSettings settings, roschar fullNode, roschar xmlnsAttributeName, out long result)
        {
            var xmlNode = new global::XmlSerDe.Common.XmlNode2(settings, fullNode, xmlnsAttributeName);
            var xmlNodeDeclaredType = xmlNode.DeclaredNodeType;

            var returnType = "long".AsSpan();
            if (!xmlNodeDeclaredType.SequenceEqual(returnType))
            {
                InvalidOperationException(returnType, xmlNodeDeclaredType);
            }

            ParseBody(xmlNode.Internals, out result);
        }
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Parse(ref global::XmlSerDe.Common.XmlDeserializeSettings settings, roschar fullNode, roschar xmlnsAttributeName, out long? result)
        {
            var xmlNode = new global::XmlSerDe.Common.XmlNode2(settings, fullNode, xmlnsAttributeName);
            var xmlNodeDeclaredType = xmlNode.DeclaredNodeType;

            var returnType = "long".AsSpan();
            if (!xmlNodeDeclaredType.SequenceEqual(returnType))
            {
                NullableInvalidOperationException(returnType, xmlNodeDeclaredType);
            }

            ParseBody(xmlNode.Internals, out result);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out long result)
        {
            result = long.Parse(Parsable(body), CultureInfo.InvariantCulture);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out long? result)
        {
            result = long.Parse(Parsable(body), CultureInfo.InvariantCulture);
        }


        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Parse(ref global::XmlSerDe.Common.XmlDeserializeSettings settings, roschar fullNode, roschar xmlnsAttributeName, out decimal result)
        {
            var xmlNode = new global::XmlSerDe.Common.XmlNode2(settings, fullNode, xmlnsAttributeName);
            var xmlNodeDeclaredType = xmlNode.DeclaredNodeType;

            var returnType = "decimal".AsSpan();
            if (!xmlNodeDeclaredType.SequenceEqual(returnType))
            {
                InvalidOperationException(returnType, xmlNodeDeclaredType);
            }

            ParseBody(xmlNode.Internals, out result);
        }
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Parse(ref global::XmlSerDe.Common.XmlDeserializeSettings settings, roschar fullNode, roschar xmlnsAttributeName, out decimal? result)
        {
            var xmlNode = new global::XmlSerDe.Common.XmlNode2(settings, fullNode, xmlnsAttributeName);
            var xmlNodeDeclaredType = xmlNode.DeclaredNodeType;

            var returnType = "decimal".AsSpan();
            if (!xmlNodeDeclaredType.SequenceEqual(returnType))
            {
                NullableInvalidOperationException(returnType, xmlNodeDeclaredType);
            }

            ParseBody(xmlNode.Internals, out result);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out decimal result)
        {
            result = decimal.Parse(Parsable(body), NumberStyles.Number, CultureInfo.InvariantCulture);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out decimal? result)
        {
            result = decimal.Parse(Parsable(body), NumberStyles.Number, CultureInfo.InvariantCulture);
        }



        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Parse(ref global::XmlSerDe.Common.XmlDeserializeSettings settings, roschar fullNode, roschar xmlnsAttributeName, out string result)
        {
            var xmlNode = new global::XmlSerDe.Common.XmlNode2(settings, fullNode, xmlnsAttributeName);
            var xmlNodeDeclaredType = xmlNode.DeclaredNodeType;

            var returnType = "string".AsSpan();
            if (!xmlNodeDeclaredType.SequenceEqual(returnType))
            {
                InvalidOperationException(returnType, xmlNodeDeclaredType);
            }

            ParseBody(xmlNode.Internals, out result);
        }
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out string result)
        {
            result = global::XmlSerDe.Common.XmlTextDecoder.DecodeElementText(body);
        }



        [DoesNotReturn]
        private static void InvalidOperationException(
            roschar returnType,
            roschar xmlNodeDeclaredType
            )
        {
            throw new InvalidOperationException($"[C# node {returnType.ToString()}] Unknown type {xmlNodeDeclaredType.ToString()}");
        }

        [DoesNotReturn]
        private static void NullableInvalidOperationException(
            roschar returnType,
            roschar xmlNodeDeclaredType
            )
        {
            throw new InvalidOperationException($"[C# node Nullable {returnType.ToString()}] Unknown type {xmlNodeDeclaredType.ToString()}");
        }
    }
}
