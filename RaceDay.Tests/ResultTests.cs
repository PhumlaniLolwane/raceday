namespace RaceDay.Tests;

public class ResultTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    public ResultTests(ApiFactory factory) => _factory = factory;

    private static object Finished(int position = 1, string time = "01:52:34") =>
        new { finishTime = time, position, status = "Finished" };

    private async Task<(HttpClient Organiser, EventDto Event, HttpClient Participant, EnrolmentDto Enrolment)> SetUp()
    {
        var (organiser, _) = await Helper.LoginAs(_factory, "Organiser");
        var ev = await Helper.CreateEvent(organiser);
        var cat = await Helper.CreateCategory(organiser, ev.EventID);
        var (participant, _) = await Helper.LoginAs(_factory, "Participant");
        var enrolment = await Helper.Enrol(participant, cat.CategoryID);
        return (organiser, ev, participant, enrolment);
    }

    [Fact]
    public async Task Organiser_CapturesResult_Returns201()
    {
        var (organiser, ev, _, enrolment) = await SetUp();
        var resp = await organiser.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentID}/results", Finished(3));

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var result = await resp.Content.ReadFromJsonAsync<ResultDto>();
        Assert.Equal(3, result!.Position);
        Assert.Equal(new TimeSpan(1, 52, 34), result.FinishTime);
        Assert.Equal(ev.EventID, result.EventID);
    }

    [Fact]
    public async Task CaptureResult_AsParticipant_Returns403()
    {
        var (_, _, participant, enrolment) = await SetUp();
        var resp = await participant.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentID}/results", Finished());
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task CaptureResult_Unauthenticated_Returns401()
    {
        var (_, _, _, enrolment) = await SetUp();
        var resp = await _factory.CreateClient().PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentID}/results", Finished());
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task CaptureResult_ByOrganiserOfADifferentEvent_Returns403()
    {
        var (_, _, _, enrolment) = await SetUp();
        var (otherOrganiser, _) = await Helper.LoginAs(_factory, "Organiser");
        var resp = await otherOrganiser.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentID}/results", Finished());
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task CaptureResult_UnknownEnrolment_Returns404()
    {
        var (organiser, _) = await Helper.LoginAs(_factory, "Organiser");
        var resp = await organiser.PostAsJsonAsync("/api/enrolments/999999/results", Finished());
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task CaptureResult_Twice_Returns409()
    {
        var (organiser, _, _, enrolment) = await SetUp();
        await organiser.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentID}/results", Finished());
        var second = await organiser.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentID}/results", Finished(2));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task CaptureResult_FinishedWithoutTime_Returns400()
    {
        var (organiser, _, _, enrolment) = await SetUp();
        var resp = await organiser.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentID}/results",
            new { position = 1, status = "Finished" });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task CaptureResult_InvalidStatus_Returns400()
    {
        var (organiser, _, _, enrolment) = await SetUp();
        var resp = await organiser.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentID}/results",
            new { finishTime = "00:30:00", position = 1, status = "Winner" });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Organiser_CorrectsResult_Returns200()
    {
        var (organiser, _, _, enrolment) = await SetUp();
        var created = await (await organiser.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentID}/results", Finished(5)))
            .Content.ReadFromJsonAsync<ResultDto>();

        var put = await organiser.PutAsJsonAsync($"/api/results/{created!.ResultID}", Finished(2, "01:40:00"));
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.Equal(2, (await put.Content.ReadFromJsonAsync<ResultDto>())!.Position);
    }

    [Fact]
    public async Task CorrectResult_AsParticipant_Returns403()
    {
        var (organiser, _, participant, enrolment) = await SetUp();
        var created = await (await organiser.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentID}/results", Finished()))
            .Content.ReadFromJsonAsync<ResultDto>();

        var put = await participant.PutAsJsonAsync($"/api/results/{created!.ResultID}", Finished(9));
        Assert.Equal(HttpStatusCode.Forbidden, put.StatusCode);
    }

    [Fact]
    public async Task Participant_SeesOwnResults_AndNotOthers()
    {
        var (organiser, ev, participantA, enrolmentA) = await SetUp();
        var cat = (await organiser.GetFromJsonAsync<List<CategoryDto>>($"/api/events/{ev.EventID}/categories"))!.First();
        var (participantB, _) = await Helper.LoginAs(_factory, "Participant");
        var enrolmentB = await Helper.Enrol(participantB, cat.CategoryID);

        await organiser.PostAsJsonAsync($"/api/enrolments/{enrolmentA.EnrolmentID}/results", Finished(1));
        await organiser.PostAsJsonAsync($"/api/enrolments/{enrolmentB.EnrolmentID}/results", Finished(2, "02:00:00"));

        var mine = await participantA.GetFromJsonAsync<List<ResultDto>>("/api/users/me/results");
        Assert.Single(mine!);
        Assert.Equal(enrolmentA.EnrolmentID, mine![0].EnrolmentID);
    }

    [Fact]
    public async Task MyResults_AsOrganiser_Returns403_AndAnonymousReturns401()
    {
        var (organiser, _) = await Helper.LoginAs(_factory, "Organiser");
        Assert.Equal(HttpStatusCode.Forbidden, (await organiser.GetAsync("/api/users/me/results")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync("/api/users/me/results")).StatusCode);
    }

    [Fact]
    public async Task Leaderboard_IsPublic_AndOrderedByPosition()
    {
        var (organiser, ev, _, enrolmentA) = await SetUp();
        var cat = (await organiser.GetFromJsonAsync<List<CategoryDto>>($"/api/events/{ev.EventID}/categories"))!.First();
        var (participantB, _) = await Helper.LoginAs(_factory, "Participant");
        var enrolmentB = await Helper.Enrol(participantB, cat.CategoryID);

        await organiser.PostAsJsonAsync($"/api/enrolments/{enrolmentA.EnrolmentID}/results", Finished(2, "02:00:00"));
        await organiser.PostAsJsonAsync($"/api/enrolments/{enrolmentB.EnrolmentID}/results", Finished(1, "01:30:00"));

        var board = await _factory.CreateClient().GetFromJsonAsync<List<ResultDto>>($"/api/events/{ev.EventID}/results");
        Assert.Equal(new[] { 1, 2 }, board!.Select(r => r.Position!.Value).ToArray());
    }

    [Fact]
    public async Task Leaderboard_UnknownEvent_Returns404() =>
        Assert.Equal(HttpStatusCode.NotFound, (await _factory.CreateClient().GetAsync("/api/events/999999/results")).StatusCode);
}
