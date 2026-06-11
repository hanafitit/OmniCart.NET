using OmniCart.Domain.Entities;

namespace OmniCart.Infrastructure.Services;

public interface IGoogleSheetsService
{
    Task AddOrderAsync(Order order, CancellationToken ct = default);
}
