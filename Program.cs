using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using PiperPicker.Proxies;
using System.Net.Http;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

builder.Services.AddHttpClient();
//builder.Services.AddScoped(sp => new SnapProxy(sp.GetRequiredService<IConfiguration>(), sp.GetRequiredService<ILogger<SnapProxy>>()));
builder.Services.AddScoped(sp => new SnapcastClientProxy(sp.GetRequiredService<IConfiguration>(), sp.GetRequiredService<ILogger<SnapcastClientProxy>>()));
builder.Services.AddScoped(sp => new MopidyProxy(sp.GetRequiredService<IHttpClientFactory>(), sp.GetRequiredService<IConfiguration>(), sp.GetRequiredService<ILogger<MopidyProxy>>()));

var app = builder.Build();


app.UseStaticFiles();

app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
