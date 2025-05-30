using Chatty.Api.Data.Entities;
using System.Threading.Channels;

namespace Chatty.Api.Services;
public interface IChatMessageQueue
{
    void Enqueue(ChatMessage message);
    ChannelReader<ChatMessage> Reader { get; }
}

public class ChatMessageQueue : IChatMessageQueue
{
    private readonly Channel<ChatMessage> _channel = Channel.CreateUnbounded<ChatMessage>();

    public void Enqueue(ChatMessage message)
    {
        _channel.Writer.TryWrite(message);
    }

    public ChannelReader<ChatMessage> Reader => _channel.Reader;
}