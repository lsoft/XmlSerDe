#if NETSTANDARD
using System;
using Microsoft.CodeAnalysis;

namespace XmlSerDe.Generator.Producer
{
    internal readonly struct Builtin
    {
        public readonly INamedTypeSymbol Symbol;
        public readonly string XmlTypeName;
        public readonly bool IsNeedToGuardWhenEncoded;

        public Builtin(
            INamedTypeSymbol symbol,
            string xmlTypeName,
            bool isNeedToGuardWhenEncoded
            )
        {
            Symbol = symbol;
            XmlTypeName = xmlTypeName;
            IsNeedToGuardWhenEncoded = isNeedToGuardWhenEncoded;
        }

    }
}
#endif
