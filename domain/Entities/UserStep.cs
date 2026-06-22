namespace OmniCart.Domain.Entities;

/// <summary>
/// FSM состояния пользователя
/// </summary>
public enum UserStep
{
    MainPage = 0,
    EnteringDeliveryAddress = 1,
    EnteringPayment = 2,
    BrowsingCatalog = 3,
    SelectingDeliveryAddress = 4,
    EnteringPhoneNumber = 5
}
