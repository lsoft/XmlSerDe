//генератор переносит using'и файла-хоста в порождённый файл, поэтому список
//здесь такой же, как у XmlSerializerDeserializer.cs, даже если сам этот файл
//половиной из них не пользуется
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using XmlSerDe;
using XmlSerDe.Internal;
using XmlSerDe.Tests.Complex.Subject;

namespace XmlSerDe.Tests.Complex
{
    //Лестница фич на одном и том же графе типов и одном exhauster'е: между
    //хостами отличается ровно набор флагов, поэтому разница во времени - это
    //цена сгенерированного кода, а не другой формы документа.
    //
    //Фабрики (XmlFactory) здесь намеренно нет: кэшированный InfoContainer -
    //общее изменяемое состояние ComplexFixture, и в замере он смешивал бы
    //стоимость фич со стоимостью переиспользования графа.
    //
    //Замеряется этой лестницей FeatureCostProbe (--feature-cost).

    [XmlExhauster(typeof(StringBuilderExhauster))]
    [XmlSubject(typeof(SerializeKeyValue), false)]
    [XmlSubject(typeof(PerformanceTime), false)]
    [XmlSubject(typeof(InfoContainer), true)]
    [XmlSubject(typeof(BaseInfo), false)]
    public partial class LadderDefault
    {
    }

    #region по одной фиче

    [XmlFeatures(XmlFeature.Markup)]
    [XmlExhauster(typeof(StringBuilderExhauster))]
    [XmlSubject(typeof(SerializeKeyValue), false)]
    [XmlSubject(typeof(PerformanceTime), false)]
    [XmlSubject(typeof(InfoContainer), true)]
    [XmlSubject(typeof(BaseInfo), false)]
    public partial class LadderOnlyMarkup
    {
    }

    [XmlFeatures(XmlFeature.CData)]
    [XmlExhauster(typeof(StringBuilderExhauster))]
    [XmlSubject(typeof(SerializeKeyValue), false)]
    [XmlSubject(typeof(PerformanceTime), false)]
    [XmlSubject(typeof(InfoContainer), true)]
    [XmlSubject(typeof(BaseInfo), false)]
    public partial class LadderOnlyCData
    {
    }

    [XmlFeatures(XmlFeature.FlexibleXsiPrefix)]
    [XmlExhauster(typeof(StringBuilderExhauster))]
    [XmlSubject(typeof(SerializeKeyValue), false)]
    [XmlSubject(typeof(PerformanceTime), false)]
    [XmlSubject(typeof(InfoContainer), true)]
    [XmlSubject(typeof(BaseInfo), false)]
    public partial class LadderOnlyFlexibleXsi
    {
    }

    [XmlFeatures(XmlFeature.CharGuard)]
    [XmlExhauster(typeof(StringBuilderExhauster))]
    [XmlSubject(typeof(SerializeKeyValue), false)]
    [XmlSubject(typeof(PerformanceTime), false)]
    [XmlSubject(typeof(InfoContainer), true)]
    [XmlSubject(typeof(BaseInfo), false)]
    public partial class LadderOnlyCharGuard
    {
    }

    #endregion

    #region накопительно, в порядке объявления XmlFeature

    //шаг 1 накопительной лестницы - это LadderOnlyMarkup,
    //шаг 2 - LadderOnlyCData (CData включает в себя Markup)

    [XmlFeatures(
        XmlFeature.CData
        | XmlFeature.FlexibleXsiPrefix
        )]
    [XmlExhauster(typeof(StringBuilderExhauster))]
    [XmlSubject(typeof(SerializeKeyValue), false)]
    [XmlSubject(typeof(PerformanceTime), false)]
    [XmlSubject(typeof(InfoContainer), true)]
    [XmlSubject(typeof(BaseInfo), false)]
    public partial class LadderPlusFlexibleXsi
    {
    }

    [XmlFeatures(XmlFeature.SystemXmlCompatible)]
    [XmlExhauster(typeof(StringBuilderExhauster))]
    [XmlSubject(typeof(SerializeKeyValue), false)]
    [XmlSubject(typeof(PerformanceTime), false)]
    [XmlSubject(typeof(InfoContainer), true)]
    [XmlSubject(typeof(BaseInfo), false)]
    public partial class LadderPlusCharGuard
    {
    }

    #endregion
}
