using PiperPicker.Proxies;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddHttpClient();

// xyzzy can this be Singleton, like MopidyProxy?
builder.Services.AddScoped(sp => new SnapcastClientProxy(sp.GetRequiredService<IConfiguration>(), sp.GetRequiredService<ILogger<SnapcastClientProxy>>()));

builder.Services.AddSingleton(sp => new MopidyProxy(sp.GetRequiredService<IHttpClientFactory>(), sp.GetRequiredService<IConfiguration>(), sp.GetRequiredService<ILogger<MopidyProxy>>()));

var app = builder.Build();
app.UseStaticFiles();
app.UseRouting();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
