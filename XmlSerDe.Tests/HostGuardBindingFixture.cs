using XmlSerDe.Common;
using XmlSerDe.Generator.Producer;
using Xunit;

namespace XmlSerDe.Tests
{
    /// <summary>
    /// Флаги → сниппеты, без Roslyn. Регрессия «страж включили, а хост его не
    /// зовёт» ловится здесь, а не полным прогоном генератора -
    /// docs/opt-in-xml-guards.md §5.0.
    /// </summary>
    public class HostGuardBindingFixture
    {
        [Fact]
        public void None_EmitsNothing()
        {
            var b = HostGuardBinding.From(XmlGuard.None, XmlFeature.None);

            Assert.False(b.Any);
            Assert.Equal("out _", b.RootBodyConsumedArgument);
            Assert.Equal("", b.RootTailStatement("    ", "fullNode", "xmlNode"));
            Assert.Equal("    CALL;", b.RootBodyStatement("    ", "CALL;"));
        }

        /// <summary>
        /// <see cref="XmlGuard.None"/> и отсутствие атрибута - одно и то же,
        /// и это здесь тавтология: читатель атрибута отдаёт None, когда его нет.
        /// </summary>
        [Fact]
        public void SingleRoot_TakesConsumedAndChecksTail()
        {
            var b = HostGuardBinding.From(XmlGuard.SingleRoot, XmlFeature.None);

            Assert.True(b.Any);
            Assert.Equal("out var bodyConsumed", b.RootBodyConsumedArgument);

            var tail = b.RootTailStatement("    ", "fullNode", "xmlNode");
            Assert.Contains(nameof(XmlScan.EnsureNoTrailingContent) + "(fullNode, xmlNode.TotalLength + bodyConsumed);", tail);
            Assert.DoesNotContain(nameof(XmlScan.EnsureNoTrailingContentMarkup), tail);
        }

        /// <summary>
        /// Единственная точка, где страж смотрит на набор фич: после корня XML
        /// разрешает Misc, и хост, понимающий комментарии, обязан понимать их
        /// и в хвосте (§5.3).
        /// </summary>
        [Fact]
        public void SingleRoot_WithMarkup_AllowsTrailingMisc()
        {
            var b = HostGuardBinding.From(XmlGuard.SingleRoot, XmlFeature.Markup);

            Assert.Contains(
                nameof(XmlScan.EnsureNoTrailingContentMarkup) + "(fullNode, xmlNode.TotalLength + bodyConsumed);",
                b.RootTailStatement("    ", "fullNode", "xmlNode")
                );
        }

        [Fact]
        public void SingleRoot_WithCData_AllowsTrailingMiscToo()
        {
            //CData включает в себя Markup, отдельной проверки набор не требует
            var b = HostGuardBinding.From(XmlGuard.SingleRoot, XmlFeature.CData);

            Assert.Contains(
                nameof(XmlScan.EnsureNoTrailingContentMarkup),
                b.RootTailStatement("    ", "fullNode", "xmlNode")
                );
        }

        /// <summary>
        /// Флаг, который хвоста не касается, хвост и не трогает: <c>out _</c>
        /// остаётся, складывать нечего.
        /// </summary>
        [Theory]
        [InlineData(XmlGuard.MatchingEndTags)]
        [InlineData(XmlGuard.UniqueAttributes)]
        [InlineData(XmlGuard.IllegalChars)]
        public void OtherGuards_DoNotTouchRootTail(XmlGuard guard)
        {
            var b = HostGuardBinding.From(guard, XmlFeature.None);

            Assert.True(b.Any);
            Assert.Equal("out _", b.RootBodyConsumedArgument);
            Assert.Equal("", b.RootTailStatement("    ", "fullNode", "xmlNode"));
        }

        /// <summary>
        /// Форма исключения - ось не отдельного флага, а «есть ли хоть один»:
        /// пользователь, включивший строгость, ждёт ошибку в форме BCL
        /// независимо от того, какой именно страж сработал (§6).
        /// </summary>
        [Theory]
        [InlineData(XmlGuard.MatchingEndTags)]
        [InlineData(XmlGuard.SingleRoot)]
        [InlineData(XmlGuard.UniqueAttributes)]
        [InlineData(XmlGuard.IllegalChars)]
        [InlineData(XmlGuard.SystemXmlCompatible)]
        public void AnyGuard_WrapsRootBody(XmlGuard guard)
        {
            var b = HostGuardBinding.From(guard, XmlFeature.None);
            var body = b.RootBodyStatement("    ", "CALL;");

            Assert.Contains("try", body);
            Assert.Contains("catch (global::XmlSerDe.Common.XmlDocumentException exception)", body);
            Assert.Contains("global::XmlSerDe.Common.XmlDocumentErrors.Wrap(exception);", body);
            Assert.Contains("CALL;", body);
        }

        [Fact]
        public void SystemXmlCompatible_IsEveryFlag()
        {
            var b = HostGuardBinding.From(XmlGuard.SystemXmlCompatible, XmlFeature.SystemXmlCompatible);

            Assert.Equal(
                XmlGuard.MatchingEndTags | XmlGuard.SingleRoot | XmlGuard.UniqueAttributes | XmlGuard.IllegalChars,
                b.Guards
                );
            Assert.Equal("out var bodyConsumed", b.RootBodyConsumedArgument);
            Assert.Contains(
                nameof(XmlScan.EnsureNoTrailingContentMarkup),
                b.RootTailStatement("    ", "fullNode", "xmlNode")
                );
        }
    }
}
