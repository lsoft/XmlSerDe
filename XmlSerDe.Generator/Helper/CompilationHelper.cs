#if NETSTANDARD
using System;
using System.Reflection.Metadata;
using Microsoft.CodeAnalysis;

namespace XmlSerDe.Generator.Helper
{
    public static class CompilationHelper
    {
        public static INamedTypeSymbol DefaultStringBuilderExhauster(
            this Compilation compilation
            )
        {
            if (compilation is null)
            {
                throw new ArgumentNullException(nameof(compilation));
            }

            return compilation.GetTypeByMetadataName("XmlSerDe.Common.DefaultStringBuilderExhauster")!;
        }

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

        public static IArrayTypeSymbol Array(
            this Compilation compilation,
            params ITypeSymbol[] genericParameters
            )
        {
            if (compilation is null)
            {
                throw new ArgumentNullException(nameof(compilation));
            }

            return
                compilation.CreateArrayTypeSymbol(genericParameters[0]);
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

        public static INamedTypeSymbol Guid(
            this Compilation compilation
            )
        {
            if (compilation is null)
            {
                throw new ArgumentNullException(nameof(compilation));
            }

            return compilation.GetTypeByMetadataName("System.Guid")!;
        }

        public static INamedTypeSymbol NGuid(
            this Compilation compilation
            )
        {
            if (compilation is null)
            {
                throw new ArgumentNullException(nameof(compilation));
            }

            return
                compilation.GetTypeByMetadataName("System.Nullable`1")!
                    .Construct(Guid(compilation))
                    ;
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

        public static INamedTypeSymbol NDateTime(
            this Compilation compilation
            )
        {
            if (compilation is null)
            {
                throw new ArgumentNullException(nameof(compilation));
            }

            return
                compilation.GetTypeByMetadataName("System.Nullable`1")!
                    .Construct(DateTime(compilation))
                    ;
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

        public static INamedTypeSymbol NBool(
            this Compilation compilation
            )
        {
            if (compilation is null)
            {
                throw new ArgumentNullException(nameof(compilation));
            }

            return
                compilation.GetTypeByMetadataName("System.Nullable`1")!
                    .Construct(Bool(compilation))
                    ;
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

        public static INamedTypeSymbol NUInt16(
            this Compilation compilation
            )
        {
            if (compilation is null)
            {
                throw new ArgumentNullException(nameof(compilation));
            }

            return
                compilation.GetTypeByMetadataName("System.Nullable`1")!
                    .Construct(UInt16(compilation))
                    ;
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

        public static INamedTypeSymbol NInt16(
            this Compilation compilation
            )
        {
            if (compilation is null)
            {
                throw new ArgumentNullException(nameof(compilation));
            }

            return
                compilation.GetTypeByMetadataName("System.Nullable`1")!
                    .Construct(Int16(compilation))
                    ;
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

        public static INamedTypeSymbol NUInt32(
            this Compilation compilation
            )
        {
            if (compilation is null)
            {
                throw new ArgumentNullException(nameof(compilation));
            }

            return
                compilation.GetTypeByMetadataName("System.Nullable`1")!
                    .Construct(UInt32(compilation))
                    ;
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

        public static INamedTypeSymbol NInt32(
            this Compilation compilation
            )
        {
            if (compilation is null)
            {
                throw new ArgumentNullException(nameof(compilation));
            }

            return
                compilation.GetTypeByMetadataName("System.Nullable`1")!
                    .Construct(Int32(compilation))
                    ;
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

        public static INamedTypeSymbol NUInt64(
            this Compilation compilation
            )
        {
            if (compilation is null)
            {
                throw new ArgumentNullException(nameof(compilation));
            }

            return
                compilation.GetTypeByMetadataName("System.Nullable`1")!
                    .Construct(UInt64(compilation))
                    ;
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

        public static INamedTypeSymbol NInt64(
            this Compilation compilation
            )
        {
            if (compilation is null)
            {
                throw new ArgumentNullException(nameof(compilation));
            }

            return
                compilation.GetTypeByMetadataName("System.Nullable`1")!
                    .Construct(Int64(compilation))
                    ;
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

        public static INamedTypeSymbol NByte(
            this Compilation compilation
            )
        {
            if (compilation is null)
            {
                throw new ArgumentNullException(nameof(compilation));
            }

            return
                compilation.GetTypeByMetadataName("System.Nullable`1")!
                    .Construct(Byte(compilation))
                    ;
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

        public static INamedTypeSymbol NSByte(
            this Compilation compilation
            )
        {
            if (compilation is null)
            {
                throw new ArgumentNullException(nameof(compilation));
            }

            return
                compilation.GetTypeByMetadataName("System.Nullable`1")!
                    .Construct(SByte(compilation))
                    ;
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

        public static INamedTypeSymbol NDecimal(
            this Compilation compilation
            )
        {
            if (compilation is null)
            {
                throw new ArgumentNullException(nameof(compilation));
            }

            return
                compilation.GetTypeByMetadataName("System.Nullable`1")!
                    .Construct(Decimal(compilation))
                    ;
        }

    }
}
#endif
