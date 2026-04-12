namespace VaclavikBC.Controllers
{
    using Google.Apis.Calendar.v3.Data;
    using Microsoft.AspNetCore.Authentication;
    using Microsoft.AspNetCore.Mvc;
    using System.Net.Http.Headers;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using VaclavikBC.Enums;
    using VaclavikBC.Models;

    public class AccountController : Controller
    {
        // provider = Google, Facebook, GitHub, Microsoft
        private readonly GoogleController _googleController;
        private readonly MicrosoftController _microsoftController;


        public AccountController(GoogleController googleController, MicrosoftController microsoftController)
        {
            _googleController = googleController;
            _microsoftController = microsoftController;
        }

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
           
            CalendarConnection calendarConnection = new CalendarConnection
            {
                Email = email,
                Provider = provider,
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpirationTime = DateTime.Parse(expiresAt)
            };

            if (provider == null)
            {

            }
            else if (provider == Providers.Google.ToString())
            {
                _googleController.ZiskejData(calendarConnection);
            }
            else if (provider == Providers.Microsoft.ToString())
            {
                //_microsoftController.ZiskejData(calendarConnection);
            }
            else if (provider == Providers.Calendly.ToString())
            {
                throw new NotImplementedException();
            }
            else
            {
                return Content("Chyba při získávání kalendářových dat");
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
                Uspesne prihlaseni. Muzete zavrit toto okno.
                </body>
                </html>
                """, "text/html");
        }
    }

}
