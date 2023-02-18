#nullable disable

using System;

namespace XmlSerDe.Tests.Complex.Subject
{
    [Serializable]
    public class PerformanceTime
    {
        public DateTime StartTime {get; set;}

        public int SecondsSpan {get; set;}
    }

}
