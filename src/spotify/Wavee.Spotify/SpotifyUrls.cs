namespace Wavee.Spotify;

public static class SpotifyUrls
{
    public static class Public
    {
        private const string Base = "https://api.spotify.com/v1";

        public const string Me = Base + "/me";
    }

    public static class Auth
    {
        private const string Base = "https://accounts.spotify.com";

        public const string Token = Base + "/api/token";
    }

    public static class Login
    {
        private const string Base = "https://login5.spotify.com";

        public const string LoginV3 = Base + "/v3/login";
    }

    public static class Track
    {
        private const string Base = "https://spclient.com";

        public static string Get(string base16Id) => Base + "/metadata/4/track/" + base16Id + "?market=from_token";
    }

    public static class Episode
    {
        private const string Base = "https://spclient.com";

        public static string Get(string base16Id) => Base + "/metadata/4/episode/" + base16Id + "?market=from_token";
    }
}