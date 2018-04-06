using SharedCode;
using SimpleUwpTwoWayComms;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
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
            this.readers = new List<MediaFrameReaderHelper>();
            this.Loaded += OnLoaded;
        }
        public int FrameCount => this.frameCount;

        async void OnLoaded(object sender, RoutedEventArgs e)
        {
            // We create our media capture and frame reader objects.
            await this.CreateMediaCaptureAndReadersAsync();

            this.StartCommsAndProcessingAsync();
        }
        async Task StartCommsAndProcessingAsync()
        {
            // Connect our comms layer.
            await this.ConnectMessagePipeAsync();

            // Send our frame description down the wire to the client.
            await this.SendFrameDescriptionsAsync();

            // Start our timer ticking.
            this.StartTimer();

            // Handle anything sent by the desktop app (asking us to switch
            // streams).
            await this.HandleIncomingMessagesAsync();
        }
        async Task ConnectMessagePipeAsync()
        {
            // We are going to advertise our socket details over bluetooth
            this.messagePipe = new AutoConnectMessagePipe(true);

            // We wait for someone else to see them and connect to us
            await this.messagePipe.WaitForConnectionAsync(
                TimeSpan.FromMilliseconds(-1));
        }
        async Task SendFrameDescriptionsAsync()
        {
            await this.messagePipe.SendAsync(this.frameDescriptionBuffer);
        }
        void StartTimer()
        {
            // We poll on an Interval to read frames and send them over the
            // network to our connected peer
            this.timer = new Timer(
                this.OnTimer,
                null,
                (int)Interval.TotalMilliseconds,
                (int)Interval.TotalMilliseconds);
        }
        async Task HandleIncomingMessagesAsync()
        {
            while (true)
            {
                try
                {
                    await this.messagePipe.ReadAndDispatchMessageLoopAsync(
                        this.OnIncomingMessageAsync);
                }
                catch (COMException)
                {
                }
                // The socket's gone a bit wrong, shutdown and try over...
                this.timer.Dispose();
                this.currentReaderIndex = 0;
                this.messagePipe.Close();

                this.StartCommsAndProcessingAsync();
            }
        }
        async Task OnIncomingMessageAsync(byte[] bits)
        {
            if (bits != null)
            {
                var messageValue = BitConverter.ToInt32(bits, Marshal.SizeOf<Int32>());
                var currentValue = this.currentReaderIndex;

                currentValue += messageValue;
                if (currentValue < 0)
                {
                    currentValue = this.readers.Count - 1;
                }
                if (currentValue >= this.readers.Count)
                {
                    currentValue = 0;
                }
                // Handle the message.
                this.currentReaderIndex = currentValue;
            }
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
                    // Ask the currently selected reader for its latest frame
                    var reader = this.readers[this.currentReaderIndex];

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

                            try
                            {
                                // Could perhaps be smarter about this await.
                                await this.messagePipe.SendAsync(reader.Buffer);
                            }
                            catch (COMException)
                            {
                                // We do nothing here, assuming that the outstanding
                                // read on the socket elsewhere in the app will catch
                                // this and restart comms.
                            }
                        }
                    }
                    this.frameCount++;
                    this.FireCounterPropertyChanges();
                }
                Interlocked.Exchange(ref this.reentrancyFlag, 0);
            }
        }
        async Task CreateMediaCaptureAndReadersAsync()
        {
            var frameSourceKinds = new MediaFrameSourceKind[]
            {
                MediaFrameSourceKind.Depth,
                MediaFrameSourceKind.Infrared,
                MediaFrameSourceKind.Color
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
                var sources =
                    this.mediaCapture.FrameSources
                    .Where(fs => frameSourceKinds.Contains(fs.Value.Info.SourceKind))
                    .Select(fs => fs.Value);

                // Build a description of what we have for our client to receive.
                this.BuildFrameSourceDescriptionMessageBuffer(sources);

                // Note: I originally wanted to open a multi source frame reader with all frame
                // sources specified but that blew up on me and so, for the moment, I am making
                // multiple readers.
                foreach (var source in sources)
                {
                    var reader = new MediaFrameReaderHelper(source.Info, this.mediaCapture);

                    this.readers.Add(reader);

                    await reader.StartAsync();
                }
                this.currentReaderIndex = 0;
            }
        }
        void BuildFrameSourceDescriptionMessageBuffer(
            IEnumerable<MediaFrameSource> sources)
        {
            var description = string.Join(
                MessageConstants.SourceListSeparator,
                sources.Select(s =>
                    $"{s.Info.SourceKind} {s.Info.MediaStreamType} " +
                    $"{s.Info.VideoProfileMediaDescription[0].Width} x {s.Info.VideoProfileMediaDescription[0].Height} @ " +
                    $"{s.Info.VideoProfileMediaDescription[0].FrameRate} fps"));

            var encoded = UTF8Encoding.UTF8.GetBytes(description);

            var message =
                BitConverter.GetBytes(MessageConstants.SourceListMessage).Concat(encoded);

            this.frameDescriptionBuffer = 
                BitConverter.GetBytes(message.Count() + Marshal.SizeOf<Int32>()).Concat(message).ToArray();
        }
        void FireCounterPropertyChanges()
        {
            this.Dispatcher.RunAsync(
                CoreDispatcherPriority.Normal,
                () =>
                {
                    this.PropertyChanged?.Invoke(this,
                        new PropertyChangedEventArgs(nameof(this.FrameCount)));
                }
            );
        }
        volatile int currentReaderIndex;
        int frameCount;
        int reentrancyFlag;
        List<MediaFrameReaderHelper> readers;
        MediaCapture mediaCapture;
        AutoConnectMessagePipe messagePipe;
        Timer timer;
        byte[] frameDescriptionBuffer;
        static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(100);
    }
}