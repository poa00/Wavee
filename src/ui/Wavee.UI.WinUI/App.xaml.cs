﻿using System;
using System.IO;
using Windows.Graphics;
using LanguageExt;
using LanguageExt.UnsafeValueAccess;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Wavee.UI.Infrastructure.Live;
using Wavee.UI.Infrastructure.Sys;
using TimeSpan = System.TimeSpan;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Eum.Spotify;
using Eum.Spotify.connectstate;
using Google.Protobuf;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Wavee.Spotify;
using Wavee.Spotify.Infrastructure.Playback;
using Wavee.UI.ViewModels;
using Wavee.UI.WinUI.Views.Setup;
using WinRT.Interop;

namespace Wavee.UI.WinUI;

public partial class App : Application
{
    public static readonly SpotifyConfig _config;

    static App()
    {
        var localCache = Environment<WaveeUIRuntime>.getEnvironmentVariable("LOCALAPPDATA").Run(Runtime)
            .ThrowIfFail();
        if (localCache.IsNone)
            throw new Exception("LOCALAPPDATA environment variable not set");

        var cachePath = Path.Combine(localCache.ValueUnsafe(), "WaveeUI", "Cache");
        Directory.CreateDirectory(cachePath);
        _config = new SpotifyConfig(
            Cache: new SpotifyCacheConfig(
                CachePath: cachePath,
                CacheNoTouchExpiration: Option<TimeSpan>.None
                ),
            Remote: new SpotifyRemoteConfig(
                DeviceName: "Wavee",
                DeviceType: DeviceType.Computer
            ),
            Playback: new SpotifyPlaybackConfig(
                PreferredQualityType.Normal,
                CrossfadeDuration: Option<TimeSpan>.None,
                Autoplay: true
            )
        );
        Runtime = WaveeUIRuntime.New(string.Empty, _config);
        var home = Environment<WaveeUIRuntime>.getEnvironmentVariable("APPDATA").Run(Runtime)
            .ThrowIfFail();
        if (home.IsNone)
        {
            throw new Exception("APPDATA environment variable not set");
        }

        const string appName = "WaveeUI";
        Runtime = Runtime.WithPath(Path.Combine(home.ValueUnsafe(), appName));
    }

    public App()
    {
        InitializeComponent();
    }

    public static WaveeUIRuntime Runtime { get; }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        _ = await UiConfig<WaveeUIRuntime>.CreateDefaultIfNotExists.Run(Runtime);
        var settings = new SettingsViewModel<WaveeUIRuntime>(Runtime);
        var defaultUserMaybe = (await UserManagment<WaveeUIRuntime>.GetDefaultUser().Run(Runtime)).ThrowIfFail();
        var content = await defaultUserMaybe.MatchAsync(
            Some: async f =>
            {
                //read from password vault
                string password = string.Empty;
                try
                {
                    using var cts = new CancellationTokenSource();
                    cts.CancelAfter(TimeSpan.FromSeconds(2));
                    password = await Task.Run(() =>
                    {
                        var passwordVault = new Windows.Security.Credentials.PasswordVault();
                        var credentials = passwordVault.Retrieve(SigninInView.VAULT_KEY,
                            f.Id);
                        credentials.RetrievePassword();
                        return credentials.Password;
                    }, cts.Token);
                }
                catch (Exception e)
                {
                    var setupView = new SetupView(Runtime);
                    return setupView;
                }

                //passowrd is a {authDataBase64}-{number_auth_typ}
                var authData = password.Split('-');
                var authDataBase64 = authData[0];
                var authType = int.Parse(authData[1]);
                var authdata = ByteString.FromBase64(authDataBase64);
                var authTyp = (AuthenticationType)authType;
                var c = new LoginCredentials
                {
                    Username = f.Id,
                    AuthData = authdata,
                    Typ = authTyp
                };
                var signinTask =
                    Spotify<WaveeUIRuntime>.Authenticate(c, CancellationToken.None)
                        .Run(Runtime);
                var shellView = new SigninInView(signinTask, true);
                return (UIElement)shellView;
            },
            None: () =>
            {
                var setupView = new SetupView(Runtime);
                return setupView;
            });
        var window = new Window
        {
            SystemBackdrop = new MicaBackdrop(),
            ExtendsContentIntoTitleBar = true,
            Content = content
        };
        MWindow = window;


        var scaleAdjustment = GetScaleAdjustment();
        var width = (await UiConfig<WaveeUIRuntime>.WindowWidth.Run(Runtime)).IfFail(800) * scaleAdjustment;
        var height = (await UiConfig<WaveeUIRuntime>.WindowHeight.Run(Runtime)).IfFail(600) * scaleAdjustment;

        var hWnd = WindowNative.GetWindowHandle(MWindow);
        var windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        var displayArea = DisplayArea.GetFromWindowId(windowId,
            DisplayAreaFallback.Primary);

        double displayRegionWidth = displayArea.WorkArea.Width;
        double displayRegionHeight = displayArea.WorkArea.Height;

        var x = (displayRegionWidth - width) / 2;
        var y = (displayRegionHeight - height) / 2;

        appWindow.Move(new PointInt32((int)x, (int)y));

        window.AppWindow.Resize(new SizeInt32(
            _Width: (int)width,
            _Height: (int)height
        ));
        SettingsViewModel<WaveeUIRuntime>.Instance.Width = (int)width;
        SettingsViewModel<WaveeUIRuntime>.Instance.Height = (int)height;

        window.SizeChanged += (sender, eventArgs) =>
        {
            SettingsViewModel<WaveeUIRuntime>.Instance.Width = (int)eventArgs.Size._width;
            SettingsViewModel<WaveeUIRuntime>.Instance.Height = (int)eventArgs.Size._height;
        };
        window.Activate();
    }

    public static Window MWindow { get; private set; }


    [DllImport("Shcore.dll", SetLastError = true)]
    internal static extern int
        GetDpiForMonitor(IntPtr hmonitor, Monitor_DPI_Type dpiType, out uint dpiX, out uint dpiY);

    internal enum Monitor_DPI_Type : int
    {
        MDT_Effective_DPI = 0,
        MDT_Angular_DPI = 1,
        MDT_Raw_DPI = 2,
        MDT_Default = MDT_Effective_DPI
    }

    private double GetScaleAdjustment()
    {
        IntPtr hWnd = WindowNative.GetWindowHandle(MWindow);
        WindowId wndId = Win32Interop.GetWindowIdFromWindow(hWnd);
        DisplayArea displayArea = DisplayArea.GetFromWindowId(wndId, DisplayAreaFallback.Primary);
        IntPtr hMonitor = Win32Interop.GetMonitorFromDisplayId(displayArea.DisplayId);

        // Get DPI.
        int result = GetDpiForMonitor(hMonitor, Monitor_DPI_Type.MDT_Default, out uint dpiX, out uint _);
        if (result != 0)
        {
            throw new Exception("Could not get DPI for monitor.");
        }

        uint scaleFactorPercent = (uint)(((long)dpiX * 100 + (96 >> 1)) / 96);
        return scaleFactorPercent / 100.0;
    }
}