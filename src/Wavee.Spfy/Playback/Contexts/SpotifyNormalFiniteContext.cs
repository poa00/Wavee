using System.Security.Cryptography;
using Eum.Spotify.context;

namespace Wavee.Spfy.Playback.Contexts;

internal sealed class SpotifyNormalFiniteContext : SpotifyPagedContext
{
    public SpotifyNormalFiniteContext(Guid connectionId,
        Context context,
        Func<SpotifyId, CancellationToken, Task<WaveeStream>> createSpotifyStream)
        : base(
            connectionId: connectionId, context: context, createSpotifyStream: createSpotifyStream)
    {
    }

    protected override ValueTask<IReadOnlyCollection<ContextPage>> GetAllPages()
    {
        throw new NotImplementedException();
    }

    public override async ValueTask<bool> MoveToRandom()
    {
        // within activePage
        // get random track
        var activePage = base._currentPageNode;
        if (activePage is null)
        {
            var nextPage = await base.NextPage();
            if (nextPage.IsNone)
            {
                return false;
            }
        }

        activePage = base._currentPageNode;
        if (activePage is null)
        {
            return false;
        }

        var tracksLength = activePage.Value.Tracks.Count;
        if (tracksLength is 0 or 1) 
            return false;
        var randomNumber = RandomNumberGenerator.GetInt32(0, tracksLength);
        var moved = await MoveTo(randomNumber);
        return moved;
    }
}