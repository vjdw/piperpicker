using System.Diagnostics.CodeAnalysis;

namespace PiperPicker.Proxies;

public partial class MopidyProxy
{
    /// <summary>
    /// Returns true if _mopidyCurrentTrack is a BBC Radio stream.
    /// </summary>
    private bool IsMopidyCurrentTrackBbcRadioStream([NotNullWhen(true)] out MopidyItem? matchingStreamConfiguration)
    {
        matchingStreamConfiguration = null;

        if (_mopidyCurrentTrack == null)
            return false;

        matchingStreamConfiguration = _radioSelectorOptions.RadioStreams.FirstOrDefault(stream => stream.Uri == _mopidyCurrentTrack.Uri);
        if (matchingStreamConfiguration == null)
            return false;

        return !string.IsNullOrEmpty(matchingStreamConfiguration.BbcStationId);
    }

    private async Task MonitorBbcTrackInformationTask(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // BBC Sounds polls every 30 seconds, so this does the same.
                await Task.Delay(30000, cancellationToken);

                if (IsMopidyCurrentTrackBbcRadioStream(out var bbcRadioStreamMopidyItem))
                {
                    await UpdateMopidyNowPlayingStateFromBbc(cancellationToken, bbcRadioStreamMopidyItem, includeTrackDescription: _mopidyNowPlayingState.IsPlaying);
                    RaiseEvent(_mopidyNowPlayingState);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation($"Stopping {nameof(MonitorBbcTrackInformationTask)}.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing BBC Track Information.");
            }
        }
    }

    private async Task<MopidyNowPlayingState> UpdateMopidyNowPlayingStateFromBbc(CancellationToken cancellationToken, MopidyItem bbcRadioStreamMopidyItem, bool includeTrackDescription)
    {
        BbcNowPlayingTitles? bbcTrackTitles = null;
        if (includeTrackDescription)
        {
            var bbcNowPlayingUrl = $"https://rms.api.bbc.co.uk/v2/services/{bbcRadioStreamMopidyItem.BbcStationId}/segments/latest?experience=domestic&offset=0&limit=1";
            var bbcNowPlaying = await _client.GetFromJsonAsync<BbcNowPlaying>(bbcNowPlayingUrl, cancellationToken);
            bbcTrackTitles = bbcNowPlaying?.Data.FirstOrDefault()?.Titles;
            if (bbcTrackTitles == null)
                _logger.LogWarning($"No BBC radio track titles found for '{bbcNowPlayingUrl}'");
        }

        return _mopidyNowPlayingState with
        {
            TrackName = bbcRadioStreamMopidyItem.Name.TrimStart('*', ' '), // Favourite stations have * prefix.
            TrackDescription = bbcTrackTitles == null
                ? ""
                : string.Join(" - ", ((List<string?>)[bbcTrackTitles?.Primary, bbcTrackTitles?.Secondary, bbcTrackTitles?.Tertiary]).Where(title => !string.IsNullOrWhiteSpace(title)))
        };
    }

    private record BbcNowPlaying(IList<BbcNowPlayingDatum> Data);

    private record BbcNowPlayingDatum(BbcNowPlayingTitles Titles, BbcNowPlayingOffset Offset, string Image_url);

    private record BbcNowPlayingTitles(string Primary, string Secondary, string Tertiary, string Entity_title);

    private record BbcNowPlayingOffset(int Start, int End, string Label, bool Now_playing);
}