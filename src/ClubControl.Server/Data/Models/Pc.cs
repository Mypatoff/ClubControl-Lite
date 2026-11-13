using System;
using ClubControl.Shared;

namespace ClubControl.Server.Data.Models;

/// <summary>
/// A billable PC: a networked gaming PC (auto-discovered once its client connects), or an
/// offline PC with no LAN connection. An offline PC is created manually by the admin (<see cref="IsManual"/>) and never
/// gets a lock screen, online tracking, or power commands, since there is no client running for it.
/// Unassigned/new PCs (Number/TariffId null) show as "New PC" in the admin panel.
/// </summary>
public class Pc
{
    public int Id { get; set; }
    public Guid MachineGuid { get; set; }
    public bool IsManual { get; set; }

    public int? Number { get; set; }

    public int? TariffId { get; set; }
    public bool Enabled { get; set; } = true;

    public string IpAddress { get; set; } = "";
    public string MacAddress { get; set; } = "";
    public string Hostname { get; set; } = "";

    public PcState State { get; set; } = PcState.Free;

    public bool IsOnline { get; set; }
    public DateTime LastHeartbeatUtc { get; set; }

    /// <summary>
    /// SHA-256 hash of this PC's auth token (see ClubHub.Heartbeat and PcTokenHasher), null until
    /// the admin has configured it (assigned a number/tariff) and it has heartbeated at least
    /// once since. Once set, every future heartbeat for this MachineGuid must present the
    /// matching token - this is what stops a second client from impersonating this PC merely by
    /// knowing or guessing its GUID.
    /// </summary>
    public string? AuthTokenHash { get; set; }

    public bool IsConfigured => Number.HasValue && TariffId.HasValue;
}
