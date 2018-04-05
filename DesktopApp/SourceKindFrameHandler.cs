using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

namespace DesktopApp
{
    class SourceKindFrameXamlImageHandler
    {
        public SourceKindFrameXamlImageHandler(Image xamlImage)
        {
            this.xamlImage = xamlImage;
            this.xamlImage.Source = new SoftwareBitmapSource();
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
                bitmap.Dispose();
            }
        }
        SoftwareBitmapSource Source => (SoftwareBitmapSource)this.xamlImage.Source;
        SoftwareBitmap latestBitmap;
        Image xamlImage;
    }
}
