using Data;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Services.Implemnetation;
using Services.Interaces;
using Web.Extesions;
using Web.ViewComponents;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient();


var config = builder.Configuration;

//builder.Services.AddDbContext<SurveyContext>(options =>
//{
//    options.UseSqlServer(config.GetConnectionString("SurveyVista"), cfg => cfg.MigrationsAssembly("Web"));
//});


builder.Services.AddRazorPages();


builder.Services.ConfigureSQLConnection(config);
builder.Services.ConfigurePageServices();
builder.Services.ConfigureBannerServices();
builder.Services.ConfigureAddress();
builder.Services.ConfigureSocialMedia();
builder.Services.ConfigureFooter();
builder.Services.ConfigureQuestionnarie();
builder.Services.ConfigureQuestion();
builder.Services.AddScoped<SurveyContext>();
builder.Services.AddTransient<NavigationViewComponent>();
builder.Services.ConfigureNewsLetter();
builder.Services.MailConfiguration();
builder.Services.MailStatConfiguration();
builder.Services.ConfigureDashboard();
builder.Services.UserResponseConfiguration();
builder.Services.ConfigureOpenAI(config);
builder.Services.AddSignalR();




var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}



app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();


app.MapControllerRoute(
    name: "page",
    pattern: "{slug}", defaults: new { Controller = "Home", Action = "Index" });
app.Use(async (context, next) =>
{
    context.Request.EnableBuffering(); // Enable buffering for reading request body multiple times
    await next.Invoke();
});

app.MapAreaControllerRoute(
    name: "MyAdminArea",
    areaName:"admin",
    pattern: "admin/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern:"{controller=Home}/{action=Index}/{id?}");


app.MapHub<NotificationHub>("/notificationHub");

app.Run();
