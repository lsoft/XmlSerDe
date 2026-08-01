#nullable disable

namespace XmlSerDe.Tests.Deep.Subject
{
    /// <summary>
    /// Self-referencing subject for the DEEP benchmark scenario: a chain of
    /// nested nodes with a single string payload at the very bottom.
    /// Member order matters - both serializers emit members in declaration
    /// order, so Child must come before Payload.
    /// </summary>
    public partial class DeepNode
    {
        public DeepNode Child { get; set; }

        public string Payload { get; set; }
    }
}
