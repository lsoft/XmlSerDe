#if NETSTANDARD
using System;
using Microsoft.CodeAnalysis;

namespace XmlSerDe.Generator.Incremental
{
    /// <summary>
    /// Диагностика, отложенная до выходного шага.
    ///
    /// Готовый <see cref="Diagnostic"/> держит <see cref="Location"/>, а тот -
    /// дерево разбора; в конвейере это означало бы удержание всей прошлой компиляции.
    /// <see cref="DiagnosticDescriptor"/> ничего лишнего не держит и сравнивается
    /// по значению, поэтому его можно нести как есть.
    /// </summary>
    internal sealed class DiagnosticInfo : IEquatable<DiagnosticInfo>
    {
        private readonly DiagnosticDescriptor _descriptor;
        private readonly LocationInfo? _location;
        private readonly EquatableArray<string> _messageArgs;

        public DiagnosticInfo(
            DiagnosticDescriptor descriptor,
            LocationInfo? location,
            params string[] messageArgs
            )
        {
            if (descriptor is null)
            {
                throw new ArgumentNullException(nameof(descriptor));
            }
            if (messageArgs is null)
            {
                throw new ArgumentNullException(nameof(messageArgs));
            }

            _descriptor = descriptor;
            _location = location;
            _messageArgs = new EquatableArray<string>(messageArgs);
        }

        public Diagnostic ToDiagnostic()
        {
            var args = new object[_messageArgs.Count];
            for (var i = 0; i < args.Length; i++)
            {
                args[i] = _messageArgs[i];
            }

            return Diagnostic.Create(
                _descriptor,
                _location is null ? Location.None : _location.ToLocation(),
                args
                );
        }

        public bool Equals(DiagnosticInfo? other)
        {
            if (other is null)
            {
                return false;
            }

            return
                _descriptor.Equals(other._descriptor)
                && Equals(_location, other._location)
                && _messageArgs.Equals(other._messageArgs)
                ;
        }

        public override bool Equals(object? obj) => Equals(obj as DiagnosticInfo);

        public override int GetHashCode()
        {
            unchecked
            {
                var result = _descriptor.GetHashCode();
                result = (result * 31) + (_location is null ? 0 : _location.GetHashCode());
                result = (result * 31) + _messageArgs.GetHashCode();
                return result;
            }
        }
    }
}
#endif
