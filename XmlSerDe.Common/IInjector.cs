using System;
using System.Collections.Generic;
using System.Text;
using roschar = System.ReadOnlySpan<char>;

namespace XmlSerDe.Common
{
    public interface IInjector
    {
        void Parse(
            ref XmlDeserializeSettings settings,
            roschar fullNode,
            roschar xmlnsAttributeName,
            out DateTime value
            );
        void Parse(
            ref XmlDeserializeSettings settings,
            roschar fullNode,
            roschar xmlnsAttributeName,
            out DateTime? value
            );
        void ParseBody(
            roschar body,
            out DateTime value
            );
        void ParseBody(
            roschar body,
            out DateTime? value
            );

        void Parse(
            ref XmlDeserializeSettings settings,
            roschar fullNode,
            roschar xmlnsAttributeName,
            out Guid value
            );
        void Parse(
            ref XmlDeserializeSettings settings,
            roschar fullNode,
            roschar xmlnsAttributeName,
            out Guid? value
            );
        void ParseBody(
            roschar body,
            out Guid value
            );
        void ParseBody(
            roschar body,
            out Guid? value
            );

        void Parse(
            ref XmlDeserializeSettings settings,
            roschar fullNode,
            roschar xmlnsAttributeName,
            out bool value
            );
        void Parse(
            ref XmlDeserializeSettings settings,
            roschar fullNode,
            roschar xmlnsAttributeName,
            out bool? value
            );
        void ParseBody(
            roschar body,
            out bool value
            );
        void ParseBody(
            roschar body,
            out bool? value
            );

        void Parse(
            ref XmlDeserializeSettings settings,
            roschar fullNode,
            roschar xmlnsAttributeName,
            out sbyte value
            );
        void Parse(
            ref XmlDeserializeSettings settings,
            roschar fullNode,
            roschar xmlnsAttributeName,
            out sbyte? value
            );
        void ParseBody(
            roschar body,
            out sbyte value
            );
        void ParseBody(
            roschar body,
            out sbyte? value
            );

        void Parse(
            ref XmlDeserializeSettings settings,
            roschar fullNode,
            roschar xmlnsAttributeName,
            out byte value
            );
        void Parse(
            ref XmlDeserializeSettings settings,
            roschar fullNode,
            roschar xmlnsAttributeName,
            out byte? value
            );
        void ParseBody(
            roschar body,
            out byte value
            );
        void ParseBody(
            roschar body,
            out byte? value
            );

        void Parse(
            ref XmlDeserializeSettings settings,
            roschar fullNode,
            roschar xmlnsAttributeName,
            out ushort value
            );
        void Parse(
            ref XmlDeserializeSettings settings,
            roschar fullNode,
            roschar xmlnsAttributeName,
            out ushort? value
            );
        void ParseBody(
            roschar body,
            out ushort value
            );
        void ParseBody(
            roschar body,
            out ushort? value
            );


        void Parse(
            ref XmlDeserializeSettings settings,
            roschar fullNode,
            roschar xmlnsAttributeName,
            out short value
            );
        void Parse(
            ref XmlDeserializeSettings settings,
            roschar fullNode,
            roschar xmlnsAttributeName,
            out short? value
            );
        void ParseBody(
            roschar body,
            out short value
            );
        void ParseBody(
            roschar body,
            out short? value
            );

        void Parse(
            ref XmlDeserializeSettings settings,
            roschar fullNode,
            roschar xmlnsAttributeName,
            out uint value
            );
        void Parse(
            ref XmlDeserializeSettings settings,
            roschar fullNode,
            roschar xmlnsAttributeName,
            out uint? value
            );
        void ParseBody(
            roschar body,
            out uint value
            );
        void ParseBody(
            roschar body,
            out uint? value
            );

        void Parse(
            ref XmlDeserializeSettings settings,
            roschar fullNode,
            roschar xmlnsAttributeName,
            out int value
            );
        void Parse(
            ref XmlDeserializeSettings settings,
            roschar fullNode,
            roschar xmlnsAttributeName,
            out int? value
            );
        void ParseBody(
            roschar body,
            out int value
            );
        void ParseBody(
            roschar body,
            out int? value
            );

        void Parse(
            ref XmlDeserializeSettings settings,
            roschar fullNode,
            roschar xmlnsAttributeName,
            out ulong value
            );
        void Parse(
            ref XmlDeserializeSettings settings,
            roschar fullNode,
            roschar xmlnsAttributeName,
            out ulong? value
            );
        void ParseBody(
            roschar body,
            out ulong value
            );
        void ParseBody(
            roschar body,
            out ulong? value
            );

        void Parse(
            ref XmlDeserializeSettings settings,
            roschar fullNode,
            roschar xmlnsAttributeName,
            out long value
            );
        void Parse(
            ref XmlDeserializeSettings settings,
            roschar fullNode,
            roschar xmlnsAttributeName,
            out long? value
            );
        void ParseBody(
            roschar body,
            out long value
            );
        void ParseBody(
            roschar body,
            out long? value
            );

        void Parse(
            ref XmlDeserializeSettings settings,
            roschar fullNode,
            roschar xmlnsAttributeName,
            out decimal value
            );
        void Parse(
            ref XmlDeserializeSettings settings,
            roschar fullNode,
            roschar xmlnsAttributeName,
            out decimal? value
            );
        void ParseBody(
            roschar body,
            out decimal value
            );
        void ParseBody(
            roschar body,
            out decimal? value
            );

        void Parse(
            ref XmlDeserializeSettings settings,
            roschar fullNode,
            roschar xmlnsAttributeName,
            out string value
            );
        void ParseBody(
            roschar body,
            out string value
            );
    }
}
