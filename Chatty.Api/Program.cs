using Chatty.Api.Data;
using Chatty.Api.Hubs;
using Chatty.Api.Services;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(policy => policy.AddDefaultPolicy(policy=> policy.WithOrigins("http://127.0.0.1:5500").AllowCredentials().AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddDbContext<ChatDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddFastEndpoints();
builder.Services.AddSignalR();
builder.Services.AddOpenApi();

builder.Services.AddSingleton<IChatMessageQueue, ChatMessageQueue>();
builder.Services.AddHostedService<ChatSaveWorker>();

var app = builder.Build();

app.UseFastEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
app.UseCors();
app.MapHub<ChatHub>("/chat-hub");

app.UseHttpsRedirection();

app.Run();