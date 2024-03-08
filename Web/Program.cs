using Data;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.Extensions.DependencyInjection;
using Services.Implemnetation;
using Services.Interaces;
using Web.Extesions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();


builder.Services.ConfigureSQLConnection(builder.Configuration);

builder.Services.ConfigurePageServices();
builder.Services.ConfigureBannerServices();
builder.Services.ConfigureAddress();
builder.Services.ConfigureSocialMedia();
builder.Services.ConfigureFooter();
builder.Services.ConfigureQuestionnarie();
//builder.Services.AddScoped<SurveyContext>();

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

app.UseAuthorization();


app.MapAreaControllerRoute(
    name: "MyAdminArea",
    areaName:"admin",
    pattern: "admin/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern:"{controller=Home}/{action=Index}/{id?}");


app.Run();
