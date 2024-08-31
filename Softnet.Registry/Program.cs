using Softnet.Registry.Services;
using Softnet.Registry.Core;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using System.Net.Http;

namespace Softnet.Registry
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.WebHost.ConfigureKestrel(serverOptions =>
            {
                // Configure HTTP/2 keep-alive pings globally
                serverOptions.Limits.Http2.KeepAlivePingDelay = TimeSpan.FromSeconds(30);   // Interval for pings
                serverOptions.Limits.Http2.KeepAlivePingTimeout = TimeSpan.FromSeconds(10); // Timeout for ping responses                                                                                            
            });

            // Add services to the container.
            builder.Services.AddGrpc();
            
            builder.Services.AddHttpContextAccessor();

            builder.Services.AddSingleton<Controller>();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            app.MapGrpcService<RegistryAPI>();
            app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

            app.Run();
        }
    }
}