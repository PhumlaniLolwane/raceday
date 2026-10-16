namespace RaceDay.Tests;

public class EventTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    public EventTests(ApiFactory factory) => _factory = factory;

    // ---------- Public vs authenticated ----------
    [Fact]
    public async Task ListEvents_Unauthenticated_Returns200() =>
        Assert.Equal(HttpStatusCode.OK, (await _factory.CreateClient().GetAsync("/api/events")).StatusCode);

    [Fact]
    public async Task CreateEvent_Unauthenticated_Returns401()
    {
        var resp = await _factory.CreateClient().PostAsJsonAsync("/api/events", Helper.EventBody());
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // ---------- Role enforcement ----------
    [Fact]
    public async Task CreateEvent_AsParticipant_Returns403()
    {
        var (participant, _) = await Helper.LoginAs(_factory, "Participant");
        var resp = await participant.PostAsJsonAsync("/api/events", Helper.EventBody());
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task UpdateAndDeleteEvent_AsParticipant_Return403()
    {
        var (organiser, _) = await Helper.LoginAs(_factory, "Organiser");
        var ev = await Helper.CreateEvent(organiser);
        var (participant, _) = await Helper.LoginAs(_factory, "Participant");

        Assert.Equal(HttpStatusCode.Forbidden, (await participant.PutAsJsonAsync($"/api/events/{ev.EventID}", Helper.EventBody("Hacked"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await participant.DeleteAsync($"/api/events/{ev.EventID}")).StatusCode);
    }

    // ---------- Organiser event management ----------
    [Fact]
    public async Task Organiser_CreatesEvent_Returns201_WithAllFields()
    {
        var (organiser, user) = await Helper.LoginAs(_factory, "Organiser");
        var resp = await organiser.PostAsJsonAsync("/api/events", Helper.EventBody("Soweto Fun Run"));

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var ev = await resp.Content.ReadFromJsonAsync<EventDto>();
        Assert.Equal("Soweto Fun Run", ev!.EventName);
        Assert.Equal(user.UserID, ev.OrganiserID);
        Assert.Equal(10m, ev.DistanceKM);
        Assert.Equal("Run", ev.EventType);
    }

    [Fact]
    public async Task CreateEvent_InvalidEventType_Returns400()
    {
        var (organiser, _) = await Helper.LoginAs(_factory, "Organiser");
        var resp = await organiser.PostAsJsonAsync("/api/events", new
        {
            eventName = "Bad", eventDate = DateTime.Today.AddDays(5).ToString("yyyy-MM-dd"),
            location = "X", distanceKM = 5, eventType = "Swim"
        });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_ZeroDistance_Returns400()
    {
        var (organiser, _) = await Helper.LoginAs(_factory, "Organiser");
        var resp = await organiser.PostAsJsonAsync("/api/events", new
        {
            eventName = "Bad", eventDate = DateTime.Today.AddDays(5).ToString("yyyy-MM-dd"),
            location = "X", distanceKM = 0, eventType = "Run"
        });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Organiser_UpdatesOwnEvent_Returns200()
    {
        var (organiser, _) = await Helper.LoginAs(_factory, "Organiser");
        var ev = await Helper.CreateEvent(organiser, "Before");
        var resp = await organiser.PutAsJsonAsync($"/api/events/{ev.EventID}", Helper.EventBody("After"));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal("After", (await resp.Content.ReadFromJsonAsync<EventDto>())!.EventName);
    }

    [Fact]
    public async Task Organiser_CannotUpdateOrDeleteAnotherOrganisersEvent()
    {
        var (owner, _) = await Helper.LoginAs(_factory, "Organiser");
        var ev = await Helper.CreateEvent(owner);
        var (other, _) = await Helper.LoginAs(_factory, "Organiser");

        Assert.Equal(HttpStatusCode.Forbidden, (await other.PutAsJsonAsync($"/api/events/{ev.EventID}", Helper.EventBody("Stolen"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.DeleteAsync($"/api/events/{ev.EventID}")).StatusCode);
    }

    [Fact]
    public async Task Organiser_DeletesOwnEvent_Returns204_ThenItIsGone()
    {
        var (organiser, _) = await Helper.LoginAs(_factory, "Organiser");
        var ev = await Helper.CreateEvent(organiser);
        await Helper.CreateCategory(organiser, ev.EventID);

        Assert.Equal(HttpStatusCode.NoContent, (await organiser.DeleteAsync($"/api/events/{ev.EventID}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await organiser.GetAsync($"/api/events/{ev.EventID}")).StatusCode);
    }

    [Fact]
    public async Task UpdateEvent_NotFound_Returns404()
    {
        var (organiser, _) = await Helper.LoginAs(_factory, "Organiser");
        Assert.Equal(HttpStatusCode.NotFound, (await organiser.PutAsJsonAsync("/api/events/999999", Helper.EventBody())).StatusCode);
    }

    [Fact]
    public async Task GetEvent_Public_IncludesCategories()
    {
        var (organiser, _) = await Helper.LoginAs(_factory, "Organiser");
        var ev = await Helper.CreateEvent(organiser);
        await Helper.CreateCategory(organiser, ev.EventID);

        var resp = await _factory.CreateClient().GetAsync($"/api/events/{ev.EventID}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var detail = await resp.Content.ReadFromJsonAsync<EventDto>();
        Assert.Single(detail!.Categories!);
    }

    [Fact]
    public async Task ListEvents_FiltersByLocation()
    {
        var (organiser, _) = await Helper.LoginAs(_factory, "Organiser");
        await organiser.PostAsJsonAsync("/api/events", new
        {
            eventName = "Cape Cycle", eventDate = DateTime.Today.AddDays(10).ToString("yyyy-MM-dd"),
            location = "Cape Town Waterfront", distanceKM = 50, eventType = "Cycle"
        });

        var list = await _factory.CreateClient().GetFromJsonAsync<List<EventDto>>("/api/events?location=Waterfront");
        Assert.NotEmpty(list!);
        Assert.All(list!, e => Assert.Contains("Waterfront", e.Location));
    }

    // ---------- Categories ----------
    [Fact]
    public async Task Organiser_AddsCategory_Returns201_AndPublicCanList()
    {
        var (organiser, _) = await Helper.LoginAs(_factory, "Organiser");
        var ev = await Helper.CreateEvent(organiser);
        var resp = await organiser.PostAsJsonAsync($"/api/events/{ev.EventID}/categories",
            new { categoryName = "Under 20", distanceKM = 5, maxParticipants = 50, entryFee = 80 });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        var list = await _factory.CreateClient().GetFromJsonAsync<List<CategoryDto>>($"/api/events/{ev.EventID}/categories");
        Assert.Contains(list!, c => c.CategoryName == "Under 20");
    }

    [Fact]
    public async Task AddCategory_AsParticipant_Returns403()
    {
        var (organiser, _) = await Helper.LoginAs(_factory, "Organiser");
        var ev = await Helper.CreateEvent(organiser);
        var (participant, _) = await Helper.LoginAs(_factory, "Participant");

        var resp = await participant.PostAsJsonAsync($"/api/events/{ev.EventID}/categories",
            new { categoryName = "X", distanceKM = 5, maxParticipants = 5, entryFee = 0 });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task AddCategory_ToAnotherOrganisersEvent_Returns403()
    {
        var (owner, _) = await Helper.LoginAs(_factory, "Organiser");
        var ev = await Helper.CreateEvent(owner);
        var (other, _) = await Helper.LoginAs(_factory, "Organiser");

        var resp = await other.PostAsJsonAsync($"/api/events/{ev.EventID}/categories",
            new { categoryName = "X", distanceKM = 5, maxParticipants = 5, entryFee = 0 });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task AddCategory_Unauthenticated_Returns401()
    {
        var resp = await _factory.CreateClient().PostAsJsonAsync("/api/events/1/categories",
            new { categoryName = "X", distanceKM = 5, maxParticipants = 5, entryFee = 0 });
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task AddCategory_InvalidData_Returns400()
    {
        var (organiser, _) = await Helper.LoginAs(_factory, "Organiser");
        var ev = await Helper.CreateEvent(organiser);
        var resp = await organiser.PostAsJsonAsync($"/api/events/{ev.EventID}/categories",
            new { categoryName = "", distanceKM = -1, maxParticipants = 0, entryFee = -5 });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Organiser_UpdatesAndDeletesCategory()
    {
        var (organiser, _) = await Helper.LoginAs(_factory, "Organiser");
        var ev = await Helper.CreateEvent(organiser);
        var cat = await Helper.CreateCategory(organiser, ev.EventID);

        var put = await organiser.PutAsJsonAsync($"/api/categories/{cat.CategoryID}",
            new { categoryName = "Renamed", distanceKM = 21.1, maxParticipants = 10, entryFee = 200 });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.Equal("Renamed", (await put.Content.ReadFromJsonAsync<CategoryDto>())!.CategoryName);

        Assert.Equal(HttpStatusCode.NoContent, (await organiser.DeleteAsync($"/api/categories/{cat.CategoryID}")).StatusCode);
    }
}
