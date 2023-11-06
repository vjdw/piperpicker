using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using PiperPicker.HostedServices;
using PiperPicker.Proxies;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

builder.Services.AddScoped<SnapProxy>(sp => new SnapProxy(sp.GetRequiredService<IConfiguration>(), sp.GetRequiredService<ILogger<SnapScopedProcessingService>>()));

var app = builder.Build();


app.UseStaticFiles();

app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
