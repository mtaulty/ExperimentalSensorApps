namespace SimpleUwpTwoWayComms
{
    using System;
    using System.Runtime.InteropServices;
    using System.Runtime.InteropServices.WindowsRuntime;
    using System.Threading.Tasks;
    using Windows.Networking;
    using Windows.Networking.Sockets;
    using Windows.Storage.Streams;

    public class MessageBase
    {
    }
    public class AutoConnectMessagePipe
    {
        public AutoConnectMessagePipe(bool advertise = true)
        {
            this.advertise = advertise;
        }
        public async Task<bool> WaitForConnectionAsync(TimeSpan timeOut)
        {
            bool connected = false;
            this.connectionMadeTask = new TaskCompletionSource<bool>();

            if (this.advertise)
            {
                this.advertiser = new BluetoothLEStreamSocketAdvertiser();
                this.advertiser.ConnectionReceived += OnRemotePipeConnected;
                await this.advertiser.StartAsync();
            }
            else
            {
                this.watcher = new BluetoothLEStreamSocketWatcher(
                  IPAddressExtensions.GetForLocalInternetProfile());

                this.watcher.StreamSocketDiscovered += OnRemotePipeDiscovered;

                this.watcher.Start();
            }
            var completed = await Task.WhenAny(
              this.connectionMadeTask.Task,
              Task.Delay(timeOut));

            connected = completed == this.connectionMadeTask.Task;

            this.StopAdvertisingWatching();

            if (connected && !this.advertise)
            {
                connected = await this.ConnectToRemotePipeAsync();
            }
            return (connected);
        }
        public bool IsConnected
        {
            get
            {
                return (this.socket != null);
            }
        }
        void OnRemotePipeConnected(object sender, StreamSocketEventArgs e)
        {
            this.socket = e.Socket;
            this.connectionMadeTask.SetResult(true);
        }
        async Task<bool> ConnectToRemotePipeAsync()
        {
            bool connected = false;

            try
            {
                this.socket = new StreamSocket();
                this.socket.Control.NoDelay = true;

                await this.socket.ConnectAsync(
                  new EndpointPair(
                    null,
                    string.Empty,
                    new HostName(this.advertisement.Address.ToString()),
                    this.advertisement.Port.ToString()));

                connected = true;
            }
            catch // TBD: what to catch here?
            {
                this.socket.Dispose();
                this.socket = null;
            }
            return (connected);
        }
        public async Task ReadAndDispatchMessageLoopAsync(
          Func<byte[], Task> handler)
        {
            while (true)
            {
                try
                {
                    // We don't want this to come back to the UI thread, we are
                    // happy to keep it going on some thread pool thread.
                    var msg = await this.ReadMessageAsync().ConfigureAwait(false);

                    if (msg == null)
                    {
                        break;
                    }

                    // We don't await this as we want to get straight back to our
                    // reading messages.
                    Task.Factory.StartNew(
                        async () =>
                        {
                            await handler(msg);
                        }
                    );
                }
                catch
                {
                    break;
                }
            }
        }
        public async Task<byte[]> ReadMessageAsync()
        {
            // TODO: lots of allocations of potentially large size, might
            // be better to allocate some big buffer and keep re-using it?
            // Or...does the GC with its large object heap do a better job
            // than I would? I suspect it does :-)
            byte[] bits = new byte[Marshal.SizeOf<int>()];

            await this.socket.InputStream.ReadAsync(bits.AsBuffer(),
              (uint)bits.Length, InputStreamOptions.None);

            int size = BitConverter.ToInt32(bits, 0) - Marshal.SizeOf<Int32>();

            if (size > 0)
            {
                bits = new byte[size];

                await this.socket.InputStream.ReadAsync(bits.AsBuffer(),
                  (uint)bits.Length, InputStreamOptions.None);
            }
            else
            {
                bits = null;
            }
            return (bits);
        }
        public void Close()
        {
            if (this.socket != null)
            {
                this.socket.Dispose();
                this.socket = null;
            }
        }
        void OnRemotePipeDiscovered(object sender,
          BluetoothLEStreamSocketDiscoveredEventArgs e)
        {
            this.advertisement = e.Advertisement;
            this.connectionMadeTask.SetResult(true);
        }
        void StopAdvertisingWatching()
        {
            if (this.watcher != null)
            {
                this.watcher.Stop();
                this.watcher.StreamSocketDiscovered -= this.OnRemotePipeDiscovered;
                this.watcher = null;
            }
            if (this.advertiser != null)
            {
                this.advertiser.Stop();
                this.advertiser = null;
            }
            this.connectionMadeTask = null;
        }
        public async Task SendAsync(byte[] bits)
        {
            if (this.socket == null)
            {
                throw new InvalidOperationException("Socket not connected");
            }
            // We expect this buffer to be prefixed with its total size otherwise
            // we can't read it back afterwards.
            await this.socket.OutputStream.WriteAsync(bits.AsBuffer());

            await this.socket.OutputStream.FlushAsync();
        }
        bool advertise;
        StreamSocket socket;
        BluetoothLEStreamSocketAdvertisement advertisement;
        BluetoothLEStreamSocketAdvertiser advertiser;
        BluetoothLEStreamSocketWatcher watcher;
        TaskCompletionSource<bool> connectionMadeTask;
    }
}