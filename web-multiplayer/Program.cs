using Microsoft.AspNetCore.SignalR;
using TicTacToe.WebMultiplayer.Components;
using TicTacToe.WebMultiplayer.Hubs;
using TicTacToe.WebMultiplayer.Services;

namespace TicTacToe.WebMultiplayer;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();
        builder.Services.AddSignalR();

        builder.Services.AddSingleton<PresenceTracker>();
        builder.Services.AddSingleton<MatchRegistry>();
        builder.Services.AddSingleton<ChallengeBroker>();
        builder.Services.AddScoped<LocalGameSession>();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        app.UseHttpsRedirection();

        app.UseAntiforgery();

        app.MapStaticAssets();
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();
        app.MapHub<GameHub>("/gamehub");

        // Wire ChallengeBroker.Expired to push notifications to clients.
        var hub = app.Services.GetRequiredService<IHubContext<GameHub>>();
        var broker = app.Services.GetRequiredService<ChallengeBroker>();
        broker.Expired += c =>
        {
            hub.Clients.Client(c.FromConnId).SendAsync("OnChallengeExpired", new { challengeId = c.ChallengeId, role = "from" });
            hub.Clients.Client(c.ToConnId)  .SendAsync("OnChallengeExpired", new { challengeId = c.ChallengeId, role = "to" });
        };

        app.Run();
    }
}
