using SharedCode.UwpComms;
using SimpleUwpTwoWayComms;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Media.Capture.Frames;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace DesktopApp
{
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
            this.Loaded += OnLoaded;
        }
        async void OnLoaded(object sender, RoutedEventArgs e)
        {
            this.frameHandlers = new Dictionary<MediaFrameSourceKind, SourceKindFrameXamlImageHandler>()
            {
                { MediaFrameSourceKind.Depth, new SourceKindFrameXamlImageHandler(this.depthImage) },
                { MediaFrameSourceKind.Infrared, new SourceKindFrameXamlImageHandler(this.irImage) }
            };
            
            // We don't advertise our socket, the HoloLens end does
            // that.
            this.messagePipe = new AutoConnectMessagePipe(false);

            // We wait for the advertisement to be seen and the connection
            // to be made.
            await this.messagePipe.WaitForConnectionAsync(
                TimeSpan.FromMilliseconds(-1));

            // And then we let messages come in and we handle them as they
            // arrive.
            await this.messagePipe.ReadAndDispatchMessageLoopAsync(
                this.MessageHandlerAsync);
        }
        async Task MessageHandlerAsync(byte[] buffer)
        {
            // This doesn't run on the UI thread, it runs on some separate task
            // picking up a thread from the threadpool.
            var bufferHelper = new BufferHelper(buffer);

            // Try to convert to a format that the screen can handle
            var bitmap = SoftwareBitmapHelper.ConvertBufferBitmapToBgra8ForXaml(
                bufferHelper.ReceivedBuffer, 
                bufferHelper.PixelFormat, 
                bufferHelper.Width, 
                bufferHelper.Height);

            // Now we want to update the one that's currently on screen with the
            // new one.
            this.frameHandlers[bufferHelper.SourceKind].ReplaceLatestBitmap(bitmap);

            // Don't await this, let it go.
            this.InvalidateAsync();
        }
        async Task InvalidateAsync()
        {
            await this.Dispatcher.RunAsync(
                CoreDispatcherPriority.Normal,
                async () =>
                {
                    foreach (var handler in this.frameHandlers.Values)
                    {
                        await handler.ReplaceXamlImageFromLatestBitmapAsync();
                    }
                }
            );
        }
        Dictionary<MediaFrameSourceKind, SourceKindFrameXamlImageHandler> frameHandlers;
        AutoConnectMessagePipe messagePipe;
    }
}
