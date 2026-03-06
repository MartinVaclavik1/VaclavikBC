namespace VaclavikBC
{
    using Microsoft.AspNetCore.Authentication;
    using Microsoft.AspNetCore.Mvc;

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

            Console.WriteLine($"Access token: {accessToken}");
            Console.WriteLine($"Refresh token: {refreshToken}");
            Console.WriteLine($"idToken: {idToken}");
            Console.WriteLine($"expiresAt: {expiresAt}");

            var claims = result.Principal.Identities.FirstOrDefault()?.Claims;

            var email = claims?.FirstOrDefault(c => c.Type.Contains("email"))?.Value;
            var name = claims?.FirstOrDefault(c => c.Type.EndsWith("name"))?.Value; //ends with, protože je zde pole nameidentifier, které to triggerovalo
            var provider = result.Properties?.Items[".AuthScheme"];

            //TODO přidat ukládání dat uživatele a tokenů
            // DB.User.CreateOrLink(email, provider, providerUserId)
            Console.WriteLine($"mail: {email}, name: {name},provider: {provider}");
            foreach (var item in claims)
            {
                Console.WriteLine(item.ToString());
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
