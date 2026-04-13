using AspNet.Security.OAuth.Calendly;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.EntityFrameworkCore;
using Microsoft.Graph;
using Microsoft.Identity.Web;
using NodaTime;
using System.Text.Json.Serialization;
using VaclavikBC.Controllers;
using VaclavikBC.Data;
using VaclavikBC.Hubs;
using VaclavikBC.Services;
using VaclavikBC.Services.Interfaces;

var builder = Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddDbContext<VaclavikBCContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("VaclavikBCContext") ?? throw new InvalidOperationException("Connection string 'VaclavikBCContext' not found.")));

builder.Services.AddScoped<ISyncService, SyncService>();
builder.Services.AddScoped<GoogleController>();
builder.Services.AddScoped<MicrosoftController>();

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.MaxDepth = 64;
    });

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

    .AddGoogle(options =>   //potřebuje cookie nahoře, ale to zas nepustí Microsoft
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
    //TODO zprovoznit microsoft a calendly
//.AddMicrosoftAccount("Microsoft", options => {    //staré
//    options.ClientId = builder.Configuration["AzureAd:ClientId"];
//    options.ClientSecret = builder.Configuration["AzureAd:ClientSecret"];
//    options.Scope.Add("wl.calendars");
//    options.Scope.Add("wl.offline_access");
//    options.SaveTokens = true;
//})
.AddCalendly("Calendly", options =>
{
    options.ClientId = builder.Configuration["Calendly:ClientId"];
    options.ClientSecret = builder.Configuration["Calendly:ClientSecret"];
    options.SaveTokens = true;
    options.CallbackPath = "/signin-calendly";
})
//.AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"), "Microsoft")
//    .EnableTokenAcquisitionToCallDownstreamApi()
//    .AddInMemoryTokenCaches();

//builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
//.AddMicrosoftIdentityWebApp(options =>
//    {
//        options.ClientId = builder.Configuration["AzureAd:ClientId"];
//        options.ClientSecret = builder.Configuration["AzureAd:ClientSecret"];
//        options.Instance = builder.Configuration["AzureAd:Instance"];
//        options.TenantId = builder.Configuration["AzureAd:TenantId"];
//        options.CallbackPath = builder.Configuration["AzureAd:CallbackPath"];
//        //builder.Configuration.GetSection("AzureAd").Bind(options);
//        options.SaveTokens = true;       
//        options.ResponseType = "code";  
//        options.Scope.Add("offline_access");

//        options.Scope.Add("email");            
//        options.Scope.Add("Calendars.Read");
//        //options.ResponseType = "code";
//        //options.SaveTokens = true; 
//        //options.Scope.Add("Calendars.Read");
//        //options.Scope.Add("offline_access");
//    }, openIdConnectScheme: "Microsoft", cookieScheme: CookieAuthenticationDefaults.AuthenticationScheme)
//    .EnableTokenAcquisitionToCallDownstreamApi()
//    .AddInMemoryTokenCaches();
.AddOpenIdConnect("Microsoft", options =>
{
    options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.Authority = "https://login.microsoftonline.com/common/v2.0";
    options.ClientId = builder.Configuration["AzureAd:ClientId"];
    options.ClientSecret = builder.Configuration["AzureAd:ClientSecret"];
    options.ResponseType = "code";
    options.SaveTokens = true;               
    options.Scope.Add("openid");
    options.Scope.Add("profile");
    options.Scope.Add("email");
    options.Scope.Add("offline_access");     
    options.Scope.Add("Calendars.Read");
    options.CallbackPath = "/signin-microsoft";
    options.TokenValidationParameters.ValidateIssuer = false;
});

builder.Services.AddScoped<GraphServiceClient>(sp =>
{
    var tokenAcquisition = sp.GetRequiredService<ITokenAcquisition>();
    var scopes = new[] { "https://graph.microsoft.com/Calendars.Read" };
    return new GraphServiceClient(
        new DelegateAuthenticationProvider(async (requestMessage) =>
        {
            var accessToken = await tokenAcquisition.GetAccessTokenForUserAsync(scopes);
            requestMessage.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        })
    );
});

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

app.MapControllers();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();
app.MapHub<CalendarSyncHub>("/calendarSyncHub");
app.Run();
