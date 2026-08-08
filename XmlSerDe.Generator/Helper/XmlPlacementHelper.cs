#if NETSTANDARD
using System.Xml.Serialization;
using Microsoft.CodeAnalysis;

namespace XmlSerDe.Generator.Helper
{
    /// <summary>
    /// Куда член попадает внутри элемента-владельца.
    /// </summary>
    internal enum XmlPlacement
    {
        /// <summary>
        /// Дочерний элемент со своим тегом - умолчание и единственное, что
        /// генератор умел раньше.
        /// </summary>
        Element = 0,

        /// <summary>
        /// Атрибут головы владельца: <c>&lt;Owner id="7"&gt;</c>.
        /// </summary>
        Attribute = 1,

        /// <summary>
        /// Текст тела владельца, без собственного тега:
        /// <c>&lt;Owner&gt;текст&lt;/Owner&gt;</c>.
        /// </summary>
        Text = 2,
    }

    /// <summary>
    /// Третья ось штатной атрибутной модели <see cref="System.Xml.Serialization"/>:
    /// не как член называется (<see cref="XmlNameHelper"/>) и не попадёт ли он
    /// в документ вовсе (<see cref="XmlPresenceHelper"/>), а в каком месте
    /// элемента-владельца он окажется.
    ///
    /// Ось эта - единственная из трёх, которая меняет форму самого элемента:
    /// голова перестаёт быть одним литералом, а тело может оказаться текстом,
    /// а не набором детей. Поэтому и вынесена отдельно.
    /// </summary>
    internal static class XmlPlacementHelper
    {
        public static XmlPlacement GetXmlPlacement(this ISymbol member)
        {
            foreach (var attribute in member.GetAttributes())
            {
                var fullName = attribute.AttributeClass?.ToFullDisplayString();
                if (fullName == typeof(XmlAttributeAttribute).FullName)
                {
                    return XmlPlacement.Attribute;
                }
                if (fullName == typeof(XmlTextAttribute).FullName)
                {
                    return XmlPlacement.Text;
                }
            }

            return XmlPlacement.Element;
        }

        /// <summary>
        /// Имя атрибута. Задаётся так же, как имена элементов: первым позиционным
        /// аргументом либо именованным <see cref="XmlAttributeAttribute.AttributeName"/>;
        /// без него атрибут называется по имени члена.
        /// </summary>
        public static string GetXmlAttributeName(this ISymbol member)
        {
            return
                XmlNameHelper.FindName(
                    member,
                    typeof(XmlAttributeAttribute).FullName,
                    nameof(XmlAttributeAttribute.AttributeName)
                    )
                ?? member.Name;
        }
    }
}
#endif
