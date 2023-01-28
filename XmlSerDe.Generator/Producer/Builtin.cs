#if NETSTANDARD
using System;
using Microsoft.CodeAnalysis;

namespace XmlSerDe.Generator.Producer
{
    internal readonly struct Builtin
    {
        public readonly string ConverterClause;
        public readonly INamedTypeSymbol Symbol;
        public readonly bool NeedToStringClause;

        public Builtin(
            string parseInvocation,
            INamedTypeSymbol symbol,
            bool needToStringClause
            )
        {
            if (parseInvocation is null)
            {
                throw new ArgumentNullException(nameof(parseInvocation));
            }

            ConverterClause = parseInvocation;
            Symbol = symbol;
            NeedToStringClause = needToStringClause;
        }
    }
}
#endif
