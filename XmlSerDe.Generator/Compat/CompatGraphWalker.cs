#if NETSTANDARD
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using XmlSerDe.Generator.Helper;
using XmlSerDe.Generator.Producer;

namespace XmlSerDe.Generator.Compat
{
    /// <summary>
    /// Состав типов, который надо сгенерировать ради одного
    /// <c>new XmlSerializer(typeof(T))</c>, - или отказ с причиной.
    ///
    /// Главная дисциплина всей затеи с фасадом: встретив что-нибудь, чего генератор
    /// не умеет, обходчик обязан <b>отказаться от типа целиком</b>, а не выдать почти
    /// правильный код. Отказ ничего не ломает - незарегистрированный тип уходит
    /// штатному <see cref="System.Xml.Serialization.XmlSerializer"/> и работает как
    /// раньше, просто без ускорения. Молча выданный неверный код сломал бы всё.
    ///
    /// Поэтому здесь всё написано «белым списком»: неизвестная конструкция - отказ,
    /// а не попытка догадаться. Про типы решает сам обходчик, про атрибуты -
    /// <see cref="CompatAttributeProbe"/>: атрибут, который генератор молча
    /// игнорирует, опаснее неподдержанного типа - тот роняет генерацию, а этот
    /// выдаёт валидный код и другой документ.
    /// </summary>
    public static class CompatGraphWalker
    {
        /// <summary>
        /// Обходит граф от корня. <paramref name="subjects"/> заполняется только при
        /// успехе; при отказе в <paramref name="refusal"/> лежит причина - она уходит
        /// в информационный диагностик, чтобы «почему у меня не ускорилось» имело ответ.
        /// </summary>
        public static bool TryWalk(
            Compilation compilation,
            INamedTypeSymbol root,
            out List<INamedTypeSymbol> subjects,
            out string refusal
            )
        {
            subjects = new List<INamedTypeSymbol>();
            refusal = "";

            if (!TryAcceptSubject(compilation, root, isRoot: true, out refusal))
            {
                return false;
            }

            var seen = new HashSet<string>();
            var queue = new Queue<INamedTypeSymbol>();

            seen.Add(root.ToGlobalDisplayString());
            queue.Enqueue(root);

            while (queue.Count > 0)
            {
                var subject = queue.Dequeue();
                subjects.Add(subject);

                foreach (var included in subject.GetXmlIncludes())
                {
                    if (!TryAcceptSubject(compilation, included, isRoot: false, out refusal))
                    {
                        return false;
                    }

                    Enqueue(included, seen, queue);
                }

                if (subject.IsAbstract && subject.GetXmlIncludes().Count == 0)
                {
                    //диспетчеризовать в такой тип нечего, и продюсер на нём бросит;
                    //здесь мы это ловим заранее и отказываемся тихо
                    refusal = $"{subject.ToGlobalDisplayString()} is abstract and declares no XmlInclude";
                    return false;
                }

                //сверка состава членов - раньше разбора любого из них: член, который
                //наш отбор выбросил, до цикла ниже не доедет вовсе, и отказ по нему
                //взяться будет неоткуда
                if (CompatMemberProbe.TryFindRefusal(compilation, subject, out refusal))
                {
                    return false;
                }

                foreach (var member in ClassSourceProducer.SelectSerializableMembers(compilation, subject))
                {
                    //атрибуты члена - раньше его типа: непонятый атрибут это отказ
                    //независимо от того, поддержан ли сам тип
                    if (CompatAttributeProbe.TryFindRefusal(member, out refusal))
                    {
                        return false;
                    }

                    var memberType = ClassSourceProducer.ParseMember(compilation, member);

                    if (!TryAcceptMemberType(compilation, member, memberType.Symbol, allowCollection: true, out var next, out refusal))
                    {
                        return false;
                    }

                    if (next is not null)
                    {
                        Enqueue(next, seen, queue);
                    }
                }
            }

            return true;
        }

        private static void Enqueue(
            INamedTypeSymbol type,
            HashSet<string> seen,
            Queue<INamedTypeSymbol> queue
            )
        {
            if (seen.Add(type.ToGlobalDisplayString()))
            {
                queue.Enqueue(type);
            }
        }

        /// <summary>
        /// Тип члена: либо он самодостаточен (встроенный, перечисление, base64),
        /// либо это коллекция, и разбираться надо с её элементом, либо это сложный
        /// тип, и он уходит в очередь отдельным субъектом.
        /// </summary>
        private static bool TryAcceptMemberType(
            Compilation compilation,
            ISymbol member,
            ITypeSymbol type,
            bool allowCollection,
            out INamedTypeSymbol? next,
            out string refusal
            )
        {
            next = null;
            refusal = "";

            if (BuiltinSourceProducer.TryGetBuiltin(compilation, type, out _))
            {
                return true;
            }

            var typeSymbol = new ClassSourceProducer.TypeSymbol(compilation, type);
            if (typeSymbol.IsEnum)
            {
                //проверяется после IsEnum, а не вместо: обычное перечисление
                //поддержано полностью, отказ только по [Flags]
                return !CompatAttributeProbe.IsUnsupportedEnum(typeSymbol.Symbol, out refusal);
            }

            if (type is IArrayTypeSymbol array)
            {
                if (array.ElementType.SpecialType == SpecialType.System_Byte
                    && !member.HasXmlArrayAttribute())
                {
                    //byte[] без явной обёртки - одна лексема base64, а не коллекция
                    return true;
                }

                if (!allowCollection)
                {
                    refusal = $"{member.Name}: nested collections are not supported";
                    return false;
                }

                return TryAcceptMemberType(compilation, member, array.ElementType, allowCollection: false, out next, out refusal);
            }

            if (type is not INamedTypeSymbol named)
            {
                refusal = $"{member.Name}: {type.ToGlobalDisplayString()} is not a named type";
                return false;
            }

            if (named.TypeArguments.Length > 0)
            {
                //единственный обобщённый тип, который умеет генератор. Всё прочее -
                //и Dictionary, и IEnumerable, и Nullable над не-встроенным типом -
                //это отказ, а не повод угадывать
                if (named.TypeArguments.Length != 1
                    || !SymbolEqualityComparer.Default.Equals(named, compilation.List(new[] { named.TypeArguments[0] })))
                {
                    refusal = $"{member.Name}: {named.ToGlobalDisplayString()} is not a supported generic type";
                    return false;
                }

                if (!allowCollection)
                {
                    refusal = $"{member.Name}: nested collections are not supported";
                    return false;
                }

                return TryAcceptMemberType(compilation, member, named.TypeArguments[0], allowCollection: false, out next, out refusal);
            }

            if (!TryAcceptSubject(compilation, named, isRoot: false, out refusal))
            {
                return false;
            }

            next = named;
            return true;
        }

        /// <summary>
        /// Требования к сложному типу: генератор пишет ему <c>new T()</c> и
        /// присваивает члены, поэтому классом и с доступным конструктором без
        /// параметров он быть обязан. Абстрактному конструктор не нужен - в него
        /// диспетчеризуют по xsi:type, а не создают напрямую.
        /// </summary>
        private static bool TryAcceptSubject(
            Compilation compilation,
            INamedTypeSymbol type,
            bool isRoot,
            out string refusal
            )
        {
            refusal = "";

            if (type.TypeKind != TypeKind.Class)
            {
                refusal = $"{type.ToGlobalDisplayString()} is not a class";
                return false;
            }
            if (type.TypeArguments.Length > 0)
            {
                refusal = $"{type.ToGlobalDisplayString()} is generic";
                return false;
            }
            if (type.SpecialType == SpecialType.System_Object || type.SpecialType == SpecialType.System_String)
            {
                refusal = $"{type.ToGlobalDisplayString()} has no members to bind";
                return false;
            }
            if (isRoot && type.IsAbstract)
            {
                //корень создаётся по имени элемента, а не по xsi:type: диспетчеризовать
                //на самом верху документа генератор не умеет
                refusal = $"{type.ToGlobalDisplayString()} is an abstract root";
                return false;
            }
            if (!type.IsAbstract && !HasAccessibleParameterlessConstructor(type))
            {
                refusal = $"{type.ToGlobalDisplayString()} has no accessible parameterless constructor";
                return false;
            }
            if (!type.IsAbstract
                && ClassSourceProducer.TryFindUnassignableRequiredMember(type, out var requiredMember))
            {
                //отказ здесь - не про документ, а про сборку: штатный сериализатор
                //такой тип обслуживает как ни в чём не бывало (проверено прогоном),
                //а сгенерированный new T() не компилируется вовсе (CS9035). Добавление
                //одного global using не имеет права ломать сборку потребителя, поэтому
                //отказ нужен даже там, где сам член прекрасно сериализуется
                refusal =
                    $"{type.ToGlobalDisplayString()}.{requiredMember.Name} is a required member:"
                    + $" the generated code creates the type with new {type.Name}() and cannot satisfy it";
                return false;
            }
            if (CompatAttributeProbe.TryFindRefusal(type, out refusal))
            {
                return false;
            }

            return true;
        }

        private static bool HasAccessibleParameterlessConstructor(INamedTypeSymbol type)
        {
            foreach (var constructor in type.InstanceConstructors)
            {
                if (constructor.Parameters.Length != 0)
                {
                    continue;
                }
                if (constructor.DeclaredAccessibility != Accessibility.Public
                    && constructor.DeclaredAccessibility != Accessibility.Internal)
                {
                    continue;
                }

                return true;
            }

            return false;
        }
    }
}
#endif
