namespace Altinn.Broker.Application.CreateNotificationOrder;

public interface ICreateNotificationOrderHandler
{
    Task Process(CreateNotificationOrderRequest request, CancellationToken cancellationToken);
}
