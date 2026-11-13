using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using ClubControl.Server.Data;
using ClubControl.Server.Data.Models;
using ClubControl.Shared;
using Microsoft.EntityFrameworkCore;

namespace ClubControl.Server.Services;

/// <summary>
/// Reports: totals and breakdowns for sessions that started within a date range or inside one working shift.
/// Income is the session's stored <see cref="Session.AmountUzs"/> - for a postpay session still running,
/// that is 0 until it ends, same as everywhere else.
/// </summary>
public class ReportsService
{
    private readonly ClubDbContext _db;

    public ReportsService(ClubDbContext db)
    {
        _db = db;
    }

    public record Totals(int TotalIncomeUzs, int SessionCount, double TotalHours);
    public record PcRow(int? PcNumber, int IncomeUzs, int SessionCount, double Hours);
    public record TariffRow(string TariffName, int IncomeUzs, int SessionCount, double Hours);
    public record TypeRow(SessionType Type, int IncomeUzs, int SessionCount, double Hours);
    /// <summary>One session: which PC, when it started and ended (null while still running), what it cost.</summary>
    public record SessionLine(int? PcNumber, DateTime StartUtc, DateTime? EndUtc, SessionType Type,
        string TariffName, int AmountUzs, string Note);
    public record Report(Totals Totals, List<PcRow> ByPc, List<TariffRow> ByTariff, List<TypeRow> ByType, List<SessionLine> Sessions);

    /// <summary>A shift for the report's shift picker; ClosedUtc is null while it is still open.</summary>
    public record ShiftInfo(int Id, DateTime OpenedUtc, DateTime? ClosedUtc);

    /// <summary>Every shift, newest first.</summary>
    public async Task<List<ShiftInfo>> GetShiftsAsync() =>
        await _db.Shifts.AsNoTracking()
            .OrderByDescending(s => s.Id)
            .Select(s => new ShiftInfo(s.Id, s.OpenedUtc, s.ClosedUtc))
            .ToListAsync();

    public async Task<Report> BuildReportAsync(DateTime startUtc, DateTime endUtc) =>
        await BuildAsync(s => s.StartUtc >= startUtc && s.StartUtc < endUtc);

    /// <summary>The same report for one working shift: every session that started while it was open, even past midnight.</summary>
    public async Task<Report> BuildShiftReportAsync(int shiftId) =>
        await BuildAsync(s => s.ShiftId == shiftId);

    /// <summary>The few columns of a session a report needs (so no whole entities are loaded and tracked).</summary>
    private record SessionRow(DateTime StartUtc, DateTime? ActualEndUtc, SessionType Type, int AmountUzs, string? Note,
        int? PcNumber, int? PcTariffId);

    private async Task<Report> BuildAsync(Expression<Func<Session, bool>> filter)
    {
        // Read-only and only the needed columns: a Month report is thousands of sessions, and this used to load them
        // all (with their PC) as tracked entities.
        var sessions = await _db.Sessions.AsNoTracking()
            .Where(filter)
            .Select(s => new SessionRow(s.StartUtc, s.ActualEndUtc, s.Type, s.AmountUzs, s.Note,
                s.Pc!.Number, s.Pc.TariffId))
            .ToListAsync();

        var tariffNames = await _db.Tariffs.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.Name);

        var now = DateTime.UtcNow;
        double HoursOf(SessionRow s) => ((s.ActualEndUtc ?? now) - s.StartUtc).TotalHours;
        string TariffNameOf(SessionRow s) => s.PcTariffId is int id ? tariffNames.GetValueOrDefault(id, "") : "";

        var totals = new Totals(
            TotalIncomeUzs: sessions.Sum(s => s.AmountUzs),
            SessionCount: sessions.Count,
            TotalHours: sessions.Sum(HoursOf));

        var byPc = sessions
            .GroupBy(s => s.PcNumber)
            .Select(g => new PcRow(g.Key, g.Sum(s => s.AmountUzs), g.Count(), g.Sum(HoursOf)))
            .OrderBy(r => r.PcNumber)
            .ToList();

        var byTariff = sessions
            .GroupBy(TariffNameOf)
            .Select(g => new TariffRow(g.Key, g.Sum(s => s.AmountUzs), g.Count(), g.Sum(HoursOf)))
            .OrderBy(r => r.TariffName)
            .ToList();

        var byType = sessions
            .GroupBy(s => s.Type)
            .Select(g => new TypeRow(g.Key, g.Sum(s => s.AmountUzs), g.Count(), g.Sum(HoursOf)))
            .OrderBy(r => r.Type)
            .ToList();

        // Newest first, so the admin sees the latest sessions at the top.
        var bySession = sessions
            .OrderByDescending(s => s.StartUtc)
            .Select(s => new SessionLine(s.PcNumber, s.StartUtc, s.ActualEndUtc, s.Type,
                TariffNameOf(s), s.AmountUzs, s.Note ?? ""))
            .ToList();

        return new Report(totals, byPc, byTariff, byType, bySession);
    }
}
