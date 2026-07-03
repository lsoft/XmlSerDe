using System;

namespace XmlSerDe.Tests
{
    /// <summary>
    /// Covers all builtin primitive types not exercised by XmlObject2/14 alone.
    /// </summary>
    public class XmlObject31
    {
        public bool BoolProperty { get; set; }
        public sbyte SByteProperty { get; set; }
        public byte ByteProperty { get; set; }
        public short ShortProperty { get; set; }
        public ushort UShortProperty { get; set; }
        public int IntProperty { get; set; }
        public uint UIntProperty { get; set; }
        public long LongProperty { get; set; }
        public ulong ULongProperty { get; set; }
        public decimal DecimalProperty { get; set; }
        public DateTime DateTimeProperty { get; set; }
        public Guid GuidProperty { get; set; }

        public bool? NullableBool { get; set; }
        public int? NullableInt { get; set; }
        public decimal? NullableDecimal { get; set; }
        public DateTime? NullableDateTime { get; set; }
        public Guid? NullableGuid { get; set; }
    }
}
