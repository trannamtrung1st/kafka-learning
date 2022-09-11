using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using TStore.Shared.Services;
using TStore.SystemApi.Services;

namespace TStore.SystemApi.Controllers
{
    [Route("api/system")]
    [ApiController]
    public class SystemController : ControllerBase
    {
        private readonly IMessageBrokerService _messageBrokerService;
        private readonly IApplicationLog _log;

        public SystemController(IMessageBrokerService messageBrokerService,
            IApplicationLog log)
        {
            _messageBrokerService = messageBrokerService;
            _log = log;
        }

        [HttpDelete("records/{topic}")]
        public async Task<IActionResult> DeleteRecords(string topic)
        {
            await _messageBrokerService.ClearRecordsAsync(topic);

            return NoContent();
        }
    }
}
