#nullable disable

using System;

namespace XmlSerDe.PerformanceTests.Subject
{
    [Serializable]
    public class PerformanceTime
    {
        public DateTime StartTime {get; set;}

        public int SecondsSpan {get; set;}
    }

}
