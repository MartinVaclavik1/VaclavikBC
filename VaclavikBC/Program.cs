using AspNet.Security.OAuth.Calendly;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.Identity.Web;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddControllersWithViews();

builder.Services.AddAuthentication(
//    options =>
//{
//    options.DefaultScheme = IdentityConstants.ApplicationScheme;
//    options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
//}
CookieAuthenticationDefaults.AuthenticationScheme
)
    .AddCookie(options =>
{
    options.LoginPath = "/Account/Login";
})
    .AddGoogle(options =>
    {
        options.ClientId = builder.Configuration["Google:ClientId"];
        options.ClientSecret = builder.Configuration["Google:ClientSecret"];
        options.Scope.Add("https://www.googleapis.com/auth/calendar.readonly");
        options.SaveTokens = true; // Store tokens in the authentication properties

        //umožní ukládat refresh token
        options.Events.OnRedirectToAuthorizationEndpoint = context =>
        {
            var url = context.RedirectUri;
            url += (url.Contains('?') ? "&" : "?") + "access_type=offline&prompt=consent";
            context.Response.Redirect(url);
            return Task.CompletedTask;
        };
    })
.AddMicrosoftAccount("Microsoft", options => {
     options.ClientId = builder.Configuration["AzureAd:ClientId"];
    options.ClientSecret = builder.Configuration["AzureAd:ClientSecret"];
    options.CallbackPath = "/signin-microsoft";
 })
.AddCalendly("Calendly", options =>
{
    options.ClientId = builder.Configuration["Calendly:ClientId"];
    options.ClientSecret = builder.Configuration["Calendly:ClientSecret"];
    options.CallbackPath = "/signin-calendly";
});

//builder.Services.AddAuthentication()
//    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"))
//    .EnableTokenAcquisitionToCallDownstreamApi()
//        .AddMicrosoftGraph(builder.Configuration.GetSection("MicrosoftGraph"))
//        .AddInMemoryTokenCaches();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

app.Run();
