using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XmlSerDe.Tests
{
    public class XmlObject16
    {
        public int MyField { get; set; }
    }

    public class XmlObject17
    {
        public List<XmlObject16> MyList { get; set; }
    }
}
