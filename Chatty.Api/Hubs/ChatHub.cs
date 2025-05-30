using Chatty.Api.Data.Entities;
using Chatty.Api.Services;
using Microsoft.AspNetCore.SignalR;

namespace Chatty.Api.Hubs;

public class ChatHub:Hub
{
    private readonly IChatMessageQueue _queue;

    public ChatHub(IChatMessageQueue queue)
    {
        _queue = queue;
    }

    public async Task SendMessage(string user, string message)
    {
        var chatMessage = new ChatMessage
        {
            User = user,
            Message = message,
            SentAt = DateTime.UtcNow
        };

        _queue.Enqueue(chatMessage);

        await Clients.All.SendAsync("ReceiveMessage", user, message);
    }
}
