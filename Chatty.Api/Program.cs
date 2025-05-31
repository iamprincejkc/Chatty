using Chatty.Api.Contracts;
using Chatty.Api.Data;
using Chatty.Api.Hubs;
using Chatty.Api.Services;
using FastEndpoints;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(policy => policy.AddDefaultPolicy(policy=> policy.WithOrigins("http://127.0.0.1:5500").AllowCredentials().AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddDbContext<ChatDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddFastEndpoints();
builder.Services.AddSignalR();
builder.Services.AddOpenApi();

builder.Services.AddSingleton<IAgentSessionTracker, AgentSessionTracker>();
builder.Services.AddSingleton<IChatMessageQueue, ChatMessageQueue>();
builder.Services.AddHostedService<ChatSaveWorker>();
builder.Services.AddHostedService<AgentCleanupService>();

var app = builder.Build();

app.UseFastEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
app.UseCors();
app.MapHub<ChatHub>("/chat-hub");

app.UseHttpsRedirection();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
    db.Database.Migrate();
}
app.Run();