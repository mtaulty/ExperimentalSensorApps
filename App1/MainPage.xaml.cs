using SimpleUwpTwoWayComms;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace App1
{
    public sealed partial class MainPage : Page, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public MainPage()
        {
            this.InitializeComponent();
            this.readers = new Dictionary<MediaFrameSourceKind, MediaFrameReaderHelper>();
            this.Loaded += OnLoaded;
        }
        public int DepthFrameCount =>
            this.readers.ContainsKey(MediaFrameSourceKind.Depth) ?
                this.readers[MediaFrameSourceKind.Depth].FrameCount : 0;

        public int IRFrameCount =>
            this.readers.ContainsKey(MediaFrameSourceKind.Infrared) ?
                this.readers[MediaFrameSourceKind.Infrared].FrameCount : 0;
        async void OnLoaded(object sender, RoutedEventArgs e)
        {
            // We are going to advertise our socket details over bluetooth
            this.messagePipe = new AutoConnectMessagePipe(true);

            // We wait for someone else to see them and connect to us
            await this.messagePipe.WaitForConnectionAsync(
                TimeSpan.FromMilliseconds(-1));

            // We create our media capture and frame reader objects
            await this.CreateMediaCaptureAndReadersAsync();

            // We poll on an Interval to read frames and send them over the
            // network to our connected peer.
            this.timer = new Timer(
                this.OnTimer,
                null,
                (int)Interval.TotalMilliseconds,
                (int)Interval.TotalMilliseconds);
        }
        async void OnTimer(object state)
        {
            // Sanity check - I'm not expecting this to re-enter although if
            // interval was < processing time then it would and I don't think
            // the code would 'handle that well'
            if (Interlocked.CompareExchange(ref this.reentrancyFlag, 1, 0) == 0)
            {
                if (this.messagePipe.IsConnected)
                {
                    // Ask each reader if it has a new frame for us (it should as they
                    // tick at 30fps)
                    foreach (var reader in this.readers.Values)
                    {
                        using (var frame = reader.TryAcquireLatestFrame())
                        {
                            // Note: Could check frame time to make sure I haven't sent it
                            // before?
                            if (frame != null)
                            {
                                // I want to go from the frame here to a byte[] and I don't know that
                                // there is some great way of doing that so copying again :-(
                                // Note: PixelBuffer is an offset into the buffer because the
                                // buffer has a header on it.
                                frame.VideoMediaFrame.SoftwareBitmap.CopyToBuffer(
                                    reader.PixelBufferAsBuffer);

                                // Could perhaps be smarter about this await.
                                await this.messagePipe.SendAsync(reader.Buffer);

                                reader.FrameCount++;
                            }
                        }
                    }
                }
                Interlocked.Exchange(ref this.reentrancyFlag, 0);
                this.FireCounterPropertyChanges();
            }
        }
        async Task CreateMediaCaptureAndReadersAsync()
        {
            var frameSourceKinds = new MediaFrameSourceKind[]
            {
                MediaFrameSourceKind.Depth,
                MediaFrameSourceKind.Infrared
            };
            // Get me the first source group that does Depth+Infrared.
            var firstSourceGroupWithSourceKinds = 
                await MediaSourceFinder.FindGroupsWithAllSourceKindsAsync(frameSourceKinds);

            if (firstSourceGroupWithSourceKinds != null)
            { 
                this.mediaCapture = new MediaCapture();

                // Note: This will blow up unless I have the restricted capability named 
                // 'perceptionSensorsExperimental' in my .appx manifest and I think that
                // being a 'restricted' capability means that any app using it could not
                // go into store.

                // Note2: I've gone with Cpu here rather than Gpu because I ultimately
                // want a byte[] that I can send down a socket. If I go with Gpu then
                // I get an IDirect3DSurface but (AFAIK) there's not much of a way
                // to get to a byte[] from that other than to copy it into a 
                // SoftwareBitmap and then to copy that SoftwareBitmap into a byte[]
                // which I don't really want to do. Hence - Cpu choice here.
                await this.mediaCapture.InitializeAsync(
                    new MediaCaptureInitializationSettings()
                    {
                        SourceGroup = firstSourceGroupWithSourceKinds,
                        MemoryPreference = MediaCaptureMemoryPreference.Cpu,
                        StreamingCaptureMode = StreamingCaptureMode.Video
                    }
                );
                List<string> sourceInfos =
                    MediaSourceFinder.FindSourceInfosWithMaxFrameRates(
                        firstSourceGroupWithSourceKinds, frameSourceKinds);

                var sources =
                    this.mediaCapture.FrameSources.Where(
                        fs => sourceInfos.Contains(fs.Key)).Select(kvp => kvp.Value);

                // Note: I originally wanted to open a multi source frame reader with all frame
                // sources specified but that blew up on me and so, for the moment, I am making
                // multiple readers.
                foreach (var source in sources)
                {
                    var reader = new MediaFrameReaderHelper(source.Info, this.mediaCapture);

                    this.readers[source.Info.SourceKind] = reader;

                    await reader.StartAsync();
                }
            }
        }
        void FireCounterPropertyChanges()
        {
            this.Dispatcher.RunAsync(
                CoreDispatcherPriority.Normal,
                () =>
                {
                    this.PropertyChanged?.Invoke(this,
                        new PropertyChangedEventArgs(nameof(this.DepthFrameCount)));

                    this.PropertyChanged?.Invoke(this,
                        new PropertyChangedEventArgs(nameof(this.IRFrameCount)));
                }
            );
        }
        int reentrancyFlag;
        Dictionary<MediaFrameSourceKind, MediaFrameReaderHelper> readers;
        MediaCapture mediaCapture;
        AutoConnectMessagePipe messagePipe;
        Timer timer;
        static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(100);
    }
}