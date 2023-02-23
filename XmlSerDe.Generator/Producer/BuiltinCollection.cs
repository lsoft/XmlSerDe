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

        public bool TryGet(ITypeSymbol target, out Builtin rbuiltin)
        {
            if (target is null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            foreach(var builtin in Builtins)
            {
                if (SymbolEqualityComparer.Default.Equals(builtin.Symbol, target))
                {
                    rbuiltin = builtin;
                    return true;
                }
            }

            rbuiltin = default;
            return false;
        }
    }
}
#endif
