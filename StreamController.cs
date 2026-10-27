using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PiperPicker.Models;
using PiperPicker.Proxies;

namespace piperpicker;

[ApiController]
public class StreamController(FfmpegStreamService ffmpeg, IOptions<RadioSelectorOptions> radioSelectorOptions) : ControllerBase
{
    [HttpGet("/stream/{stationId}.aac")]
    public async Task Get(string stationId, CancellationToken requestAborted)
    {
        var streamMopidyItem = radioSelectorOptions.Value.RadioStreams.FirstOrDefault(s => s.BbcStationId == stationId);
        if (streamMopidyItem?.ExternalUri == null)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }
        
        ffmpeg.Start(stationId, streamMopidyItem.ExternalUri);
        
        var subscription = ffmpeg.TrySubscribe(stationId);
        if (subscription is null)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        Response.ContentType = "audio/aac";
        Response.Headers.CacheControl = "no-store";
        HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

        var (backlog, live, unsubscribe) = subscription.Value;
        try
        {
            foreach (var chunk in backlog)
                await Response.Body.WriteAsync(chunk, requestAborted);

            await foreach (var chunk in live.ReadAllAsync(requestAborted))
                await Response.Body.WriteAsync(chunk, requestAborted);
        }
        catch (OperationCanceledException) { }
        catch (IOException) { }
        finally { unsubscribe(); }
    }
}