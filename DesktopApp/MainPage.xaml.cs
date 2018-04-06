using SharedCode;
using SharedCode.UwpComms;
using SimpleUwpTwoWayComms;
using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace DesktopApp
{
    public sealed partial class MainPage : Page, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public MainPage()
        {
            this.InitializeComponent();
            this.Loaded += OnLoaded;
        }
        public string StatusText
        {
            get => this.statusText;
            private set
            {
                this.statusText = value;
                this.FirePropertyChanged();
            }
        }
        public string SourceDescription
        {
            get
            {
                var description = "no description of stream source";

                if ((this.sourceDescriptions != null) &&
                    (this.currentSourceIndex >= 0) &&
                    (this.currentSourceIndex < this.sourceDescriptions.Length))
                {
                    description = this.sourceDescriptions[this.currentSourceIndex];
                }
                return (description);
            }
        }
        async void OnLoaded(object sender, RoutedEventArgs e)
        {
            this.frameHandler = new XamlImageFrameHandler(this.image);

            while (true)
            {
                this.StatusText = "disconnected";

                // We don't advertise our socket, the HoloLens end does
                // that.
                this.messagePipe = new AutoConnectMessagePipe(false);

                // We wait for the advertisement to be seen and the connection
                // to be made.
                await this.messagePipe.WaitForConnectionAsync(
                    TimeSpan.FromMilliseconds(-1));

                this.StatusText = "connected";

                // And then we let messages come in and we handle them as they
                // arrive.
                await this.messagePipe.ReadAndDispatchMessageLoopAsync(
                    this.MessageHandlerAsync);

                this.messagePipe.Close();
            }
        }
        async Task MessageHandlerAsync(byte[] buffer)
        {
            // This doesn't run on the UI thread, it runs on some separate task
            // picking up a thread from the threadpool.
            var bufferHelper = new BufferHelper(buffer);

            if (bufferHelper.ReceivedMessageType == MessageConstants.FrameMessage)
            {
                // Try to convert to a format that the screen can handle
                var bitmap = SoftwareBitmapHelper.ConvertBufferBitmapToBgra8ForXaml(
                    bufferHelper.ReceivedBuffer,
                    bufferHelper.PixelFormat,
                    bufferHelper.ReceivedWidth,
                    bufferHelper.ReceivedHeight);

                // Now we want to update the one that's currently on screen with the
                // new one.
                this.frameHandler.ReplaceLatestBitmap(bitmap);

                Interlocked.Increment(ref this.frameCount);

                // Don't await this, let it go.
                this.InvalidateAsync();
            }
            else if (bufferHelper.ReceivedMessageType == MessageConstants.SourceListMessage)
            {
                this.HandleSourceDescriptionMessage(buffer);
            }
        }
        void HandleSourceDescriptionMessage(byte[] buffer)
        {
            var decoded = UTF8Encoding.UTF8.GetString(buffer);

            this.sourceDescriptions = decoded.Split(MessageConstants.SourceListSeparator);

            this.FirePropertyChanged(nameof(this.SourceDescription));
        }
        async Task InvalidateAsync()
        {
            await this.Dispatcher.RunAsync(
                CoreDispatcherPriority.Normal,
                async () =>
                {
                    this.statusText = $"Processed {this.frameCount} frames";

                    this.PropertyChanged?.Invoke(this,
                        new PropertyChangedEventArgs(nameof(this.StatusText)));

                    await this.frameHandler.ReplaceXamlImageFromLatestBitmapAsync();
                }
            );
        }
        public async void OnNextStream()
        {
            await this.RequestAdjacentStreamAsync(1);
        }
        public async void OnPreviousStream()
        {
            await this.RequestAdjacentStreamAsync(-1);
        }
        void OnRotateRight()
        {
            this.frameHandler.Rotate(true);    
        }
        void OnRotateLeft()
        {
            this.frameHandler.Rotate(false);
        }
        async Task RequestAdjacentStreamAsync(int increment)
        {
            if (this.messagePipe.IsConnected && (this.sourceDescriptions != null))
            {
                // At the moment, it doesn't matter what this message looks like
                // as there only is 1 message which means 'next'
                var bits = 
                    BitConverter.GetBytes(12).Concat(
                    BitConverter.GetBytes(MessageConstants.NextPreviousMessage)).Concat(
                    BitConverter.GetBytes(increment))
                    .ToArray();

                await this.messagePipe.SendAsync(bits);

                var newValue = this.currentSourceIndex + increment;
                if (newValue < 0)
                {
                    newValue = this.sourceDescriptions.Length - 1;
                }
                if (newValue >= this.sourceDescriptions.Length)
                {
                    newValue = 0;
                }
                this.currentSourceIndex = newValue;

                this.Dispatch(
                    () =>
                    {
                        this.frameHandler.ResetRotation();
                    }
                );
                this.FirePropertyChanged(nameof(this.SourceDescription));
            }
        }
        async Task Dispatch(Action a)
        {
            await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => a());
        }
        async Task Dispatch(Func<Task> a)
        {
            await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () => await a());
        }
        void FirePropertyChanged([CallerMemberName] string property = null)
        {
            this.Dispatcher.RunAsync(
                CoreDispatcherPriority.Normal,
                () =>
                {
                    this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
                }
            );
        }
        string statusText;
        int frameCount;
        string[] sourceDescriptions;
        int currentSourceIndex;
        XamlImageFrameHandler frameHandler;
        AutoConnectMessagePipe messagePipe;
    }
}