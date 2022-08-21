using Confluent.Kafka;
using Confluent.Kafka.Admin;
using FFMpegCore;
using FFMpegCore.Extend;
using FFMpegCore.Pipes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TStore
{
    internal class Program
    {
        static Queue<BitmapVideoFrameWrapper> _queue = new Queue<BitmapVideoFrameWrapper>();

        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");

            CreateTopic().Wait();

            for (var i = 0; i < 7; i++)
            {
                var thread = new Thread(() => Listen(i));
                thread.IsBackground = true;
                thread.Start();
            }

            Transcode();
        }

        static async Task CreateTopic()
        {

            var adminConfig = new AdminClientConfig
            {
                BootstrapServers = "localhost:9092",
                ClientId = "Stream2"
            };
            var admin = new AdminClientBuilder(adminConfig).Build();

            if (!admin.GetMetadata(TimeSpan.FromSeconds(10)).Topics.Any(t => t.Topic == "stream"))
            {
                await admin.CreateTopicsAsync(new[]
                {
                    new TopicSpecification()
                    {
                        NumPartitions = 7,
                        Name = "stream"
                    }
                });
            }

        }

        static void Listen(int i)
        {
            var config = new ConsumerConfig
            {
                BootstrapServers = "localhost:9092",
                GroupId = "processor",
                AutoOffsetReset = AutoOffsetReset.Latest,
            };

            bool cancelled = false;

            using (var consumer = new ConsumerBuilder<string, string>(config).Build())
            {
                consumer.Subscribe("stream");

                while (!cancelled)
                {
                    var message = consumer.Consume();
                    var videoData = message.Message.Value;
                    var bytes = Convert.FromBase64String(videoData);
                    var bitmap = new Bitmap(Image.FromStream(new MemoryStream(bytes)));
                    Graphics g = Graphics.FromImage(bitmap);
                    g.DrawString($"{DateTime.Now}", new Font(FontFamily.GenericSerif, 20), new SolidBrush(Color.White), new Point(0, 0));
                    _queue.Enqueue(new BitmapVideoFrameWrapper(bitmap));
                    Console.WriteLine($"Processed {i}");
                }

                consumer.Close();
            }
        }

        static void Transcode()
        {
            GlobalFFOptions.Configure(opt => opt.BinaryFolder = "./ffmpeg");
            var output = "./output";
            if (Directory.Exists(output))
            {
                Directory.Delete("./output", true);
            }
            Directory.CreateDirectory("./output");
            //string inputFile = "./video.mp4";
            string ffmpegOutput = "./output/index.m3u8";
            using (File.Create(ffmpegOutput)) { }
            string[] line =
            {
                "#EXTM3U",
                "#EXT-X-VERSION:3",
                "#EXT-X-STREAM-INF:BANDWIDTH=420000,RESOLUTION=320x240",
                "240p.m3u8"
            };
            File.WriteAllLines(ffmpegOutput, line);

            //Command  
            string conversionArgs = string.Format(
                "-hide_banner -y" +
                " -vf scale=w=320:h=240:force_original_aspect_ratio=decrease -c:a aac -ar 48000 -c:v libx264 -pix_fmt yuv420p -crf 23 -sc_threshold 0 -g 48 -keyint_min 48 -hls_time 3 -hls_playlist_type event -b:v 800k -maxrate 856k -bufsize 1200k -b:a 96k");
            //" -vf scale=w=640:h=360:force_original_aspect_ratio=decrease -c:a aac -ar 48000 -c:v vp9 -f webm -crf 23 -sc_threshold 0 -g 48 -keyint_min 48 -hls_playlist_type event -b:v 800k -maxrate 856k -bufsize 1200k -b:a 96k");

            BitmapVideoFrameWrapper lastFrame = null;

            IVideoFrame GetNextFrame()
            {
                if (_queue.TryDequeue(out var videoData))
                {
                    lastFrame = videoData;
                }

                if (lastFrame == null)
                {
                    var color = new Random().Next(256);
                    var bitmap = new Bitmap(320, 240);
                    Graphics g = Graphics.FromImage(bitmap);
                    g.Clear(Color.FromArgb(color, color, color));
                    g.DrawString($"{DateTime.Now}", new Font(FontFamily.GenericSerif, 20), new SolidBrush(Color.White), new Point(0, 0));
                    lastFrame = new BitmapVideoFrameWrapper(bitmap);
                }

                return lastFrame;
            }

            IEnumerable<IVideoFrame> CreateFrames()
            {
                while (true)
                {
                    yield return GetNextFrame(); //method that generates of receives the next frame
                }
            }

            RawVideoPipeSource videoFramesSource = new RawVideoPipeSource(CreateFrames())
            {
                FrameRate = 360 //set source frame rate
            };

            while (true)
            {
                try
                {
                    FFMpegArgumentProcessor processor = FFMpegArguments
                        //.FromPipeInput(videoFramesSource)
                        .FromPipeInput(videoFramesSource)
                        .OutputToFile(ffmpegOutput, true, opt => opt.WithCustomArgument(conversionArgs));

                    processor.ProcessSynchronously();
                }
                catch (Exception) { }
            }
        }
    }
}
