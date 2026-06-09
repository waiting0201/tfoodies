using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace tfoodies.Libs
{
    public class GoogleReCaptcha
    {
        public bool Success { get; set; }

        public bool GetCaptchaResponse(string message)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    var content = new FormUrlEncodedContent(new[] {
                        new KeyValuePair<string, string>("secret", "6LcLZz8UAAAAALE6-maED4AiQRWMwA4lVPUC3U3n"),
                        new KeyValuePair<string, string>("response", message),
                    });

                    var result = client.PostAsync("https://www.google.com/recaptcha/api/siteverify", content).Result;
                    var resultContent = result.Content.ReadAsStringAsync().Result;
                    var data = JsonConvert.DeserializeObject<GoogleReCaptcha>(resultContent);

                    return data.Success;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
