using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Data;

namespace RaceDay.Tests;

/// <summary>Boots the real API in memory, swapping SQL Server for an in-memory database.</summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = "RaceDayTests-" + Guid.NewGuid();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor is not null) services.Remove(descriptor);
            services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(_dbName));
        });
    }
}

public static class Helper
{
    public const string Password = "Password123!";

    public static string UniqueEmail() => $"{Guid.NewGuid():N}@test.com";

    /// <summary>Registers a fresh user and logs in. The returned client carries the session cookie.</summary>
    public static async Task<(HttpClient Client, UserDto User)> LoginAs(ApiFactory factory, string role)
    {
        var client = factory.CreateClient();
        var email = UniqueEmail();
        var reg = await client.PostAsJsonAsync("/api/auth/register",
            new { fullName = "Test " + role, email, password = Password, role });
        reg.EnsureSuccessStatusCode();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        login.EnsureSuccessStatusCode();
        return (client, (await login.Content.ReadFromJsonAsync<UserDto>())!);
    }

    public static object EventBody(string name = "Test Run", int daysAhead = 30) => new
    {
        eventName = name,
        description = "A test event",
        eventDate = DateTime.Today.AddDays(daysAhead).ToString("yyyy-MM-dd"),
        location = "Johannesburg",
        distanceKM = 10.0,
        eventType = "Run"
    };

    public static async Task<EventDto> CreateEvent(HttpClient organiser, string name = "Test Run")
    {
        var resp = await organiser.PostAsJsonAsync("/api/events", EventBody(name));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<EventDto>())!;
    }

    public static async Task<CategoryDto> CreateCategory(HttpClient organiser, int eventId, int max = 100)
    {
        var resp = await organiser.PostAsJsonAsync($"/api/events/{eventId}/categories",
            new { categoryName = "10km", distanceKM = 10.0, maxParticipants = max, entryFee = 150 });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<CategoryDto>())!;
    }

    public static async Task<EnrolmentDto> Enrol(HttpClient participant, int categoryId)
    {
        var resp = await participant.PostAsJsonAsync("/api/enrolments", new { categoryId });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<EnrolmentDto>())!;
    }
}
