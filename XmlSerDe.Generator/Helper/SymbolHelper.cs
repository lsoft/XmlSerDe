#if NETSTANDARD
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace XmlSerDe.Generator.Helper
{
    internal static class SymbolHelper
    {
        /// <summary>
        /// Имя типа в форме метаданных: <c>Ns.Outer+Inner</c>, у обобщённых - с
        /// арностью (<c>Ns.Pair`2</c>). Это то единственное, что генератор проносит
        /// через инкрементальный конвейер вместо самого символа: строка ничего
        /// не держит живым и сравнивается по значению, а разрешить её обратно
        /// в символ можно у любой компиляции.
        /// </summary>
        public static string ToMetadataName(this INamedTypeSymbol type)
        {
            var nesting = new Stack<INamedTypeSymbol>();
            for (var current = type.OriginalDefinition; current is not null; current = current.ContainingType)
            {
                nesting.Push(current);
            }

            var sb = new StringBuilder();

            var ns = nesting.Peek().ContainingNamespace;
            if (ns is not null && !ns.IsGlobalNamespace)
            {
                sb.Append(ns.ToDisplayString());
                sb.Append('.');
            }

            var first = true;
            foreach (var part in nesting)
            {
                if (!first)
                {
                    sb.Append('+');
                }

                sb.Append(part.MetadataName);
                first = false;
            }

            return sb.ToString();
        }

        public static bool IsPartial(this ITypeSymbol _toType)
        {
            if (_toType.DeclaringSyntaxReferences.Length > 1)
            {
                //it's partial!
                return true;
            }
            else
            {
                var syntax = _toType.DeclaringSyntaxReferences[0].GetSyntax();
                if (syntax is ClassDeclarationSyntax cds)
                {
                    if (cds.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static string GetReturnModifiers(this IMethodSymbol methodSymbol)
        {
            if (methodSymbol.ReturnsByRefReadonly)
            {
                return "ref readonly ";
            }
            if (methodSymbol.ReturnsByRef)
            {
                return "ref ";
            }

            return string.Empty;
        }

        public static bool IsByRef(this IParameterSymbol s)
        {
            return s.RefKind == RefKind.Ref || s.RefKind == RefKind.RefReadOnly;
        }


        public static bool IsOut(this IParameterSymbol s)
        {
            return s.RefKind == RefKind.Out;
        }
    }
}
#endif
