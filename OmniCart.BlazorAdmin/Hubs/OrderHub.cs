using Microsoft.AspNetCore.SignalR;

namespace OmniCart.BlazorAdmin.Hubs;

public class OrderHub : Hub
{
    public async Task SendOrderNotification(int orderId, decimal totalPrice, string customerName)
    {
        await Clients.All.SendAsync("NewOrder", orderId, totalPrice, customerName);
    }
}
