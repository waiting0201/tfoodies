using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Web;

using tfoodies.Models;
using tfoodies.Service;
using tfoodies.Libs;

namespace tfoodies.Commons
{
    public static class General
    {
        private static IDiscountsService discountsService = new DiscountsService();
        private static IProductsService productsService = new ProductsService();

        public static DiscountResponse GetDiscount()
        {
            string discountcode = (string)HttpContext.Current.Session["DiscountCode"];

            DiscountResponse dr = new DiscountResponse();

            if(discountcode != "" && discountcode != null)
            {
                Discounts discount = discountsService.Get().Where(a => a.discountcode == discountcode).FirstOrDefault();
                if (discount != null)
                {
                    if (discount.isdisable == 0)
                    {
                        if (discount.expiredate != null && DateTime.Compare(Convert.ToDateTime(discount.expiredate), DateTime.Now.ToUniversalTime().AddHours(8)) < 0)
                        {
                            dr.rscode = "260";
                            dr.rsmessage = "折扣碼使用過期";
                            dr.discount = 0;
                            HttpContext.Current.Session["DiscountCode"] = "";
                        }
                        else if (discount.startdate != null && DateTime.Compare(DateTime.Now.ToUniversalTime().AddHours(8), Convert.ToDateTime(discount.startdate)) < 0)
                        {
                            dr.rscode = "280";
                            dr.rsmessage = "折扣碼尚無法使用";
                            dr.discount = 0;
                            HttpContext.Current.Session["DiscountCode"] = "";
                        }
                        else if (discount.isonetime == 1 && discount.Orders.Count() > 0)
                        {
                            dr.rscode = "300";
                            dr.rsmessage = "折扣碼已使用";
                            dr.discount = 0;
                            HttpContext.Current.Session["DiscountCode"] = "";
                        }
                        else
                        {
                            dr.rscode = "200";
                            dr.rsmessage = "";
                            dr.discountid = discount.discountid;

                            if (discount.istype == (int)EnumDiscountType.折扣)
                            {
                                int totaldiscount = 0;
                                Cart ca = new Cart();

                                IEnumerable<CartItem> cartitems = ca.Contents();

                                foreach(CartItem item in cartitems)
                                {
                                    int discountprice = Convert.ToInt32(Math.Round(item.Quantity * item.FixPrice * discount.v, MidpointRounding.AwayFromZero));

                                    int price = (item.SubTotal >= discountprice) ? discountprice : item.SubTotal;

                                    totaldiscount = totaldiscount + (item.SubTotal - price);
                                }

                                dr.discount = totaldiscount;
                            }
                            else
                            {
                                dr.discount = Convert.ToInt32(discount.v);
                            }
                        }
                    }
                    else
                    {
                        dr.rscode = "240";
                        dr.rsmessage = "折扣碼無法使用";
                        dr.discount = 0;
                        HttpContext.Current.Session["DiscountCode"] = "";
                    }
                }
                else
                {
                    dr.rscode = "220";
                    dr.rsmessage = "查無此折扣碼";
                    dr.discount = 0;
                    HttpContext.Current.Session["DiscountCode"] = "";
                }
            }
            else
            {
                dr.rscode = "320";
                dr.rsmessage = "沒有輸入折扣碼";
                dr.discount = 0;
                HttpContext.Current.Session["DiscountCode"] = "";
            }

            return dr;
        }

        public static int GetFreight(int totalprice)
        {
            int freightset = Convert.ToInt32(ConfigurationManager.AppSettings["freightset"]);
            int freightlimit = Convert.ToInt32(ConfigurationManager.AppSettings["freightlimit"]);

            int f = 0;
            if (totalprice != 0)
            {
                f = (totalprice >= freightlimit) ? 0 : freightset;
            }

            return f;
        }

        public static int GetAmountPrice(int totalprice)
        {
            DiscountResponse dr = GetDiscount();

            int a = totalprice;
            a = a + GetFreight(a) - dr.discount;
            return a;
        }

        public static string GetClientIP()
        {
            string ip = string.Empty;
            if (HttpContext.Current.Request.ServerVariables["HTTP_VIA"] == null)
            {
                ip = HttpContext.Current.Request.ServerVariables["REMOTE_ADDR"].ToString();
                ip = (ip == "::1") ? "114.35.29.141" : ip;
            }
            else
            {
                ip = HttpContext.Current.Request.ServerVariables["HTTP_X_FORWARDED_FOR"].ToString();  
            }
            return ip;
        }

        public static string EncryptCookie(string source)
        {
            DESCryptoServiceProvider des = new DESCryptoServiceProvider();
            byte[] key = Encoding.ASCII.GetBytes("16816888");
            byte[] iv = Encoding.ASCII.GetBytes("88888888");
            byte[] dataByteArray = Encoding.UTF8.GetBytes(source);

            des.Key = key;
            des.IV = iv;

            string encrypt = string.Empty;
            using(MemoryStream ms = new MemoryStream())
            using(CryptoStream cs = new CryptoStream(ms, des.CreateEncryptor(), CryptoStreamMode.Write))
            {
                cs.Write(dataByteArray, 0, dataByteArray.Length);
                cs.FlushFinalBlock();
                encrypt = Convert.ToBase64String(ms.ToArray());
            }

            return encrypt;
        }

        public static string DecryptCookie(string encrypt)
        {
            DESCryptoServiceProvider des = new DESCryptoServiceProvider();
            byte[] key = Encoding.ASCII.GetBytes("16816888");
            byte[] iv = Encoding.ASCII.GetBytes("88888888");
            
            des.Key = key;
            des.IV = iv;

            byte[] dataByteArray = Convert.FromBase64String(encrypt);
            using(MemoryStream ms = new MemoryStream())
            {
                using(CryptoStream cs = new CryptoStream(ms, des.CreateDecryptor(), CryptoStreamMode.Write))
                {
                    cs.Write(dataByteArray, 0, dataByteArray.Length);
                    cs.FlushFinalBlock();
                    return Encoding.UTF8.GetString(ms.ToArray());
                }
            }
        }
    }
}