using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Web;

namespace tfoodiesBackend.Commons
{
    public static class ImageResize
    {
        public static Bitmap MakeResize(Image image, int maxSize)
        {
            int newWidth, newHeight;
            float ratio = 1;

            Bitmap srcImage = new Bitmap(image);

            if (srcImage.Width > srcImage.Height)
            { // h
                ratio = (float)maxSize / (float)srcImage.Width;
            }
            else
            { // v
                ratio = (float)maxSize / (float)srcImage.Height;
            }

            ratio = (ratio > 1) ? 1 : ratio;

            newWidth = (int)((float)srcImage.Width * ratio);
            newHeight = (int)((float)srcImage.Height * ratio);

            Rectangle clipRectangle = new Rectangle(0, 0, srcImage.Width, srcImage.Height);

            Bitmap bmp = new Bitmap(newWidth, newHeight);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(srcImage, new Rectangle(0, 0, newWidth, newHeight), clipRectangle, GraphicsUnit.Pixel);
            }

            srcImage.Dispose();
            return bmp;
        }

        public static byte[] MakeResize(byte[] imagebyte, int maxSize, string contenttype)
        {
            MemoryStream oms = new MemoryStream(imagebyte);
            Image oimg = Image.FromStream(oms);
            Bitmap oBitmap = MakeResize(oimg, maxSize);

            byte[] byteArray = new byte[0];
            using (MemoryStream stream = new MemoryStream())
            {
                if (contenttype == "image/jpeg" || contenttype == "image/jpg")
                {
                    oBitmap.Save(stream, ImageFormat.Jpeg);
                }
                else if (contenttype == "image/png")
                {
                    oBitmap.Save(stream, ImageFormat.Png);
                }
                else if (contenttype == "image/gif")
                {
                    oBitmap.Save(stream, ImageFormat.Gif);
                }

                stream.Close();

                byteArray = stream.ToArray();
            }

            oimg.Dispose();
            oBitmap.Dispose();

            return byteArray;
        }

        public static byte[] StreamToByte(Stream input)
        {
            byte[] buffer = new byte[16 * 1024];
            using (MemoryStream ms = new MemoryStream())
            {
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }
                return ms.ToArray();
            }
        }
    }
}