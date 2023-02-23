using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XmlSerDe.Tests
{
    public class XmlObject18
    {
        public List<XmlEnum18> Enums
        {
            get; set;
        }
    }

    public enum XmlEnum18
    {
        A,
        B,
        C
    }
}
