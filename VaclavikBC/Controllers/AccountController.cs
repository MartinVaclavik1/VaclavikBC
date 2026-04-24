namespace VaclavikBC.Controllers
{
    using Microsoft.AspNetCore.Authentication;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Mvc;
    using System.Security.Claims;
    using VaclavikBC.Enums;
    using VaclavikBC.Models;

    public class AccountController : Controller
    {
        // provider = Google, Facebook, GitHub, Microsoft
        private readonly GoogleController _googleController;
        private readonly MicrosoftController _microsoftController;
        private readonly CalendlyController _calendlyController;


        public AccountController(GoogleController googleController, MicrosoftController microsoftController, 
            CalendlyController calendlyController)
        {
            _googleController = googleController;
            _microsoftController = microsoftController;
            _calendlyController = calendlyController;
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
            var result = await HttpContext.AuthenticateAsync(IdentityConstants.ExternalScheme);

            if (!result.Succeeded)
                return Content("Přihlášení selhalo");

            var accessToken = result.Properties?.GetTokenValue("access_token");
            var refreshToken = result.Properties?.GetTokenValue("refresh_token");
            var expiresAt = result.Properties?.GetTokenValue("expires_at");
            var claims = result.Principal.Identities.FirstOrDefault()?.Claims;

            var email = claims?.FirstOrDefault(c => c.Type.Contains("email"))?.Value;
            var name = claims?.FirstOrDefault(c => c.Type.EndsWith("name"))?.Value; //ends with, protože je zde pole nameidentifier, které to triggerovalo
            var provider = result.Properties?.Items[".AuthScheme"];
           

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return Unauthorized("User isn't logged in");

            if (provider == null)
            {
                return Content("Error getting calendar data");
            }

            CalendarConnection calendarConnection = new CalendarConnection
            {
                UserId = userId,
                Email = email,
                Provider = provider,
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpirationTime = DateTime.Parse(expiresAt)
            };

            switch (provider)
            {
                case nameof(Providers.Google):
                    await _googleController.ZiskejData(calendarConnection);
                    break;
                case nameof(Providers.Microsoft):
                    await _microsoftController.ZiskejData(calendarConnection);
                    break;
                case nameof(Providers.Calendly):
                    await _calendlyController.ZiskejData(calendarConnection);
                    break;
                default:
                    return Content("Error getting calendar data");
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
                    Login successful. You can close this window.
                </body>
                </html>
                """, "text/html");
        }
    }

}
