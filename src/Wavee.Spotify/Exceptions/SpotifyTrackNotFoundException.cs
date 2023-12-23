namespace Wavee.Spotify.Exceptions;

public sealed class SpotifyTrackNotFoundException : Exception
{
    public SpotifyTrackNotFoundException(string trackId, HttpRequestException httpRequestException) : base($"Track with id {trackId} not found.", httpRequestException)
    {
        
    }
}