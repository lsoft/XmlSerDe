#if NETSTANDARD
using System;

namespace XmlSerDe.Generator.Incremental
{
    /// <summary>
    /// Точка вызова <c>new XmlSerializer(typeof(T))</c> в виде значения: имя типа
    /// и место, куда вешать диагностику отказа. Символ <c>T</c> сюда не кладётся -
    /// он будет разрешён заново по имени, уже от свежей компиляции.
    /// </summary>
    internal sealed class CompatCallSiteRef : IEquatable<CompatCallSiteRef>
    {
        public readonly string TypeMetadataName;
        public readonly LocationInfo? Location;

        public CompatCallSiteRef(string typeMetadataName, LocationInfo? location)
        {
            if (string.IsNullOrEmpty(typeMetadataName))
            {
                throw new ArgumentException($"'{nameof(typeMetadataName)}' cannot be null or empty.", nameof(typeMetadataName));
            }

            TypeMetadataName = typeMetadataName;
            Location = location;
        }

        /// <summary>
        /// Порядок задан именем типа, а не порядком обнаружения: один и тот же тип
        /// обычно называют в нескольких местах, диагностика вешается на первую точку
        /// по этому порядку, и он не должен меняться от того, что где-то в стороне
        /// поправили строчку кода. Иначе результат генерации «менялся» бы от правок,
        /// которые к нему отношения не имеют, и кэш не срабатывал бы никогда.
        /// </summary>
        public static int Compare(CompatCallSiteRef x, CompatCallSiteRef y)
        {
            var byName = string.CompareOrdinal(x.TypeMetadataName, y.TypeMetadataName);
            if (byName != 0)
            {
                return byName;
            }

            var xFile = x.Location is null ? "" : x.Location.FilePath;
            var yFile = y.Location is null ? "" : y.Location.FilePath;

            var byFile = string.CompareOrdinal(xFile, yFile);
            if (byFile != 0)
            {
                return byFile;
            }

            var xStart = x.Location is null ? -1 : x.Location.TextSpan.Start;
            var yStart = y.Location is null ? -1 : y.Location.TextSpan.Start;

            return xStart.CompareTo(yStart);
        }

        public bool Equals(CompatCallSiteRef? other)
        {
            if (other is null)
            {
                return false;
            }

            return
                string.Equals(TypeMetadataName, other.TypeMetadataName, StringComparison.Ordinal)
                && Equals(Location, other.Location)
                ;
        }

        public override bool Equals(object? obj) => Equals(obj as CompatCallSiteRef);

        public override int GetHashCode()
        {
            unchecked
            {
                return (TypeMetadataName.GetHashCode() * 31) + (Location is null ? 0 : Location.GetHashCode());
            }
        }
    }
}
#endif
