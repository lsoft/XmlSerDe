#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace XmlSerDe.Tests.Complex.Subject
{
    public enum KeyValueKindEnum
    {
        Zero,
        One,
        Two,
        Three
    }

    public static class KeyValueKindParser
    {
        [MethodImpl(TestMethodImplOptions.AggressiveOptimization)]
        public static KeyValueKindEnum Parse(
            ReadOnlySpan<char> stringRepresentation
            )
        {
            return (KeyValueKindEnum)Enum.Parse(typeof(KeyValueKindEnum), stringRepresentation.ToString());
            //return EnumsNET.Enums.Parse<KeyValueKindEnum>(stringRepresentation);
        }
    }
}
