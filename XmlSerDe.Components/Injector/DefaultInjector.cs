using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
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
        /// В netstandard2.0 span-перегрузок нет у DateTime/Guid/decimal, поэтому
        /// вход там материализуется в строку. Целые, bool и duration разбираются
        /// из спана на всех таргетах.
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

        /// <summary>
        /// RoundtripKind обязателен: без него DateTime.Parse переводит "...T14:30:45Z"
        /// в местное время и ставит Kind = Local. Мгновение при этом сохраняется, но
        /// лексическая форма - нет, поэтому round-trip не замыкается ни с
        /// System.Xml.Serialization (тот разбирает через XmlConvert и Kind сохраняет),
        /// ни с собственной сериализацией: формат "yyyy-MM-ddTHH:mm:ss.FFFFFFFK"
        /// выведет уже не "Z", а смещение машины, на которой шёл разбор.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out global::System.DateTime result)
        {
            result = DateTime.Parse(Parsable(body), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out global::System.DateTime? result)
        {
            result = DateTime.Parse(Parsable(body), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
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
            result = XmlSpanParse.ParseBoolean(body);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out bool? result)
        {
            result = XmlSpanParse.ParseBoolean(body);
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
            result = XmlSpanParse.ParseSByte(body);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out sbyte? result)
        {
            result = XmlSpanParse.ParseSByte(body);
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
            result = XmlSpanParse.ParseByte(body);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out byte? result)
        {
            result = XmlSpanParse.ParseByte(body);
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
            result = XmlSpanParse.ParseUInt16(body);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out ushort? result)
        {
            result = XmlSpanParse.ParseUInt16(body);
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
            result = XmlSpanParse.ParseInt16(body);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out short? result)
        {
            result = XmlSpanParse.ParseInt16(body);
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
            result = XmlSpanParse.ParseUInt32(body);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out uint? result)
        {
            result = XmlSpanParse.ParseUInt32(body);
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
            result = XmlSpanParse.ParseInt32(body);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out int? result)
        {
            result = XmlSpanParse.ParseInt32(body);
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
            result = XmlSpanParse.ParseUInt64(body);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out ulong? result)
        {
            result = XmlSpanParse.ParseUInt64(body);
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
            result = XmlSpanParse.ParseInt64(body);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out long? result)
        {
            result = XmlSpanParse.ParseInt64(body);
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
        public void Parse(ref global::XmlSerDe.Common.XmlDeserializeSettings settings, roschar fullNode, roschar xmlnsAttributeName, out float result)
        {
            var xmlNode = new global::XmlSerDe.Common.XmlNode2(settings, fullNode, xmlnsAttributeName);
            var xmlNodeDeclaredType = xmlNode.DeclaredNodeType;

            var returnType = "float".AsSpan();
            if (!xmlNodeDeclaredType.SequenceEqual(returnType))
            {
                InvalidOperationException(returnType, xmlNodeDeclaredType);
            }

            ParseBody(xmlNode.Internals, out result);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Parse(ref global::XmlSerDe.Common.XmlDeserializeSettings settings, roschar fullNode, roschar xmlnsAttributeName, out float? result)
        {
            var xmlNode = new global::XmlSerDe.Common.XmlNode2(settings, fullNode, xmlnsAttributeName);
            var xmlNodeDeclaredType = xmlNode.DeclaredNodeType;

            var returnType = "float".AsSpan();
            if (!xmlNodeDeclaredType.SequenceEqual(returnType))
            {
                NullableInvalidOperationException(returnType, xmlNodeDeclaredType);
            }

            ParseBody(xmlNode.Internals, out result);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out float result)
        {
            result = global::XmlSerDe.Common.XmlNumberLexis.ParseSingle(body);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out float? result)
        {
            result = global::XmlSerDe.Common.XmlNumberLexis.ParseSingle(body);
        }



        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Parse(ref global::XmlSerDe.Common.XmlDeserializeSettings settings, roschar fullNode, roschar xmlnsAttributeName, out double result)
        {
            var xmlNode = new global::XmlSerDe.Common.XmlNode2(settings, fullNode, xmlnsAttributeName);
            var xmlNodeDeclaredType = xmlNode.DeclaredNodeType;

            var returnType = "double".AsSpan();
            if (!xmlNodeDeclaredType.SequenceEqual(returnType))
            {
                InvalidOperationException(returnType, xmlNodeDeclaredType);
            }

            ParseBody(xmlNode.Internals, out result);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Parse(ref global::XmlSerDe.Common.XmlDeserializeSettings settings, roschar fullNode, roschar xmlnsAttributeName, out double? result)
        {
            var xmlNode = new global::XmlSerDe.Common.XmlNode2(settings, fullNode, xmlnsAttributeName);
            var xmlNodeDeclaredType = xmlNode.DeclaredNodeType;

            var returnType = "double".AsSpan();
            if (!xmlNodeDeclaredType.SequenceEqual(returnType))
            {
                NullableInvalidOperationException(returnType, xmlNodeDeclaredType);
            }

            ParseBody(xmlNode.Internals, out result);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out double result)
        {
            result = global::XmlSerDe.Common.XmlNumberLexis.ParseDouble(body);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out double? result)
        {
            result = global::XmlSerDe.Common.XmlNumberLexis.ParseDouble(body);
        }



        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Parse(ref global::XmlSerDe.Common.XmlDeserializeSettings settings, roschar fullNode, roschar xmlnsAttributeName, out char result)
        {
            var xmlNode = new global::XmlSerDe.Common.XmlNode2(settings, fullNode, xmlnsAttributeName);
            var xmlNodeDeclaredType = xmlNode.DeclaredNodeType;

            var returnType = "char".AsSpan();
            if (!xmlNodeDeclaredType.SequenceEqual(returnType))
            {
                InvalidOperationException(returnType, xmlNodeDeclaredType);
            }

            ParseBody(xmlNode.Internals, out result);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Parse(ref global::XmlSerDe.Common.XmlDeserializeSettings settings, roschar fullNode, roschar xmlnsAttributeName, out char? result)
        {
            var xmlNode = new global::XmlSerDe.Common.XmlNode2(settings, fullNode, xmlnsAttributeName);
            var xmlNodeDeclaredType = xmlNode.DeclaredNodeType;

            var returnType = "char".AsSpan();
            if (!xmlNodeDeclaredType.SequenceEqual(returnType))
            {
                NullableInvalidOperationException(returnType, xmlNodeDeclaredType);
            }

            ParseBody(xmlNode.Internals, out result);
        }
        /// <summary>
        /// В документе лежит кодовая точка числом, а не сам символ, - см.
        /// <see cref="global::XmlSerDe.Common.IExhauster.Append(char)"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out char result)
        {
            result = (char)XmlSpanParse.ParseUInt16(body);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out char? result)
        {
            ParseBody(body, out char value);
            result = value;
        }



        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Parse(ref global::XmlSerDe.Common.XmlDeserializeSettings settings, roschar fullNode, roschar xmlnsAttributeName, out TimeSpan result)
        {
            var xmlNode = new global::XmlSerDe.Common.XmlNode2(settings, fullNode, xmlnsAttributeName);
            var xmlNodeDeclaredType = xmlNode.DeclaredNodeType;

            var returnType = "duration".AsSpan();
            if (!xmlNodeDeclaredType.SequenceEqual(returnType))
            {
                InvalidOperationException(returnType, xmlNodeDeclaredType);
            }

            ParseBody(xmlNode.Internals, out result);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Parse(ref global::XmlSerDe.Common.XmlDeserializeSettings settings, roschar fullNode, roschar xmlnsAttributeName, out TimeSpan? result)
        {
            var xmlNode = new global::XmlSerDe.Common.XmlNode2(settings, fullNode, xmlnsAttributeName);
            var xmlNodeDeclaredType = xmlNode.DeclaredNodeType;

            var returnType = "duration".AsSpan();
            if (!xmlNodeDeclaredType.SequenceEqual(returnType))
            {
                NullableInvalidOperationException(returnType, xmlNodeDeclaredType);
            }

            ParseBody(xmlNode.Internals, out result);
        }
        /// <summary>
        /// Длительность ISO-8601: та же грамматика, что у XmlConvert.ToTimeSpan
        /// (годы и месяцы, дробные секунды, знак), но без промежуточной строки.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out TimeSpan result)
        {
            result = XmlNumberLexis.ParseDuration(body);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out TimeSpan? result)
        {
            ParseBody(body, out TimeSpan value);
            result = value;
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
