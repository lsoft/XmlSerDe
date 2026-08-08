#if NETSTANDARD
using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace XmlSerDe.Generator.Incremental
{
    /// <summary>
    /// Место в исходнике, пригодное для инкрементального конвейера.
    ///
    /// Сам <see cref="Location"/> туда класть нельзя: он держит живым
    /// <see cref="SyntaxTree"/>, а с ним и всю компиляцию прошлого поколения.
    /// Здесь только строка и две структуры-значения, а настоящий
    /// <see cref="Location"/> собирается уже в выходном шаге.
    /// </summary>
    internal sealed class LocationInfo : IEquatable<LocationInfo>
    {
        public readonly string FilePath;
        public readonly TextSpan TextSpan;
        public readonly LinePositionSpan LineSpan;

        public LocationInfo(
            string filePath,
            TextSpan textSpan,
            LinePositionSpan lineSpan
            )
        {
            if (filePath is null)
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            FilePath = filePath;
            TextSpan = textSpan;
            LineSpan = lineSpan;
        }

        public static LocationInfo? From(SyntaxNode node)
        {
            var location = node.GetLocation();
            if (location.SourceTree is null)
            {
                return null;
            }

            return new LocationInfo(
                location.SourceTree.FilePath,
                location.SourceSpan,
                location.GetLineSpan().Span
                );
        }

        public Location ToLocation() => Location.Create(FilePath, TextSpan, LineSpan);

        public bool Equals(LocationInfo? other)
        {
            if (other is null)
            {
                return false;
            }

            return
                string.Equals(FilePath, other.FilePath, StringComparison.Ordinal)
                && TextSpan.Equals(other.TextSpan)
                && LineSpan.Equals(other.LineSpan)
                ;
        }

        public override bool Equals(object? obj) => Equals(obj as LocationInfo);

        public override int GetHashCode()
        {
            unchecked
            {
                return (FilePath.GetHashCode() * 31) + TextSpan.GetHashCode();
            }
        }
    }
}
#endif
