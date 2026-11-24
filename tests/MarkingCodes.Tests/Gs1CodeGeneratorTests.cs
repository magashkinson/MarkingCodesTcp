using System.Text.RegularExpressions;
using MarkingCodes.Server;

namespace MarkingCodes.Tests;

public class Gs1CodeGeneratorTests
{
    [Theory]
    [InlineData("629104150021", '3')]
    [InlineData("400638133393", '1')]
    public void CheckDigit_MatchesGs1Algorithm(string data, char expected) =>
        Assert.Equal(expected, Gs1CodeGenerator.CheckDigit(data));

    [Fact]
    public void Generate_ProducesValidStructure()
    {
        var generator = new Gs1CodeGenerator(new Random(42));
        var pattern = new Regex(@"^01(\d{14})21[A-Za-z0-9]{13}\x1D91[A-Za-z0-9]{4}\x1D92[A-Za-z0-9]{44}$");

        for (var i = 0; i < 100; i++)
        {
            var match = pattern.Match(generator.Generate());
            Assert.True(match.Success);
            var gtin = match.Groups[1].Value;
            Assert.Equal(gtin[^1], Gs1CodeGenerator.CheckDigit(gtin.AsSpan(0, 13)));
        }
    }
}
