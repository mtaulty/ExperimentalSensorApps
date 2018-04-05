using SharedCode.UwpComms;
using System;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Storage.Streams;

namespace App1
{
    internal class MediaFrameReaderHelper
    {
        public MediaFrameReaderHelper(MediaFrameSourceInfo sourceInfo,
            MediaCapture mediaCapture)
        {
            this.sourceInfo = sourceInfo;
            this.mediaCapture = mediaCapture;
            this.CreateBuffer();
        }
        public async Task StartAsync()
        {
            this.frameReader = await this.mediaCapture.CreateFrameReaderAsync(
                this.mediaCapture.FrameSources[this.sourceInfo.Id]);

            this.frameReader.AcquisitionMode = MediaFrameReaderAcquisitionMode.Realtime;

            await this.frameReader.StartAsync();
        }
        public MediaFrameReference TryAcquireLatestFrame()
        {
            return (this.frameReader.TryAcquireLatestFrame());
        }
        public byte[] Buffer
        {
            get => this.buffer;
            private set => this.buffer = value;
        }
        public IBuffer PixelBufferAsBuffer =>
            BufferHelper.GetSendPixelBufferAsIBuffer(this.buffer);

        public int FrameCount { get; set; }

        void CreateBuffer()
        {
            var bytesPerPixel = BufferHelper.GetBytesPerPixelForSourceKind(
                this.sourceInfo.SourceKind);

            // TODO: Unsure what the right thing to do here is with many descriptions?
            var description = this.sourceInfo.VideoProfileMediaDescription[0];

            Int32 pixelBufferSize =
                (Int32)(description.Height * description.Width * bytesPerPixel);

            // We also need a little 'header' on this buffer to store...
            // (4 bytes) - the size of the buffer in total.
            // (1 byte) - the source kind
            // (4 bytes) - width
            // (4 bytes) - height
            Int32 totalBufferSize = pixelBufferSize + BufferHelper.SendHeaderSize;

            this.buffer = new byte[totalBufferSize];

            BufferHelper.SetHeaderValues(
                this.buffer,
                totalBufferSize,
                this.sourceInfo.SourceKind,
                (Int32)description.Width,
                (Int32)description.Height);
        }
        MediaFrameSourceInfo sourceInfo;
        MediaCapture mediaCapture;
        MediaFrameReader frameReader;
        byte[] buffer;
    }
}
