using ClubControl.Server.Data;
using ClubControl.Server.Data.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ClubControl.Server.Tests;

/// <summary>Starting the server on a club.db: a new one is created, a broken one is refused with an explanation (see <see cref="DatabaseUpgrade"/>).</summary>
public class DatabaseUpgradeTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly string _backupDirectory = Path.Combine(Path.GetTempPath(), "clubcontrol-upgrade-" + Guid.NewGuid());

    public DatabaseUpgradeTests() => _connection.Open();

    private ClubDbContext NewDb() => new(new DbContextOptionsBuilder<ClubDbContext>().UseSqlite(_connection).Options);

    [Fact]
    public async Task The_database_refuses_a_PC_with_a_missing_tariff_and_the_deletion_of_a_tariff_in_use()
    {
        using (var db = NewDb())
            await DatabaseUpgrade.MigrateAsync(db, _backupDirectory);

        using (var db = NewDb())
        {
            db.Pcs.Add(new Pc { MachineGuid = Guid.NewGuid(), Number = 1, TariffId = 999 });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        int tariffId;
        using (var db = NewDb())
        {
            var tariff = new Tariff { Name = "Standard", PricePerHourUzs = 10_000, MinPaymentUzs = 500, MaxPaymentUzs = 50_000 };
            db.Tariffs.Add(tariff);
            await db.SaveChangesAsync();
            db.Pcs.Add(new Pc { MachineGuid = Guid.NewGuid(), Number = 1, TariffId = tariff.Id });
            await db.SaveChangesAsync();
            tariffId = tariff.Id;
        }

        // Like the Settings window: the tariff is deleted without any PC loaded (EF would clear a tracked PC's tariff by itself).
        using (var db = NewDb())
        {
            var tariff = await db.Tariffs.SingleAsync(t => t.Id == tariffId);
            db.Tariffs.Remove(tariff);
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
    }

    [Fact]
    public async Task A_new_database_is_created_without_a_backup()
    {
        using (var db = NewDb())
            await DatabaseUpgrade.MigrateAsync(db, _backupDirectory);

        using var created = NewDb();
        Assert.Empty(await created.Database.GetPendingMigrationsAsync());
        Assert.False(Directory.Exists(_backupDirectory));
    }

    [Fact]
    public async Task A_new_database_starts_with_the_shift_auto_close_at_8_hours()
    {
        using (var db = NewDb())
            await DatabaseUpgrade.MigrateAsync(db, _backupDirectory);

        using var created = NewDb();
        Assert.Equal(8, (await SettingsHelper.GetOrCreateAsync(created)).ShiftAutoCloseHours);
    }

    [Fact]
    public async Task A_database_from_before_migrations_existed_is_refused_with_an_explanation()
    {
        using (var legacy = NewDb())
            await legacy.Database.EnsureCreatedAsync(); // tables, but no migration history

        using var db = NewDb();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => DatabaseUpgrade.MigrateAsync(db, _backupDirectory));
        Assert.Contains("pre-migrations", error.Message);
    }

    public void Dispose()
    {
        _connection.Dispose();
        if (Directory.Exists(_backupDirectory))
            Directory.Delete(_backupDirectory, recursive: true);
    }
}
