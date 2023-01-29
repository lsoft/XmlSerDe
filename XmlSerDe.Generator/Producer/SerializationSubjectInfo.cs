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
        public readonly string? FactoryInvocation;

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
            FactoryInvocation = null;
        }

        private SerializationSubjectInfo(
            INamedTypeSymbol subject,
            List<INamedTypeSymbol> derived,
            string? factoryInvocation
            )
        {
            Subject = subject;
            Derived = derived;
            FactoryInvocation = factoryInvocation;
        }

        public void AddDerived(INamedTypeSymbol derived)
        {
            if (derived is null)
            {
                throw new ArgumentNullException(nameof(derived));
            }

            Derived.Add(derived);
        }

        public SerializationSubjectInfo WithFactoryInvocation(string factoryInvocation)
        {
            return new SerializationSubjectInfo(
                Subject,
                Derived,
                factoryInvocation
                );
        }
    }
}
#endif
