using OmniCart.Domain.Entities;

namespace OmniCart.Infrastructure.Telegram;

public interface IOrderNotificationService
{
    void NotifyNewOrder(Order order);
}
