using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XmlSerDe.Common;
using XmlSerDe.Components.Exhauster;

namespace XmlSerDe.Tests
{
    public class Utf8BinaryExhausterChild : Utf8BinaryExhauster
    {
        private readonly MemoryStream _stream;

        public Utf8BinaryExhausterChild(
            MemoryStream stream
            )
        {
            if (stream is null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            _stream = stream;
        }

        protected override void Write(byte[] data, int length)
        {
            _stream.Write(data, 0, length);
        }
    }
}
