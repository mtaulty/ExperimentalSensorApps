using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace DesktopApp
{
    class XamlImageFrameHandler
    {
        public XamlImageFrameHandler(Image xamlImage)
        {
            this.xamlImage = xamlImage;
            this.xamlImage.Source = new SoftwareBitmapSource();
        }
        public void ResetRotation()
        {
            ((RotateTransform)this.xamlImage.RenderTransform).Angle = 0;
        }
        public void Rotate(bool right)
        {
            var angle = right ? 90 : -90;
            ((RotateTransform)this.xamlImage.RenderTransform).Angle += angle;
        }
        public void ReplaceLatestBitmap(SoftwareBitmap newBitmap)
        {
            var existingBitmap = Interlocked.Exchange(ref this.latestBitmap, newBitmap);
            existingBitmap?.Dispose();
        }
        public async Task ReplaceXamlImageFromLatestBitmapAsync()
        {
            var bitmap = Interlocked.Exchange(ref this.latestBitmap, null);

            if (bitmap != null)
            {
                await this.Source.SetBitmapAsync(bitmap);

                // I find that if the bitmap changes size then the Image does not necessarily
                // update to reflect that hence trying to make this explicit call to force that.
                this.xamlImage.Width = bitmap.PixelWidth;
                this.xamlImage.Height = bitmap.PixelHeight;

                bitmap.Dispose();
            }
        }
        SoftwareBitmapSource Source => (SoftwareBitmapSource)this.xamlImage.Source;
        SoftwareBitmap latestBitmap;
        Image xamlImage;
    }
}
