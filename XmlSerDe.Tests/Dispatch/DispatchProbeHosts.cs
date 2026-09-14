//Хосты на одном и том же субъекте и одном и том же наборе флагов: отличается
//только тип экзостера, а значит - только форма вызова в сгенерированном
//Serialize. Всё остальное генератор пишет в них посимвольно одинаково.
//
//генератор переносит using'и файла-хоста в порождённый файл, поэтому список
//здесь такой же, как у других хостов, даже если сам этот файл половиной из них
//не пользуется
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using XmlSerDe;
using XmlSerDe.Internal;

namespace XmlSerDe.Tests.Dispatch
{
    #region вырожденное тело

    [XmlExhauster(typeof(ThinSealedExhauster))]
    [XmlSubject(typeof(XmlObject2), true)]
    public partial class ThinSealedHost
    {
    }

    /// <summary>
    /// На этом хосте генератор обязан выдать XMLSERDE010: зарегистрированный
    /// тип не запечатан.
    /// </summary>
    [XmlExhauster(typeof(ThinOpenExhauster))]
    [XmlSubject(typeof(XmlObject2), true)]
    public partial class ThinOpenHost
    {
    }

    /// <summary>
    /// Тот же экзостер, что у <see cref="ThinOpenHost"/>, но отдельный хост -
    /// значит, отдельный сгенерированный метод и отдельный сайт вызова. Через
    /// него проба гоняет обоих наследников сразу, чтобы сайт стал полиморфным;
    /// в <see cref="ThinOpenHost"/> при этом по-прежнему ходит один тип.
    /// </summary>
    [XmlExhauster(typeof(ThinOpenExhauster))]
    [XmlSubject(typeof(XmlObject2), true)]
    public partial class ThinPolyHost
    {
    }

    /// <summary>
    /// Форма, которую и рекомендуем: наследник запечатан и переопределяет
    /// методы сам.
    /// </summary>
    [XmlExhauster(typeof(ThinSealedOverrideExhauster))]
    [XmlSubject(typeof(XmlObject2), true)]
    public partial class ThinSealedOverrideHost
    {
    }

    #endregion

    #region настоящее тело

    [XmlExhauster(typeof(FatSealedExhauster))]
    [XmlSubject(typeof(XmlObject2), true)]
    public partial class FatSealedHost
    {
    }

    [XmlExhauster(typeof(FatOpenExhauster))]
    [XmlSubject(typeof(XmlObject2), true)]
    public partial class FatOpenHost
    {
    }

    [XmlExhauster(typeof(FatOpenExhauster))]
    [XmlSubject(typeof(XmlObject2), true)]
    public partial class FatPolyHost
    {
    }

    [XmlExhauster(typeof(FatSealedOverrideExhauster))]
    [XmlSubject(typeof(XmlObject2), true)]
    public partial class FatSealedOverrideHost
    {
    }

    #endregion
}
