using ClubControl.Shared;

namespace ClubControl.Server.Data.Models;

/// <summary>
/// A named price plan the admin creates/edits freely (not just a fixed Standard/VIP pair) - prices are in whole UZS.
/// </summary>
public class Tariff
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public int PricePerHourUzs { get; set; }
    public int MinPaymentUzs { get; set; }
    public int MaxPaymentUzs { get; set; }
}
