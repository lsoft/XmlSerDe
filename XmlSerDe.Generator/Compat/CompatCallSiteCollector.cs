#if NETSTANDARD
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using XmlSerDe.Generator.Helper;

namespace XmlSerDe.Generator.Compat
{
    /// <summary>
    /// Точки вызова, в которых назван тип, обслуживаемый фасадом
    /// <see cref="XmlSerDe.Compat"/>, а не штатным сериализатором. Их три формы:
    ///
    /// <code>
    /// new XmlSerializer(typeof(T))
    /// XmlSerializer.FromTypes(new[] { typeof(T1), typeof(T2), })
    /// factory.CreateSerializer(typeof(T))   // XmlSerDe.Compat.XmlSerializerFactory
    /// </code>
    ///
    /// Фильтр стоит здесь, в syntax provider'е, и всё, что дороже сравнения строки,
    /// делается уже после того, как узел оказался нужной формой. Проект, не
    /// подключивший фасад, не платит за это ничего: ни одна точка вызова не пройдёт
    /// проверку имени типа.
    ///
    /// Общее правило для всех трёх форм: тип обязан быть назван литеральным
    /// <c>typeof</c>. <c>new XmlSerializer(t)</c> с переменной на этапе сборки
    /// не говорит ничего, и ускорить такой вызов можно только по счастливой
    /// случайности.
    ///
    /// Добавочные аргументы (<c>defaultNamespace</c>, <c>extraTypes</c>,
    /// <see cref="System.Xml.Serialization.XmlAttributeOverrides"/>,
    /// <see cref="System.Xml.Serialization.XmlRootAttribute"/>) на решение здесь не
    /// влияют, хотя ускорить вызов с непустым добавочным аргументом фасад и не станет.
    /// Знать об этом на этапе сборки нечем - аргумент бывает переменной, - а решает
    /// в итоге рантайм: тип регистрируется, а конструктор смотрит, годятся ли
    /// аргументы. Лишняя регистрация стоит немного сгенерированного кода и ни одного
    /// такта; несделанная стоила бы ускорения там, где оно было возможно.
    /// </summary>
    public static class CompatCallSiteCollector
    {
        public const string FacadeFullName = "XmlSerDe.Compat.XmlSerializer";
        public const string FactoryFullName = "XmlSerDe.Compat.XmlSerializerFactory";

        public const string FromTypesMethodName = "FromTypes";
        public const string CreateSerializerMethodName = "CreateSerializer";

        public static bool IsCandidate(SyntaxNode node)
        {
            if (node is ObjectCreationExpressionSyntax oce)
            {
                return
                    oce.ArgumentList is not null
                    && oce.ArgumentList.Arguments.Count >= 1
                    && oce.ArgumentList.Arguments[0].Expression is TypeOfExpressionSyntax
                    ;
            }

            if (node is InvocationExpressionSyntax ie)
            {
                var name = GetSimpleName(ie.Expression);

                return
                    (name == FromTypesMethodName || name == CreateSerializerMethodName)
                    && ie.ArgumentList.Arguments.Count >= 1
                    ;
            }

            return false;
        }

        /// <summary>
        /// Типы, названные в точке вызова. Их больше одного только у
        /// <c>FromTypes</c>; пустой список означает «это не наша точка вызова
        /// либо тип назван не литерально».
        /// </summary>
        public static List<INamedTypeSymbol> CollectRootTypes(
            SemanticModel semanticModel,
            SyntaxNode node
            )
        {
            var result = new List<INamedTypeSymbol>();

            if (node is ObjectCreationExpressionSyntax oce)
            {
                var single = TryGetRootType(semanticModel, oce);
                if (single is not null)
                {
                    result.Add(single);
                }

                return result;
            }

            if (node is InvocationExpressionSyntax ie)
            {
                CollectFromInvocation(semanticModel, ie, result);
            }

            return result;
        }

        /// <summary>
        /// Возвращает тип из <c>typeof</c>, если вызов действительно конструирует фасад.
        /// </summary>
        public static INamedTypeSymbol? TryGetRootType(
            SemanticModel semanticModel,
            ObjectCreationExpressionSyntax oce
            )
        {
            if (!IsCandidate(oce))
            {
                return null;
            }

            if (semanticModel.GetSymbolInfo(oce).Symbol is not IMethodSymbol constructor)
            {
                return null;
            }
            if (constructor.ContainingType?.ToFullDisplayString() != FacadeFullName)
            {
                return null;
            }

            return TryGetTypeOfArgument(semanticModel, oce.ArgumentList!.Arguments[0].Expression);
        }

        private static void CollectFromInvocation(
            SemanticModel semanticModel,
            InvocationExpressionSyntax ie,
            List<INamedTypeSymbol> result
            )
        {
            if (semanticModel.GetSymbolInfo(ie).Symbol is not IMethodSymbol method)
            {
                return;
            }

            var containing = method.ContainingType?.ToFullDisplayString();
            var firstArgument = ie.ArgumentList.Arguments[0].Expression;

            if (method.Name == FromTypesMethodName && containing == FacadeFullName)
            {
                //FromTypes принимает массив, и польза от него ровно в том, что типов
                //в нём несколько. Литерально названы бывают не все - берём те,
                //что названы: типы друг от друга не зависят
                foreach (var element in GetArrayElements(firstArgument))
                {
                    var type = TryGetTypeOfArgument(semanticModel, element);
                    if (type is not null)
                    {
                        result.Add(type);
                    }
                }

                return;
            }

            if (method.Name == CreateSerializerMethodName && containing == FactoryFullName)
            {
                var type = TryGetTypeOfArgument(semanticModel, firstArgument);
                if (type is not null)
                {
                    result.Add(type);
                }
            }
        }

        /// <summary>
        /// Элементы массива, записанного прямо в аргументе: <c>new[] { ... }</c>
        /// либо <c>new Type[] { ... }</c>. Массив, приехавший переменной, на этапе
        /// сборки не раскрывается - как и <c>typeof</c> в переменной.
        /// </summary>
        private static IEnumerable<ExpressionSyntax> GetArrayElements(ExpressionSyntax expression)
        {
            InitializerExpressionSyntax? initializer = expression switch
            {
                ImplicitArrayCreationExpressionSyntax implicitArray => implicitArray.Initializer,
                ArrayCreationExpressionSyntax array => array.Initializer,
                _ => null,
            };

            if (initializer is null)
            {
                yield break;
            }

            foreach (var element in initializer.Expressions)
            {
                yield return element;
            }
        }

        private static INamedTypeSymbol? TryGetTypeOfArgument(
            SemanticModel semanticModel,
            ExpressionSyntax expression
            )
        {
            if (expression is not TypeOfExpressionSyntax typeOf)
            {
                return null;
            }

            return semanticModel.GetTypeInfo(typeOf.Type).Type as INamedTypeSymbol;
        }

        private static string? GetSimpleName(ExpressionSyntax expression)
        {
            return expression switch
            {
                MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
                IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
                MemberBindingExpressionSyntax binding => binding.Name.Identifier.ValueText,
                _ => null,
            };
        }
    }
}
#endif
