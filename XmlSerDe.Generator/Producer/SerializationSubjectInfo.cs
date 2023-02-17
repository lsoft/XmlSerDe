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
        public readonly List<INamedTypeSymbol> Deriveds;
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

            Deriveds = new List<INamedTypeSymbol>();
            FactoryInvocation = null;
            ParserInvocation = null;
        }

        private SerializationSubjectInfo(
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

        public SerializationSubjectInfo WithFactoryInvocation(string factoryInvocation)
        {
            return new SerializationSubjectInfo(
                Subject,
                IsRoot,
                Deriveds,
                factoryInvocation,
                ParserInvocation
                );
        }

        public SerializationSubjectInfo WithParserInvocation(string parserInvocation)
        {
            return new SerializationSubjectInfo(
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
