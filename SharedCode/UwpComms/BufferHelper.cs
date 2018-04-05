using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
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

        public MediaFrameSourceKind SourceKind => GetKind(this.Buffer);
        public int Width => GetWidth(this.Buffer);
        public int Height => GetHeight(this.Buffer);
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

            switch (GetKind(buffer))
            {
                case MediaFrameSourceKind.Infrared:
                    format = BitmapPixelFormat.Gray8;
                    break;
                case MediaFrameSourceKind.Depth:
                    format = BitmapPixelFormat.Gray16;
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
        public static Int32 GetWidth(byte[] buffer)
        {
            return (GetIntFromBuffer(buffer, 1));
        }
        public static Int32 GetHeight(byte[] buffer)
        {
            return (GetIntFromBuffer(buffer, 5));
        }
        public static MediaFrameSourceKind GetKind(byte[] buffer)
        {
            return ((MediaFrameSourceKind)buffer[0]);
        }
        public static void SetHeaderValues(
            byte[] buffer,
            Int32 totalSize,
            MediaFrameSourceKind sourceKind,
            Int32 width,
            Int32 height)
        {
            // Size
            CopyIntToBuffer(buffer, 0, totalSize);

            // Source Kind
            buffer[Marshal.SizeOf<Int32>()] = (byte)sourceKind;

            // Width
            BufferHelper.CopyIntToBuffer(
                buffer,
                Marshal.SizeOf<Int32>() + 1,
                width);

            // Height
            BufferHelper.CopyIntToBuffer(
                buffer,
                (Marshal.SizeOf<Int32>() * 2) + 1,
                height);
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
                    Marshal.SizeOf<byte>() +    // one for the source kind
                    Marshal.SizeOf<Int32>() +   // width
                    Marshal.SizeOf<Int32>());   // height
            }
        }
    }
}
