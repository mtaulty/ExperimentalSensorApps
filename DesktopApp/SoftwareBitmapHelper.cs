using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace DesktopApp
{
    static class SoftwareBitmapHelper
    {
        public static SoftwareBitmap ConvertBufferBitmapToBgra8ForXaml(
            IBuffer buffer,
            BitmapPixelFormat format,
            int width,
            int height)
        {
            SoftwareBitmap bitmap = null;

            var intermediate = SoftwareBitmap.CreateCopyFromBuffer(
                 buffer, format, width, height);

            if (intermediate.BitmapPixelFormat != BitmapPixelFormat.Bgra8)
            {
                bitmap = SoftwareBitmap.Convert(
                    intermediate, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

                intermediate.Dispose();
            }
            else
            {
                bitmap = intermediate;
            }
            return (bitmap);
        }
    }
}
