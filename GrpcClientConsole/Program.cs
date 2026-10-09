using Grpc.Core;
using Grpc.Net.Client;
using Shared.Protos;

Console.WriteLine("Connecting to gRPC Server at http://localhost:50051...");

// Establish HTTP/2 channel to console server
using var channel = GrpcChannel.ForAddress("http://localhost:50051");
var client = new OrderProcessing.OrderProcessingClient(channel);

// 1. Unary RPC Request
Console.WriteLine("\n--> Sending CreateOrder Request...");
var response = await client.CreateOrderAsync(new CreateOrderRequest
{
	AccountId = "ACC-90021",
	Symbol = "MSFT",
	Price = 425.50,
	Quantity = 15
});

Console.WriteLine($"[Client] Order Executed -> ID: {response.OrderId}, Status: {response.Status}");

// 2. Server Streaming RPC Request
Console.WriteLine("\n--> Subscribing to StreamOrderStatus...");
using var streamCall = client.StreamOrderStatus(new OrderStatusRequest { OrderId = response.OrderId });

await foreach (var update in streamCall.ResponseStream.ReadAllAsync())
{
	Console.WriteLine($"[Client Stream Receiver] Order: {update.OrderId} | Status: {update.CurrentStatus} | Time: {update.Timestamp}");
}

Console.WriteLine("\nStreaming finished. Press Enter to exit.");
Console.ReadLine();