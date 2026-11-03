using System.Text.RegularExpressions;
using Xunit;

namespace Knx.EtsBridge.Addin.Tests
{
    public class BridgeTokenTests
    {
        [Fact]
        public void Generate_Returns64LowercaseHexChars()
        {
            var token = BridgeToken.Generate();

            Assert.Matches(new Regex("^[0-9a-f]{64}$"), token);
        }

        [Fact]
        public void Generate_ReturnsADifferentTokenEachTime()
        {
            Assert.NotEqual(BridgeToken.Generate(), BridgeToken.Generate());
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Resolve_EmptyPreference_GeneratesARandomToken(string? configured)
        {
            var token = BridgeToken.Resolve(configured);

            Assert.Equal(64, token.Length);
        }

        [Fact]
        public void Resolve_ConfiguredToken_IsKeptAndTrimmed()
        {
            Assert.Equal("fixed-token", BridgeToken.Resolve("  fixed-token "));
        }

        [Fact]
        public void FixedTimeEquals_SameToken_Matches()
        {
            Assert.True(BridgeToken.FixedTimeEquals("abc123", "abc123"));
        }

        [Theory]
        [InlineData("abc124")]   // last character differs
        [InlineData("xbc123")]   // first character differs
        [InlineData("abc12")]    // shorter
        [InlineData("abc1234")]  // longer
        [InlineData("abc123\0")] // trailing NUL
        [InlineData("ABC123")]   // case-sensitive
        [InlineData("")]
        [InlineData(null)]
        public void FixedTimeEquals_DifferentToken_DoesNotMatch(string? actual)
        {
            Assert.False(BridgeToken.FixedTimeEquals("abc123", actual));
        }

        [Theory]
        [InlineData("", "")]
        [InlineData(null, null)]
        [InlineData(null, "")]
        public void FixedTimeEquals_EmptyExpectedToken_NeverMatches(string? expected, string? actual)
        {
            // There is no "no authentication" mode: an empty expected token must not
            // let an empty/missing request token through.
            Assert.False(BridgeToken.FixedTimeEquals(expected, actual));
        }
    }
}
