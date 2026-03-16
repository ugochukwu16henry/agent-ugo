using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace Ugo.Orchestrator.Hubs;

public record AgentMessage(string AgentName, string Thought, string Status, DateTime Timestamp);

public class AgentUgoHub : Hub
{
    /// <summary>
    /// High-speed relay for parallel agent updates to all connected clients.
    /// </summary>
    public async Task BroadcastThought(AgentMessage message)
        => await Clients.All.SendAsync("ReceiveThought", message);
}

