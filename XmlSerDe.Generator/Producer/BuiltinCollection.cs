#if NETSTANDARD
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace XmlSerDe.Generator.Producer
{
    internal readonly struct BuiltinCollection
    {
        public readonly IReadOnlyList<Builtin> Builtins;

        public BuiltinCollection(List<Builtin> builtins)
        {
            if (builtins is null)
            {
                throw new ArgumentNullException(nameof(builtins));
            }

            Builtins = builtins;
        }

        public bool Contains(INamedTypeSymbol target)
        {
            if (target is null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            return Builtins.Any(b => SymbolEqualityComparer.Default.Equals(b.Symbol, target));
        }
    }
}
#endif
