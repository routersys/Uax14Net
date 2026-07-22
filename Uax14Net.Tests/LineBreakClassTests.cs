using Uax14Net;

namespace Uax14Net.Tests;

public class LineBreakClassTests
{
    [Theory]
    [InlineData(0x0041, LineBreakClass.AL)]
    [InlineData(0x0020, LineBreakClass.SP)]
    [InlineData(0x000A, LineBreakClass.LF)]
    [InlineData(0x000D, LineBreakClass.CR)]
    [InlineData(0x0009, LineBreakClass.BA)]
    [InlineData(0x00A0, LineBreakClass.GL)]
    [InlineData(0x2060, LineBreakClass.WJ)]
    [InlineData(0x200B, LineBreakClass.ZW)]
    [InlineData(0x200D, LineBreakClass.ZWJ)]
    [InlineData(0x0030, LineBreakClass.NU)]
    [InlineData(0x0028, LineBreakClass.OP)]
    [InlineData(0x0029, LineBreakClass.CP)]
    [InlineData(0x00AB, LineBreakClass.QU)]
    [InlineData(0x2018, LineBreakClass.QU)]
    [InlineData(0x4E00, LineBreakClass.ID)]
    [InlineData(0x1F600, LineBreakClass.ID)]
    [InlineData(0x1F3FB, LineBreakClass.EM)]
    [InlineData(0x0E01, LineBreakClass.SA)]
    [InlineData(0x1100, LineBreakClass.JL)]
    [InlineData(0xAC00, LineBreakClass.H2)]
    [InlineData(0x05D0, LineBreakClass.HL)]
    [InlineData(0x1F1E6, LineBreakClass.RI)]
    [InlineData(0x1B05, LineBreakClass.AK)]
    [InlineData(0x11003, LineBreakClass.AP)]
    public void ClassMatchesUnicodeData(int codePoint, LineBreakClass expected)
    {
        Assert.Equal(expected, LineBreaker.GetLineBreakClass(codePoint));
    }

    [Fact]
    public void UnassignedDefaultsFollowDerivedLineBreak()
    {
        Assert.Equal(LineBreakClass.ID, LineBreaker.GetLineBreakClass(0x2A6E0));
        Assert.Equal(LineBreakClass.PR, LineBreaker.GetLineBreakClass(0x20C2));
        Assert.Equal(LineBreakClass.XX, LineBreaker.GetLineBreakClass(0x0378));
    }
}
