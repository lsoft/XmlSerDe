#nullable disable

using System;

namespace XmlSerDe.PerformanceTests.Subject
{
    [Serializable]
    public class SerializeKeyValue
    {
        public KeyValueKindEnum Key;

        public PerformanceTime Value;
    }

}
