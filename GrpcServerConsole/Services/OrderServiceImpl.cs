using Grpc.Core;
using Shared.Protos;

namespace GrpcServerConsole.Services;

public class OrderServiceImpl : OrderProcessing.OrderProcessingBase
{
	public override Task<OrderResponse> CreateOrder(CreateOrderRequest request, ServerCallContext context)
	{
		Console.WriteLine($"[Server] Received order for {request.Quantity}x {request.Symbol} from Account {request.AccountId}");

		return Task.FromResult(new OrderResponse
		{
			OrderId = Guid.NewGuid().ToString("N")[..8].ToUpper(),
			Status = "EXECUTED",
			IsSuccess = true
		});
	}

	public override async Task StreamOrderStatus(
		OrderStatusRequest request,
		IServerStreamWriter<OrderStatusUpdate> responseStream,
		ServerCallContext context)
	{
		string[] stages = ["Received", "Processing", "Filled", "Completed"];

		foreach (var stage in stages)
		{
			if (context.CancellationToken.IsCancellationRequested) break;

			Console.WriteLine($"[Server] Pushing status '{stage}' for Order {request.OrderId}");

			await responseStream.WriteAsync(new OrderStatusUpdate
			{
				OrderId = request.OrderId,
				CurrentStatus = stage,
				Timestamp = DateTime.UtcNow.ToString("O")
			});

			await Task.Delay(1000); // Simulate progress interval
		}
	}
}