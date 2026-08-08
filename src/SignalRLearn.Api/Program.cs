using System.Text.Json.Serialization;
using Scalar.AspNetCore;
using SignalRLearn.Api.Hubs;
using SignalRLearn.Api.Services;

var builder = WebApplication.CreateBuilder(args);
var clientOrigin = builder.Configuration["ClientOrigin"] ?? "https://localhost:7005";

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();
builder.Services
    .AddSignalR()
    .AddJsonProtocol(options =>
        options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IOrderStore, InMemoryOrderStore>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("Client", policy => policy
        .WithOrigins(clientOrigin)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options => options
        .WithTitle("SignalR Orders API")
        .DisableAgent());
}

app.UseHttpsRedirection();
app.UseCors("Client");
app.UseAuthorization();

app.MapControllers();
app.MapHub<OrderHub>("/hubs/orders");
app.MapGet("/", () => Results.Ok(new
{
    name = "SignalR Orders API",
    api = "/api/orders",
    hub = "/hubs/orders",
    documentation = "/scalar"
}));

app.Run();

public partial class Program;
