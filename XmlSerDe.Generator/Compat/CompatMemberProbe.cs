#if NETSTANDARD
using System.Collections.Generic;
using System.Xml.Serialization;
using Microsoft.CodeAnalysis;
using XmlSerDe.Generator.Helper;
using XmlSerDe.Generator.Producer;

namespace XmlSerDe.Generator.Compat
{
    /// <summary>
    /// Состав членов по правилам System.Xml.Serialization - и сверка его с нашим.
    ///
    /// <see cref="CompatAttributeProbe"/> закрывает случай «атрибут, который генератор
    /// молча игнорирует». Здесь закрывается случай хуже: <b>член, который генератор
    /// молча не видит</b>. Пропуск члена и отказ от типа были независимыми
    /// механизмами - <see cref="CompatGraphWalker"/> перебирал уже отобранные члены,
    /// и то, что отбор кого-то выбросил, до отказа просто не доходило. Итог тот же
    /// самый, ради предотвращения которого всё это и заведено: код выходит валидный,
    /// тип отчитывается ускоренным, а документ получается другой - причём с потерей
    /// данных в обе стороны (снято прогоном на <c>init</c>-свойстве).
    ///
    /// Поэтому здесь члены перебираются заново, по правилам BCL, и сверяются с тем,
    /// что отдал наш отбор. Расхождение в любую сторону - отказ:
    /// <list type="bullet">
    /// <item>BCL пишет, мы пропускаем - в документе не хватает элемента;</item>
    /// <item>мы пишем, BCL пропускает - в документе лишний элемент;</item>
    /// <item>BCL сам отказывается от типа - ускорять нечего, фолбэк воспроизведёт
    /// его исключение слово в слово.</item>
    /// </list>
    ///
    /// Совпадающие пропуски отказом <b>не</b> являются и перечислены здесь поимённо:
    /// непубличный член, <c>[XmlIgnore]</c>, <c>readonly</c>-поле не-коллекции,
    /// свойство без сеттера типа строки, массива или сложного типа. Каждый снят
    /// прогоном рядом со штатным сериализатором, а не взят из README: отказ там,
    /// где BCL тоже молчит, - это потеря ускорения ни за что.
    /// </summary>
    public static class CompatMemberProbe
    {
        private const string EnumerableFullName = "System.Collections.IEnumerable";
        private const string DictionaryFullName = "System.Collections.IDictionary";

        /// <summary>
        /// Что System.Xml.Serialization делает с членом.
        /// </summary>
        private enum BclVerdict
        {
            /// <summary>
            /// Не участвует в обмене вовсе.
            /// </summary>
            Skipped,

            /// <summary>
            /// Пишется и читается.
            /// </summary>
            Serialized,

            /// <summary>
            /// Из-за этого члена штатный сериализатор отказывается от типа целиком -
            /// бросает при построении или при первой же записи.
            /// </summary>
            Refused,
        }

        public static bool TryFindRefusal(
            Compilation compilation,
            INamedTypeSymbol type,
            out string refusal
            )
        {
            refusal = "";

            //имена внутри списка кандидатов уникальны: скрытый через new член
            //разрешается в один, на месте базового
            var ours = new HashSet<string>();
            foreach (var member in ClassSourceProducer.SelectSerializableMembers(compilation, type))
            {
                ours.Add(member.Name);
            }

            foreach (var member in ClassSourceProducer.SelectCandidateMembers(type))
            {
                var verdict = Classify(member, out var because);
                var mine = ours.Contains(member.Name);

                if (verdict == BclVerdict.Refused)
                {
                    refusal =
                        $"{member.Name}: System.Xml.Serialization refuses the whole type because of this member ({because})";
                    return true;
                }
                if (verdict == BclVerdict.Serialized && !mine)
                {
                    refusal =
                        $"{member.Name}: System.Xml.Serialization serializes this member, the generated code skips it ({because})";
                    return true;
                }
                if (verdict == BclVerdict.Skipped && mine)
                {
                    refusal =
                        $"{member.Name}: System.Xml.Serialization skips this member ({because}), the generated code would write it";
                    return true;
                }
            }

            return false;
        }

        private static BclVerdict Classify(
            ISymbol member,
            out string because
            )
        {
            because = "";

            if (HasXmlIgnore(member))
            {
                because = "XmlIgnore";
                return BclVerdict.Skipped;
            }

            //BCL берёт члены через BindingFlags.Public: internal и
            //protected internal он не видит так же, как private (снято прогоном
            //на поле и на свойстве)
            if (member.DeclaredAccessibility != Accessibility.Public)
            {
                because = "the member is not public";
                return BclVerdict.Skipped;
            }

            if (member is IPropertySymbol property)
            {
                return ClassifyProperty(property, out because);
            }
            if (member is IFieldSymbol field)
            {
                return ClassifyField(field, out because);
            }

            because = "not a property and not a field";
            return BclVerdict.Skipped;
        }

        private static BclVerdict ClassifyProperty(
            IPropertySymbol property,
            out string because
            )
        {
            because = "";

            if (property.IsIndexer)
            {
                because = "an indexer";
                return BclVerdict.Skipped;
            }
            if (property.GetMethod is null)
            {
                //писать в документ нечего: прочитать значение неоткуда
                because = "a set-only property";
                return BclVerdict.Skipped;
            }
            if (property.GetMethod.DeclaredAccessibility != Accessibility.Public)
            {
                //сериализатор строится, но на первой же записи падает
                //MethodAccessException (снято прогоном)
                because = "no public getter";
                return BclVerdict.Refused;
            }
            if (property.SetMethod is null)
            {
                return ClassifyWithoutAssignment(property.Type, "a property without a setter", out because);
            }
            if (property.SetMethod.DeclaredAccessibility != Accessibility.Public
                && !property.SetMethod.IsInitOnly)
            {
                //"Cannot deserialize type ... because it contains property ...
                //which has no public setter" - бросается уже из конструктора
                //XmlSerializer (снято прогоном на private/protected/internal set)
                because = "no public setter";
                return BclVerdict.Refused;
            }

            //init-сеттер для рефлексии - обычный сеттер: BCL такое свойство и пишет,
            //и читает (снято прогоном). Присвоить его из сгенерированного кода нельзя:
            //init доступен только в инициализаторе объекта, а генератор пишет new T()
            //и присваивает члены по одному
            because = property.SetMethod.IsInitOnly
                ? "an init-only property: it can be assigned only in an object initializer"
                : "a settable property";
            return BclVerdict.Serialized;
        }

        private static BclVerdict ClassifyField(
            IFieldSymbol field,
            out string because
            )
        {
            because = "";

            if (field.IsConst)
            {
                because = "a constant";
                return BclVerdict.Skipped;
            }
            if (field.IsReadOnly)
            {
                return ClassifyWithoutAssignment(field.Type, "a readonly field", out because);
            }

            because = "a public field";
            return BclVerdict.Serialized;
        }

        /// <summary>
        /// Член, которому нельзя присвоить: <c>readonly</c>-поле или свойство без
        /// сеттера. BCL такой член всё-таки обслуживает, если его тип - коллекция:
        /// разобранные элементы уходят в <c>Add</c> уже созданного экземпляра.
        ///
        /// Что именно считается коллекцией - снято прогоном, а не выведено:
        /// <c>List&lt;T&gt;</c>, <c>Collection&lt;T&gt;</c> и наследник
        /// <c>List&lt;T&gt;</c> наполняются; массив, строка, сложный тип и число -
        /// пропускаются; интерфейс (<c>IList&lt;T&gt;</c>, <c>ICollection&lt;T&gt;</c>)
        /// тоже пропускается; <c>IDictionary</c> роняет построение сериализатора.
        /// </summary>
        private static BclVerdict ClassifyWithoutAssignment(
            ITypeSymbol type,
            string shape,
            out string because
            )
        {
            because = shape;

            if (type is IArrayTypeSymbol)
            {
                //наполнить массив через Add нельзя, а заменить его нечем
                because = shape + " of an array type";
                return BclVerdict.Skipped;
            }
            if (type.SpecialType == SpecialType.System_String)
            {
                //строка перечислима, но коллекцией для обмена не считается
                because = shape + " of type string";
                return BclVerdict.Skipped;
            }
            if (type.TypeKind == TypeKind.Interface)
            {
                because = shape + " of an interface type";
                return BclVerdict.Skipped;
            }
            if (Implements(type, DictionaryFullName))
            {
                because = shape + " implementing IDictionary";
                return BclVerdict.Refused;
            }
            if (Implements(type, EnumerableFullName))
            {
                because = shape + " of a collection type filled through Add;"
                    + " the generated code fills only List<T> this way";
                return BclVerdict.Serialized;
            }

            return BclVerdict.Skipped;
        }

        private static bool Implements(ITypeSymbol type, string interfaceFullName)
        {
            foreach (var iface in type.AllInterfaces)
            {
                if (iface.ToFullDisplayString() == interfaceFullName)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasXmlIgnore(ISymbol member)
        {
            foreach (var attribute in member.GetAttributes())
            {
                if (attribute.AttributeClass?.ToFullDisplayString() == typeof(XmlIgnoreAttribute).FullName)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
#endif
