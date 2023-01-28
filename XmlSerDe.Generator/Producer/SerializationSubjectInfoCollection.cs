#if NETSTANDARD
using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace XmlSerDe.Generator.Producer
{
    internal readonly struct SerializationSubjectInfoCollection
    {
        public readonly IReadOnlyList<SerializationSubjectInfo> Infos;

        public SerializationSubjectInfoCollection(List<SerializationSubjectInfo> infos)
        {
            if (infos is null)
            {
                throw new ArgumentNullException(nameof(infos));
            }

            Infos = infos;
        }

        public bool TryGetSubject(
            INamedTypeSymbol target,
            out SerializationSubjectInfo info
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
