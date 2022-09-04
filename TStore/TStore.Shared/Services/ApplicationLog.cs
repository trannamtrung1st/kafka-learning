using System.Threading.Tasks;

namespace TStore.Shared.Services
{
    public interface IApplicationLog
    {
        Task LogAsync(string message);
    }

    public class ApplicationLog : IApplicationLog
    {
        private readonly IRealtimeNotiService _realtimeNotiService;

        public string Id { get; }

        public ApplicationLog(string id,
            IRealtimeNotiService realtimeNotiService)
        {
            Id = id;
            _realtimeNotiService = realtimeNotiService;
        }

        public Task LogAsync(string message)
        {
            return _realtimeNotiService.NotifyLogAsync(Id, message);
        }
    }
}
