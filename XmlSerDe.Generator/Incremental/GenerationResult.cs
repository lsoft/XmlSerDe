#if NETSTANDARD
using System;

namespace XmlSerDe.Generator.Incremental
{
    /// <summary>
    /// Один сгенерированный файл: имя и текст, больше ничего.
    /// </summary>
    internal sealed class GeneratedSource : IEquatable<GeneratedSource>
    {
        public readonly string HintName;
        public readonly string Text;

        public GeneratedSource(string hintName, string text)
        {
            if (string.IsNullOrEmpty(hintName))
            {
                throw new ArgumentException($"'{nameof(hintName)}' cannot be null or empty.", nameof(hintName));
            }
            if (text is null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            HintName = hintName;
            Text = text;
        }

        public bool Equals(GeneratedSource? other)
        {
            if (other is null)
            {
                return false;
            }

            return
                string.Equals(HintName, other.HintName, StringComparison.Ordinal)
                && string.Equals(Text, other.Text, StringComparison.Ordinal)
                ;
        }

        public override bool Equals(object? obj) => Equals(obj as GeneratedSource);

        public override int GetHashCode()
        {
            unchecked
            {
                return (HintName.GetHashCode() * 31) + Text.GetHashCode();
            }
        }
    }

    /// <summary>
    /// Всё, что генератор хочет сказать компилятору, - в виде значения.
    ///
    /// Это и есть та самая перегородка, ради которой затевалась переделка: связывание
    /// символов гоняется на каждое нажатие клавиши (иначе сгенерированный код
    /// устаревал бы при правке типа в соседнем файле), но если результат совпал
    /// с прошлым, выходной шаг помечается Cached, и Roslyn переиспользует уже
    /// разобранные деревья вместо того, чтобы разбирать их заново.
    /// </summary>
    internal sealed class GenerationResult : IEquatable<GenerationResult>
    {
        public static readonly GenerationResult Empty = new GenerationResult(
            EquatableArray<GeneratedSource>.Empty,
            EquatableArray<DiagnosticInfo>.Empty
            );

        public readonly EquatableArray<GeneratedSource> Sources;
        public readonly EquatableArray<DiagnosticInfo> Diagnostics;

        public GenerationResult(
            EquatableArray<GeneratedSource> sources,
            EquatableArray<DiagnosticInfo> diagnostics
            )
        {
            Sources = sources;
            Diagnostics = diagnostics;
        }

        public bool Equals(GenerationResult? other)
        {
            if (other is null)
            {
                return false;
            }

            return
                Sources.Equals(other.Sources)
                && Diagnostics.Equals(other.Diagnostics)
                ;
        }

        public override bool Equals(object? obj) => Equals(obj as GenerationResult);

        public override int GetHashCode()
        {
            unchecked
            {
                return (Sources.GetHashCode() * 31) + Diagnostics.GetHashCode();
            }
        }
    }
}
#endif
