#nullable disable

using System;

namespace XmlSerDe.Tests.Complex.Subject
{
    [Serializable]
    public class SerializeKeyValue
    {
        public KeyValueKindEnum Key;

        public PerformanceTime Value;
    }

}
