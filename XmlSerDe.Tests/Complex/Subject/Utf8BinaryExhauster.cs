using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XmlSerDe.Common;
using XmlSerDe.Components.Exhauster;

namespace XmlSerDe.Tests.Complex.Subject
{
    public class Utf8BinaryExhausterEmpty : Utf8BinaryExhauster
    {
        protected override void Write(byte[] data, int length)
        {
            //nothing to do in tests
        }
    }
}
