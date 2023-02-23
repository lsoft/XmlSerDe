using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XmlSerDe.Tests
{
    public class XmlObject22
    {
        public XmlEnum22[] Enums
        {
            get; set;
        }
    }

    public enum XmlEnum22
    {
        A,
        B,
        C
    }
}
