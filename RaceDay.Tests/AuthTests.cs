using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Data;

namespace RaceDay.Tests;

public class AuthTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    public AuthTests(ApiFactory factory) => _factory = factory;

    private static object NewUser(string email, string role = "Participant", string password = Helper.Password) =>
        new { fullName = "Test User", email, password, role };

    // ---------- Registration ----------
    [Theory]
    [InlineData("Participant")]
    [InlineData("Organiser")]
    public async Task Register_Valid_Returns201_AndNeverReturnsThePassword(string role)
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/auth/register", NewUser(Helper.UniqueEmail(), role));

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(role, body);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        var client = _factory.CreateClient();
        var email = Helper.UniqueEmail();
        await client.PostAsJsonAsync("/api/auth/register", NewUser(email));
        var second = await client.PostAsJsonAsync("/api/auth/register", NewUser(email));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Register_InvalidRole_Returns400()
    {
        var resp = await _factory.CreateClient().PostAsJsonAsync("/api/auth/register", NewUser(Helper.UniqueEmail(), "Admin"));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Register_ShortPassword_Returns400()
    {
        var resp = await _factory.CreateClient().PostAsJsonAsync("/api/auth/register", NewUser(Helper.UniqueEmail(), password: "short"));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Register_InvalidEmail_Returns400()
    {
        var resp = await _factory.CreateClient().PostAsJsonAsync("/api/auth/register", NewUser("not-an-email"));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Register_StoresAHashedPassword_NotThePlainText()
    {
        var email = Helper.UniqueEmail();
        await _factory.CreateClient().PostAsJsonAsync("/api/auth/register", NewUser(email));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == email);

        Assert.NotEqual(Helper.Password, user.PasswordHash);
        Assert.DoesNotContain(Helper.Password, user.PasswordHash);
    }

    // ---------- Login ----------
    [Fact]
    public async Task Login_ValidCredentials_Returns200_WithRole()
    {
        var (_, user) = await Helper.LoginAs(_factory, "Organiser");
        Assert.Equal("Organiser", user.Role);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var client = _factory.CreateClient();
        var email = Helper.UniqueEmail();
        await client.PostAsJsonAsync("/api/auth/register", NewUser(email));
        var resp = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "WrongPassword1!" });
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Login_UnknownEmail_Returns401()
    {
        var resp = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { email = Helper.UniqueEmail(), password = Helper.Password });
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Login_MissingFields_Returns400()
    {
        var resp = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email = "" });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Logout_EndsTheSession()
    {
        var (client, _) = await Helper.LoginAs(_factory, "Participant");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/users/me")).StatusCode);

        var logout = await client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/users/me")).StatusCode);
    }

    // ---------- Authenticated vs unauthenticated ----------
    [Fact]
    public async Task GetProfile_Unauthenticated_Returns401() =>
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync("/api/users/me")).StatusCode);

    [Fact]
    public async Task GetProfile_Authenticated_ReturnsOwnProfile()
    {
        var (client, user) = await Helper.LoginAs(_factory, "Participant");
        var resp = await client.GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var me = await resp.Content.ReadFromJsonAsync<UserDto>();
        Assert.Equal(user.UserID, me!.UserID);
    }

    [Fact]
    public async Task UpdateProfile_Authenticated_ChangesName()
    {
        var (client, _) = await Helper.LoginAs(_factory, "Participant");
        var email = Helper.UniqueEmail();
        var resp = await client.PutAsJsonAsync("/api/users/me", new { fullName = "New Name", email });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var updated = await resp.Content.ReadFromJsonAsync<UserDto>();
        Assert.Equal("New Name", updated!.FullName);
    }

    [Fact]
    public async Task UpdateProfile_EmailTakenByAnotherUser_Returns409()
    {
        var (_, other) = await Helper.LoginAs(_factory, "Organiser");
        var (client, _) = await Helper.LoginAs(_factory, "Participant");
        var resp = await client.PutAsJsonAsync("/api/users/me", new { fullName = "Someone", email = other.Email });
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    [Fact]
    public async Task UpdateProfile_Unauthenticated_Returns401()
    {
        var resp = await _factory.CreateClient().PutAsJsonAsync("/api/users/me",
            new { fullName = "X", email = Helper.UniqueEmail() });
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }
}
