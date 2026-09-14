#if NETSTANDARD
using System;
using XmlSerDe;
using XmlSerDe.Internal;

namespace XmlSerDe.Generator.Producer
{
    /// <summary>
    /// Compile-time binding of <see cref="XmlFeature"/> to the primitive names
    /// a host emits. Built once from the OR of <c>[XmlFeatures]</c> (or from
    /// <see cref="XmlFeature.SystemXmlCompatible"/> for compat) and then only
    /// read. Flag tests live here, not in <see cref="ClassSourceProducer"/>
    /// generate methods — see docs/opt-in-xml-features.md §4.5.
    /// </summary>
    public readonly struct HostFeatureBinding
    {
        public readonly XmlFeature Features;

        /// <summary>
        /// <c>XmlScan</c> method the host calls for a tag head.
        /// </summary>
        public readonly string ReadHead;

        /// <summary>
        /// Arguments inserted before <c>cursor</c> for <see cref="ReadHead"/>,
        /// including a trailing comma when non-empty. Empty for the two
        /// specialized methods that take no markup flags.
        /// </summary>
        public readonly string ReadHeadPrefixArgs;

        public readonly string ReadTextBody;
        public readonly string ReadTextBodyPrefixArgs;

        public readonly string SkipBody;
        public readonly string SkipBodyPrefixArgs;

        public readonly string GetPreciseNodeType;
        public readonly string IsNil;
        public readonly string DecodeElementText;

        /// <summary>
        /// <see cref="XmlFeature.CharGuard"/>: строки пишутся через checked
        /// <c>AppendEncoded</c>, иначе через <c>AppendEncodedUnchecked</c>.
        /// Оба варианта одинаково терпимы к null и одинаково пишут прямо
        /// в буфер exhauster'а - отличается только проверка Char production.
        /// </summary>
        public readonly bool CharGuard;

        /// <summary>
        /// Аргументы флагов для <c>BuiltinCodeHelper.CutXmlHead</c>, включая
        /// хвостовую запятую. Пусто у default-хоста: там снимается только
        /// XMLDecl и ведущие пробелы.
        /// </summary>
        public readonly string CutXmlHeadPrefixArgs;

        public bool CData
        {
            get
            {
                return Has(Features, XmlFeature.CData);
            }
        }

        private HostFeatureBinding(
            XmlFeature features,
            string readHead,
            string readHeadPrefixArgs,
            string readTextBody,
            string readTextBodyPrefixArgs,
            string skipBody,
            string skipBodyPrefixArgs,
            string getPreciseNodeType,
            string isNil,
            string decodeElementText,
            bool charGuard,
            string cutXmlHeadPrefixArgs
            )
        {
            Features = features;
            ReadHead = readHead;
            ReadHeadPrefixArgs = readHeadPrefixArgs;
            ReadTextBody = readTextBody;
            ReadTextBodyPrefixArgs = readTextBodyPrefixArgs;
            SkipBody = skipBody;
            SkipBodyPrefixArgs = skipBodyPrefixArgs;
            GetPreciseNodeType = getPreciseNodeType;
            IsNil = isNil;
            DecodeElementText = decodeElementText;
            CharGuard = charGuard;
            CutXmlHeadPrefixArgs = cutXmlHeadPrefixArgs;
        }

        public static HostFeatureBinding From(XmlFeature features)
        {
            //CData включает в себя Markup, поэтому одной проверки хватает на обе
            var markup = Has(features, XmlFeature.Markup);
            var cdata = Has(features, XmlFeature.CData);
            var flexibleXsi = Has(features, XmlFeature.FlexibleXsiPrefix);
            var charGuard = Has(features, XmlFeature.CharGuard);

            string readHead;
            string readHeadPrefixArgs;
            if (markup)
            {
                readHead = nameof(XmlScan.ReadHeadMarkup);
                readHeadPrefixArgs = Bool(cdata) + ", " + Bool(flexibleXsi) + ", ";
            }
            else if (flexibleXsi)
            {
                readHead = nameof(XmlScan.ReadHeadFlexibleXsi);
                readHeadPrefixArgs = "";
            }
            else
            {
                readHead = nameof(XmlScan.ReadHead);
                readHeadPrefixArgs = "";
            }

            string readTextBody;
            string readTextBodyPrefixArgs;
            if (markup)
            {
                readTextBody = nameof(XmlScan.ReadTextBodyMarkup);
                readTextBodyPrefixArgs = Bool(cdata) + ", ";
            }
            else
            {
                readTextBody = nameof(XmlScan.ReadTextBody);
                readTextBodyPrefixArgs = "";
            }

            return new HostFeatureBinding(
                features,
                readHead,
                readHeadPrefixArgs,
                readTextBody,
                readTextBodyPrefixArgs,
                markup ? nameof(XmlScan.SkipBodyMarkup) : nameof(XmlScan.SkipBody),
                markup ? Bool(cdata) + ", " : "",
                flexibleXsi ? nameof(XmlHead.GetPreciseNodeType) : nameof(XmlHead.GetXsiType),
                flexibleXsi ? nameof(XmlHead.IsNil) : nameof(XmlHead.IsXsiNil),
                cdata ? nameof(XmlTextDecoder.DecodeElementTextWithCData) : nameof(XmlTextDecoder.DecodeElementText),
                charGuard,
                markup ? "true, " : ""
                );
        }

        public string ReadHeadInvocation(string cursor, string xmlnsAttributeName, string result)
        {
            return ReadHeadPrefixArgs
                + cursor + ", "
                + xmlnsAttributeName + ", ref "
                + result;
        }

        public string ReadTextBodyInvocation(
            string body,
            string isBodyless,
            string declaredNodeType,
            string text,
            string consumed
            )
        {
            return ReadTextBodyPrefixArgs
                + body + ", "
                + isBodyless + ", "
                + declaredNodeType + ", out var "
                + text + ", out "
                + consumed;
        }

        public string SkipBodyInvocation(string body, string isBodyless)
        {
            return SkipBodyPrefixArgs + body + ", " + isBodyless;
        }

        /// <summary>
        /// Каким методом доставать <c>xsi:type</c>. Ось здесь вторая, помимо
        /// <see cref="XmlFeature.FlexibleXsiPrefix"/>: есть ли у типа наследники.
        ///
        /// Есть - <c>xsi:type</c> по нему диспетчеризуют, атрибут в документе
        /// обычно стоит, и отрицательный фильтр только заставил бы читать
        /// голову дважды. Нет - атрибут не диспетчеризует ничего, он может лишь
        /// совпасть с собственным именем типа или стать ошибкой; в реальных
        /// документах его там не бывает, и фильтр промахивается, то есть
        /// выигрывает. Замер - probe <c>--head-walk</c>, x0.81 против x1.02.
        ///
        /// Наблюдаемое поведение у всех четырёх вариантов одинаковое:
        /// фильтр строг в одну сторону, ложное срабатывание проваливается
        /// в тот же разбор.
        /// </summary>
        public string PreciseNodeTypeAccessor(bool hasDerived)
        {
            if (hasDerived)
            {
                return GetPreciseNodeType;
            }

            return Has(Features, XmlFeature.FlexibleXsiPrefix)
                ? nameof(XmlHead.GetPreciseNodeTypePrefiltered)
                : nameof(XmlHead.GetXsiTypePrefiltered);
        }

        /// <summary>
        /// Оператор, объявляющий <paramref name="varName"/> с разобранным телом
        /// builtin-члена.
        ///
        /// Без <see cref="XmlFeature.CData"/> - по-прежнему через
        /// <see cref="IInjector.ParseBody"/>: пользовательский инжектор для
        /// строки не должен молча перестать вызываться из-за фичи, которую
        /// никто не включал. С <see cref="XmlFeature.CData"/> у
        /// <see cref="IInjector"/> подходящей перегрузки нет, и строка
        /// декодируется напрямую - это единственный случай, когда хост обходит
        /// инжектор (docs/opt-in-xml-features.md §16).
        /// </summary>
        public string ParseBodyStatement(
            string text,
            bool isString,
            string typeGlobalName,
            string varName
            )
        {
            if (isString && CData)
            {
                return "var " + varName + " = global::" + typeof(XmlTextDecoder).FullName
                    + "." + DecodeElementText + "(" + text + ");";
            }

            return "inj." + nameof(IInjector.ParseBody)
                + "(" + text + ", out " + typeGlobalName + " " + varName + ");";
        }

        /// <summary>
        /// Запись текста тела элемента. Обе ветки идут через exhauster, а не
        /// через <c>Append(Encode(value))</c>: экранирование прямо в буфер
        /// не аллоцирует, и null остаётся null'ом, а не
        /// <c>ArgumentNullException</c>. Флаг меняет ровно одно - guard.
        /// </summary>
        public string AppendEncodedStatement(string exh, string value)
        {
            var method = CharGuard
                ? nameof(IExhauster.AppendEncoded)
                : nameof(IExhauster.AppendEncodedUnchecked);

            return exh + "." + method + "(" + value + ");";
        }

        public string AppendAttributeEncodedStatement(string exh, string value)
        {
            var method = CharGuard
                ? nameof(IExhauster.AppendAttributeEncoded)
                : nameof(IExhauster.AppendAttributeEncodedUnchecked);

            return exh + "." + method + "(" + value + ");";
        }

        public string CutXmlHeadInvocation(string xml)
        {
            if (string.IsNullOrEmpty(CutXmlHeadPrefixArgs))
            {
                return xml;
            }

            return CutXmlHeadPrefixArgs + xml;
        }

        /// <summary>
        /// Сравнение с полной маской, а не с нулём: <see cref="XmlFeature.CData"/>
        /// составная (включает <see cref="XmlFeature.Markup"/>), и проверка
        /// "хоть один бит совпал" объявляла бы CData у любого разметочного хоста.
        /// </summary>
        private static bool Has(XmlFeature features, XmlFeature flag)
        {
            return (features & flag) == flag;
        }

        private static string Bool(bool value)
        {
            return value ? "true" : "false";
        }
    }
}
#endif
