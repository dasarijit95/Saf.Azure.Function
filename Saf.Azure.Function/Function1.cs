using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json.Linq;

namespace Saf.Azure.Function
{
    public static class GetQuotes
    {
        [FunctionName("GetQuotes")]
        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)]HttpRequest req, TraceWriter log)
        {
            string ticker = req.Query["name"];
            string responseContent = string.Empty;

            var headerValue = req.Headers["Authorization"].ToString();
            if (String.IsNullOrWhiteSpace(headerValue))
            {
                return new StatusCodeResult(403);
            }

            try
            {
                var bearerToken = headerValue.Split(' ')[1];
                if (!ValidateTokenAsync(bearerToken))
                {
                    return new StatusCodeResult(403);
                }

                using (var httpClient = new HttpClient())
                {
                    var url = $"https://www.alphavantage.co/query?function=TIME_SERIES_DAILY&symbol=fb&apikey=1S0EIMAF3ORTOAIS";
                    var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, url);
                    var httpResponseMessage = await httpClient.SendAsync(httpRequestMessage);
                    if (httpResponseMessage.IsSuccessStatusCode)
                    {
                        responseContent = await httpResponseMessage.Content.ReadAsStringAsync();
                        var response = JObject.Parse(responseContent);

                        // Extract today's data
                        var data = response["Time Series (Daily)"][DateTime.Now.ToString("yyyy-MM-dd")];
                        return new JsonResult(data);
                    }
                }
            }
            catch (Exception ex)
            {
                log.Info(ex.Message);
            }

            return new StatusCodeResult(500);
        }

        private static bool ValidateTokenAsync(string accessToken)
        {
            DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var handler = new JwtSecurityTokenHandler();
            if (!handler.CanReadToken(accessToken))
            {
                return false;
            }

            var jwtToken = handler.ReadToken(accessToken) as JwtSecurityToken;
            var audience = jwtToken.Claims.First(claim => claim.Type == "aud").Value;
            var expiry = epoch.AddSeconds(Convert.ToInt64(jwtToken.Claims.First(claim => claim.Type == "exp").Value));

            if (audience.Equals("4de0af9a-e121-4413-b8c4-fde3f2519ba0") && expiry > DateTime.UtcNow)
            {
                return true;
            }

            return false;
        }
    }
}
