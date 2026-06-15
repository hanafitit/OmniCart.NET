using Microsoft.AspNetCore.SignalR;

namespace OmniCart.BlazorAdmin.Hubs;

public class OrderHub : Hub
{
    public async Task SendNewOrderNotification()
    {
        await Clients.All.SendAsync("NewOrderReceived");
    }
}
