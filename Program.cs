using PiperPicker.Proxies;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddHttpClient();

// Needs to be scoped to match how snapcast's JSON RPC calls work (the RPC sender doesn't receive notifications of that change).
// So if piperpicker is open in multiple browsers, each needs its own instance of SnapcastClientProxy to correctly get notifications of volume change.
builder.Services.AddScoped(sp => new SnapcastClientProxy(sp.GetRequiredService<IConfiguration>(), sp.GetRequiredService<ILogger<SnapcastClientProxy>>()));

builder.Services.AddSingleton(sp => new MopidyProxy(sp.GetRequiredService<IHttpClientFactory>(), sp.GetRequiredService<IConfiguration>(), sp.GetRequiredService<ILogger<MopidyProxy>>()));

var app = builder.Build();
app.UseStaticFiles();
app.UseRouting();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
