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
        /// Значение из атрибута в виде литерала C#, годного для сравнения
        /// <c>obj.Member != литерал</c>, - или <c>false</c>, если сравнить их нельзя.
        ///
        /// Раздельный ответ здесь нужен потому, что <see cref="DefaultValueAttribute"/>
        /// принимает <c>object</c> и типу члена ничем не обязан. Три случая, каждый
        /// снят прогоном BCL:
        ///
        /// <list type="number">
        /// <item>значение <b>того же</b> типа - обычное сравнение;</item>
        /// <item>число при члене-перечислении (<c>[DefaultValue(1)]</c> при
        /// <c>IntEnum</c>): BCL приводит число к перечислению и член со значением
        /// <c>Option1</c> в документ не пишет - поэтому здесь явное приведение,
        /// а не отказ;</item>
        /// <item>значение <b>несопоставимого</b> типа (<c>[DefaultValue(true)]</c>
        /// при члене <c>int</c>): у BCL такое умолчание не совпадает никогда,
        /// и член пишется всегда - поэтому <c>false</c>, то есть охраны нет вовсе.</item>
        /// </list>
        ///
        /// Раньше литерал строился без оглядки на тип члена, и случаи 2 и 3 давали
        /// не расхождение формата, а <b>несобираемый</b> код (CS0019); случаи же
        /// <c>NaN</c> и бесконечностей - CS0103, потому что <c>ToString()</c> у них
        /// даёт <c>NaN</c> и <c>Infinity</c>, а это не литералы C#.
        /// </summary>
        public static bool TryGetComparableLiteral(
            this TypedConstant constant,
            Compilation compilation,
            ITypeSymbol memberType,
            out string literal
            )
        {
            literal = "";

            var target = UnwrapNullable(memberType);

            if (constant.IsNull)
            {
                //сравнение с null годится только там, где член вообще может быть null
                if (target.IsValueType && !IsNullable(memberType))
                {
                    return false;
                }

                literal = "null";
                return true;
            }

            if (target is INamedTypeSymbol named && named.EnumUnderlyingType is not null)
            {
                if (constant.Kind != TypedConstantKind.Enum && !IsIntegral(constant.Type))
                {
                    return false;
                }

                //значение в своих скобках: (E)-1 C# читает как вычитание (CS0075),
                //а отрицательная константа у перечисления - обычное дело
                literal = $"(({target.ToGlobalDisplayString()})({constant.Value}))";
                return true;
            }

            if (constant.Kind == TypedConstantKind.Enum || constant.Type is null)
            {
                //перечисление при члене-неперечислении: сравнить нечем
                return false;
            }

            if (!IsComparable(compilation, constant.Type, target))
            {
                return false;
            }

            literal = FormatValue(constant.Value!);
            return true;
        }

        /// <summary>
        /// <c>a != b</c> собирается, если неявное приведение есть хоть в одну
        /// сторону: <c>float</c> с <c>double</c>-константой сравнивается в
        /// <c>double</c>, а <c>decimal</c> с <c>double</c> - никак (CS0019).
        /// </summary>
        private static bool IsComparable(Compilation compilation, ITypeSymbol from, ITypeSymbol to)
        {
            return compilation.ClassifyCommonConversion(from, to).IsImplicit
                || compilation.ClassifyCommonConversion(to, from).IsImplicit;
        }

        /// <summary>
        /// <c>SymbolDisplay.FormatPrimitive</c> зовёт <c>ToString()</c>, а у
        /// <c>NaN</c> и бесконечностей он даёт слова, которых в C# нет
        /// (<c>NaN</c>, <c>Infinity</c>, <c>-Infinity</c>). Их приходится называть
        /// самим - сравнение с ними законно и осмысленно: <c>x != double.NaN</c>
        /// истинно всегда, то есть член пишется всегда, ровно как у BCL.
        /// </summary>
        private static string FormatValue(object value)
        {
            switch (value)
            {
                case double d when double.IsNaN(d):
                    return "double.NaN";
                case double d when double.IsPositiveInfinity(d):
                    return "double.PositiveInfinity";
                case double d when double.IsNegativeInfinity(d):
                    return "double.NegativeInfinity";
                case float f when float.IsNaN(f):
                    return "float.NaN";
                case float f when float.IsPositiveInfinity(f):
                    return "float.PositiveInfinity";
                case float f when float.IsNegativeInfinity(f):
                    return "float.NegativeInfinity";
            }

            return Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatPrimitive(
                value,
                quoteStrings: true,
                useHexadecimalNumbers: false
                );
        }

        private static bool IsNullable(ITypeSymbol type)
        {
            return type is INamedTypeSymbol nts
                && nts.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
        }

        private static ITypeSymbol UnwrapNullable(ITypeSymbol type)
        {
            if (type is INamedTypeSymbol nts
                && nts.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
                && nts.TypeArguments.Length == 1)
            {
                return nts.TypeArguments[0];
            }

            return type;
        }

        private static bool IsIntegral(ITypeSymbol? type)
        {
            switch (type?.SpecialType)
            {
                case SpecialType.System_SByte:
                case SpecialType.System_Byte:
                case SpecialType.System_Int16:
                case SpecialType.System_UInt16:
                case SpecialType.System_Int32:
                case SpecialType.System_UInt32:
                case SpecialType.System_Int64:
                case SpecialType.System_UInt64:
                    return true;
                default:
                    return false;
            }
        }
    }
}
#endif
