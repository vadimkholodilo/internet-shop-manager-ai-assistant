using ShopLoader.Validation;

namespace ShopLoader.UnitTests;

public sealed class LoadReportTests
{
    [Fact]
    public void SummaryMentionsCountsAndFailures()
    {
        var report = new LoadReport();
        report.Category(created: true);
        report.Product(created: false);
        report.AddFailure(new RecordError("product", "X", "bad"));

        var summary = report.Summary();

        Assert.Contains("1 created", summary);
        Assert.Contains("Failed records: 1", summary);
    }
}
