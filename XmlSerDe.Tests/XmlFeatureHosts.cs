using System;
using XmlSerDe.Common;
using XmlSerDe.Components.Exhauster;

namespace XmlSerDe.Tests
{
    [XmlFeatures(XmlFeature.Markup)]
    [XmlSubject(typeof(XmlObject1), true)]
    [XmlSubject(typeof(XmlObject2), true)]
    [XmlSubject(typeof(XmlObject3), true)]
    [XmlSubject(typeof(XmlObject4Abstract), false)]
    [XmlSubject(typeof(XmlObject5), true)]
    [XmlSubject(typeof(XmlObject9Base), false)]
    [XmlSubject(typeof(XmlObject10), true)]
    public partial class XmlSerializerDeserializerMarkup
    {
    }

    [XmlFeatures(XmlFeature.CData)]
    [XmlSubject(typeof(XmlObject2), true)]
    [XmlSubject(typeof(XmlObject4Abstract), false)]
    [XmlSubject(typeof(XmlObject5), true)]
    [XmlSubject(typeof(XmlObject33), true)]
    public partial class XmlSerializerDeserializerCData
    {
    }

    [XmlFeatures(XmlFeature.FlexibleXsiPrefix)]
    [XmlSubject(typeof(XmlObject2), true)]
    [XmlSubject(typeof(XmlObject4Abstract), false)]
    [XmlSubject(typeof(XmlObject5), true)]
    public partial class XmlSerializerDeserializerFlexibleXsi
    {
    }

    [XmlFeatures(XmlFeature.CharGuard)]
    [XmlExhauster(typeof(StringBuilderExhauster))]
    [XmlSubject(typeof(XmlObject2), true)]
    public partial class XmlSerializerDeserializerCharGuard
    {
    }

    [XmlFeatures(XmlFeature.SystemXmlCompatible)]
    [XmlExhauster(typeof(StringBuilderExhauster))]
    [XmlSubject(typeof(XmlObject1), true)]
    [XmlSubject(typeof(XmlObject2), true)]
    [XmlSubject(typeof(XmlObject4Abstract), false)]
    [XmlSubject(typeof(XmlObject5), true)]
    [XmlSubject(typeof(XmlObject33), true)]
    public partial class XmlSerializerDeserializerFull
    {
    }

    [XmlFeatures(XmlFeature.CData)]
    [XmlFeatures(XmlFeature.CharGuard)]
    [XmlExhauster(typeof(StringBuilderExhauster))]
    [XmlSubject(typeof(XmlObject2), true)]
    public partial class XmlSerializerDeserializerTwoAttributes
    {
    }

    [XmlFeatures(XmlFeature.CData | XmlFeature.CharGuard)]
    [XmlExhauster(typeof(StringBuilderExhauster))]
    [XmlSubject(typeof(XmlObject2), true)]
    public partial class XmlSerializerDeserializerOrFlags
    {
    }

    /// <summary>
    /// Инжектор, по которому видно, звали его или нет. Нужен, чтобы
    /// «строка по-прежнему разбирается через <see cref="IInjector"/>» было
    /// проверено поведением, а не только текстом сгенерированного файла.
    /// </summary>
    public sealed class SuffixInjector : XmlSerDe.Components.Injector.DefaultInjector
    {
        public int StringCalls;

        public new void ParseBody(ReadOnlySpan<char> body, out string result)
        {
            StringCalls++;
            result = XmlTextDecoder.DecodeElementText(body) + "!";
        }
    }

    [XmlInjector(typeof(SuffixInjector))]
    [XmlSubject(typeof(XmlObject2), true)]
    public partial class XmlSerializerDeserializerCustomInjector
    {
    }

    /// <summary>
    /// Markup без CData: строковый член такого хоста обязан по-прежнему идти
    /// через инжектор - обход декодером есть цена именно CDATA.
    /// </summary>
    [XmlFeatures(XmlFeature.Markup)]
    [XmlInjector(typeof(SuffixInjector))]
    [XmlSubject(typeof(XmlObject2), true)]
    public partial class XmlSerializerDeserializerMarkupCustomInjector
    {
    }
}
