#if NETSTANDARD
using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace XmlSerDe.Generator.Producer
{
    internal readonly struct SerializationSubjectInfo
    {
        public readonly INamedTypeSymbol Subject;
        public readonly bool IsRoot;
        public readonly List<INamedTypeSymbol> Derived;
        public readonly string? FactoryInvocation;
        public readonly string? ParserInvocation;

        public SerializationSubjectInfo(
            INamedTypeSymbol subject,
            bool isRoot
            )
        {
            if (subject is null)
            {
                throw new ArgumentNullException(nameof(subject));
            }

            Subject = subject;
            IsRoot = isRoot;

            Derived = new List<INamedTypeSymbol>();
            FactoryInvocation = null;
            ParserInvocation = null;
        }

        private SerializationSubjectInfo(
            INamedTypeSymbol subject,
            bool isRoot,
            List<INamedTypeSymbol> derived,
            string? factoryInvocation,
            string? parserInvocation
            )
        {
            Subject = subject;
            IsRoot = isRoot;
            Derived = derived;
            FactoryInvocation = factoryInvocation;
            ParserInvocation = parserInvocation;
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
                IsRoot,
                Derived,
                factoryInvocation,
                ParserInvocation
                );
        }

        public SerializationSubjectInfo WithParserInvocation(string parserInvocation)
        {
            return new SerializationSubjectInfo(
                Subject,
                IsRoot,
                Derived,
                FactoryInvocation,
                parserInvocation
                );
        }
    }
}
#endif
