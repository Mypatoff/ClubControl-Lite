using ClubControl.Server.Services;
using ClubControl.Shared;

namespace ClubControl.Server.Tests;

/// <summary>The sessions list of a report.</summary>
public class ReportsServiceTests : SessionTestBase
{
    [Fact]
    public async Task Sessions_list_shows_which_pc_started_when_and_whether_it_has_ended()
    {
        using (var db = NewDb())
        {
            await ServiceFor(db).StartPrepayByMoneyAsync(Pc1, 3_000, note: "birthday");
            await ServiceFor(db).StartPostpayAsync(Pc2);
        }

        using (var db = NewDb())
            await ServiceFor(db).EndSessionEarlyAsync(Pc2);

        using var reportDb = NewDb();
        var report = await new ReportsService(reportDb).BuildReportAsync(DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(1));

        Assert.Equal(2, report.Sessions.Count);

        var running = Assert.Single(report.Sessions, s => s.Note == "birthday");
        Assert.Null(running.EndUtc);
        Assert.Equal(3_000, running.AmountUzs);
        Assert.InRange(running.StartUtc, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1));

        var ended = Assert.Single(report.Sessions, s => s.Type == SessionType.Postpay);
        Assert.NotNull(ended.EndUtc);
        Assert.True(report.Sessions[0].StartUtc >= report.Sessions[1].StartUtc, "newest session first");
    }
}
