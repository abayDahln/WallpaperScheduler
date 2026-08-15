using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using WallpaperScheduler.Models;
using WallpaperScheduler.Services;

namespace WallpaperScheduler.Helpers
{
    public static class ThumbnailGenerator
    {
        private const int MaxDimension = 512;

        public static string ThumbPathFor(string fileName) =>
            Path.Combine(ConfigService.ThumbsDir, Path.GetFileNameWithoutExtension(fileName) + ".jpg");

        public static async Task EnsureThumbAsync(WallpaperItem item)
        {
            string thumb = ThumbPathFor(item.FileName);
            if (File.Exists(thumb)) return;

            string src = Path.Combine(ConfigService.WallpapersDir, item.FileName);
            if (!File.Exists(src)) return;

            try
            {
                Directory.CreateDirectory(ConfigService.ThumbsDir);

                var srcFile = await StorageFile.GetFileFromPathAsync(src);
                using var srcStream = await srcFile.OpenAsync(FileAccessMode.Read);
                var decoder = await BitmapDecoder.CreateAsync(srcStream);

                uint ow = decoder.OrientedPixelWidth;
                uint oh = decoder.OrientedPixelHeight;
                uint sw = ow, sh = oh;
                uint max = Math.Max(ow, oh);
                if (max > MaxDimension)
                {
                    double scale = max / (double)MaxDimension;
                    sw = (uint)Math.Max(1, Math.Round(ow / scale));
                    sh = (uint)Math.Max(1, Math.Round(oh / scale));
                }

                var transform = new BitmapTransform { ScaledWidth = sw, ScaledHeight = sh };
                var pixelData = await decoder.GetPixelDataAsync(
                    BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, transform,
                    ExifOrientationMode.RespectExifOrientation, ColorManagementMode.ColorManageToSRgb);

                using var outStream = new InMemoryRandomAccessStream();
                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, outStream);
                encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, sw, sh, 96, 96, pixelData.DetachPixelData());
                await encoder.FlushAsync();

                byte[] bytes = new byte[outStream.Size];
                using (var reader = new DataReader(outStream.GetInputStreamAt(0)))
                {
                    await reader.LoadAsync((uint)bytes.Length);
                    reader.ReadBytes(bytes);
                }
                File.WriteAllBytes(thumb, bytes);
            }
            catch
            {
                // ponytail: decode/encode failures fall back to the original file for previews
            }
        }
    }
}