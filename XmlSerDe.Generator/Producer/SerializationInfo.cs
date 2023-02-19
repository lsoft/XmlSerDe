#if NETSTANDARD
using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace XmlSerDe.Generator.Producer
{
    public readonly struct SerializationInfo
    {
        public readonly INamedTypeSymbol Subject;
        public readonly bool IsRoot;
        public readonly List<INamedTypeSymbol> Deriveds;
        public readonly string? FactoryInvocation;
        public readonly string? ParserInvocation;

        public SerializationInfo(
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

            Deriveds = new List<INamedTypeSymbol>();
            FactoryInvocation = null;
            ParserInvocation = null;
        }

        private SerializationInfo(
            INamedTypeSymbol subject,
            bool isRoot,
            List<INamedTypeSymbol> deriveds,
            string? factoryInvocation,
            string? parserInvocation
            )
        {
            Subject = subject;
            IsRoot = isRoot;
            Deriveds = deriveds;
            FactoryInvocation = factoryInvocation;
            ParserInvocation = parserInvocation;
        }

        public void AddDerived(INamedTypeSymbol derived)
        {
            if (derived is null)
            {
                throw new ArgumentNullException(nameof(derived));
            }

            Deriveds.Add(derived);
        }

        public SerializationInfo WithFactoryInvocation(string factoryInvocation)
        {
            return new SerializationInfo(
                Subject,
                IsRoot,
                Deriveds,
                factoryInvocation,
                ParserInvocation
                );
        }

        public SerializationInfo WithParserInvocation(string parserInvocation)
        {
            return new SerializationInfo(
                Subject,
                IsRoot,
                Deriveds,
                FactoryInvocation,
                parserInvocation
                );
        }
    }
}
#endif
