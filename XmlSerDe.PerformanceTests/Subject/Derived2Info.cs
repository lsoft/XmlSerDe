#nullable disable

using System;
using System.Collections.Generic;

namespace XmlSerDe.PerformanceTests.Subject
{
    [Serializable]
    public class Derived2Info : BaseInfo
    {
        public bool HotKeyUsed
        {
            get;
            set;
        }

        public int StepsCounter
        {
            get;
            set;
        }

        public List<SerializeKeyValue> EventsTime
        {
            get;
            set;
        }


        public override InfoTypeEnum InfoType
        {
            get
            {
                return InfoTypeEnum.Derived2;
            }
        }

    }

}
