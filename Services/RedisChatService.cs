using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace MVC.Services
{
    public record RedisOptions
    {
        public string Configuration { get; init; } = "localhost:6379";
        public string InstanceName { get; init; } = "MVC:";
    }

    public interface IChatService
    {
        void EnsureUser(string username);
        void SendToUser(string fromUser, string toUser, string text);
        string? ReceiveOneForUser(string username);
        IReadOnlyList<string> GetHistory(string userA, string userB, int take = 50);
    }

    public class RedisChatService : IChatService
    {
        private readonly RedisOptions _options;
        private readonly IConnectionMultiplexer _muxer;
        private readonly IDatabase _db;

        public RedisChatService(IOptions<RedisOptions> options, IConnectionMultiplexer muxer)
        {
            _options = options.Value;
            _muxer = muxer;
            _db = _muxer.GetDatabase();
        }

        private string GetQueueKey(string username) => _options.InstanceName + "queue:" + username.ToLowerInvariant();
        private string GetConversationKey(string userA, string userB)
        {
            var a = userA.ToLowerInvariant();
            var b = userB.ToLowerInvariant();
            if (string.CompareOrdinal(a, b) <= 0)
            {
                return _options.InstanceName + "conv:" + a + ":" + b;
            }
            return _options.InstanceName + "conv:" + b + ":" + a;
        }

        public void EnsureUser(string username)
        {
            // No-op for Redis; list will be created on first push
        }

        public void SendToUser(string fromUser, string toUser, string text)
        {
            var payload = $"{fromUser}|{DateTime.UtcNow:o}|{text}";
            _db.ListRightPush(GetQueueKey(toUser), payload);
            _db.ListRightPush(GetConversationKey(fromUser, toUser), payload);
        }

        public string? ReceiveOneForUser(string username)
        {
            var value = _db.ListLeftPop(GetQueueKey(username));
            return value.IsNull ? null : value.ToString();
        }

        public IReadOnlyList<string> GetHistory(string userA, string userB, int take = 50)
        {
            var key = GetConversationKey(userA, userB);
            // Take last N messages (most recent at end)
            var len = _db.ListLength(key);
            if (len == 0) return Array.Empty<string>();
            var start = Math.Max(0, (int)len - take);
            var values = _db.ListRange(key, start, -1);
            return values.Select(v => v.ToString()).Where(s => s != null).Select(s => s!).ToArray();
        }
    }
}


