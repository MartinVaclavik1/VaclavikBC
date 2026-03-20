namespace VaclavikBC.Controllers
{
    using Microsoft.AspNetCore.Authentication;
    using Microsoft.AspNetCore.Mvc;
    using System.Net.Http.Headers;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    public class AccountController : Controller
    {
        // provider = Google, Facebook, GitHub, Microsoft
        public IActionResult OAuthLogin(string provider)
        {
            var props = new AuthenticationProperties
            {
                RedirectUri = "/Account/OAuthResponse"
            };

            return Challenge(props, provider);
        }

        public async Task<IActionResult> OAuthResponse()
        {
            var result = await HttpContext.AuthenticateAsync();

            if (!result.Succeeded)
                return Content("Přihlášení selhalo");

            var accessToken = await HttpContext.GetTokenAsync("access_token");
            var refreshToken = await HttpContext.GetTokenAsync("refresh_token");
            var idToken = await HttpContext.GetTokenAsync("id_token");
            var expiresAt = await HttpContext.GetTokenAsync("expires_at");

            var claims = result.Principal.Identities.FirstOrDefault()?.Claims;

            var email = claims?.FirstOrDefault(c => c.Type.Contains("email"))?.Value;
            var name = claims?.FirstOrDefault(c => c.Type.EndsWith("name"))?.Value; //ends with, protože je zde pole nameidentifier, které to triggerovalo
            var provider = result.Properties?.Items[".AuthScheme"];

            //TODO přidat ukládání dat uživatele a tokenů
            // DB.User.CreateOrLink(email, provider, providerUserId)

            //TODO smazat výpisy
            Console.WriteLine($"Access token: {accessToken}");
            Console.WriteLine($"mail: {email}, name: {name},provider: {provider}");
            Console.WriteLine($"Refresh token: {refreshToken}");
            Console.WriteLine($"idToken: {idToken}");
            Console.WriteLine($"expiresAt: {expiresAt}");
            foreach (var item in claims)
            {
                Console.WriteLine(item.ToString());
            }

            using var client = new HttpClient();

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await client.GetAsync(
                "https://www.googleapis.com/calendar/v3/users/me/calendarList");

            var content = await response.Content.ReadAsStringAsync();

            Console.WriteLine(content);

            string pageToken = null;
            using (StreamWriter writer = new StreamWriter("vystup.txt"))
            {
                do
                {
                    //https://www.googleapis.com/calendar/v3/calendars/{NAZEV KALENDARE (id)}/events    bcpracemartin@gmail.com
                    var url = "https://www.googleapis.com/calendar/v3/calendars/primary/events" +
                    "?singleEvents=false";

                    if (pageToken != null)
                        url += $"&pageToken={pageToken}";

                    var ulohy = await client.GetAsync(url);
                    ulohy.EnsureSuccessStatusCode();

                    var json = await ulohy.Content.ReadAsStringAsync();


                    using var doc = JsonDocument.Parse(json);


                    if (doc.RootElement.TryGetProperty("nextPageToken", out var token))
                    {
                        pageToken = token.GetString();
                    }
                    else { pageToken = null; }

                    //Console.WriteLine(json);
                    writer.WriteLine(json);
                } while (pageToken != null);
            }
            //zobrazí oznámení o přihlášení a zavře okno po časové prodlevě
            return Content("""
                <!DOCTYPE html>
                <html>
                <head>
                    <script>
                        (function(){
                            try {
                                if (window.opener && !window.opener.closed) {
                                    window.opener.postMessage('oauth-success', '*');
                                }
                            } catch(e){}

                            setTimeout(function(){
                                window.close();
                            }, 3000);
                        })();
                    </script>
                </head>
                <body>
                Úspěšné přihlášení. Můžete zavřít toto okno.
                </body>
                </html>
                """, "text/html");
        }
    }

}
