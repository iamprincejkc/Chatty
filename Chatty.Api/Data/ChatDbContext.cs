using Chatty.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Chatty.Api.Data;


public class ChatDbContext : DbContext
{
    public ChatDbContext(DbContextOptions<ChatDbContext> options)
        : base(options) { }

    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<AgentSession> AgentSessions => Set<AgentSession>();
}