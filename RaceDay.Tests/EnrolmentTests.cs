namespace RaceDay.Tests;

public class EnrolmentTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    public EnrolmentTests(ApiFactory factory) => _factory = factory;

    private async Task<(HttpClient Organiser, EventDto Event, CategoryDto Category)> SetUpEvent(int max = 100)
    {
        var (organiser, _) = await Helper.LoginAs(_factory, "Organiser");
        var ev = await Helper.CreateEvent(organiser);
        var cat = await Helper.CreateCategory(organiser, ev.EventID, max);
        return (organiser, ev, cat);
    }

    [Fact]
    public async Task Participant_Enrols_Returns201_AndLinksParticipantEventAndCategory()
    {
        var (_, ev, cat) = await SetUpEvent();
        var (participant, user) = await Helper.LoginAs(_factory, "Participant");

        var resp = await participant.PostAsJsonAsync("/api/enrolments", new { categoryId = cat.CategoryID });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var enrolment = await resp.Content.ReadFromJsonAsync<EnrolmentDto>();
        Assert.Equal(user.UserID, enrolment!.ParticipantID);
        Assert.Equal(cat.CategoryID, enrolment.CategoryID);
        Assert.Equal(ev.EventID, enrolment.EventID);
        Assert.Equal("Registered", enrolment.Status);
    }

    [Fact]
    public async Task Enrol_Twice_InSameCategory_Returns409()
    {
        var (_, _, cat) = await SetUpEvent();
        var (participant, _) = await Helper.LoginAs(_factory, "Participant");
        await Helper.Enrol(participant, cat.CategoryID);

        var second = await participant.PostAsJsonAsync("/api/enrolments", new { categoryId = cat.CategoryID });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Enrol_WhenCategoryIsFull_Returns409()
    {
        var (_, _, cat) = await SetUpEvent(max: 1);
        var (first, _) = await Helper.LoginAs(_factory, "Participant");
        var (second, _) = await Helper.LoginAs(_factory, "Participant");
        await Helper.Enrol(first, cat.CategoryID);

        var resp = await second.PostAsJsonAsync("/api/enrolments", new { categoryId = cat.CategoryID });
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    [Fact]
    public async Task Enrol_UnknownCategory_Returns404()
    {
        var (participant, _) = await Helper.LoginAs(_factory, "Participant");
        var resp = await participant.PostAsJsonAsync("/api/enrolments", new { categoryId = 999999 });
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Enrol_Unauthenticated_Returns401()
    {
        var resp = await _factory.CreateClient().PostAsJsonAsync("/api/enrolments", new { categoryId = 1 });
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Enrol_AsOrganiser_Returns403()
    {
        var (organiser, _, cat) = await SetUpEvent();
        var resp = await organiser.PostAsJsonAsync("/api/enrolments", new { categoryId = cat.CategoryID });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Participant_SeesOnlyOwnEnrolments()
    {
        var (_, _, cat) = await SetUpEvent();
        var (a, userA) = await Helper.LoginAs(_factory, "Participant");
        var (b, _) = await Helper.LoginAs(_factory, "Participant");
        await Helper.Enrol(a, cat.CategoryID);
        await Helper.Enrol(b, cat.CategoryID);

        var mine = await a.GetFromJsonAsync<List<EnrolmentDto>>("/api/users/me/enrolments");
        Assert.Single(mine!);
        Assert.Equal(userA.UserID, mine![0].ParticipantID);
    }

    [Fact]
    public async Task MyEnrolments_AsOrganiser_Returns403()
    {
        var (organiser, _) = await Helper.LoginAs(_factory, "Organiser");
        Assert.Equal(HttpStatusCode.Forbidden, (await organiser.GetAsync("/api/users/me/enrolments")).StatusCode);
    }

    [Fact]
    public async Task Organiser_ViewsEnrolmentsForOwnEvent()
    {
        var (organiser, ev, cat) = await SetUpEvent();
        var (participant, _) = await Helper.LoginAs(_factory, "Participant");
        await Helper.Enrol(participant, cat.CategoryID);

        var list = await organiser.GetFromJsonAsync<List<EnrolmentDto>>($"/api/events/{ev.EventID}/enrolments");
        Assert.Single(list!);
    }

    [Fact]
    public async Task EventEnrolments_ForbiddenForOtherOrganiser_ParticipantAndAnonymous()
    {
        var (_, ev, _) = await SetUpEvent();
        var (otherOrganiser, _) = await Helper.LoginAs(_factory, "Organiser");
        var (participant, _) = await Helper.LoginAs(_factory, "Participant");

        Assert.Equal(HttpStatusCode.Forbidden, (await otherOrganiser.GetAsync($"/api/events/{ev.EventID}/enrolments")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await participant.GetAsync($"/api/events/{ev.EventID}/enrolments")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync($"/api/events/{ev.EventID}/enrolments")).StatusCode);
    }

    [Fact]
    public async Task Participant_CancelsOwnEnrolment_Returns204_AndCanReEnrol()
    {
        var (_, _, cat) = await SetUpEvent();
        var (participant, _) = await Helper.LoginAs(_factory, "Participant");
        var enrolment = await Helper.Enrol(participant, cat.CategoryID);

        Assert.Equal(HttpStatusCode.NoContent, (await participant.DeleteAsync($"/api/enrolments/{enrolment.EnrolmentID}")).StatusCode);

        var mine = await participant.GetFromJsonAsync<List<EnrolmentDto>>("/api/users/me/enrolments");
        Assert.Equal("Cancelled", mine!.Single().Status);

        var again = await participant.PostAsJsonAsync("/api/enrolments", new { categoryId = cat.CategoryID });
        Assert.Equal(HttpStatusCode.Created, again.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotCancelSomeoneElsesEnrolment_Returns403()
    {
        var (_, _, cat) = await SetUpEvent();
        var (owner, _) = await Helper.LoginAs(_factory, "Participant");
        var (other, _) = await Helper.LoginAs(_factory, "Participant");
        var enrolment = await Helper.Enrol(owner, cat.CategoryID);

        Assert.Equal(HttpStatusCode.Forbidden, (await other.DeleteAsync($"/api/enrolments/{enrolment.EnrolmentID}")).StatusCode);
    }

    [Fact]
    public async Task EventWithEnrolments_CannotBeDeleted_Returns409()
    {
        var (organiser, ev, cat) = await SetUpEvent();
        var (participant, _) = await Helper.LoginAs(_factory, "Participant");
        await Helper.Enrol(participant, cat.CategoryID);

        Assert.Equal(HttpStatusCode.Conflict, (await organiser.DeleteAsync($"/api/events/{ev.EventID}")).StatusCode);
    }
}
