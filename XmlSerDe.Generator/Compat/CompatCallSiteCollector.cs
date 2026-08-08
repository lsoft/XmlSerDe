#if NETSTANDARD
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using XmlSerDe.Generator.Helper;

namespace XmlSerDe.Generator.Compat
{
    /// <summary>
    /// Точки вызова <c>new XmlSerializer(typeof(T))</c>, где <c>XmlSerializer</c> -
    /// это фасад <see cref="XmlSerDe.Compat"/>, а не штатный.
    ///
    /// Фильтр стоит здесь, в syntax provider'е, и всё, что дороже сравнения строки,
    /// делается уже после того, как узел оказался конструированием объекта с одним
    /// аргументом. Проект, не подключивший фасад, не платит за это ничего: ни одна
    /// точка вызова не пройдёт проверку имени типа.
    /// </summary>
    public static class CompatCallSiteCollector
    {
        public const string FacadeFullName = "XmlSerDe.Compat.XmlSerializer";

        public static bool IsCandidate(SyntaxNode node)
        {
            return
                node is ObjectCreationExpressionSyntax oce
                && oce.ArgumentList is not null
                && oce.ArgumentList.Arguments.Count == 1
                && oce.ArgumentList.Arguments[0].Expression is TypeOfExpressionSyntax
                ;
        }

        /// <summary>
        /// Возвращает тип из <c>typeof</c>, если вызов действительно конструирует фасад.
        /// Литеральный <c>typeof</c> - обязательное условие: <c>new XmlSerializer(t)</c>
        /// с переменной ничего не говорит о типе на этапе сборки, и ускорить такой
        /// вызов можно только по счастливой случайности.
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

            var typeOf = (TypeOfExpressionSyntax)oce.ArgumentList!.Arguments[0].Expression;

            return semanticModel.GetTypeInfo(typeOf.Type).Type as INamedTypeSymbol;
        }
    }
}
#endif
