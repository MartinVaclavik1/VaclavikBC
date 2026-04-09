namespace VaclavikBC.Controllers
{
    using Microsoft.AspNetCore.Authentication;
    using Microsoft.AspNetCore.Mvc;
    using System.Net.Http.Headers;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using VaclavikBC.Enums;

    public class AccountController : Controller
    {
        // provider = Google, Facebook, GitHub, Microsoft
        private readonly GoogleController _googleController;

        public AccountController(GoogleController googleController)
        {
            _googleController = googleController;
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

            //TODO přidat ukládání dat uživatele a tokenů
            // DB.User.CreateOrLink(email, provider, providerUserId)

            if (provider == null)
            {

            }
            else if (provider == Providers.Google.ToString())
            {
                _googleController.ZiskejData(accessToken);
            }
            else if (provider == Providers.Microsoft.ToString())
            {
                throw new NotImplementedException();
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
