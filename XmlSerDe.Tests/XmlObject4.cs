using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XmlSerDe.Tests
{
    public class XmlObject4Specific1 : XmlObject4Abstract
    {
        public int IntProperty
        {
            get;
            set;
        }
    }

    public class XmlObject4Specific2 : XmlObject4Abstract
    {
        public int IntProperty
        {
            get;
            set;
        }
    }

    public abstract class XmlObject4Abstract
    {
        public string StringProperty
        {
            get;
            set;
        }
    }
}
