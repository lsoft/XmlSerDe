using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace XmlSerDe.Tests
{
    public class XmlObject13
    {
        [XmlIgnore]
        public int IntProperty
        {
            get;
            set;
        }

        public string StringProperty
        {
            get;
            set;
        }
    }
}
