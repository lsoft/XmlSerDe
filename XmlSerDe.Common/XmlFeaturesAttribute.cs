using System;

namespace XmlSerDe
{
    /// <summary>
    /// Opt-in XML constructions for a serializer host. Several attributes on
    /// one class are combined with OR. <see cref="XmlFeature.None"/> is the
    /// same as omitting the attribute. Subject types, members, and assemblies
    /// are ignored — features belong to the document reader/writer, not to
    /// a mapped type.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class XmlFeaturesAttribute : Attribute
    {
        public XmlFeature Features { get; }

        public XmlFeaturesAttribute(XmlFeature features)
        {
            Features = features;
        }
    }
}
