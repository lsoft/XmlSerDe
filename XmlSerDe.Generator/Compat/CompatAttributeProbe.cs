#if NETSTANDARD
using System.Xml.Serialization;
using Microsoft.CodeAnalysis;
using XmlSerDe.Generator.Helper;

namespace XmlSerDe.Generator.Compat
{
    /// <summary>
    /// Атрибуты, которые обходчик обязан заметить, чтобы <b>отказаться</b>.
    ///
    /// <see cref="CompatGraphWalker"/> проверяет типы: класс ли, обобщённый ли, есть ли
    /// конструктор. Этого мало. Штатная атрибутная модель умеет вещи, которых генератор
    /// не умеет, и худшие из них - те, что не роняют генерацию: код выходит валидный,
    /// тип отчитывается ускоренным, а документ получается <b>другой</b>. С
    /// <c>[XmlRoot(Namespace=...)]</c> это уже не косметика - штатный сериализатор
    /// такой документ не прочитает вовсе.
    ///
    /// Поэтому здесь собрано ровно то, что генератор молча игнорирует. Живёт это
    /// в compat-половине генератора, а не в общей: родной путь <c>[XmlSubject]</c>
    /// решает про те же атрибуты сам, и решение там другое - у него нет фолбэка,
    /// в который можно уйти.
    /// </summary>
    public static class CompatAttributeProbe
    {
        private const string NamespaceArgument = "Namespace";
        private const string TypeArgument = "Type";
        private const string DataTypeArgument = "DataType";

        /// <summary>
        /// Атрибуты на самом типе.
        /// </summary>
        public static bool TryFindRefusal(INamedTypeSymbol type, out string refusal)
        {
            if (HasNamespace(type, typeof(XmlRootAttribute).FullName)
                || HasNamespace(type, typeof(XmlTypeAttribute).FullName))
            {
                //XmlSerDe пишет пространства имён фиксированными литералами, поэтому
                //объявить чужое ему нечем - документ вышел бы без него, и BCL на
                //чтении не узнал бы собственный корень
                refusal = $"{type.ToGlobalDisplayString()}: XML namespaces are not supported";
                return true;
            }

            foreach (var iface in type.AllInterfaces)
            {
                if (iface.ToFullDisplayString() == "System.Xml.Serialization.IXmlSerializable")
                {
                    //форму документа для такого типа решает он сам, в своих ReadXml/WriteXml,
                    //а генератор обошёл бы его члены и написал совсем другое
                    refusal = $"{type.ToGlobalDisplayString()} implements IXmlSerializable";
                    return true;
                }
            }

            refusal = "";
            return false;
        }

        /// <summary>
        /// Атрибуты на члене. Проверяется до разбора его типа: непонятый атрибут -
        /// это отказ независимо от того, поддержан ли сам тип.
        /// </summary>
        public static bool TryFindRefusal(ISymbol member, out string refusal)
        {
            foreach (var attributeType in new[]
                {
                    typeof(XmlElementAttribute).FullName,
                    typeof(XmlAttributeAttribute).FullName,
                    typeof(XmlArrayAttribute).FullName,
                    typeof(XmlArrayItemAttribute).FullName,
                })
            {
                if (HasNamespace(member, attributeType))
                {
                    refusal = $"{member.Name}: XML namespaces are not supported";
                    return true;
                }
            }

            foreach (var unsupported in new[]
                {
                    typeof(XmlChoiceIdentifierAttribute).FullName,
                    typeof(XmlAnyElementAttribute).FullName,
                    typeof(XmlAnyAttributeAttribute).FullName,
                    typeof(XmlNamespaceDeclarationsAttribute).FullName,
                })
            {
                if (Has(member, unsupported))
                {
                    refusal = $"{member.Name}: {unsupported} is not supported";
                    return true;
                }
            }

            //DataType задаёт лексическую форму значения, а не имя: с ним BCL пишет
            //DateTime как "2020-01-02" вместо полного dateTime, а byte[] - как
            //"01FF" вместо base64 (снято прогоном). Генератор про это свойство
            //не знает и пишет свою форму - документ выходит другой
            foreach (var attributeType in new[]
                {
                    typeof(XmlElementAttribute).FullName,
                    typeof(XmlAttributeAttribute).FullName,
                    typeof(XmlArrayItemAttribute).FullName,
                    typeof(XmlTextAttribute).FullName,
                })
            {
                if (HasDataType(member, attributeType))
                {
                    refusal = $"{member.Name}: explicit DataType is not supported";
                    return true;
                }
            }

            //имя элемента, выбираемое по типу значения: у BCL это несколько
            //[XmlElement] на одном члене либо один с typeof. Генератор берёт первое
            //имя и пишет им всё подряд
            if (IsTypeDriven(member, typeof(XmlElementAttribute).FullName))
            {
                refusal = $"{member.Name}: type-driven XmlElement names are not supported";
                return true;
            }
            if (IsTypeDriven(member, typeof(XmlArrayItemAttribute).FullName))
            {
                refusal = $"{member.Name}: type-driven XmlArrayItem names are not supported";
                return true;
            }

            refusal = "";
            return false;
        }

        private static bool HasNamespace(ISymbol symbol, string attributeFullName)
        {
            foreach (var attribute in symbol.GetAttributes())
            {
                if (attribute.AttributeClass?.ToFullDisplayString() != attributeFullName)
                {
                    continue;
                }

                foreach (var namedArgument in attribute.NamedArguments)
                {
                    if (namedArgument.Key == NamespaceArgument
                        && namedArgument.Value.Value is string ns
                        && ns.Length > 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Перечисление, помеченное <see cref="System.FlagsAttribute"/>.
        ///
        /// Худший случай из всех, что здесь перечислены: генератор для комбинации
        /// значений уходит в <c>ToString()</c>, а тот пишет <c>"Read, Write"</c>.
        /// BCL пишет <c>"Read Write"</c> (список по xsd), и на нашей форме падает
        /// «Instance validation error: 'Read,' is not a valid value» - то есть
        /// документ получается такой, который штатный сериализатор прочитать
        /// не может вовсе. Проверено прогоном в обе стороны.
        ///
        /// Значение флага на этапе сборки неизвестно, поэтому отказ - от типа
        /// целиком, а не от «плохих» значений.
        /// </summary>
        public static bool IsUnsupportedEnum(ITypeSymbol type, out string refusal)
        {
            if (type.TypeKind == TypeKind.Enum && Has(type, "System.FlagsAttribute"))
            {
                refusal = $"{type.ToGlobalDisplayString()} is a [Flags] enum";
                return true;
            }

            refusal = "";
            return false;
        }

        private static bool HasDataType(ISymbol symbol, string attributeFullName)
        {
            foreach (var attribute in symbol.GetAttributes())
            {
                if (attribute.AttributeClass?.ToFullDisplayString() != attributeFullName)
                {
                    continue;
                }

                foreach (var namedArgument in attribute.NamedArguments)
                {
                    if (namedArgument.Key == DataTypeArgument
                        && namedArgument.Value.Value is string dataType
                        && dataType.Length > 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool Has(ISymbol symbol, string attributeFullName)
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

        /// <summary>
        /// Больше одного такого атрибута либо один, но с типом: и то и другое
        /// означает «имя выбирается по типу значения».
        /// </summary>
        private static bool IsTypeDriven(ISymbol symbol, string attributeFullName)
        {
            var count = 0;

            foreach (var attribute in symbol.GetAttributes())
            {
                if (attribute.AttributeClass?.ToFullDisplayString() != attributeFullName)
                {
                    continue;
                }

                count++;
                if (count > 1)
                {
                    return true;
                }

                foreach (var constructorArgument in attribute.ConstructorArguments)
                {
                    if (constructorArgument.Kind == TypedConstantKind.Type)
                    {
                        return true;
                    }
                }

                foreach (var namedArgument in attribute.NamedArguments)
                {
                    if (namedArgument.Key == TypeArgument
                        && namedArgument.Value.Kind == TypedConstantKind.Type
                        && namedArgument.Value.Value is not null)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
#endif
