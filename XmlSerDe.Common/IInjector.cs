using System;
using System.Collections.Generic;
using System.Text;
using roschar = System.ReadOnlySpan<char>;

namespace XmlSerDe
{
    public interface IInjector
    {
        void Parse(
            ref XmlParseContext context,
            roschar fullNode,
            out DateTime value
            );
        void Parse(
            ref XmlParseContext context,
            roschar fullNode,
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
            ref XmlParseContext context,
            roschar fullNode,
            out Guid value
            );
        void Parse(
            ref XmlParseContext context,
            roschar fullNode,
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
            ref XmlParseContext context,
            roschar fullNode,
            out bool value
            );
        void Parse(
            ref XmlParseContext context,
            roschar fullNode,
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
            ref XmlParseContext context,
            roschar fullNode,
            out sbyte value
            );
        void Parse(
            ref XmlParseContext context,
            roschar fullNode,
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
            ref XmlParseContext context,
            roschar fullNode,
            out byte value
            );
        void Parse(
            ref XmlParseContext context,
            roschar fullNode,
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
            ref XmlParseContext context,
            roschar fullNode,
            out ushort value
            );
        void Parse(
            ref XmlParseContext context,
            roschar fullNode,
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
            ref XmlParseContext context,
            roschar fullNode,
            out short value
            );
        void Parse(
            ref XmlParseContext context,
            roschar fullNode,
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
            ref XmlParseContext context,
            roschar fullNode,
            out uint value
            );
        void Parse(
            ref XmlParseContext context,
            roschar fullNode,
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
            ref XmlParseContext context,
            roschar fullNode,
            out int value
            );
        void Parse(
            ref XmlParseContext context,
            roschar fullNode,
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
            ref XmlParseContext context,
            roschar fullNode,
            out ulong value
            );
        void Parse(
            ref XmlParseContext context,
            roschar fullNode,
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
            ref XmlParseContext context,
            roschar fullNode,
            out long value
            );
        void Parse(
            ref XmlParseContext context,
            roschar fullNode,
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
            ref XmlParseContext context,
            roschar fullNode,
            out decimal value
            );
        void Parse(
            ref XmlParseContext context,
            roschar fullNode,
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
            ref XmlParseContext context,
            roschar fullNode,
            out float value
            );
        void Parse(
            ref XmlParseContext context,
            roschar fullNode,
            out float? value
            );
        void ParseBody(
            roschar body,
            out float value
            );
        void ParseBody(
            roschar body,
            out float? value
            );

        void Parse(
            ref XmlParseContext context,
            roschar fullNode,
            out double value
            );
        void Parse(
            ref XmlParseContext context,
            roschar fullNode,
            out double? value
            );
        void ParseBody(
            roschar body,
            out double value
            );
        void ParseBody(
            roschar body,
            out double? value
            );

        void Parse(
            ref XmlParseContext context,
            roschar fullNode,
            out char value
            );
        void Parse(
            ref XmlParseContext context,
            roschar fullNode,
            out char? value
            );
        void ParseBody(
            roschar body,
            out char value
            );
        void ParseBody(
            roschar body,
            out char? value
            );

        void Parse(
            ref XmlParseContext context,
            roschar fullNode,
            out TimeSpan value
            );
        void Parse(
            ref XmlParseContext context,
            roschar fullNode,
            out TimeSpan? value
            );
        void ParseBody(
            roschar body,
            out TimeSpan value
            );
        void ParseBody(
            roschar body,
            out TimeSpan? value
            );

        void Parse(
            ref XmlParseContext context,
            roschar fullNode,
            out string value
            );
        void ParseBody(
            roschar body,
            out string value
            );
    }
}
