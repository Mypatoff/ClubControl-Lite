using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using ClubControl.Server.Data;
using ClubControl.Server.Data.Models;
using ClubControl.Server.Hubs;
using ClubControl.Shared;
using ClubControl.Shared.Dto;
using Microsoft.AspNetCore.SignalR;

namespace ClubControl.Server.Services;

/// <summary>
/// Power control (SPEC section 6): Wake-on-LAN magic packets for "Turn on", and Shutdown/Restart
/// commands for everything else. Each action can target one PC, several, or all of them.
/// </summary>
public class PowerService
{
    private readonly ClubDbContext _db;
    private readonly IHubContext<ClubHub> _hub;

    public PowerService(ClubDbContext db, IHubContext<ClubHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    /// <summary>Sends a WoL magic packet to every PC with a known MAC address. Works whether or not the PC is online.</summary>
    public async Task<int> TurnOnAsync(IEnumerable<Pc> pcs)
    {
        var sent = 0;
        foreach (var pc in pcs.Where(p => !string.IsNullOrWhiteSpace(p.MacAddress)))
        {
            await SendMagicPacketAsync(pc.MacAddress);
            sent++;
        }

        if (sent > 0)
            await LogEventAsync($"Wake-on-LAN sent to {sent} PC(s)");

        return sent;
    }

    /// <summary>
    /// A networked computer that is not connected right now and can be woken: it has reported its MAC
    /// address at least once. (A PC that is on but has lost its client also matches; the packet is harmless to it.)
    /// </summary>
    public static bool NeedsWakeUp(Pc pc) =>
        !pc.IsManual && !pc.IsOnline && !string.IsNullOrWhiteSpace(pc.MacAddress);

    /// <summary>
    /// Called after sessions were started: sends Wake-on-LAN to those PCs that are switched off, so the
    /// admin does not have to press "Turn on" as well. The client then connects and gets its session from
    /// the server like after any restart (SPEC 5). Returns how many PCs were woken.
    /// </summary>
    public async Task<int> WakeForSessionAsync(IEnumerable<Pc> pcs)
    {
        var woken = pcs.Where(NeedsWakeUp).ToList();
        foreach (var pc in woken)
            await SendMagicPacketAsync(pc.MacAddress);

        if (woken.Count > 0)
            await LogEventAsync($"Session started on switched-off PC {string.Join(", ", woken.Select(p => "#" + p.Number))}: Wake-on-LAN sent");

        return woken.Count;
    }

    public Task<int> ShutdownAsync(IEnumerable<Pc> pcs) =>
        SendCommandToOnlineAsync(pcs, _ => new ClientCommand { Type = CommandType.Shutdown }, "Shutdown");

    public Task<int> RestartAsync(IEnumerable<Pc> pcs) =>
        SendCommandToOnlineAsync(pcs, _ => new ClientCommand { Type = CommandType.Restart }, "Restart");

    /// <summary>Sends one command per online PC; <paramref name="buildCommand"/> returning null skips that PC.</summary>
    private async Task<int> SendCommandToOnlineAsync(IEnumerable<Pc> pcs, Func<Pc, ClientCommand?> buildCommand, string actionName)
    {
        var sent = 0;
        foreach (var pc in pcs.Where(p => p.IsOnline))
        {
            if (!ClubHub.TryGetConnectionId(pc.MachineGuid, out var connectionId))
                continue;

            var command = buildCommand(pc);
            if (command is null)
                continue;

            await _hub.Clients.Client(connectionId).SendAsync(HubMethodNames.ReceiveCommand, command);
            sent++;
        }

        if (sent > 0)
            await LogEventAsync($"{actionName} sent to {sent} PC(s)");

        return sent;
    }

    private async Task LogEventAsync(string message)
    {
        _db.EventLogEntries.Add(new EventLogEntry { Type = EventType.PowerCommand, Message = message });
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Sends the magic packet as a directed broadcast on every active IPv4 interface's own subnet
    /// (handles multi-NIC/VLAN servers where the target PC is only reachable via one specific
    /// adapter, and where routers/switches do not forward the generic broadcast across subnets).
    /// Falls back to the plain 255.255.255.255 broadcast if no interface yielded a usable subnet -
    /// better to try the naive approach than to silently send nothing on an unusual network.
    /// </summary>
    private static async Task SendMagicPacketAsync(string macAddress)
    {
        var packet = WakeOnLan.BuildMagicPacket(macAddress);
        var sentFromAnyInterface = false;

        foreach (var (localAddress, broadcastAddress) in LocalNetworkInterfaces.GetBroadcastEndpoints())
        {
            try
            {
                using var udpClient = new UdpClient(new IPEndPoint(localAddress, 0)) { EnableBroadcast = true };
                await udpClient.SendAsync(packet, packet.Length, new IPEndPoint(broadcastAddress, WakeOnLan.Port));
                sentFromAnyInterface = true;
            }
            catch (SocketException)
            {
                // This interface couldn't send a broadcast (e.g. disabled broadcast, link down
                // mid-send) - other interfaces are still tried below.
            }
        }

        if (!sentFromAnyInterface)
        {
            try
            {
                using var udpClient = new UdpClient { EnableBroadcast = true };
                await udpClient.SendAsync(packet, packet.Length, new IPEndPoint(IPAddress.Broadcast, WakeOnLan.Port));
            }
            catch (SocketException)
            {
                // No usable network path for a broadcast at all - nothing more can be done here;
                // the caller only logs how many WoL packets were attempted.
            }
        }
    }
}
