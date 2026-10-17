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
        //await Task.Delay(1000, requestAborted); // xyzzy could be reduced?
        
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