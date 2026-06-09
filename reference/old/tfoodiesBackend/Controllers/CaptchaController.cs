using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Mvc;

namespace tfoodiesBackend.Controllers
{
    public class CaptchaController : Controller
    {
        public void VerificationCode()
        {
            // 是否產生驗證碼
            bool isCreate = true;

            // Session["CreateTime"]: 建立驗證碼的時間
            if (Session["CreateTime"] == null)
            {
                Session["CreateTime"] = DateTime.Now;
            }
            else
            {
                DateTime startTime = Convert.ToDateTime(Session["CreateTime"]);
                DateTime endTime = Convert.ToDateTime(DateTime.Now);
                TimeSpan ts = endTime - startTime;


                // 重新產生驗證碼的間隔
                if (ts.Minutes > 1)
                {
                    isCreate = true;
                    Session["CreateTime"] = DateTime.Now;
                }
                else
                {
                    isCreate = false;
                }
            }

            Response.ContentType = "image/gif";
            //建立 Bitmap 物件和繪製
            Bitmap basemap = new Bitmap(153, 44);
            Graphics graph = Graphics.FromImage(basemap);
            graph.FillRectangle(new SolidBrush(Color.White), 0, 0, 153, 44);
            Font font = new Font(FontFamily.GenericSerif, 36, FontStyle.Bold, GraphicsUnit.Pixel);
            Random random = new Random();
            // 英數
            //string letters = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz023456789";
            // 數字
            string letters = "023456789";
            // 天干地支生肖
            //string letters = "甲乙丙丁戊己庚辛壬癸子丑寅卯辰巳午未申酉戍亥鼠牛虎免龍蛇馬羊猴雞狗豬";
            string letter;
            StringBuilder sb = new StringBuilder();


            if (isCreate)
            {
                // 加入隨機二個字
                // 英文4 ~ 5字，中文2 ~ 3字
                for (int word = 0; word < 4; word++)
                {
                    letter = letters.Substring(random.Next(0, letters.Length - 1), 1);
                    sb.Append(letter);


                    // 繪製字串 
                    graph.DrawString(letter, font, new SolidBrush(Color.Black), word * 30, random.Next(0, 10));
                }
            }
            else
            {
                // 使用先前的驗證碼來產生
                string currentCode = Session["ValidateCode"].ToString();
                sb.Append(currentCode);

                foreach (char item in currentCode)
                {
                    letter = item.ToString();
                    // 繪製字串
                    graph.DrawString(letter, font, new SolidBrush(Color.Black), currentCode.IndexOf(item) * 30, random.Next(0, 10));
                }
            }


            // 混亂背景
            Pen linePen = new Pen(new SolidBrush(Color.Black), 2);
            for (int x = 0; x < 10; x++)
            {
                graph.DrawLine(linePen, new Point(random.Next(0, 199), random.Next(0, 59)), new Point(random.Next(0, 199), random.Next(0, 59)));
            }

            // 儲存圖片並輸出至stream      
            basemap.Save(Response.OutputStream, ImageFormat.Gif);
            // 將產生字串儲存至 Sesssion
            Session["ValidateCode"] = sb.ToString();
            Response.End();
        }
    }
}