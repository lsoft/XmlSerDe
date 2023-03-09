#if NETSTANDARD
using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace XmlSerDe.Generator.Producer
{
    public readonly struct SerializationInfoCollection
    {
        public readonly List<INamedTypeSymbol> ExhaustList;
        public readonly List<INamedTypeSymbol> InjectorList;
        public readonly IReadOnlyList<SerializationInfo> Infos;

        public SerializationInfoCollection(
            List<INamedTypeSymbol> exhaustList,
            List<INamedTypeSymbol> injectorList,
            List<SerializationInfo> infos
            )
        {
            if (exhaustList is null)
            {
                throw new ArgumentNullException(nameof(exhaustList));
            }
            if (injectorList is null)
            {
                throw new ArgumentNullException(nameof(injectorList));
            }
            if (infos is null)
            {
                throw new ArgumentNullException(nameof(infos));
            }

            infos.ForEach(i => i.Check());

            ExhaustList = exhaustList;
            InjectorList = injectorList;
            Infos = infos;
        }

        public bool TryGetSubject(
            ITypeSymbol target,
            out SerializationInfo info
            )
        {
            for (int i = 0; i < Infos.Count; i++)
            {
                var ssi = Infos[i];
                if (SymbolEqualityComparer.Default.Equals(ssi.Subject, target))
                {
                    info = ssi;
                    return true;
                }
            }

            info = default;
            return false;
        }
    }
}
#endif
