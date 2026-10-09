# .NET 10 gRPC Console Demo

Server and client console apps built with **.NET 10** and **gRPC**. Also a quick-revision sheet for gRPC and .NET interviews.

---

## 1. Project Structure

```
dotnet-grpc-console-demo/
├── GrpcServer/      # ASP.NET Core gRPC server (Kestrel, HTTP/2)
│   ├── Protos/      # .proto contract
│   ├── Services/    # Service implementations
│   └── Program.cs
└── GrpcClient/      # Console client (Grpc.Net.Client)
    ├── Protos/      # Same .proto (client mode)
    └── Program.cs
```

## 2. Run

```bash
# Terminal 1
cd GrpcServer && dotnet run
# Terminal 2
cd GrpcClient && dotnet run
```

Requires the .NET 10 SDK (`dotnet --version`).

---

## 3. gRPC Basics

- **gRPC** is a high-performance, open-source RPC framework from Google.
- **Transport:** HTTP/2.
- **Serialization:** Protocol Buffers (binary).
- **Contract-first:** the `.proto` file is the source of truth.
- **Code generation:** `Grpc.Tools` generates the C# client and server base classes at build time.
- **Cross-language:** C#, Java, Go, Python, Node, etc.

### Why gRPC over REST

| Feature | gRPC | REST (JSON) |
|---|---|---|
| Protocol | HTTP/2 | HTTP/1.1 (usually) |
| Payload | Protobuf (binary, small) | JSON (text, larger) |
| Contract | Strict `.proto` | Optional (OpenAPI) |
| Streaming | Native, 4 types | Hard (SSE/WebSockets) |
| Browser support | Needs gRPC-Web | Native |
| Speed | Faster | Slower |
| Human-readable | No | Yes |
| Codegen | Built-in | Third-party |

### HTTP/2 features used

- Multiplexing: many calls over one connection.
- Binary framing.
- Header compression (HPACK).
- Full-duplex streaming.
- Server push (not used by gRPC).

---

## 4. Four Communication Patterns

| Type | Client | Server | Use case |
|---|---|---|---|
| **Unary** | 1 request | 1 response | Normal call (get by id) |
| **Server streaming** | 1 request | N responses | Live feed, large download |
| **Client streaming** | N requests | 1 response | Upload, batch aggregate |
| **Bidirectional streaming** | N requests | N responses | Chat, real-time sync |

```proto
service Greeter {
  rpc SayHello (HelloRequest) returns (HelloReply);                       // Unary
  rpc ListUpdates (Req) returns (stream Update);                          // Server streaming
  rpc Upload (stream Chunk) returns (Summary);                            // Client streaming
  rpc Chat (stream Msg) returns (stream Msg);                             // Bidirectional
}
```

---

## 5. Protocol Buffers (.proto)

```proto
syntax = "proto3";
option csharp_namespace = "GrpcServer";
package greet;

message HelloRequest {
  string name = 1;
  repeated string tags = 2;   // list
  map<string,string> meta = 3;
}
message HelloReply {
  string message = 1;
}
```

- **Field numbers** (`= 1`) identify fields on the wire. Never change or reuse them.
- **Scalar types:** `string`, `int32`, `int64`, `bool`, `double`, `float`, `bytes`.
- **Collections:** `repeated` is a list, `map<k,v>` is a dictionary.
- **Well-known types:** `google.protobuf.Timestamp`, `Duration`, `Empty`, `Any`, `Struct`.
- **Enums:** the first value must be `0`.
- **Nullable:** use wrapper types (`google.protobuf.StringValue`) or `optional`.
- **Default values:** there is no null for scalars (string is `""`, int is `0`).
- **Backward compatibility:** add new fields with new numbers and mark removed ones `reserved`.
- **Mapping:** `string` to `string`, `int32` to `int`, `int64` to `long`, `bytes` to `ByteString`, `repeated` to `RepeatedField<T>`, `Timestamp` to `Google.Protobuf.WellKnownTypes.Timestamp`.

---

## 6. Server Side (.NET 10)

**Packages:** `Grpc.AspNetCore`

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddGrpc();
// builder.Services.AddGrpcReflection();   // optional, for grpcurl / Postman

var app = builder.Build();
app.MapGrpcService<GreeterService>();
// app.MapGrpcReflectionService();
app.Run();
```

```csharp
public class GreeterService : Greeter.GreeterBase
{
    public override Task<HelloReply> SayHello(HelloRequest request, ServerCallContext context)
        => Task.FromResult(new HelloReply { Message = $"Hello {request.Name}" });

    public override async Task ListUpdates(Req req, IServerStreamWriter<Update> stream, ServerCallContext ctx)
    {
        for (int i = 0; i < 5 && !ctx.CancellationToken.IsCancellationRequested; i++)
        {
            await stream.WriteAsync(new Update { Value = i });
            await Task.Delay(500, ctx.CancellationToken);
        }
    }
}
```

- **.csproj:** `<Protobuf Include="Protos\greet.proto" GrpcServices="Server" />`
- **Kestrel:** needs HTTP/2. On Windows and Linux, TLS (HTTPS) is the default for HTTP/2. Plain HTTP/2 (h2c) needs `Protocols = Http2` on the endpoint.
- **DI:** services are **scoped per call** by default and support constructor injection.
- **`ServerCallContext`** gives access to metadata, deadline, cancellation token, peer, auth context, and status.

## 7. Client Side (.NET 10)

**Packages:** `Grpc.Net.Client`, `Google.Protobuf`, `Grpc.Tools`

```csharp
using var channel = GrpcChannel.ForAddress("https://localhost:5001");
var client = new Greeter.GreeterClient(channel);

// Unary
var reply = await client.SayHelloAsync(new HelloRequest { Name = "Nikhil" });

// Server streaming
using var call = client.ListUpdates(new Req());
await foreach (var u in call.ResponseStream.ReadAllAsync())
    Console.WriteLine(u.Value);

// Client streaming
using var up = client.Upload();
await up.RequestStream.WriteAsync(new Chunk { /*...*/ });
await up.RequestStream.CompleteAsync();
var summary = await up;

// Bidirectional
using var chat = client.Chat();
var reader = Task.Run(async () => {
    await foreach (var m in chat.ResponseStream.ReadAllAsync()) Console.WriteLine(m.Text);
});
await chat.RequestStream.WriteAsync(new Msg { Text = "hi" });
await chat.RequestStream.CompleteAsync();
await reader;
```

- **.csproj:** `<Protobuf Include="Protos\greet.proto" GrpcServices="Client" />`
- **`GrpcChannel`** is expensive and thread-safe. Create once and reuse it. Clients are cheap and also thread-safe.
- **DI integration:** `services.AddGrpcClient<Greeter.GreeterClient>(o => o.Address = new Uri("..."));` (package `Grpc.Net.ClientFactory`). It uses `HttpClientFactory` under the hood.

---

## 8. Key Concepts

### Deadlines and Cancellation
```csharp
await client.SayHelloAsync(req, deadline: DateTime.UtcNow.AddSeconds(5));
// Exceeds deadline => StatusCode.DeadlineExceeded
```
Deadlines propagate across service calls. Always set them.

### Metadata (headers and trailers)
```csharp
var headers = new Metadata { { "authorization", "Bearer <token>" } };
await client.SayHelloAsync(req, headers);
```

### Error Handling
```csharp
try { ... }
catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound) { ... }
// Server: throw new RpcException(new Status(StatusCode.InvalidArgument, "bad input"));
```

Common status codes:

| Code | Meaning |
|---|---|
| `OK` | Success |
| `Cancelled` | Caller cancelled |
| `InvalidArgument` | Bad request |
| `DeadlineExceeded` | Timeout |
| `NotFound` | Resource missing |
| `AlreadyExists` | Duplicate |
| `PermissionDenied` | Authenticated but forbidden |
| `Unauthenticated` | No or invalid credentials |
| `ResourceExhausted` | Quota or rate limit |
| `Unimplemented` | Method not supported |
| `Internal` | Server bug |
| `Unavailable` | Transient, retry |

### Interceptors
Like middleware for gRPC. Derive from `Interceptor` and override `UnaryServerHandler` or `AsyncUnaryCall`. Use for logging, auth, metrics, and exception handling.
```csharp
builder.Services.AddGrpc(o => o.Interceptors.Add<LoggingInterceptor>());
```

### Security
- **TLS:** HTTPS by default. Use `https://` for the channel address.
- **Auth:** JWT bearer, client certificates (mTLS), and cookies are all supported through standard ASP.NET Core auth. Add `[Authorize]` on the service or method.
- **Call credentials:** attach tokens per call using `CallCredentials`.

### Retries and Resilience
Configure `ServiceConfig` with `RetryPolicy` on the channel. It retries on `Unavailable`, with exponential backoff. Also supports **hedging**.

### Health Checks
`Grpc.AspNetCore.HealthChecks`. Implements the standard `grpc.health.v1`, used by Kubernetes and load balancers.

### Load Balancing
- **Client-side:** `DnsResolver`, `StaticResolver`, `RoundRobinBalancer` via `GrpcChannelOptions` and `ServiceConfig`.
- **Proxy-side:** use an L7 balancer that understands HTTP/2 (Envoy, NGINX, YARP).
- **Why:** HTTP/2 uses one long-lived connection, so an L4 balancer sends all calls to one server.

### Reflection
`Grpc.AspNetCore.Server.Reflection` lets tools like **grpcurl**, **Postman**, and **grpcui** discover services without the `.proto` file.

### gRPC-Web
Browsers can't use raw HTTP/2 gRPC. Use `Grpc.AspNetCore.Web` plus `app.UseGrpcWeb()`. It supports unary and server streaming only.

### JSON Transcoding (.NET 7+)
`Microsoft.AspNetCore.Grpc.JsonTranscoding` exposes gRPC services as REST/JSON APIs via `google.api.http` annotations. It also supports OpenAPI/Swagger.

### Compression
gzip is supported. Configure it with `ResponseCompressionAlgorithm`, or per call with `WriteOptions`.

### Message Size Limits
Default max receive size is **4 MB** on the server. Change it with `MaxReceiveMessageSize` and `MaxSendMessageSize`.

### Keep-Alive
Configure `SocketsHttpHandler` (`KeepAlivePingDelay`, `KeepAlivePingTimeout`, `EnableMultipleHttp2Connections`) for long-lived streams.

---

## 9. Testing and Tools

- **grpcurl:** curl for gRPC (`grpcurl -plaintext localhost:5000 list`).
- **Postman:** native gRPC support.
- **Unit testing:** call service methods directly with a mocked `ServerCallContext` (`TestServerCallContext`).
- **Integration testing:** `WebApplicationFactory<Program>` + `GrpcChannel.ForAddress(..., new GrpcChannelOptions { HttpHandler = factory.Server.CreateHandler() })`.

---

## 10. .NET 10 Key Points

- **.NET 10** is an **LTS** release (Nov 2025), supported for 3 years. It ships with **C# 14**.
- **Runtime:** better JIT (inlining, devirtualization, stack allocation of more objects), improved GC, and AVX10.2 support.
- **C# 14:** `field` keyword in properties, extension members (extension properties and static extensions), null-conditional assignment (`a?.b = x`), `nameof` on unbound generics, implicit `Span<T>` conversions, lambda parameter modifiers without types.
- **ASP.NET Core 10:** built-in validation for Minimal APIs, improved OpenAPI (3.1) generation, Server-Sent Events support, passkey support in Identity, Blazor improvements, and better metrics.
- **File-based apps:** run a single `.cs` file with `dotnet run app.cs`. No project file needed.
- **Solution format:** `.slnx` is the new XML-based solution format (replaces the classic `.sln`).
- **EF Core 10:** LINQ improvements, better JSON and complex-type support, vector search support.
- **Native AOT:** gRPC supports Native AOT in the .NET 8+ line. Use Protobuf with source-generated serialization only.
- **Unchanged since earlier versions:**
  - Top-level statements and minimal hosting (`WebApplication.CreateBuilder`).
  - Nullable reference types enabled by default.
  - `ImplicitUsings`.

---

## 11. Core .NET Interview Revision

- **CLR:** runtime that does JIT compilation, GC, type safety, and exception handling.
- **IL and JIT:** C# compiles to IL, and the JIT turns it into machine code at runtime. Tiered compilation and PGO are on by default.
- **GC generations:** Gen0 (short-lived), Gen1, Gen2, and LOH (large objects, 85 KB or more). Concurrent and background GC reduce pauses.
- **Value vs reference types:** structs and primitives (stack or inline) vs classes (heap). Boxing and unboxing cost allocations.
- **`async`/`await`:** compiler state machine, no thread blocked during the wait. Avoid `.Result` and `.Wait()` (deadlocks). Use `ConfigureAwait(false)` in libraries.
- **`Task` vs `ValueTask`:** `ValueTask` avoids allocation when a result is often synchronous.
- **DI lifetimes:** Singleton (one for the app), Scoped (one per request or call), Transient (new every time). Avoid injecting scoped services into singletons.
- **Middleware pipeline:** an ordered chain of `Use`, `Run`, and `Map`. Order matters (auth before endpoints).
- **Minimal APIs vs Controllers:** Minimal APIs are lightweight and faster to start. Controllers give filters, model binding conventions, and structure.
- **Configuration:** `appsettings.json`, environment variables, user secrets, and the Options pattern (`IOptions<T>`, `IOptionsSnapshot<T>`, `IOptionsMonitor<T>`).
- **Logging:** `ILogger<T>`, structured logging, providers (Serilog, OpenTelemetry).
- **LINQ:** deferred vs immediate execution. `IEnumerable` runs in memory, `IQueryable` translates to SQL.
- **EF Core:** change tracker, `AsNoTracking`, migrations, N+1 problem (use `Include` or projection), compiled queries.
- **Records:** immutable value-based equality, `with` expressions.
- **Span/Memory:** zero-copy slicing, stack-only `Span<T>`.
- **Generics:** type safety without boxing, with constraints (`where T : class`).
- **SOLID:** Single responsibility, Open/closed, Liskov, Interface segregation, Dependency inversion.
- **`IDisposable`:** release unmanaged resources with `using`. The finalizer is the safety net.

---

## 12. Interview Q&A (gRPC)

**Q: What is gRPC and why use it?**
A high-performance RPC framework using HTTP/2 and Protobuf. It gives smaller payloads, streaming, strong contracts, and codegen. It is ideal for microservice-to-microservice calls.

**Q: gRPC vs REST: when to choose which?**
gRPC for internal, low-latency, streaming, or polyglot services. REST for public APIs, browser-first clients, and simple human-readable debugging.

**Q: Why is gRPC faster?**
Binary Protobuf, HTTP/2 multiplexing, header compression, a persistent connection, and no text parsing.

**Q: What are the four call types?**
Unary, server streaming, client streaming, bidirectional streaming.

**Q: What happens if a field number changes?**
It breaks wire compatibility, because old clients decode data into the wrong fields. Never change or reuse numbers. Use `reserved`.

**Q: How do you handle errors?**
The server throws `RpcException` with a `StatusCode`. The client catches `RpcException`. Rich details are possible with `Google.Rpc.Status`.

**Q: How do you secure gRPC?**
TLS, JWT bearer tokens in metadata, mTLS, and `[Authorize]`.

**Q: How do you call gRPC from a browser?**
gRPC-Web with a proxy or middleware, or JSON transcoding to expose REST.

**Q: Why does load balancing need special care?**
HTTP/2 holds a single long-lived connection, so an L4 balancer pins all calls to one pod. Use an L7 balancer or client-side load balancing.

**Q: What is a deadline and why does it matter?**
It is an absolute time limit for a call that propagates downstream. It prevents hung requests and resource leaks.

**Q: What are interceptors?**
Cross-cutting middleware for gRPC calls (logging, auth, metrics, error mapping).

**Q: Is `GrpcChannel` thread-safe? How should it be used?**
Yes. Create once, reuse, and dispose on shutdown. Creating one per call is a performance anti-pattern.

**Q: How do you test gRPC services?**
Unit test with a fake `ServerCallContext`, and integration test with `WebApplicationFactory`. Use grpcurl or Postman for manual checks.

**Q: Downsides of gRPC?**
Not human-readable, limited browser support, harder debugging, tooling and proxy requirements, and `.proto` versioning discipline.

---

## 13. Quick Cheat Sheet

| Topic | Remember |
|---|---|
| Transport | HTTP/2 |
| Format | Protobuf (binary) |
| Server pkg | `Grpc.AspNetCore` |
| Client pkg | `Grpc.Net.Client` |
| Codegen | `Grpc.Tools` + `<Protobuf Include=... GrpcServices=.../>` |
| Server base | `Greeter.GreeterBase` |
| Client class | `Greeter.GreeterClient` |
| Error type | `RpcException` + `StatusCode` |
| Stream read | `ReadAllAsync()` |
| Stream write | `WriteAsync()` then `CompleteAsync()` |
| Timeout | `deadline:` parameter |
| Auth | Metadata `authorization` header |
| Browser | gRPC-Web or JSON transcoding |
| Max msg | 4 MB default |
| Debug tool | grpcurl / Postman + Reflection |
| .NET 10 | LTS, C# 14, `.slnx`, file-based apps |

---

## 14. References

- https://grpc.io/docs/
- https://learn.microsoft.com/aspnet/core/grpc/
- https://protobuf.dev/
- https://learn.microsoft.com/dotnet/core/whats-new/dotnet-10/overview

---

## 📄 License

This project is licensed under the [**MIT License**](https://github.com/Nikhil767/dotnet-grpc-console-demo/blob/main/LICENSE).

Thank you for reading to the end! 