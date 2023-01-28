using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XmlSerDe.Tests
{
    public class XmlObject11Specific1 : XmlObject11Abstract
    {
        public int IntProperty
        {
            get;
            set;
        }
    }

    public class XmlObject11Specific2 : XmlObject11Abstract
    {
        public int IntProperty
        {
            get;
            set;
        }
    }

    public abstract class XmlObject11Abstract
    {
        public string StringProperty
        {
            get;
            set;
        }
    }
}
