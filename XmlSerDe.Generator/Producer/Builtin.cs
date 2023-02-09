#if NETSTANDARD
using System;
using Microsoft.CodeAnalysis;

namespace XmlSerDe.Generator.Producer
{
    internal readonly struct Builtin
    {
        public readonly string ConverterClause;
        public readonly INamedTypeSymbol Symbol;
        public readonly string XmlTypeName;
        public readonly string ToStringClause;
        public readonly bool IsNeedToGuardWhenEncoded;

        public Builtin(
            string parseInvocation,
            INamedTypeSymbol symbol,
            string xmlTypeName,
            string toStringClause,
            bool isNeedToGuardWhenEncoded
            )
        {
            if (parseInvocation is null)
            {
                throw new ArgumentNullException(nameof(parseInvocation));
            }

            ConverterClause = parseInvocation;
            Symbol = symbol;
            XmlTypeName = xmlTypeName;
            ToStringClause = toStringClause;
            IsNeedToGuardWhenEncoded = isNeedToGuardWhenEncoded;
        }

    }
}
#endif
