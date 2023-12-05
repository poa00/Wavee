﻿using System.Diagnostics;
using Mediator;
using Wavee.Spotify.Common;
using Wavee.Spotify.Common.Contracts;
using Wavee.Spotify.Domain.Common;
using Wavee.Spotify.Domain.Tracks;
using Wavee.UI.Domain.Album;
using Wavee.UI.Domain.Artist;
using Wavee.UI.Features.Artist.Queries;

namespace Wavee.UI.Features.Artist.QueryHandlers;

public sealed class GetAlbumsForArtistQueryHandler : IQueryHandler<GetAlbumsForArtistQuery, ArtistAlbumsResult>
{
    private readonly ISpotifyClient _spotifyClient;

    public GetAlbumsForArtistQueryHandler(ISpotifyClient spotifyClient)
    {
        _spotifyClient = spotifyClient;
    }


    public async ValueTask<ArtistAlbumsResult> Handle(GetAlbumsForArtistQuery query, CancellationToken cancellationToken)
    {
        // Step 1. Fetch albums
        var (albums, total) = await (query.Group switch
        {
            DiscographyGroupType.Album => _spotifyClient.Artist.GetDiscographyAlbumsAsync(SpotifyId.FromUri(query.Id),
                offset: (uint)query.Offset,
                limit: (uint)query.Limit,
                cancellationToken),
            DiscographyGroupType.Single => _spotifyClient.Artist.GetDiscographySinglesAsync(SpotifyId.FromUri(query.Id),
                offset: (uint)query.Offset,
                limit: (uint)query.Limit,
                cancellationToken),
            DiscographyGroupType.Compilation => _spotifyClient.Artist.GetDiscographyCompilationsAsync(SpotifyId.FromUri(query.Id),
                offset: (uint)query.Offset,
                limit: (uint)query.Limit,
                cancellationToken),
            null => _spotifyClient.Artist.GetDiscographyAllAsync(SpotifyId.FromUri(query.Id),
                offset: (uint)query.Offset,
                limit: (uint)query.Limit,
                cancellationToken),
            _ => throw new ArgumentOutOfRangeException()
        });


        IReadOnlyDictionary<SpotifyId, IReadOnlyCollection<SpotifyAlbumTrack>> tracks = new Dictionary<SpotifyId, IReadOnlyCollection<SpotifyAlbumTrack>>();
        if (query.FetchTracks)
        {
            // Step 2. Fetch tracks
            tracks = await GetTracksForAlbums(albums.Select(f => f.Uri), cancellationToken);
        }

        return new ArtistAlbumsResult
        {
            Total = total,
            Albums = albums.Select(f => new SimpleAlbumEntity()
            {
                Images = f.Images,
                Name = f.Name,
                Id = f.Uri.ToString(),
                Tracks = tracks.TryGetValue(f.Uri, out var tr) ? tr
                    .Select(x => new AlbumTrackEntity()
                    {
                        Duration = x.Duration,
                        Id = x.Uri.ToString(),
                        Name = x.Name,
                        PlayCount = x.PlayCount
                    })
                    .ToArray() : null,
                Year = (ushort)f.ReleaseDate.Year,
                Type = f.Type
            }).ToArray()
        };
    }

    private async Task<IReadOnlyDictionary<SpotifyId, IReadOnlyCollection<SpotifyAlbumTrack>>> GetTracksForAlbums(IEnumerable<SpotifyId> select, CancellationToken cancellationToken)
    {
        //var tracks = new Dictionary<SpotifyId, IReadOnlyCollection<SpotifyAlbumTrack>>();
        var tracks = select.ToDictionary(x => x,
            x => Task.Run(async () => await _spotifyClient.Album.GetTracks(x, cancellationToken), cancellationToken));
        await Task.WhenAll(tracks.Values);
        // foreach (var album in select)
        // {
        //     var albumTracks = await _spotifyClient.Album.GetTracks(album, cancellationToken);
        //     tracks.Add(album, albumTracks);
        // }
        return tracks.ToDictionary(x => x.Key, x => x.Value.Result);
    }
}