using Chatty.Api.Data;
using Chatty.Api.Data.Entities;
using System.Threading.Channels;

namespace Chatty.Api.Services;

public class ChatSaveWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IChatMessageQueue _queue;
    private readonly ILogger<ChatSaveWorker> _logger;

    public ChatSaveWorker(IServiceScopeFactory scopeFactory, IChatMessageQueue queue, ILogger<ChatSaveWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _queue = queue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

            db.ChatMessages.Add(message);
            await db.SaveChangesAsync(stoppingToken);

            _logger.LogInformation($"Saved message from {message.User} at {message.SentAt}");
        }
    }
}