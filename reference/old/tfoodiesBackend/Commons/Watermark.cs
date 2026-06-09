using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Drawing;
using System.Drawing.Imaging;
using System.Net;
using System.Drawing.Drawing2D;

namespace tfoodiesBackend.Commons
{
    public static class Watermark
    {
        public static byte[] MakeWatermark(byte[] imagebyte, string watermarkurl, string contenttype)
        {
            byte[] imagewithwatermark = new byte[0];
            //byte[] wbyte = new byte[0];

            MemoryStream oms = new MemoryStream(imagebyte);
            Image oimg = Image.FromStream(oms);

            //using (WebClient wc = new WebClient())
            //{
            //    wbyte = wc.DownloadData(watermarkurl);         
            //}

            FileStream fs = File.OpenRead(HttpContext.Current.Server.MapPath("~" + watermarkurl));
            byte[] wbyte = new byte[fs.Length];
            fs.Read(wbyte, 0, wbyte.Length);
            fs.Close();


            MemoryStream wms = new MemoryStream(wbyte);
            Image wimg = Image.FromStream(wms);
            //Bitmap wBitmap = ImageResize.MakeResize(wimg, 188);
            Bitmap wBitmap = new Bitmap(wimg);

            Graphics ogr = Graphics.FromImage(oimg);

            int x = oimg.Width - (wBitmap.Width + 10);
            int y = oimg.Height - (wBitmap.Height + 10);

            ogr.DrawImage(wBitmap, x, y);

            using (MemoryStream rms = new MemoryStream())
            {
                if (contenttype == "image/jpeg" || contenttype == "image/jpg")
                {
                    oimg.Save(rms, ImageFormat.Jpeg);
                }
                else if (contenttype == "image/png")
                {
                    oimg.Save(rms, ImageFormat.Png);
                }
                else if (contenttype == "image/gif")
                {
                    oimg.Save(rms, ImageFormat.Gif);
                }

                rms.Close();

                imagewithwatermark = rms.ToArray();
            }


            oimg.Dispose();
            wimg.Dispose();
            ogr.Dispose();

            return imagewithwatermark;
        }
    }
}