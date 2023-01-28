using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XmlSerDe.Tests
{

    public class XmlObject9Specific1 : XmlObject9Base
    {
        public int IntProperty
        {
            get;
            set;
        }
    }

    public class XmlObject9Specific2 : XmlObject9Base
    {
        public int IntProperty
        {
            get;
            set;
        }
    }

    public class XmlObject9Base
    {
        public string StringProperty
        {
            get;
            set;
        }
    }
}
