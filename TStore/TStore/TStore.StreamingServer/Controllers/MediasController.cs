using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TStore.StreamingServer.Controllers
{
    [ApiController]
    [Route("api/medias")]
    public class MediasController : ControllerBase
    {
        private readonly ILogger<MediasController> _logger;

        public MediasController(ILogger<MediasController> logger)
        {
            _logger = logger;
        }

        [HttpGet("{fileName}")]
        public IActionResult GetStream([FromRoute] string fileName)
        {
            var output = @"D:\Learning\Kafka\KafkaSource\TStore\TStore\TStore.StreamingServer\bin\Debug\net6.0\output";
            fileName = $@"{output}\{fileName}";
            if (System.IO.File.Exists(fileName))
            {
                FileStream stream = System.IO.File.OpenRead(fileName);

                return File(stream, "application/x-mpegURL");
            }

            return NotFound();
        }
    }
}
