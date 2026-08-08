#if NETSTANDARD
using System.Collections.Generic;
using System.Xml.Serialization;
using Microsoft.CodeAnalysis;

namespace XmlSerDe.Generator.Helper
{
    /// <summary>
    /// Имена, под которыми типы и члены попадают в XML.
    ///
    /// Генератор до этого подставлял имя C# везде, где нужно имя элемента, поэтому
    /// переименовать что-либо было нельзя вовсе. Здесь собрано единственное место,
    /// где имя выбирается, и стоит оно ровно ничего: всё считается на этапе сборки
    /// и попадает в сгенерированный код готовым литералом, так что типу без единого
    /// атрибута достаётся тот же самый код, что и раньше.
    ///
    /// Разбираются штатные атрибуты <see cref="System.Xml.Serialization"/>, а не
    /// собственные: имя элемента - ровно то место, где повторять чужой атрибут своим
    /// не было бы никакого смысла.
    /// </summary>
    internal static class XmlNameHelper
    {
        /// <summary>
        /// Имя типа в XML: то, что попадает в xsi:type и в элемент коллекции.
        /// На корень влияет тоже, но <see cref="XmlRootAttribute"/> его перебивает.
        /// </summary>
        public static string GetXmlTypeName(this ITypeSymbol type)
        {
            return
                FindName(type, typeof(XmlTypeAttribute).FullName, nameof(XmlTypeAttribute.TypeName))
                ?? type.Name;
        }

        /// <summary>
        /// Имя корневого элемента. <see cref="XmlRootAttribute"/> действует только на
        /// корень: тот же тип внутри чужого документа продолжает называться своим
        /// <see cref="GetXmlTypeName"/>.
        /// </summary>
        public static string GetXmlRootName(this ITypeSymbol type)
        {
            return
                FindName(type, typeof(XmlRootAttribute).FullName, nameof(XmlRootAttribute.ElementName))
                ?? type.GetXmlTypeName();
        }

        /// <summary>
        /// Имя элемента для обычного (не коллекционного) члена.
        /// </summary>
        public static string GetXmlElementName(this ISymbol member)
        {
            return
                FindName(member, typeof(XmlElementAttribute).FullName, nameof(XmlElementAttribute.ElementName))
                ?? member.Name;
        }

        /// <summary>
        /// Имя обёртки коллекции. У коллекции своё имя задаёт
        /// <see cref="XmlArrayAttribute"/>, а не <see cref="XmlElementAttribute"/>:
        /// последний означает совсем другую форму - элементы без обёртки, - и она
        /// не поддержана (см. <see cref="HasXmlElementAttribute"/>).
        /// </summary>
        public static string GetXmlArrayName(this ISymbol member)
        {
            return
                FindName(member, typeof(XmlArrayAttribute).FullName, nameof(XmlArrayAttribute.ElementName))
                ?? member.Name;
        }

        /// <summary>
        /// Имя элемента коллекции, если оно задано явно. Иначе - null, и элемент
        /// называется по своему типу.
        /// </summary>
        public static string? GetXmlArrayItemName(this ISymbol member)
        {
            return FindName(member, typeof(XmlArrayItemAttribute).FullName, nameof(XmlArrayItemAttribute.ElementName));
        }

        /// <summary>
        /// Есть ли на члене <see cref="XmlElementAttribute"/> в любом виде.
        /// На коллекции он задаёт форму без обёртки, которой генератор не умеет,
        /// и промолчать здесь значило бы выдать документ, не совпадающий с атрибутом.
        /// </summary>
        public static bool HasXmlElementAttribute(this ISymbol member)
        {
            return HasAttribute(member, typeof(XmlElementAttribute).FullName);
        }

        /// <summary>
        /// Объявлена ли на члене явная обёртка коллекции - <see cref="XmlArrayAttribute"/>
        /// либо <see cref="XmlArrayItemAttribute"/>. Для <c>byte[]</c> это вопрос не про
        /// имя, а про саму форму: без обёртки System.Xml.Serialization пишет его одной
        /// лексемой base64, а с обёрткой возвращает ему вид обычной коллекции.
        /// </summary>
        public static bool HasXmlArrayAttribute(this ISymbol member)
        {
            return
                HasAttribute(member, typeof(XmlArrayAttribute).FullName)
                || HasAttribute(member, typeof(XmlArrayItemAttribute).FullName)
                ;
        }

        /// <summary>
        /// Имя члена перечисления в XML.
        /// </summary>
        public static string GetXmlEnumName(this IFieldSymbol field)
        {
            return
                FindName(field, typeof(XmlEnumAttribute).FullName, nameof(XmlEnumAttribute.Name))
                ?? field.Name;
        }

        /// <summary>
        /// Явный порядок элемента (<see cref="XmlElementAttribute.Order"/> либо
        /// <see cref="XmlArrayAttribute.Order"/>). -1 - порядок не задан;
        /// именно это значение стоит в самих атрибутах по умолчанию.
        /// </summary>
        public static int GetXmlOrder(this ISymbol member)
        {
            var order = FindOrder(member, typeof(XmlElementAttribute).FullName, nameof(XmlElementAttribute.Order));
            if (order >= 0)
            {
                return order;
            }

            return FindOrder(member, typeof(XmlArrayAttribute).FullName, nameof(XmlArrayAttribute.Order));
        }

        /// <summary>
        /// Наследники, объявленные штатным <see cref="XmlIncludeAttribute"/>.
        /// </summary>
        public static List<INamedTypeSymbol> GetXmlIncludes(this ITypeSymbol type)
        {
            var result = new List<INamedTypeSymbol>();

            foreach (var attribute in type.GetAttributes())
            {
                if (attribute.AttributeClass?.ToFullDisplayString() != typeof(XmlIncludeAttribute).FullName)
                {
                    continue;
                }
                if (attribute.ConstructorArguments.Length == 0)
                {
                    continue;
                }

                var ca0 = attribute.ConstructorArguments[0];
                if (ca0.Kind != TypedConstantKind.Type)
                {
                    continue;
                }
                if (ca0.Value is INamedTypeSymbol included)
                {
                    result.Add(included);
                }
            }

            return result;
        }

        /// <summary>
        /// Имя в этих атрибутах приходит двумя равноправными способами: первым
        /// позиционным аргументом (<c>[XmlRoot("имя")]</c>) и именованным свойством
        /// (<c>[XmlRoot(ElementName = "имя")]</c>). Позиционный аргумент бывает и не
        /// строкой (<c>[XmlElement(typeof(T))]</c>), а именованным задают ещё и Order
        /// без всякого имени, поэтому проверяется и вид аргумента тоже.
        /// </summary>
        internal static string? FindName(
            ISymbol symbol,
            string attributeFullName,
            string namedArgumentName
            )
        {
            foreach (var attribute in symbol.GetAttributes())
            {
                if (attribute.AttributeClass?.ToFullDisplayString() != attributeFullName)
                {
                    continue;
                }

                foreach (var namedArgument in attribute.NamedArguments)
                {
                    if (namedArgument.Key != namedArgumentName)
                    {
                        continue;
                    }
                    if (namedArgument.Value.Value is string named && named.Length > 0)
                    {
                        return named;
                    }
                }

                if (attribute.ConstructorArguments.Length > 0)
                {
                    var ca0 = attribute.ConstructorArguments[0];
                    if (ca0.Kind == TypedConstantKind.Primitive && ca0.Value is string positional && positional.Length > 0)
                    {
                        return positional;
                    }
                }
            }

            return null;
        }

        private static int FindOrder(
            ISymbol symbol,
            string attributeFullName,
            string namedArgumentName
            )
        {
            foreach (var attribute in symbol.GetAttributes())
            {
                if (attribute.AttributeClass?.ToFullDisplayString() != attributeFullName)
                {
                    continue;
                }

                foreach (var namedArgument in attribute.NamedArguments)
                {
                    if (namedArgument.Key == namedArgumentName && namedArgument.Value.Value is int order)
                    {
                        return order;
                    }
                }
            }

            return -1;
        }

        private static bool HasAttribute(
            ISymbol symbol,
            string attributeFullName
            )
        {
            foreach (var attribute in symbol.GetAttributes())
            {
                if (attribute.AttributeClass?.ToFullDisplayString() == attributeFullName)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
#endif
