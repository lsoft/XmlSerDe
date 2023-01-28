#if NETSTANDARD
using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace XmlSerDe.Generator.Extractor
{
    //public static class MethodArgumentExtractor
    //{
    //    public static List<DetectedMethodArgument> GetMethodArguments(
    //        this IMethodSymbol methodSymbol
    //        )
    //    {
    //        if (methodSymbol is null)
    //        {
    //            throw new ArgumentNullException(nameof(methodSymbol));
    //        }

    //        var result = new List<DetectedMethodArgument>();

    //        for (var i = 0; i < methodSymbol.Parameters.Length; i++)
    //        {
    //            var p = methodSymbol.Parameters[i];

    //            result.Add(
    //                new DetectedMethodArgument(
    //                    i,
    //                    p.Name,
    //                    p.Type,
    //                    p.RefKind,
    //                    p.HasExplicitDefaultValue,
    //                    () => p.ExplicitDefaultValue
    //                    )
    //                );
    //        }

    //        return
    //            result;
    //    }
    //}
}
#endif
