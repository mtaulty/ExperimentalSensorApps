using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;
using Windows.Media.Capture.Frames;
using Windows.Storage.Streams;

namespace SharedCode.UwpComms
{
    // This is fairly nasty in the sense that I 'sort of' know what it's meant to
    // do but someone coming to it cold would struggle with the various size
    // manipulations it's doing. TODO: make it nicer.
    public class BufferHelper
    {
        public BufferHelper(byte[] buffer)
        {
            this.Buffer = buffer;
        }
        public byte[] Buffer { get; set; }

        public MediaFrameSourceKind ReceivedSourceKind => GetKindFromReceivedBuffer(this.Buffer);
        public int ReceivedWidth => GetWidthFromReceivedBuffer(this.Buffer);
        public int ReceivedHeight => GetHeightFromReceivedBuffer(this.Buffer);
        public int ReceivedMessageType => GetMessageTypeFromReceivedBuffer(this.Buffer);
        public BitmapPixelFormat PixelFormat => GetBitmapPixelFormat(this.Buffer);
        public IBuffer ReceivedBuffer => GetReceivedPixelBufferAsIBuffer(this.Buffer);
        public IBuffer SentBuffer => GetSendPixelBufferAsIBuffer(this.Buffer);

        public static IBuffer GetReceivedPixelBufferAsIBuffer(byte[] buffer)
        {
            var pixelLength = buffer.Length - BufferHelper.ReceiveHeaderSize;

            var iBuffer = buffer.AsBuffer(BufferHelper.ReceiveHeaderSize, pixelLength);

            return (iBuffer);
        }
        public static IBuffer GetSendPixelBufferAsIBuffer(byte[] buffer)
        {
            var pixelLength = buffer.Length - BufferHelper.SendHeaderSize;

            var iBuffer = buffer.AsBuffer(BufferHelper.SendHeaderSize, pixelLength);

            return (iBuffer);
        }
        public static BitmapPixelFormat GetBitmapPixelFormat(byte[] buffer)
        {
            var format = BitmapPixelFormat.Bgra8;

            switch (GetKindFromReceivedBuffer(buffer))
            {
                case MediaFrameSourceKind.Infrared:
                    format = BitmapPixelFormat.Gray8;
                    break;
                case MediaFrameSourceKind.Depth:
                    format = BitmapPixelFormat.Gray16;
                    break;
                case MediaFrameSourceKind.Color:
                    format = BitmapPixelFormat.Bgra8;
                    break;
                default:
                    Debug.Assert(false);
                    break;
            }
            return (format);
        }

        public static int GetBytesPerPixelForSourceKind(MediaFrameSourceKind sourceKind)
        {
            var bytesPerPixel = 0;

            // I make some assumptions here around the size of the pixel data on
            // a per 'sourcekind' basis. These could easily break/be wrong over
            // time if Color for instance suddenly came in as a 2 byte array or
            // Depth jumped to 4 bytes.
            switch (sourceKind)
            {
                case MediaFrameSourceKind.Color:
                    bytesPerPixel = 4;
                    break;
                case MediaFrameSourceKind.Infrared:
                    bytesPerPixel = 1;
                    break;
                case MediaFrameSourceKind.Depth:
                    bytesPerPixel = 2;
                    break;
                default:
                    Debug.Assert(false);
                    break;
            }
            return (bytesPerPixel);
        }
        public static Int32 GetWidthFromReceivedBuffer(byte[] buffer)
        {
            // TODO: fix this - hardwired numbers aren't nice.
            return (GetIntFromBuffer(buffer, 5));
        }
        public static Int32 GetHeightFromReceivedBuffer(byte[] buffer)
        {
            // TODO: fix this - hardwired numbers aren't nice.
            return (GetIntFromBuffer(buffer, 9));
        }
        public static MediaFrameSourceKind GetKindFromReceivedBuffer(byte[] buffer)
        {
            // TODO: fix this - hardwired numbers aren't nice.
            return ((MediaFrameSourceKind)buffer[4]);
        }
        public static Int32 GetMessageTypeFromReceivedBuffer(byte[] buffer)
        {
            // TODO: fix this - hardwired numbers aren't nice.
            return (GetIntFromBuffer(buffer, 0));
        }
        public static void SetHeaderValues(
            byte[] buffer,
            Int32 totalSize,
            MediaFrameSourceKind sourceKind,
            Int32 width,
            Int32 height)
        {
            int offset = 0;

            // Size
            CopyIntToBuffer(buffer, 0, totalSize);
            offset += Marshal.SizeOf<Int32>();

            // Message Type
            CopyIntToBuffer(buffer, offset, MessageConstants.FrameMessage);
            offset += Marshal.SizeOf<Int32>();

            // Source Kind
            buffer[offset] = (byte)sourceKind;
            offset += Marshal.SizeOf<Byte>();

            // Width
            BufferHelper.CopyIntToBuffer(buffer, offset, width);
            offset += Marshal.SizeOf<Int32>();

            // Height
            BufferHelper.CopyIntToBuffer(buffer, offset, height);
        }
        static Int32 GetIntFromBuffer(byte[] buffer, int bufferOffset)
        {
            var value = BitConverter.ToInt32(buffer, bufferOffset);
            return (value);
        }
        static void CopyIntToBuffer(byte[] buffer, int bufferOffset, Int32 value)
        {
            var bits = BitConverter.GetBytes(value);
            Array.Copy(bits, 0, buffer, bufferOffset, bits.Length);
        }
        public static int ReceiveHeaderSize
        {
            get
            {
                return (SendHeaderSize - Marshal.SizeOf<Int32>());
            }
        }
        public static int SendHeaderSize
        {
            get
            {
                return (
                    Marshal.SizeOf<Int32>() +   // 4 bytes for the size of the buffer
                    Marshal.SizeOf<Int32>() +   // 4 bytes for the message type
                    Marshal.SizeOf<byte>() +    // one for the source kind
                    Marshal.SizeOf<Int32>() +   // width
                    Marshal.SizeOf<Int32>());   // height
            }
        }
    }
}
