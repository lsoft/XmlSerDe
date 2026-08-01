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
            result = DateTime.Parse(body, CultureInfo.InvariantCulture);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out global::System.DateTime? result)
        {
            result = DateTime.Parse(body, CultureInfo.InvariantCulture);
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
            result = global::System.Guid.Parse(body);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out global::System.Guid? result)
        {
            result = global::System.Guid.Parse(body);
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
            result = bool.Parse(body);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out bool? result)
        {
            result = bool.Parse(body);
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
            result = sbyte.Parse(body, CultureInfo.InvariantCulture);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out sbyte? result)
        {
            result = sbyte.Parse(body, CultureInfo.InvariantCulture);
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
            result = byte.Parse(body, CultureInfo.InvariantCulture);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out byte? result)
        {
            result = byte.Parse(body, CultureInfo.InvariantCulture);
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
            result = ushort.Parse(body, CultureInfo.InvariantCulture);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out ushort? result)
        {
            result = ushort.Parse(body, CultureInfo.InvariantCulture);
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
            result = short.Parse(body, CultureInfo.InvariantCulture);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out short? result)
        {
            result = short.Parse(body, CultureInfo.InvariantCulture);
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
            result = uint.Parse(body, CultureInfo.InvariantCulture);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out uint? result)
        {
            result = uint.Parse(body, CultureInfo.InvariantCulture);
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
            result = int.Parse(body, CultureInfo.InvariantCulture);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out int? result)
        {
            result = int.Parse(body, CultureInfo.InvariantCulture);
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
            result = ulong.Parse(body, CultureInfo.InvariantCulture);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out ulong? result)
        {
            result = ulong.Parse(body, CultureInfo.InvariantCulture);
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
            result = long.Parse(body, CultureInfo.InvariantCulture);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out long? result)
        {
            result = long.Parse(body, CultureInfo.InvariantCulture);
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
            result = decimal.Parse(body, NumberStyles.Number, CultureInfo.InvariantCulture);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out decimal? result)
        {
            result = decimal.Parse(body, NumberStyles.Number, CultureInfo.InvariantCulture);
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
