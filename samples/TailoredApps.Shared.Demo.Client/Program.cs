using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using TailoredApps.Demo.Client;
using TailoredApps.Demo.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// The Web API serves this client, so its address is the address of the page.
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<Session>();
builder.Services.AddScoped<Basket>();
builder.Services.AddScoped<ShopApi>();
builder.Services.AddMudServices();

await builder.Build().RunAsync();
