using System;
using ClubControl.Shared;

namespace ClubControl.Server.Data.Models;

/// <summary>Single-row table holding every General setting plus the server's own persistent identity.</summary>
public class Setting
{
    public int Id { get; set; } = 1;

    public Guid ServerId { get; set; } = Guid.NewGuid();

    public string ClubName { get; set; } = "ClubControl";
    public string LockScreenText { get; set; } = "";
    public LockScreenLanguage LockScreenLanguage { get; set; } = LockScreenLanguage.English;
    public AdminPanelLanguage AdminPanelLanguage { get; set; } = AdminPanelLanguage.Russian;

    public int WarningMinutes1 { get; set; } = 5;
    public int WarningMinutes2 { get; set; } = 1;

    public AfterSessionAction AfterSessionAction { get; set; } = AfterSessionAction.CloseProcesses;
    public string ProcessListCsv { get; set; } = "steam.exe";

    /// <summary>PBKDF2 hash of the password that unlocks a locked client PC from its hotkey. Null = none set yet.</summary>
    public string? ClientUnlockPasswordHash { get; set; }

    /// <summary>Idle-locked PCs (not limit-reached, not mid-session) shut themselves down after this many minutes. 0 = disabled.</summary>
    public int AutoShutdownAfterMinutes { get; set; } = 1;

    /// <summary>A shift nobody closed ends by itself after this many hours without any session starting or ending (and none running).</summary>
    public int ShiftAutoCloseHours { get; set; } = 8;

    public string? AdminPasswordHash { get; set; }
}
