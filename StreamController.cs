using Microsoft.AspNetCore.Mvc;
using PiperPicker.Proxies;

namespace piperpicker;

[ApiController]
public class StreamController(FfmpegStreamService ffmpeg) : ControllerBase
{
    [HttpGet("/stream/{stationId}.aac")]
    public async Task Get(string stationId, CancellationToken requestAborted)
    {
        var source = ffmpeg.TryAttach(stationId, requestAborted, out var token);
        if (source is null)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        Response.ContentType = "audio/aac";
        Response.Headers.CacheControl = "no-store";

        try
        {
            await source.CopyToAsync(Response.Body, token);
        }
        catch (OperationCanceledException) { }
        catch (IOException) { }
    }
}