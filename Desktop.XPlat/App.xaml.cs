﻿using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Remotely.Desktop.Core;
using Remotely.Desktop.Core.Interfaces;
using Remotely.Desktop.Core.Services;
using Remotely.Desktop.XPlat.Services;
using Remotely.Desktop.XPlat.Views;
using Remotely.Shared.Utilities;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Remotely.Desktop.XPlat
{
    public class App : Application
    {
        private static IServiceProvider Services => ServiceContainer.Instance;

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            //if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            //{
            //    desktop.MainWindow = new MainWindow
            //    {
            //        DataContext = new MainWindowViewModel(),
            //    };
            //}

            base.OnFrameworkInitializationCompleted();

            _ = Task.Run(Startup);
        }

        private void BuildServices()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddLogging(builder =>
            {
                builder.AddConsole().AddDebug();
            });

            serviceCollection.AddSingleton<IScreenCaster, ScreenCaster>();
            serviceCollection.AddSingleton<ICasterSocket, CasterSocket>();
            serviceCollection.AddSingleton<IdleTimer>();
            serviceCollection.AddSingleton<Conductor>();
            serviceCollection.AddSingleton<IChatClientService, ChatHostService>();
            serviceCollection.AddTransient<Viewer>();
            serviceCollection.AddScoped<IWebRtcSessionFactory, WebRtcSessionFactory>();
            serviceCollection.AddScoped<IDtoMessageHandler, DtoMessageHandler>();
            serviceCollection.AddScoped<IDeviceInitService, DeviceInitService>();

            switch (EnvironmentHelper.Platform)
            {
                case Shared.Enums.Platform.Linux:
                    {
                        serviceCollection.AddSingleton<IKeyboardMouseInput, KeyboardMouseInputLinux>();
                        serviceCollection.AddSingleton<IClipboardService, ClipboardServiceLinux>();
                        serviceCollection.AddSingleton<IAudioCapturer, AudioCapturerLinux>();
                        serviceCollection.AddSingleton<IChatUiService, ChatUiServiceLinux>();
                        serviceCollection.AddTransient<IScreenCapturer, ScreenCapturerLinux>();
                        serviceCollection.AddScoped<IFileTransferService, FileTransferServiceLinux>();
                        serviceCollection.AddSingleton<ICursorIconWatcher, CursorIconWatcherLinux>();
                        serviceCollection.AddSingleton<ISessionIndicator, SessionIndicatorLinux>();
                        serviceCollection.AddSingleton<IShutdownService, ShutdownServiceLinux>();
                        serviceCollection.AddScoped<IRemoteControlAccessService, RemoteControlAccessServiceLinux>();
                        serviceCollection.AddScoped<IConfigService, ConfigServiceLinux>();
                    }
                    break;
                case Shared.Enums.Platform.MacOS:
                    {

                    }
                    break;
                default:
                    throw new PlatformNotSupportedException();
            }

            ServiceContainer.Instance = serviceCollection.BuildServiceProvider();
        }


        private async Task Startup()
        {

            BuildServices();

            var conductor = Services.GetRequiredService<Conductor>();

            var args = Environment.GetCommandLineArgs().SkipWhile(x => !x.StartsWith("-"));
            Logger.Write("Processing Args: " + string.Join(", ", args));
            conductor.ProcessArgs(args.ToArray());

            await Services.GetRequiredService<IDeviceInitService>().GetInitParams();

            if (conductor.Mode == Core.Enums.AppMode.Chat)
            {
                await Services.GetRequiredService<IChatClientService>().StartChat(conductor.RequesterID, conductor.OrganizationName);
            }
            else if (conductor.Mode == Core.Enums.AppMode.Unattended)
            {
                var casterSocket = Services.GetRequiredService<ICasterSocket>();
                await casterSocket.Connect(conductor.Host).ConfigureAwait(false);
                await casterSocket.SendDeviceInfo(conductor.ServiceID, Environment.MachineName, conductor.DeviceID).ConfigureAwait(false);
                await casterSocket.NotifyRequesterUnattendedReady(conductor.RequesterID).ConfigureAwait(false);
                Services.GetRequiredService<IdleTimer>().Start();
                Services.GetRequiredService<IClipboardService>().BeginWatching();
                Services.GetRequiredService<IKeyboardMouseInput>().Init();
            }
            else
            {
                await Dispatcher.UIThread.InvokeAsync(() => {
                    this.RunWithMainWindow<MainWindow>();
                });
            }
        }


    }
}
