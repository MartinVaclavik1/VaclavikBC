using AspNet.Security.OAuth.Calendly;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
using VaclavikBC.Controllers;
using VaclavikBC.Data;
using VaclavikBC.Hubs;
using VaclavikBC.Services;
using VaclavikBC.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddDbContext<VaclavikBCContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("VaclavikBCContext") ?? throw new InvalidOperationException("Connection string 'VaclavikBCContext' not found.")));

builder.Services.AddScoped<ISyncService, SyncService>();
builder.Services.AddScoped<GoogleController>();

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
    .AddGoogle(options =>
    {
        options.ClientId = builder.Configuration["Google:ClientId"];
        options.ClientSecret = builder.Configuration["Google:ClientSecret"];
        options.Scope.Add("https://www.googleapis.com/auth/calendar.readonly");
        options.SaveTokens = true; // Store tokens in the authentication properties

        //umo�n� ukl�dat refresh token
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
    options.SaveTokens = true;
    options.CallbackPath = "/signin-microsoft";
 })
.AddCalendly("Calendly", options =>
{
    options.ClientId = builder.Configuration["Calendly:ClientId"];
    options.ClientSecret = builder.Configuration["Calendly:ClientSecret"];
    options.SaveTokens = true;
    options.CallbackPath = "/signin-calendly";
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
