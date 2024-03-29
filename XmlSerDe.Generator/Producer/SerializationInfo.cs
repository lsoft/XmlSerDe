﻿#if NETSTANDARD
using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using XmlSerDe.Common;
using XmlSerDe.Generator.Helper;

namespace XmlSerDe.Generator.Producer
{
    public readonly struct SerializationInfo
    {
        public readonly INamedTypeSymbol Subject;
        public readonly bool IsRoot;
        public readonly List<INamedTypeSymbol> Deriveds;
        public readonly string? FactoryInvocation;

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
        }

        private SerializationInfo(
            INamedTypeSymbol subject,
            bool isRoot,
            List<INamedTypeSymbol> deriveds,
            string? factoryInvocation
            )
        {
            Subject = subject;
            IsRoot = isRoot;
            Deriveds = deriveds;
            FactoryInvocation = factoryInvocation;
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
                factoryInvocation
                );
        }


        internal void Check()
        {
            if (Subject.IsAbstract)
            {
                if (Deriveds.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"{Subject.ToGlobalDisplayString()} must have at least one declared implementation via {nameof(XmlDerivedSubjectAttribute)}"
                        );
                }
            }
        }
    }
}
#endif
