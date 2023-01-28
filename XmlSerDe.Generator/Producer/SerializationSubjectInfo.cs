#if NETSTANDARD
using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace XmlSerDe.Generator.Producer
{
    internal readonly struct SerializationSubjectInfo
    {
        public readonly INamedTypeSymbol Subject;
        public readonly List<INamedTypeSymbol> Derived;

        public SerializationSubjectInfo(
            INamedTypeSymbol subject
            )
        {
            if (subject is null)
            {
                throw new ArgumentNullException(nameof(subject));
            }

            Subject = subject;
            Derived = new List<INamedTypeSymbol>();
        }

       public void AddDerived(INamedTypeSymbol derived)
        {
            if (derived is null)
            {
                throw new ArgumentNullException(nameof(derived));
            }

            Derived.Add(derived);
        }

    }
}
#endif
