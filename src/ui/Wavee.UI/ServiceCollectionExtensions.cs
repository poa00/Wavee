﻿using Eum.Spotify.connectstate;
using Microsoft.Extensions.DependencyInjection;
using Wavee.Players.NAudio;
using Wavee.Spotify;
using Wavee.Spotify.Application.Authentication.Modules;
using Wavee.UI.Features.Identity.ViewModels;
using Wavee.UI.Features.Library.DataAcces;
using Wavee.UI.Features.Library.ViewModels;
using Wavee.UI.Features.Library.ViewModels.Artist;
using Wavee.UI.Features.Listen;
using Wavee.UI.Features.MainWindow;
using Wavee.UI.Features.NowPlaying.ViewModels;
using Wavee.UI.Features.Shell.ViewModels;

namespace Wavee.UI;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWaveeUI(this IServiceCollection collection,
        FetchRedirectUrlDelegate openBrowser,
        string storagePath)
    {
        return collection.AddSpotify(new SpotifyClientConfig
        {
            Storage = new StorageSettings
            {
                Path = storagePath
            },
            Remote = new SpotifyRemoteConfig
            {
                DeviceName = "temp",
                DeviceType = DeviceType.Unknown
            },
            Playback = new SpotifyPlaybackConfig
            {
                InitialVolume = 0,
                PreferedQuality = SpotifyAudioQuality.Normal
            }
        })
            .WithStoredOrOAuthModule(openBrowser)
            .WithPlayer<NAudioPlayer>()
            .AddScoped<ILibraryRepository, LibraryRepository>();
    }

    public static IServiceCollection AddViewModels(this IServiceCollection collection)
    {
        return collection
            .AddScoped<MainWindowViewModel>()
            .AddScoped<ShellViewModel>()
            .AddScoped<IdentityViewModel>()
            .AddScoped<ListenViewModel>()
            .AddScoped<LibrariesViewModel>()
            .AddScoped<LibrarySongsViewModel>()
            .AddScoped<LibraryAlbumsViewModel>()
            .AddScoped<LibraryArtistsViewModel>()
            .AddScoped<LibraryPodcastsViewModel>()
            .AddScoped<NowPlayingViewModel>();
    }
}