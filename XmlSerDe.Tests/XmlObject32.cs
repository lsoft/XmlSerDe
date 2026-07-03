using System.Collections.Generic;

namespace XmlSerDe.Tests
{
    /// <summary>
    /// Empty and null collection edge cases.
    /// </summary>
    public class XmlObject32
    {
        public List<string> EmptyList { get; set; }
        public List<string> NullList { get; set; }
        public string[] EmptyArray { get; set; }
        public int[] NullArray { get; set; }
    }
}
