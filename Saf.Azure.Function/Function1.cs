using System;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Saf.Azure.Function
{
    public static class GetQuotes
    {
        [FunctionName("GetQuotes")]
        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)]HttpRequest req, TraceWriter log)
        {
            // Read request body to get ticker
            dynamic requestBody = JsonConvert.DeserializeObject(await new StreamReader(req.Body).ReadToEndAsync());
            string ticker = requestBody?.ticker;

            // Check bearer token
            var headerValue = req.Headers["Authorization"].ToString();
            if (String.IsNullOrWhiteSpace(headerValue))
            {
                return new StatusCodeResult(401);
            }

            JObject result = new JObject
            {
                ["symbol"] = ticker
            };

            try
            {
                var bearerToken = headerValue.Split(' ')[1];
                if (!ValidateToken(bearerToken))
                {
                    return new StatusCodeResult(401);
                }

                // Call third party API for getting data
                using (var httpClient = new HttpClient())
                {
                    // Get api key from key-vault
                    var apiKey = await GetSecret("saf-azure-fuction-getquotes-apikey", "saf-service-dev");
                    var url = $"https://www.alphavantage.co/query?function=TIME_SERIES_DAILY&symbol={ticker}&apikey={apiKey}";
                    var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, url);
                    var httpResponseMessage = await httpClient.SendAsync(httpRequestMessage);
                    if (httpResponseMessage.IsSuccessStatusCode)
                    {
                        var responseContent = await httpResponseMessage.Content.ReadAsStringAsync();
                        var api_data_1 = JObject.Parse(responseContent);

                        // Process returned data
                        result["open"] = api_data_1["Time Series (Daily)"][DateTime.Now.ToString("yyyy-MM-dd")]["1. open"];
                        result["high"] = api_data_1["Time Series (Daily)"][DateTime.Now.ToString("yyyy-MM-dd")]["2. high"];
                        result["low"] = api_data_1["Time Series (Daily)"][DateTime.Now.ToString("yyyy-MM-dd")]["3. low"];
                        result["close"] = api_data_1["Time Series (Daily)"][DateTime.Now.ToString("yyyy-MM-dd")]["4. close"];
                        result["volume"] = api_data_1["Time Series (Daily)"][DateTime.Now.ToString("yyyy-MM-dd")]["5. volume"];

                        // Call second API to get earnings data
                        url = $"https://api.iextrading.com/1.0/stock/{ticker}/earnings";
                        httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, url);
                        httpResponseMessage = await httpClient.SendAsync(httpRequestMessage);
                        if (httpResponseMessage.IsSuccessStatusCode)
                        {
                            responseContent = await httpResponseMessage.Content.ReadAsStringAsync();
                            var api_data_2 = JObject.Parse(responseContent);
                            result["earnings"] = api_data_2["earnings"];
                        }   
                    }
                }
                return new JsonResult(result);
            }
            catch (Exception ex)
            {
                log.Info(ex.Message);
            }

            return new StatusCodeResult(500);
        }

        private static bool ValidateToken(string accessToken)
        {
            DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var audiences = Environment.GetEnvironmentVariable("Audiences").Split('|');
            var handler = new JwtSecurityTokenHandler();
            if (!handler.CanReadToken(accessToken))
            {
                return false;
            }

            var jwtToken = handler.ReadToken(accessToken) as JwtSecurityToken;
            var audience = jwtToken.Claims.First(claim => claim.Type == "aud").Value;
            var expiry = epoch.AddSeconds(Convert.ToInt64(jwtToken.Claims.First(claim => claim.Type == "exp").Value));

            if (audiences.Contains(audience) && expiry > DateTime.UtcNow)
            {
                return true;
            }

            return false;
        }

        private static async Task<string> GetSecret(string secretName, string keyVaultName)
        {
            // Use MSI to access key-vault
            var tokenProvider = new AzureServiceTokenProvider(Environment.GetEnvironmentVariable("TokenProviderConnectionStirng"));
            var keyVaultClient = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(tokenProvider.KeyVaultTokenCallback));
            var secretBundle = await keyVaultClient.GetSecretAsync($"https://{keyVaultName}.vault.azure.net", secretName);
            return secretBundle?.Value;
        }
    }
}
