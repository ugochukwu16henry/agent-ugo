using Microsoft.AspNetCore.SignalR;

namespace AgentUgo.Dashboard.Hubs;

public record AgentMessage(string AgentName, string Thought, string Status, DateTime Timestamp);

public class AgentUgoHub : Hub
{
    public async Task BroadcastThought(AgentMessage message)
        => await Clients.All.SendAsync("ReceiveThought", message);
}
