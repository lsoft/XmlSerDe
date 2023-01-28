#if NETSTANDARD
using System;
using Microsoft.CodeAnalysis;

namespace XmlSerDe.Generator.Helper
{
    public static class CompilationHelper
    {
        public static INamedTypeSymbol Func(
            this Compilation compilation,
            params ITypeSymbol[] genericParameters
            )
        {
            if (compilation is null)
            {
                throw new ArgumentNullException(nameof(compilation));
            }

            return
                compilation.GetTypeByMetadataName("System.Func`" + genericParameters.Length)!
                    .Construct(genericParameters)
                    ;
        }

        public static INamedTypeSymbol ValueTask(
            this Compilation compilation
            )
        {
            if (compilation is null)
            {
                throw new ArgumentNullException(nameof(compilation));
            }

            return compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask")!;
        }

        public static INamedTypeSymbol ValueTask(
            this Compilation compilation,
            params ITypeSymbol[] genericParameters
            )
        {
            if (compilation is null)
            {
                throw new ArgumentNullException(nameof(compilation));
            }

            return
                compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask`" + genericParameters.Length)!
                    .Construct(genericParameters)
                    ;
        }

        public static INamedTypeSymbol List(
            this Compilation compilation,
            params ITypeSymbol[] genericParameters
            )
        {
            if (compilation is null)
            {
                throw new ArgumentNullException(nameof(compilation));
            }

            return
                compilation.GetTypeByMetadataName("System.Collections.Generic.List`" + genericParameters.Length)!
                    .Construct(genericParameters)
                    ;
        }
        public static INamedTypeSymbol Exception(
            this Compilation compilation
            )
        {
            if (compilation is null)
            {
                throw new ArgumentNullException(nameof(compilation));
            }

            return compilation.GetTypeByMetadataName("System.Exception")!;
        }

        public static INamedTypeSymbol SystemType(
            this Compilation compilation
            )
        {
            if (compilation is null)
            {
                throw new ArgumentNullException(nameof(compilation));
            }

            return compilation.GetTypeByMetadataName("System.Type")!;
        }

        public static INamedTypeSymbol Object(
            this Compilation compilation
            )
        {
            if (compilation is null)
            {
                throw new ArgumentNullException(nameof(compilation));
            }

            return compilation.GetTypeByMetadataName("System.Object")!;
        }

        public static INamedTypeSymbol DateTime(
            this Compilation compilation
            )
        {
            if (compilation is null)
            {
                throw new ArgumentNullException(nameof(compilation));
            }

            return compilation.GetTypeByMetadataName("System.DateTime")!;
        }

        public static INamedTypeSymbol Bool(
            this Compilation compilation
            )
        {
            if (compilation is null)
            {
                throw new ArgumentNullException(nameof(compilation));
            }

            return compilation.GetTypeByMetadataName("System.Boolean")!;
        }

        public static INamedTypeSymbol Void(
            this Compilation compilation
            )
        {
            if (compilation is null)
            {
                throw new ArgumentNullException(nameof(compilation));
            }

            return compilation.GetTypeByMetadataName("System.Void")!;
        }

        public static INamedTypeSymbol String(
            this Compilation compilation
            )
        {
            if (compilation is null)
            {
                throw new ArgumentNullException(nameof(compilation));
            }

            return compilation.GetTypeByMetadataName("System.String")!;
        }

        public static INamedTypeSymbol UInt16(
            this Compilation compilation
            )
        {
            if (compilation is null)
            {
                throw new ArgumentNullException(nameof(compilation));
            }

            return compilation.GetTypeByMetadataName("System.UInt16")!;
        }

        public static INamedTypeSymbol Int16(
            this Compilation compilation
            )
        {
            if (compilation is null)
            {
                throw new ArgumentNullException(nameof(compilation));
            }

            return compilation.GetTypeByMetadataName("System.Int16")!;
        }

        public static INamedTypeSymbol UInt32(
            this Compilation compilation
            )
        {
            if (compilation is null)
            {
                throw new ArgumentNullException(nameof(compilation));
            }

            return compilation.GetTypeByMetadataName("System.UInt32")!;
        }

        public static INamedTypeSymbol Int32(
            this Compilation compilation
            )
        {
            if (compilation is null)
            {
                throw new ArgumentNullException(nameof(compilation));
            }

            return compilation.GetTypeByMetadataName("System.Int32")!;
        }

        public static INamedTypeSymbol UInt64(
            this Compilation compilation
            )
        {
            if (compilation is null)
            {
                throw new ArgumentNullException(nameof(compilation));
            }

            return compilation.GetTypeByMetadataName("System.UInt64")!;
        }

        public static INamedTypeSymbol Int64(
            this Compilation compilation
            )
        {
            if (compilation is null)
            {
                throw new ArgumentNullException(nameof(compilation));
            }

            return compilation.GetTypeByMetadataName("System.Int64")!;
        }

        public static INamedTypeSymbol Byte(
            this Compilation compilation
            )
        {
            if (compilation is null)
            {
                throw new ArgumentNullException(nameof(compilation));
            }

            return compilation.GetTypeByMetadataName("System.Byte")!;
        }

        public static INamedTypeSymbol SByte(
            this Compilation compilation
            )
        {
            if (compilation is null)
            {
                throw new ArgumentNullException(nameof(compilation));
            }

            return compilation.GetTypeByMetadataName("System.SByte")!;
        }

        public static INamedTypeSymbol Decimal(
            this Compilation compilation
            )
        {
            if (compilation is null)
            {
                throw new ArgumentNullException(nameof(compilation));
            }

            return compilation.GetTypeByMetadataName("System.Decimal")!;
        }
    }
}
#endif
