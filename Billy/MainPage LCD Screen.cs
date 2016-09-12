using System;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

namespace Billy
{
    public sealed partial class MainPage : Page
    {
        AdaFruitTFT tft = null;

        public async Task<RenderTargetBitmap> GetImage(UIElement target, int width, int height)
        {
            var renderBitmap = new RenderTargetBitmap();
            //var scale = new ScaleTransform();
            //scale.ScaleX = 0.5;
            //scale.ScaleY = 0.40;
            var oldTransform = target.RenderTransform;
            //target.RenderTransform = scale;
            await renderBitmap.RenderAsync(target, width, height);
            target.RenderTransform = oldTransform;
            return renderBitmap;
        }

        private void ShowDeadEyes()
        {
            imgLeftEye.Source = new BitmapImage(new Uri(this.BaseUri, "/Assets/Cross.jpg"));
            imgRightEye.Source = new BitmapImage(new Uri(this.BaseUri, "/Assets/Cross.jpg"));
            ShowImage.Begin();
        }

        private void ShowLoveEyes()
        {
            imgLeftEye.Source = new BitmapImage(new Uri(this.BaseUri, "/Assets/heart.jpg"));
            imgRightEye.Source = new BitmapImage(new Uri(this.BaseUri, "/Assets/heart.jpg"));
            Love.Begin();
        }

        private void ShowDollarEyes()
        {
            imgLeftEye.Source = new BitmapImage(new Uri(this.BaseUri, "/Assets/dollar.gif"));
            imgRightEye.Source = new BitmapImage(new Uri(this.BaseUri, "/Assets/dollar.gif"));
            Love.Begin();
        }

        private void ShowConfusedEyes()
        {
            imgLeftEye.Source = new BitmapImage(new Uri(this.BaseUri, "/Assets/Spiral.jpg"));
            imgRightEye.Source = new BitmapImage(new Uri(this.BaseUri, "/Assets/Spiral.jpg"));
            Confused.Begin();
        }

        private async void SetLCD()
        {
            if (tft == null) return;

            await tft.initialize();
            tft.fillRect(0, 0, tft.MaxWidth, tft.MaxHeight, Windows.UI.Colors.White);
            //

            //tft.orientation = AdaFruitTFT.Orientation.Landscape;

            tft.fillRect(30, 200, 50, 50, Windows.UI.Colors.Black);
            tft.fillRect(160, 200, 50, 50, Windows.UI.Colors.Black);
            tft.fillRect(50, 120, 140, 50, Windows.UI.Colors.MediumPurple);
            tft.fillRect(52, 122, 136, 46, Windows.UI.Colors.White);

            //RenderTargetBitmap image = await GetImage(this, tft.MaxWidth, tft.MaxHeight);
            //RenderTargetBitmap image = await GetImage(this, tft.MaxHeight, tft.MaxWidth );

            //await tft.Render(image);
        }
    }
}
