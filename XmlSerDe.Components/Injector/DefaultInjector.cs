using System;
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
        public void Parse(ref XmlDeserializeSettings settings, roschar fullNode, roschar xmlnsAttributeName, out DateTime result)
        {
            var xmlNode = new XmlSerDe.Common.XmlNode2(settings, fullNode, xmlnsAttributeName);
            var xmlNodeDeclaredType = xmlNode.DeclaredNodeType;

            if (!xmlNodeDeclaredType.SequenceEqual("dateTime".AsSpan()))
            {
                throw new InvalidOperationException("[C# node dateTime] Unknown type " + xmlNodeDeclaredType.ToString());
            }

            ParseBody(xmlNode.Internals, out result);
        }
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Parse(ref XmlDeserializeSettings settings, roschar fullNode, roschar xmlnsAttributeName, out DateTime? result)
        {
            var xmlNode = new XmlSerDe.Common.XmlNode2(settings, fullNode, xmlnsAttributeName);
            var xmlNodeDeclaredType = xmlNode.DeclaredNodeType;

            if (!xmlNodeDeclaredType.SequenceEqual("dateTime".AsSpan()))
            {
                throw new InvalidOperationException("[C# node Nullable dateTime] Unknown type " + xmlNodeDeclaredType.ToString());
            }

            ParseBody(xmlNode.Internals, out result);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out DateTime result)
        {
            result = DateTime.Parse(body);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out DateTime? result)
        {
            result = DateTime.Parse(body);
        }



        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Parse(ref XmlDeserializeSettings settings, roschar fullNode, roschar xmlnsAttributeName, out Guid result)
        {
            var xmlNode = new XmlSerDe.Common.XmlNode2(settings, fullNode, xmlnsAttributeName);
            var xmlNodeDeclaredType = xmlNode.DeclaredNodeType;

            if (!xmlNodeDeclaredType.SequenceEqual("guid".AsSpan()))
            {
                throw new InvalidOperationException("[C# node guid] Unknown type " + xmlNodeDeclaredType.ToString());
            }

            ParseBody(xmlNode.Internals, out result);
        }
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Parse(ref XmlDeserializeSettings settings, roschar fullNode, roschar xmlnsAttributeName, out Guid? result)
        {
            var xmlNode = new XmlSerDe.Common.XmlNode2(settings, fullNode, xmlnsAttributeName);
            var xmlNodeDeclaredType = xmlNode.DeclaredNodeType;

            if (!xmlNodeDeclaredType.SequenceEqual("guid".AsSpan()))
            {
                throw new InvalidOperationException("[C# node Nullable guid] Unknown type " + xmlNodeDeclaredType.ToString());
            }

            ParseBody(xmlNode.Internals, out result);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out Guid result)
        {
            result = Guid.Parse(body);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out Guid? result)
        {
            result = Guid.Parse(body);
        }


        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Parse(ref XmlDeserializeSettings settings, roschar fullNode, roschar xmlnsAttributeName, out bool result)
        {
            var xmlNode = new XmlSerDe.Common.XmlNode2(settings, fullNode, xmlnsAttributeName);
            var xmlNodeDeclaredType = xmlNode.DeclaredNodeType;

            if (!xmlNodeDeclaredType.SequenceEqual("boolean".AsSpan()))
            {
                throw new InvalidOperationException("[C# node boolean] Unknown type " + xmlNodeDeclaredType.ToString());
            }

            ParseBody(xmlNode.Internals, out result);
        }
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Parse(ref XmlDeserializeSettings settings, roschar fullNode, roschar xmlnsAttributeName, out bool? result)
        {
            var xmlNode = new XmlSerDe.Common.XmlNode2(settings, fullNode, xmlnsAttributeName);
            var xmlNodeDeclaredType = xmlNode.DeclaredNodeType;

            if (!xmlNodeDeclaredType.SequenceEqual("boolean".AsSpan()))
            {
                throw new InvalidOperationException("[C# node Nullable boolean] Unknown type " + xmlNodeDeclaredType.ToString());
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
        public void Parse(ref XmlDeserializeSettings settings, roschar fullNode, roschar xmlnsAttributeName, out sbyte result)
        {
            var xmlNode = new XmlSerDe.Common.XmlNode2(settings, fullNode, xmlnsAttributeName);
            var xmlNodeDeclaredType = xmlNode.DeclaredNodeType;

            if (!xmlNodeDeclaredType.SequenceEqual("byte".AsSpan()))
            {
                throw new InvalidOperationException("[C# node byte] Unknown type " + xmlNodeDeclaredType.ToString());
            }

            ParseBody(xmlNode.Internals, out result);
        }
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Parse(ref XmlDeserializeSettings settings, roschar fullNode, roschar xmlnsAttributeName, out sbyte? result)
        {
            var xmlNode = new XmlSerDe.Common.XmlNode2(settings, fullNode, xmlnsAttributeName);
            var xmlNodeDeclaredType = xmlNode.DeclaredNodeType;

            if (!xmlNodeDeclaredType.SequenceEqual("byte".AsSpan()))
            {
                throw new InvalidOperationException("[C# node Nullable byte] Unknown type " + xmlNodeDeclaredType.ToString());
            }

            ParseBody(xmlNode.Internals, out result);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out sbyte result)
        {
            result = sbyte.Parse(body);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out sbyte? result)
        {
            result = sbyte.Parse(body);
        }


        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Parse(ref XmlDeserializeSettings settings, roschar fullNode, roschar xmlnsAttributeName, out byte result)
        {
            var xmlNode = new XmlSerDe.Common.XmlNode2(settings, fullNode, xmlnsAttributeName);
            var xmlNodeDeclaredType = xmlNode.DeclaredNodeType;

            if (!xmlNodeDeclaredType.SequenceEqual("unsignedByte".AsSpan()))
            {
                throw new InvalidOperationException("[C# node unsignedByte] Unknown type " + xmlNodeDeclaredType.ToString());
            }

            ParseBody(xmlNode.Internals, out result);
        }
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Parse(ref XmlDeserializeSettings settings, roschar fullNode, roschar xmlnsAttributeName, out byte? result)
        {
            var xmlNode = new XmlSerDe.Common.XmlNode2(settings, fullNode, xmlnsAttributeName);
            var xmlNodeDeclaredType = xmlNode.DeclaredNodeType;

            if (!xmlNodeDeclaredType.SequenceEqual("unsignedByte".AsSpan()))
            {
                throw new InvalidOperationException("[C# node Nullable unsignedByte] Unknown type " + xmlNodeDeclaredType.ToString());
            }

            ParseBody(xmlNode.Internals, out result);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out byte result)
        {
            result = byte.Parse(body);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out byte? result)
        {
            result = byte.Parse(body);
        }


        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Parse(ref XmlDeserializeSettings settings, roschar fullNode, roschar xmlnsAttributeName, out ushort result)
        {
            var xmlNode = new XmlSerDe.Common.XmlNode2(settings, fullNode, xmlnsAttributeName);
            var xmlNodeDeclaredType = xmlNode.DeclaredNodeType;

            if (!xmlNodeDeclaredType.SequenceEqual("unsignedShort".AsSpan()))
            {
                throw new InvalidOperationException("[C# node unsignedShort] Unknown type " + xmlNodeDeclaredType.ToString());
            }

            ParseBody(xmlNode.Internals, out result);
        }
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Parse(ref XmlDeserializeSettings settings, roschar fullNode, roschar xmlnsAttributeName, out ushort? result)
        {
            var xmlNode = new XmlSerDe.Common.XmlNode2(settings, fullNode, xmlnsAttributeName);
            var xmlNodeDeclaredType = xmlNode.DeclaredNodeType;

            if (!xmlNodeDeclaredType.SequenceEqual("unsignedShort".AsSpan()))
            {
                throw new InvalidOperationException("[C# node Nullable unsignedShort] Unknown type " + xmlNodeDeclaredType.ToString());
            }

            ParseBody(xmlNode.Internals, out result);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out ushort result)
        {
            result = ushort.Parse(body);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out ushort? result)
        {
            result = ushort.Parse(body);
        }


        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Parse(ref XmlDeserializeSettings settings, roschar fullNode, roschar xmlnsAttributeName, out short result)
        {
            var xmlNode = new XmlSerDe.Common.XmlNode2(settings, fullNode, xmlnsAttributeName);
            var xmlNodeDeclaredType = xmlNode.DeclaredNodeType;

            if (!xmlNodeDeclaredType.SequenceEqual("short".AsSpan()))
            {
                throw new InvalidOperationException("[C# node short] Unknown type " + xmlNodeDeclaredType.ToString());
            }

            ParseBody(xmlNode.Internals, out result);
        }
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Parse(ref XmlDeserializeSettings settings, roschar fullNode, roschar xmlnsAttributeName, out short? result)
        {
            var xmlNode = new XmlSerDe.Common.XmlNode2(settings, fullNode, xmlnsAttributeName);
            var xmlNodeDeclaredType = xmlNode.DeclaredNodeType;

            if (!xmlNodeDeclaredType.SequenceEqual("short".AsSpan()))
            {
                throw new InvalidOperationException("[C# node Nullable short] Unknown type " + xmlNodeDeclaredType.ToString());
            }

            ParseBody(xmlNode.Internals, out result);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out short result)
        {
            result = short.Parse(body);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out short? result)
        {
            result = short.Parse(body);
        }


        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Parse(ref XmlDeserializeSettings settings, roschar fullNode, roschar xmlnsAttributeName, out uint result)
        {
            var xmlNode = new XmlSerDe.Common.XmlNode2(settings, fullNode, xmlnsAttributeName);
            var xmlNodeDeclaredType = xmlNode.DeclaredNodeType;

            if (!xmlNodeDeclaredType.SequenceEqual("unsignedInt".AsSpan()))
            {
                throw new InvalidOperationException("[C# node unsignedInt] Unknown type " + xmlNodeDeclaredType.ToString());
            }

            ParseBody(xmlNode.Internals, out result);
        }
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Parse(ref XmlDeserializeSettings settings, roschar fullNode, roschar xmlnsAttributeName, out uint? result)
        {
            var xmlNode = new XmlSerDe.Common.XmlNode2(settings, fullNode, xmlnsAttributeName);
            var xmlNodeDeclaredType = xmlNode.DeclaredNodeType;

            if (!xmlNodeDeclaredType.SequenceEqual("unsignedInt".AsSpan()))
            {
                throw new InvalidOperationException("[C# node Nullable unsignedInt] Unknown type " + xmlNodeDeclaredType.ToString());
            }

            ParseBody(xmlNode.Internals, out result);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out uint result)
        {
            result = uint.Parse(body);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out uint? result)
        {
            result = uint.Parse(body);
        }


        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Parse(ref XmlDeserializeSettings settings, roschar fullNode, roschar xmlnsAttributeName, out int result)
        {
            var xmlNode = new XmlSerDe.Common.XmlNode2(settings, fullNode, xmlnsAttributeName);
            var xmlNodeDeclaredType = xmlNode.DeclaredNodeType;

            if (!xmlNodeDeclaredType.SequenceEqual("int".AsSpan()))
            {
                throw new InvalidOperationException("[C# node int] Unknown type " + xmlNodeDeclaredType.ToString());
            }

            ParseBody(xmlNode.Internals, out result);
        }
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Parse(ref XmlDeserializeSettings settings, roschar fullNode, roschar xmlnsAttributeName, out int? result)
        {
            var xmlNode = new XmlSerDe.Common.XmlNode2(settings, fullNode, xmlnsAttributeName);
            var xmlNodeDeclaredType = xmlNode.DeclaredNodeType;

            if (!xmlNodeDeclaredType.SequenceEqual("int".AsSpan()))
            {
                throw new InvalidOperationException("[C# node Nullable int] Unknown type " + xmlNodeDeclaredType.ToString());
            }

            ParseBody(xmlNode.Internals, out result);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out int result)
        {
            result = int.Parse(body);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out int? result)
        {
            result = int.Parse(body);
        }


        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Parse(ref XmlDeserializeSettings settings, roschar fullNode, roschar xmlnsAttributeName, out ulong result)
        {
            var xmlNode = new XmlSerDe.Common.XmlNode2(settings, fullNode, xmlnsAttributeName);
            var xmlNodeDeclaredType = xmlNode.DeclaredNodeType;

            if (!xmlNodeDeclaredType.SequenceEqual("unsignedLong".AsSpan()))
            {
                throw new InvalidOperationException("[C# node unsignedLong] Unknown type " + xmlNodeDeclaredType.ToString());
            }

            ParseBody(xmlNode.Internals, out result);
        }
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Parse(ref XmlDeserializeSettings settings, roschar fullNode, roschar xmlnsAttributeName, out ulong? result)
        {
            var xmlNode = new XmlSerDe.Common.XmlNode2(settings, fullNode, xmlnsAttributeName);
            var xmlNodeDeclaredType = xmlNode.DeclaredNodeType;

            if (!xmlNodeDeclaredType.SequenceEqual("unsignedLong".AsSpan()))
            {
                throw new InvalidOperationException("[C# node Nullable unsignedLong] Unknown type " + xmlNodeDeclaredType.ToString());
            }

            ParseBody(xmlNode.Internals, out result);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out ulong result)
        {
            result = ulong.Parse(body);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out ulong? result)
        {
            result = ulong.Parse(body);
        }


        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Parse(ref XmlDeserializeSettings settings, roschar fullNode, roschar xmlnsAttributeName, out long result)
        {
            var xmlNode = new XmlSerDe.Common.XmlNode2(settings, fullNode, xmlnsAttributeName);
            var xmlNodeDeclaredType = xmlNode.DeclaredNodeType;

            if (!xmlNodeDeclaredType.SequenceEqual("long".AsSpan()))
            {
                throw new InvalidOperationException("[C# node long] Unknown type " + xmlNodeDeclaredType.ToString());
            }

            ParseBody(xmlNode.Internals, out result);
        }
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Parse(ref XmlDeserializeSettings settings, roschar fullNode, roschar xmlnsAttributeName, out long? result)
        {
            var xmlNode = new XmlSerDe.Common.XmlNode2(settings, fullNode, xmlnsAttributeName);
            var xmlNodeDeclaredType = xmlNode.DeclaredNodeType;

            if (!xmlNodeDeclaredType.SequenceEqual("long".AsSpan()))
            {
                throw new InvalidOperationException("[C# node Nullable long] Unknown type " + xmlNodeDeclaredType.ToString());
            }

            ParseBody(xmlNode.Internals, out result);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out long result)
        {
            result = long.Parse(body);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out long? result)
        {
            result = long.Parse(body);
        }


        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Parse(ref XmlDeserializeSettings settings, roschar fullNode, roschar xmlnsAttributeName, out decimal result)
        {
            var xmlNode = new XmlSerDe.Common.XmlNode2(settings, fullNode, xmlnsAttributeName);
            var xmlNodeDeclaredType = xmlNode.DeclaredNodeType;

            if (!xmlNodeDeclaredType.SequenceEqual("decimal".AsSpan()))
            {
                throw new InvalidOperationException("[C# node decimal] Unknown type " + xmlNodeDeclaredType.ToString());
            }

            ParseBody(xmlNode.Internals, out result);
        }
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Parse(ref XmlDeserializeSettings settings, roschar fullNode, roschar xmlnsAttributeName, out decimal? result)
        {
            var xmlNode = new XmlSerDe.Common.XmlNode2(settings, fullNode, xmlnsAttributeName);
            var xmlNodeDeclaredType = xmlNode.DeclaredNodeType;

            if (!xmlNodeDeclaredType.SequenceEqual("decimal".AsSpan()))
            {
                throw new InvalidOperationException("[C# node Nullable decimal] Unknown type " + xmlNodeDeclaredType.ToString());
            }

            ParseBody(xmlNode.Internals, out result);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out decimal result)
        {
            result = long.Parse(body);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out decimal? result)
        {
            result = long.Parse(body);
        }


        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Parse(ref XmlDeserializeSettings settings, roschar fullNode, roschar xmlnsAttributeName, out string result)
        {
            var xmlNode = new XmlSerDe.Common.XmlNode2(settings, fullNode, xmlnsAttributeName);
            var xmlNodeDeclaredType = xmlNode.DeclaredNodeType;

            if (!xmlNodeDeclaredType.SequenceEqual("string".AsSpan()))
            {
                throw new InvalidOperationException("[C# node string] Unknown type " + xmlNodeDeclaredType.ToString());
            }

            ParseBody(xmlNode.Internals, out result);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBody(roschar body, out string result)
        {
            result = global::System.Net.WebUtility.HtmlDecode(body.ToString());
        }

    }
}
