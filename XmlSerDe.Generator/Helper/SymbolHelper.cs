#if NETSTANDARD
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace XmlSerDe.Generator.Helper
{
    internal static class SymbolHelper
    {
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
