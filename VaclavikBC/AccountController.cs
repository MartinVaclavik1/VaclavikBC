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
                return Content("Login failed");

            var claims = result.Principal.Identities.FirstOrDefault()?.Claims;

            var email = claims?.FirstOrDefault(c => c.Type.Contains("email"))?.Value;
            var name = claims?.FirstOrDefault(c => c.Type.EndsWith("name"))?.Value; //ends with, protože je zde pole nameidentifier, které to triggerovalo
            var provider = result.Properties?.Items[".AuthScheme"];

            // ✅ SAVE / LINK USER HERE
            // DB.User.CreateOrLink(email, provider, providerUserId)
            Console.WriteLine($"mail: {email}, name: {name},provider: {provider}");
            foreach (var item in claims)
            {
                Console.WriteLine(item.ToString());
            }
            //    return Content(@"
            //    <script>
            //        window.opener.location.reload();
            //        window.close();
            //    </script>
            //", "text/html");
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
