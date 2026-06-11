using OxidizePdf.NET;

namespace OxidizePdf.NET.Tests;

/// <summary>
/// Behavioral tests for TXT-016 text validation. The validator classifies
/// dates, monetary amounts, contract numbers and party names within an
/// already-extracted text string.
/// </summary>
public class TextValidationTests
{
    private const string ContractText =
        "This agreement was signed on 30 September 2016 for $1,000,000 between ABC Corp and XYZ LLC.";

    [Fact]
    public void ValidateContract_FindsDateAndMonetaryAmount()
    {
        var result = TextValidation.ValidateContract(ContractText);

        Assert.True(result.Found);
        Assert.Contains(result.Matches, m => m.MatchType == "date");
        Assert.Contains(result.Matches, m => m.MatchType == "monetaryAmount");
        // The monetary match must carry the real substring.
        var amount = result.Matches.First(m => m.MatchType == "monetaryAmount");
        Assert.Contains("1,000,000", amount.Text);
    }

    [Fact]
    public void ExtractKeyInfo_GroupsDatesAndAmounts()
    {
        var info = TextValidation.ExtractKeyInfo(ContractText);

        Assert.True(info.ContainsKey("dates"), "must group dates");
        Assert.True(info.ContainsKey("monetary_amounts"), "must group monetary amounts");
        Assert.NotEmpty(info["monetary_amounts"]);
    }

    [Fact]
    public void Search_FindsTargetOccurrence()
    {
        var result = TextValidation.Search("The total due is $500 on the invoice.", "$500");
        Assert.True(result.Found);
        Assert.Contains(result.Matches, m => m.Text.Contains("500"));
    }

    [Fact]
    public void ValidateContract_NullText_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => TextValidation.ValidateContract(null!));
    }
}
