using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using TicTacToeArena.Client;
using TicTacToeArena.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Register HttpClient
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(builder.Configuration["ApiUrl"]!)
});


// Register GameService correctly
builder.Services.AddScoped<GameService>(sp =>
{
    // Pass IConfiguration to GameService constructor
    var config = sp.GetRequiredService<IConfiguration>();
    return new GameService(config);
});

await builder.Build().RunAsync();