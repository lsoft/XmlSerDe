using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml.Serialization;
using XmlSerDe.Components.Exhauster;
using XmlSerDe.Components.Injector;
using XmlSerDe.Tests.Deep.Subject;
using Xunit;

namespace XmlSerDe.Tests.Deep
{
    /// <summary>
    /// DEEP scenario: one node inside another, <see cref="Depth"/> levels down,
    /// with a single string at the bottom and nothing else. Complements the
    /// wide-but-shallow ComplexFixture document: here the cost of deserialization
    /// is dominated by nesting depth rather than by the number of elements.
    ///
    /// The document is emitted without indentation on purpose - whitespace between
    /// elements would grow quadratically with depth and would mask what this
    /// scenario is meant to expose.
    /// </summary>
    public class DeepFixture
    {
        /// <summary>
        /// Total number of nested elements, root included.
        /// </summary>
        public const int Depth = 100;

        public const string PayloadString = "the deepest payload value";

        public static readonly XmlSerializer SystemXmlSerializer = new XmlSerializer(
            typeof(DeepNode)
            );

        /// <summary>
        /// &lt;DeepNode&gt;&lt;Child&gt;...&lt;Payload&gt;...&lt;/Payload&gt;...&lt;/Child&gt;&lt;/DeepNode&gt;
        /// </summary>
        public static readonly string DeepXml = BuildDeepXml(Depth);

        public static readonly DeepNode DefaultObject = BuildDeepObject(Depth);

        private static string BuildDeepXml(int depth)
        {
            var sb = new StringBuilder();

            sb.Append("<DeepNode>");
            for (var i = 1; i < depth; i++)
            {
                sb.Append("<Child>");
            }

            sb.Append("<Payload>");
            sb.Append(PayloadString);
            sb.Append("</Payload>");

            for (var i = 1; i < depth; i++)
            {
                sb.Append("</Child>");
            }
            sb.Append("</DeepNode>");

            return sb.ToString();
        }

        private static DeepNode BuildDeepObject(int depth)
        {
            var node = new DeepNode
            {
                Payload = PayloadString
            };

            for (var i = 1; i < depth; i++)
            {
                node = new DeepNode
                {
                    Child = node
                };
            }

            return node;
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static DeepNode Deserialize_SystemXml(string xml)
        {
            using (var reader = new StringReader(xml))
            {
                var r = (DeepNode)SystemXmlSerializer.Deserialize(reader);
                return r;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static DeepNode Deserialize_XmlSerDe(ReadOnlySpan<char> xml)
        {
            DeepXmlSerializerDeserializer.Deserialize(DefaultInjector.Instance, xml, out DeepNode r);
            return r;
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static string Serialize_SystemXml(DeepNode node)
        {
            using var ms = new MemoryStream();
            SystemXmlSerializer.Serialize(ms, node);
            var xml = Encoding.UTF8.GetString(ms.GetBuffer().AsSpan(0, (int)ms.Length));
            return xml;
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static string Serialize_XmlSerDe(DeepNode node)
        {
            var exhauster = new DefaultStringBuilderExhauster();
            DeepXmlSerializerDeserializer.Serialize(exhauster, node, false);
            return exhauster.ToString();
        }

        #region correctness guards for the benchmark

        [Fact]
        public void Serialize_ProducesTheSameXmlAsTheHandBuiltDocument()
        {
            Assert.Equal(DeepXml, Serialize_XmlSerDe(DefaultObject));
        }

        [Fact]
        public void Deserialize_XmlSerDe_WalksTheWholeChain()
        {
            var deserialized = Deserialize_XmlSerDe(DeepXml.AsSpan());

            AssertChain(deserialized);
        }

        [Fact]
        public void Deserialize_XmlSerDe_MatchesSystemXml()
        {
            var bySystemXml = Deserialize_SystemXml(DeepXml);
            var byXmlSerDe = Deserialize_XmlSerDe(DeepXml.AsSpan());

            AssertChain(bySystemXml);
            AssertChain(byXmlSerDe);
        }

        private static void AssertChain(DeepNode root)
        {
            Assert.NotNull(root);

            var node = root;
            for (var level = 1; level < Depth; level++)
            {
                Assert.Null(node.Payload);
                Assert.NotNull(node.Child);
                node = node.Child;
            }

            Assert.Null(node.Child);
            Assert.Equal(PayloadString, node.Payload);
        }

        #endregion
    }
}
