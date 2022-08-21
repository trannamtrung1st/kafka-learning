using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TStore.MediaStreamingServer.Controllers
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
            if (System.IO.File.Exists(fileName))
            {
                System.IO.FileStream stream = System.IO.File.OpenRead(
                    $@"D:\Learning\Kafka\KafkaSource\TStore\TStore\TStore\bin\Debug\netcoreapp3.1\output\{fileName}");

                return File(stream, "application/x-mpegURL");
            }

            return NotFound();
        }
    }
}
