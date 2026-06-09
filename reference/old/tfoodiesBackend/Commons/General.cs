using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web;

using tfoodies.Models;
using tfoodies.Service;
using tfoodies.Libs;

namespace tfoodiesBackend.Commons
{
    public static class General
    {
        private static string HashKey = ConfigurationManager.AppSettings["HashKey"];
        private static string HashIV = ConfigurationManager.AppSettings["HashIV"];

        private static tfoodiesEntities db = new tfoodiesEntities();
        private static IPurchasesService purchasesService = new PurchasesService();
        private static IPurchaseDetailsService purchasedetailsService = new PurchaseDetailsService();
        private static IExpendituresService expendituresService = new ExpendituresService();
        private static IOutcomesService outcomesService = new OutcomesService();
        private static IReturnsService returnsService = new ReturnsService();
        private static IRefoundsService refoundsService = new RefoundsService(db);
        private static IOrdersService ordersService = new OrdersService(db);

        public static void CheckPurchaseStatus(Guid purchasedetailid, int detailstatus)
        {
            Purchasedetails pd = purchasedetailsService.GetByID(purchasedetailid);
            pd.status = detailstatus;

            purchasedetailsService.Update(pd);
            purchasedetailsService.SaveChanges();

            Purchases purchase = purchasesService.GetByID(pd.purchaseid);

            // 全部品項數
            int a = purchase.Purchasedetails.Count();
            // 已入庫的品項數
            int c = purchase.Purchasedetails.Where(p => p.status > 0).Count();
            int r = a - c;

            if(r == a)
            {
                //未入庫
                purchase.status = 1;
            }else if(r == 0)
            {
                //已入庫
                purchase.status = 2;
            }else
            {
                //部分入庫
                purchase.status = 3;
            }

            purchasesService.Update(purchase);
            purchasesService.SaveChanges();
        }

        public static void CheckExpenditureStatus(Guid expenditureid)
        {
            Expenditures expenditure = expendituresService.GetByID(expenditureid);

            int totalsum = expenditure.Expendituredetails.Sum(a => a.price);

            IEnumerable<Outcomes> outcomes = outcomesService.Get().Where(a => a.expenditureid == expenditureid);
            int totalpay = (outcomes == null) ? 0 : outcomes.Sum(a => a.amount);

            if(totalpay == 0)
            {
                expenditure.status = (int)EnumExpenditureStatus.未付款;
            }else if(totalpay != 0 && totalpay < totalsum)
            {
                expenditure.status = (int)EnumExpenditureStatus.部分付款;
            }else if(totalpay == totalsum)
            {
                expenditure.status = (int)EnumExpenditureStatus.已付款;
            }

            expendituresService.Update(expenditure);
            expendituresService.SaveChanges();
        }

        public static NameValueCollection GetQueryString(string strurl)
        {
            Uri url = new Uri(strurl);
            string queryString = url.Query;

            if(queryString != "")
            {
                queryString = queryString.Replace("?", "");
                NameValueCollection result = new NameValueCollection(StringComparer.OrdinalIgnoreCase);

                if (!string.IsNullOrEmpty(queryString))
                {
                    string[] Query = queryString.Split('&');
                    foreach (string pars in Query)
                    {
                        string[] pas = pars.Split('=');
                        result[pas[0]] = pas[1];
                    }
                }
                return result;
            }

            return null;
        }

        public static string EncryptAES256(string source)//加密        
        {
            string sSecretKey = HashKey;
            string iv = HashIV;

            byte[] sourceBytes = AddPKCS7Padding(Encoding.UTF8.GetBytes(source), 32);             
            var aes = new RijndaelManaged();             
            aes.Key = Encoding.UTF8.GetBytes(sSecretKey);             
            aes.IV = Encoding.UTF8.GetBytes(iv);             
            aes.Mode = CipherMode.CBC;             
            aes.Padding = PaddingMode.None; 
            ICryptoTransform transform = aes.CreateEncryptor(); 
 
            return ByteArrayToHex(transform.TransformFinalBlock(sourceBytes, 0, sourceBytes.Length)).ToLower(); 
        } 

        public static string DecryptAES256(string encryptData)//解密         
        {
            string sSecretKey = HashKey;
            string iv = HashIV; 
 
            var encryptBytes = HexStringToByteArray(encryptData.ToUpper());             
            var aes = new RijndaelManaged();             
            aes.Key = Encoding.UTF8.GetBytes(sSecretKey);             
            aes.IV = Encoding.UTF8.GetBytes(iv);             
            aes.Mode = CipherMode.CBC;             
            aes.Padding = PaddingMode.None;             
            ICryptoTransform transform = aes.CreateDecryptor(); 
 
            return Encoding.UTF8.GetString(RemovePKCS7Padding(transform.TransformFinalBlock (encryptBytes, 0, encryptBytes.Length))); 
        }

        private static byte[] AddPKCS7Padding(byte[] data, int iBlockSize) 
        {             
            int iLength = data.Length;             
            byte cPadding = (byte)(iBlockSize - (iLength % iBlockSize));             
            var output = new byte[iLength + cPadding];             
            Buffer.BlockCopy(data, 0, output, 0, iLength);             
            
            for (var i = iLength; i < output.Length; i++)                 
                output[i] = (byte)cPadding;             
            
            return output;         
        } 
 
        private static byte[] RemovePKCS7Padding(byte[] data)         
        {             
            int iLength = data[data.Length - 1];             
            var output = new byte[data.Length - iLength];             
            Buffer.BlockCopy(data, 0, output, 0, output.Length);             
            
            return output;         
        } 
 
        private static string ByteArrayToHex(byte[] barray)         
        {             
            char[] c = new char[barray.Length * 2];             
            byte b;             
            for (int i = 0; i < barray.Length; ++i)             
            {                 
                b = ((byte)(barray[i] >> 4));                 
                c[i * 2] = (char)(b > 9 ? b + 0x37 : b + 0x30);                 
                b = ((byte)(barray[i] & 0xF));                 
                c[i * 2 + 1] = (char)(b > 9 ? b + 0x37 : b + 0x30);             
            } 
 
            return new string(c);         
        } 
 
        private static byte[] HexStringToByteArray(string hexString)         
        { 
            int hexStringLength = hexString.Length;             
            byte[] b = new byte[hexStringLength / 2];             
            for (int i = 0; i < hexStringLength; i += 2)             
            {                 
                int topChar = (hexString[i] > 0x40 ? hexString[i] - 0x37 : hexString[i] - 0x30) << 4;                 
                int bottomChar = hexString[i + 1] > 0x40 ? hexString[i + 1] - 0x37 : hexString[i + 1] - 0x30;                 
                b[i / 2] = Convert.ToByte(topChar + bottomChar);             
            }             
            
            return b;         
        }
    }
}