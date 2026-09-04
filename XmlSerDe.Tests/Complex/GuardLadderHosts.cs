//генератор переносит using'и файла-хоста в порождённый файл, поэтому список
//здесь такой же, как у XmlSerializerDeserializer.cs, даже если сам этот файл
//половиной из них не пользуется
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using XmlSerDe.Common;
using XmlSerDe.Components.Exhauster;
using XmlSerDe.Tests.Complex.Subject;
using XmlSerDe.Tests.Deep.Subject;

namespace XmlSerDe.Tests.Complex
{
    //Лестница стражей: между хостами отличается ровно [XmlGuards], тип, документ
    //и exhauster одни и те же, поэтому разница во времени - это цена
    //включённого флага, а не другой формы документа. Тем же приёмом сделана
    //FeatureLadderHosts для XmlFeature.
    //
    //Документ ни одного нарушения не содержит: меряется цена самой способности
    //отказать, а не цена отказа.
    //
    //Замеряется этой лестницей GuardCostProbe (--guard-cost).

    [XmlExhauster(typeof(StringBuilderExhauster))]
    [XmlSubject(typeof(SerializeKeyValue), false)]
    [XmlSubject(typeof(PerformanceTime), false)]
    [XmlSubject(typeof(InfoContainer), true)]
    [XmlSubject(typeof(BaseInfo), false)]
    public partial class GuardLadderDefault
    {
    }

    #region по одному стражу

    [XmlGuards(XmlGuard.MatchingEndTags)]
    [XmlExhauster(typeof(StringBuilderExhauster))]
    [XmlSubject(typeof(SerializeKeyValue), false)]
    [XmlSubject(typeof(PerformanceTime), false)]
    [XmlSubject(typeof(InfoContainer), true)]
    [XmlSubject(typeof(BaseInfo), false)]
    public partial class GuardLadderOnlyMatchingEndTags
    {
    }

    [XmlGuards(XmlGuard.SingleRoot)]
    [XmlExhauster(typeof(StringBuilderExhauster))]
    [XmlSubject(typeof(SerializeKeyValue), false)]
    [XmlSubject(typeof(PerformanceTime), false)]
    [XmlSubject(typeof(InfoContainer), true)]
    [XmlSubject(typeof(BaseInfo), false)]
    public partial class GuardLadderOnlySingleRoot
    {
    }

    [XmlGuards(XmlGuard.UniqueAttributes)]
    [XmlExhauster(typeof(StringBuilderExhauster))]
    [XmlSubject(typeof(SerializeKeyValue), false)]
    [XmlSubject(typeof(PerformanceTime), false)]
    [XmlSubject(typeof(InfoContainer), true)]
    [XmlSubject(typeof(BaseInfo), false)]
    public partial class GuardLadderOnlyUniqueAttributes
    {
    }

    [XmlGuards(XmlGuard.IllegalChars)]
    [XmlExhauster(typeof(StringBuilderExhauster))]
    [XmlSubject(typeof(SerializeKeyValue), false)]
    [XmlSubject(typeof(PerformanceTime), false)]
    [XmlSubject(typeof(InfoContainer), true)]
    [XmlSubject(typeof(BaseInfo), false)]
    public partial class GuardLadderOnlyIllegalChars
    {
    }

    #endregion

    #region накопительно

    [XmlGuards(XmlGuard.MatchingEndTags | XmlGuard.SingleRoot)]
    [XmlExhauster(typeof(StringBuilderExhauster))]
    [XmlSubject(typeof(SerializeKeyValue), false)]
    [XmlSubject(typeof(PerformanceTime), false)]
    [XmlSubject(typeof(InfoContainer), true)]
    [XmlSubject(typeof(BaseInfo), false)]
    public partial class GuardLadderPlusSingleRoot
    {
    }

    [XmlGuards(XmlGuard.MatchingEndTags | XmlGuard.SingleRoot | XmlGuard.UniqueAttributes)]
    [XmlExhauster(typeof(StringBuilderExhauster))]
    [XmlSubject(typeof(SerializeKeyValue), false)]
    [XmlSubject(typeof(PerformanceTime), false)]
    [XmlSubject(typeof(InfoContainer), true)]
    [XmlSubject(typeof(BaseInfo), false)]
    public partial class GuardLadderPlusUniqueAttributes
    {
    }

    [XmlGuards(XmlGuard.SystemXmlCompatible)]
    [XmlExhauster(typeof(StringBuilderExhauster))]
    [XmlSubject(typeof(SerializeKeyValue), false)]
    [XmlSubject(typeof(PerformanceTime), false)]
    [XmlSubject(typeof(InfoContainer), true)]
    [XmlSubject(typeof(BaseInfo), false)]
    public partial class GuardLadderFull
    {
    }

    #endregion

    #region DEEP: 100 уровней, ни одного атрибута, одна строка

    //MatchingEndTags платит по сравнению имени на каждый уровень, и DEEP - это
    //документ, где таких сравнений больше всего на элемент полезных данных.
    //Остальным стражам здесь платить нечем, и это тоже результат.

    [XmlExhauster(typeof(StringBuilderExhauster))]
    [XmlSubject(typeof(DeepNode), true)]
    public partial class GuardDeepLadderDefault
    {
    }

    [XmlGuards(XmlGuard.MatchingEndTags)]
    [XmlExhauster(typeof(StringBuilderExhauster))]
    [XmlSubject(typeof(DeepNode), true)]
    public partial class GuardDeepLadderOnlyMatchingEndTags
    {
    }

    [XmlGuards(XmlGuard.SystemXmlCompatible)]
    [XmlExhauster(typeof(StringBuilderExhauster))]
    [XmlSubject(typeof(DeepNode), true)]
    public partial class GuardDeepLadderFull
    {
    }

    #endregion
}
