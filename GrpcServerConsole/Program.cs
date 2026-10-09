using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using GrpcServerConsole.Services;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(opt =>
{
	opt.ListenLocalhost(50051, o => o.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2);
});

builder.Services.AddGrpc();

var app = builder.Build();

app.MapGrpcService<OrderServiceImpl>();

Console.WriteLine("===========================================");
Console.WriteLine(" gRPC Server Console running on :50051 ");
Console.WriteLine("===========================================");

await app.RunAsync();