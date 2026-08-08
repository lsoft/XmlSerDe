#if NETSTANDARD
using System.ComponentModel;
using System.Xml.Serialization;
using Microsoft.CodeAnalysis;

namespace XmlSerDe.Generator.Helper
{
    /// <summary>
    /// Два штатных способа <see cref="System.Xml.Serialization"/> сказать
    /// «этого члена в документе быть не должно». В отличие от имён
    /// (<see cref="XmlNameHelper"/>) оба меняют не написание, а сам состав
    /// документа, поэтому оба относятся к записи и почти не относятся к чтению.
    ///
    /// Что именно делает BCL, здесь не выведено из документации, а снято
    /// прогоном самого <c>XmlSerializer</c> по типу со всеми этими случаями:
    /// формулировки в MSDN оставляют слишком много места для догадок, а цена
    /// догадки - молчаливое расхождение формата.
    /// </summary>
    internal static class XmlPresenceHelper
    {
        private const string DefaultValueAttributeFullName = "System.ComponentModel.DefaultValueAttribute";

        /// <summary>
        /// Спутник <c>XxxSpecified</c>: публичный <see cref="bool"/> с именем члена
        /// плюс «Specified». Если он есть, член пишется только когда спутник
        /// <c>true</c>, а на чтении спутник взводится, как только элемент встретился.
        ///
        /// <see cref="XmlIgnoreAttribute"/> на самом спутнике на это не влияет
        /// (проверено): он решает только, попадёт ли в документ сам флаг. Поэтому
        /// спутник ищется по имени и типу, а не по атрибутам.
        /// </summary>
        public static ISymbol? GetSpecifiedCompanion(this ISymbol member)
        {
            var containingType = member.ContainingType;
            if (containingType is null)
            {
                return null;
            }

            foreach (var candidate in containingType.GetMembers(member.Name + "Specified"))
            {
                if (candidate.IsStatic)
                {
                    continue;
                }
                if (candidate.DeclaredAccessibility != Accessibility.Public)
                {
                    continue;
                }

                if (candidate is IPropertySymbol property)
                {
                    if (property.GetMethod is null || property.SetMethod is null)
                    {
                        continue;
                    }
                    if (property.Type.SpecialType != SpecialType.System_Boolean)
                    {
                        continue;
                    }

                    return candidate;
                }

                if (candidate is IFieldSymbol field)
                {
                    if (field.IsReadOnly || field.IsConst)
                    {
                        continue;
                    }
                    if (field.Type.SpecialType != SpecialType.System_Boolean)
                    {
                        continue;
                    }

                    return candidate;
                }
            }

            return null;
        }

        /// <summary>
        /// <see cref="DefaultValueAttribute"/>: член, равный этому значению,
        /// в документ не попадает.
        ///
        /// Только запись. На чтении BCL значение по умолчанию <b>не</b> восстанавливает
        /// (проверено: отсутствующий элемент оставляет член равным <c>default(T)</c>,
        /// а не значению из атрибута), и повторять это здесь было бы улучшением,
        /// которое разошлось бы с оригиналом.
        ///
        /// Перегрузка <c>DefaultValueAttribute(Type, string)</c> намеренно
        /// пропускается: она разбирает строку через <c>TypeConverter</c> в рантайме,
        /// и на этапе сборки узнать её результат нечем.
        /// </summary>
        public static bool TryGetDefaultValue(this ISymbol member, out TypedConstant value)
        {
            foreach (var attribute in member.GetAttributes())
            {
                if (attribute.AttributeClass is null)
                {
                    continue;
                }
                if (attribute.AttributeClass.ToFullDisplayString() != DefaultValueAttributeFullName)
                {
                    continue;
                }
                if (attribute.ConstructorArguments.Length != 1)
                {
                    continue;
                }

                value = attribute.ConstructorArguments[0];
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>
        /// Значение из атрибута в виде литерала C#. Перечисление приводится явно:
        /// в атрибуте оно лежит уже числом, а сравнивать с членом надо в его
        /// собственном типе.
        /// </summary>
        public static string ToLiteral(this TypedConstant constant)
        {
            if (constant.IsNull)
            {
                return "null";
            }

            if (constant.Kind == TypedConstantKind.Enum && constant.Type is not null)
            {
                return $"(({constant.Type.ToGlobalDisplayString()}){constant.Value})";
            }

            return Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatPrimitive(
                constant.Value!,
                quoteStrings: true,
                useHexadecimalNumbers: false
                );
        }
    }
}
#endif
