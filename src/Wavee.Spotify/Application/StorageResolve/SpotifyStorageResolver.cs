using System.Net.Http.Headers;
using Eum.Spotify.storage;
using Google.Protobuf;
using Mediator;
using Wavee.Infrastructure.Playback.Decrypt;
using Wavee.Spotify.Application.AudioKeys.QueryHandlers;
using Wavee.Spotify.Application.Decrypt;
using Wavee.Spotify.Application.Playback.Queries;

namespace Wavee.Spotify.Application.StorageResolve;

internal sealed class SpotifyStorageResolver : ISpotifyStorageResolver
{
    private readonly HttpClient _httpClient;
    private readonly IMediator _mediator;

    public SpotifyStorageResolver(IHttpClientFactory httpClientFactory, IMediator mediator)
    {
        _mediator = mediator;
        _httpClient = httpClientFactory.CreateClient(Constants.SpotifyRemoteStateHttpClietn);
    }

    public ValueTask<SpotifyStreamingFile> GetStorageFile(ByteString fileFileId, CancellationToken cancellationToken)
    {
        //TODO: Caching of chunks
        return new ValueTask<SpotifyStreamingFile>(GetFromCdn(fileFileId, cancellationToken));
    }

    private async Task<SpotifyStreamingFile> GetFromCdn(ByteString fileFileId, CancellationToken cancellationToken)
    {
        const string url = "https://spclient.com/storage-resolve/files/audio/interactive/{0}";
        var hexId = SpotifyGetAudioKeyQueryHandler.ToBase16(fileFileId.ToByteArray());
        var audioUrl = string.Format(url, hexId);
        using var request = new HttpRequestMessage(HttpMethod.Get, audioUrl);
        //accept protobuf
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/protobuf"));
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var res = StorageResolveResponse.Parser.ParseFrom(stream);
        var cdnUrl = res.Cdnurl.First();

        var firstChunkLength = SpotifyUnoffsettedStream.ChunkSize - 1;
        using var firstChunkRequest = new HttpRequestMessage(HttpMethod.Get, cdnUrl);
        firstChunkRequest.Headers.Range = new RangeHeaderValue(0, firstChunkLength);
        using var firstChunkResponse = await _httpClient.SendAsync(firstChunkRequest, cancellationToken);
        firstChunkResponse.EnsureSuccessStatusCode();
        var firstChunk = await firstChunkResponse.Content.ReadAsByteArrayAsync(cancellationToken);
        var totalSize = firstChunkResponse.Content.Headers.ContentRange.Length.Value;
        return new SpotifyStreamingFile(
            totalSize: totalSize,
            cdnUrl: cdnUrl,
            firstChunk: firstChunk,
            mediator: _mediator,
            fileId: fileFileId
        );
    }
}

public interface ISpotifyStorageResolver
{
    ValueTask<SpotifyStreamingFile> GetStorageFile(ByteString fileFileId, CancellationToken cancellationToken);
    //   ValueTask<string> GetStreamingUrl(ByteString fileFileId, CancellationToken cancellationToken);
}

public sealed class SpotifyStreamingFile
{
    private readonly ByteString _fileId;
    private readonly string _cdnUrl;
    private readonly byte[] _firstChunk;
    private readonly IMediator _mediator;

    public SpotifyStreamingFile(long totalSize,
        string cdnUrl,
        ByteString fileId,
        byte[] firstChunk,
        IMediator mediator)
    {
        TotalSize = totalSize;
        _cdnUrl = cdnUrl;
        _firstChunk = firstChunk;
        _mediator = mediator;
        _fileId = fileId;
    }

    public long TotalSize { get; }

    public ValueTask<byte[]> GetChunk(int index, CancellationToken cancellationToken)
    {
        if (index is 0) return new ValueTask<byte[]>(_firstChunk);

        return _mediator.Send(new SpotifyGetChunkQuery
        {
            FileId = _fileId,
            CdnUrl = _cdnUrl,
            Index = index
        }, cancellationToken);
    }
}