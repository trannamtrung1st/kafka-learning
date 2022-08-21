using TStore.StreamingServer.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var services = builder.Services;

services.AddControllers();
services.AddSignalR().AddJsonProtocol();

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseDeveloperExceptionPage();

app.UseHttpsRedirection();

app.UseRouting();

app.UseCors(opt =>
{
    opt.AllowAnyHeader()
        .AllowAnyMethod()
        .AllowAnyOrigin();
});

app.UseAuthorization();

app.MapControllers();
app.MapHub<VideoHub>("/video/stream");

var thread = new Thread(() => VideoHub.Transcode());
thread.IsBackground = true;
thread.Start();

app.Run();
