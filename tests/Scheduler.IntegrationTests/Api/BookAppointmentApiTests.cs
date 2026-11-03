using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Scheduler.Domain.Enums;
using Scheduler.IntegrationTests.Infrastructure;

namespace Scheduler.IntegrationTests.Api;

/// <summary>FR-01 – FR-04 end to end: HTTP → controller → use case → PostgreSQL (SPEC §7).</summary>
[Collection(ApiCollection.Name)]
[Trait("Category", "Api")]
public sealed class BookAppointmentApiTests(SchedulerApiFactory factory)
{
    private const string Url = "/api/v1/appointments";
    private const string TueNine = "2026-11-10T09:00:00+07:00";

    private readonly HttpClient _client = factory.CreateClient();
    private readonly ApiTestData _data = new(factory);

    private static DateTimeOffset Tue(int hour, int minute = 0) =>
        new(2026, 11, 10, hour, minute, 0, TimeSpan.FromHours(7));

    private Task<HttpResponseMessage> BookAsync(
        Scenario scenario,
        string startTime = TueNine,
        Guid? customerId = null,
        Guid? vehicleId = null,
        Guid? dealershipId = null,
        Guid? serviceTypeId = null) =>
        _client.PostAsJsonAsync(Url, new
        {
            customerId = customerId ?? scenario.CustomerId,
            vehicleId = vehicleId ?? scenario.VehicleId,
            dealershipId = dealershipId ?? scenario.DealershipId,
            serviceTypeId = serviceTypeId ?? ApiTestData.OilChange,
            startTime,
            notes = "Strange noise from front left wheel"
        });

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private static async Task<JsonElement> ShouldBeProblemAsync(
        HttpResponseMessage response, HttpStatusCode status, string code)
    {
        var body = await ReadJsonAsync(response);
        response.StatusCode.Should().Be(status, body.ToString());
        body.GetProperty("code").GetString().Should().Be(code);
        return body;
    }

    // ---- Happy path ----

    [Fact]
    [Trait("AC", "AC-01.1")]
    public async Task Post_FreeBayAndTechnician_Returns201WithAssignedResources()
    {
        var scenario = await _data.CreateDealershipAsync();

        var response = await BookAsync(scenario);

        var body = await ReadJsonAsync(response);
        response.StatusCode.Should().Be(HttpStatusCode.Created, body.ToString());
        response.Headers.Location.Should().NotBeNull();
        body.GetProperty("status").GetString().Should().Be("Confirmed");
        body.GetProperty("startTime").GetString().Should().Be("2026-11-10T09:00:00+07:00");
        body.GetProperty("endTime").GetString().Should().Be("2026-11-10T10:00:00+07:00");
        body.GetProperty("technician").GetProperty("id").GetGuid().Should().Be(scenario.TechnicianIds[0]);
        body.GetProperty("serviceBay").GetProperty("id").GetGuid().Should().Be(scenario.BayIds[0]);
        body.GetProperty("serviceType").GetProperty("code").GetString().Should().Be("OIL_CHANGE");
    }

    [Fact]
    [Trait("AC", "AC-01.2")]
    public async Task Get_AfterBooking_ReturnsFullRecord()
    {
        var scenario = await _data.CreateDealershipAsync();
        var created = await BookAsync(scenario);

        var response = await _client.GetAsync(created.Headers.Location);

        var body = await ReadJsonAsync(response);
        response.StatusCode.Should().Be(HttpStatusCode.OK, body.ToString());
        body.GetProperty("customerId").GetGuid().Should().Be(scenario.CustomerId);
        body.GetProperty("vehicle").GetProperty("id").GetGuid().Should().Be(scenario.VehicleId);
        body.GetProperty("technician").GetProperty("id").GetGuid().Should().Be(scenario.TechnicianIds[0]);
        body.GetProperty("serviceBay").GetProperty("id").GetGuid().Should().Be(scenario.BayIds[0]);
        body.GetProperty("startTime").GetString().Should().Be("2026-11-10T09:00:00+07:00");
        body.GetProperty("endTime").GetString().Should().Be("2026-11-10T10:00:00+07:00");
        body.GetProperty("status").GetString().Should().Be("Confirmed");
    }

    // ---- Availability ----

    [Fact]
    [Trait("AC", "AC-01.3")]
    [Trait("AC", "AC-05.1")]
    public async Task Post_BayBusyForPartOfWindow_Returns409WithSuggestions()
    {
        var scenario = await _data.CreateDealershipAsync();
        var otherVehicle = await _data.CreateVehicleAsync(scenario.CustomerId);
        var otherTechnician = (await _data.CreateDealershipAsync()).TechnicianIds[0];
        await _data.BookDirectlyAsync(scenario, scenario.BayIds[0], otherTechnician, otherVehicle, Tue(9, 30));

        var response = await BookAsync(scenario);

        var body = await ShouldBeProblemAsync(response, HttpStatusCode.Conflict, "SLOT_UNAVAILABLE");
        body.GetProperty("suggestedStartTimes").EnumerateArray().Select(e => e.GetString())
            .Should().Equal("2026-11-10T10:30:00+07:00", "2026-11-10T10:45:00+07:00", "2026-11-10T11:00:00+07:00");
    }

    [Fact]
    [Trait("AC", "AC-01.6")]
    public async Task Post_ResourcesBookedUntilExactlyStart_Returns201()
    {
        var scenario = await _data.CreateDealershipAsync();
        var otherVehicle = await _data.CreateVehicleAsync(scenario.CustomerId);
        await _data.BookDirectlyAsync(scenario, scenario.BayIds[0], scenario.TechnicianIds[0], otherVehicle, Tue(8));

        var response = await BookAsync(scenario);

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    [Trait("AC", "AC-01.4")]
    public async Task Post_OnlyQualifiedTechnicianBusyForPartOfWindow_Returns409()
    {
        var scenario = await _data.CreateDealershipAsync(liftBays: 1, technicians: 1);
        // The technician is busy 08:30–09:30 in another bay; the Lift bay itself stays free.
        var otherBay = await _data.AddBayAsync(scenario, "General 1", BayType.General);
        var otherVehicle = await _data.CreateVehicleAsync(scenario.CustomerId);
        await _data.BookDirectlyAsync(scenario, otherBay, scenario.TechnicianIds[0], otherVehicle, Tue(8, 30));

        var response = await BookAsync(scenario);

        await ShouldBeProblemAsync(response, HttpStatusCode.Conflict, "SLOT_UNAVAILABLE");
    }

    [Fact]
    [Trait("AC", "AC-01.5")]
    public async Task Post_OnlyFreeTechnicianLacksQualification_Returns409()
    {
        var scenario = await _data.CreateDealershipAsync(liftBays: 1, technicians: 0);
        await _data.AddTechnicianAsync(scenario, [Qualification.Brakes]);

        var response = await BookAsync(scenario);

        await ShouldBeProblemAsync(response, HttpStatusCode.Conflict, "SLOT_UNAVAILABLE");
    }

    [Fact]
    [Trait("AC", "AC-01.7")]
    public async Task Post_OnlyFreeBayHasWrongType_Returns409()
    {
        var scenario = await _data.CreateDealershipAsync(liftBays: 0, technicians: 1);
        await _data.AddBayAsync(scenario, "General 1", BayType.General);

        var response = await BookAsync(scenario);

        await ShouldBeProblemAsync(response, HttpStatusCode.Conflict, "SLOT_UNAVAILABLE");
    }

    [Fact]
    [Trait("AC", "AC-01.8")]
    public async Task Post_TwoFreeTechnicians_AssignsTheOneWithFewerBookedMinutes()
    {
        var scenario = await _data.CreateDealershipAsync(liftBays: 1, technicians: 2);
        // Technician 1 already has 120 minutes on Tuesday (13:00–15:00), in another bay.
        var otherBay = await _data.AddBayAsync(scenario, "General 1", BayType.General);
        var otherVehicle = await _data.CreateVehicleAsync(scenario.CustomerId);
        await _data.BookDirectlyAsync(scenario, otherBay, scenario.TechnicianIds[0], otherVehicle, Tue(13));
        await _data.BookDirectlyAsync(scenario, otherBay, scenario.TechnicianIds[0], otherVehicle, Tue(14));

        var response = await BookAsync(scenario);

        var body = await ReadJsonAsync(response);
        response.StatusCode.Should().Be(HttpStatusCode.Created, body.ToString());
        body.GetProperty("technician").GetProperty("id").GetGuid().Should().Be(scenario.TechnicianIds[1]);
    }

    [Fact]
    [Trait("AC", "AC-01.9")]
    public async Task Post_TwoFreeLiftBays_AssignsLowestName()
    {
        var scenario = await _data.CreateDealershipAsync(liftBays: 2, technicians: 1);

        var response = await BookAsync(scenario);

        var body = await ReadJsonAsync(response);
        response.StatusCode.Should().Be(HttpStatusCode.Created, body.ToString());
        body.GetProperty("serviceBay").GetProperty("name").GetString().Should().Be("Bay 1");
    }

    [Fact]
    [Trait("AC", "AC-01.10")]
    public async Task Post_OnlyInactiveBayAndTechnician_Returns409()
    {
        var scenario = await _data.CreateDealershipAsync(liftBays: 0, technicians: 0);
        await _data.AddBayAsync(scenario, "Bay 1", BayType.Lift, isActive: false);
        await _data.AddTechnicianAsync(scenario, [Qualification.General], isActive: false);

        var response = await BookAsync(scenario);

        await ShouldBeProblemAsync(response, HttpStatusCode.Conflict, "SLOT_UNAVAILABLE");
    }

    // ---- Rules and errors ----

    [Fact]
    [Trait("AC", "AC-01.11")]
    public async Task Post_EndsAfterClosing_Returns422OutsideOpeningHours()
    {
        var scenario = await _data.CreateDealershipAsync();

        var response = await BookAsync(scenario, "2026-11-10T16:30:00+07:00");

        await ShouldBeProblemAsync(response, HttpStatusCode.UnprocessableEntity, "OUTSIDE_OPENING_HOURS");
    }

    [Fact]
    [Trait("AC", "AC-01.12")]
    public async Task Post_Sunday_Returns422OutsideOpeningHours()
    {
        var scenario = await _data.CreateDealershipAsync();

        var response = await BookAsync(scenario, "2026-11-15T09:00:00+07:00");

        await ShouldBeProblemAsync(response, HttpStatusCode.UnprocessableEntity, "OUTSIDE_OPENING_HOURS");
    }

    [Fact]
    [Trait("AC", "AC-01.14")]
    public async Task Post_SixtyOneDaysAhead_Returns422OutsideBookingHorizon()
    {
        var scenario = await _data.CreateDealershipAsync();

        // Frozen now is Mon 2026-11-09 08:00 +07:00; 61 days later is Sat 2027-01-09 (an open day).
        var response = await BookAsync(scenario, "2027-01-09T09:00:00+07:00");

        await ShouldBeProblemAsync(response, HttpStatusCode.UnprocessableEntity, "OUTSIDE_BOOKING_HORIZON");
    }

    [Fact]
    [Trait("AC", "AC-01.13")]
    public async Task Post_ThirtyMinutesFromNow_Returns422OutsideBookingHorizon()
    {
        var scenario = await _data.CreateDealershipAsync();

        var response = await BookAsync(scenario, "2026-11-09T08:30:00+07:00");

        await ShouldBeProblemAsync(response, HttpStatusCode.UnprocessableEntity, "OUTSIDE_BOOKING_HORIZON");
    }

    [Theory]
    [InlineData("2026-11-10T09:10:00+07:00")]
    [InlineData("2026-11-10T09:00:30+07:00")]
    [Trait("AC", "AC-01.15")]
    public async Task Post_StartNotOnQuarterHour_Returns400InvalidStartTime(string startTime)
    {
        var scenario = await _data.CreateDealershipAsync();

        var response = await BookAsync(scenario, startTime);

        await ShouldBeProblemAsync(response, HttpStatusCode.BadRequest, "INVALID_START_TIME");
    }

    [Fact]
    [Trait("AC", "AC-01.16")]
    public async Task Post_StartWithoutOffset_Returns400InvalidStartTime()
    {
        var scenario = await _data.CreateDealershipAsync();

        var response = await BookAsync(scenario, "2026-11-10T09:00:00");

        await ShouldBeProblemAsync(response, HttpStatusCode.BadRequest, "INVALID_START_TIME");
    }

    [Fact]
    [Trait("AC", "AC-01.17")]
    public async Task Post_VehicleOfAnotherCustomer_Returns422VehicleNotOwned()
    {
        var scenario = await _data.CreateDealershipAsync();
        var (_, someoneElsesVehicle) = await _data.CreateCustomerWithVehicleAsync();

        var response = await BookAsync(scenario, vehicleId: someoneElsesVehicle);

        await ShouldBeProblemAsync(response, HttpStatusCode.UnprocessableEntity, "VEHICLE_NOT_OWNED");
    }

    [Fact]
    [Trait("AC", "AC-01.18")]
    public async Task Post_VehicleBookedAtAnotherDealership_Returns409VehicleAlreadyBooked()
    {
        var scenario = await _data.CreateDealershipAsync();
        var elsewhere = await _data.CreateDealershipAsync();
        await _data.BookDirectlyAsync(elsewhere, elsewhere.BayIds[0], elsewhere.TechnicianIds[0], scenario.VehicleId, Tue(9, 30));

        var response = await BookAsync(scenario);

        await ShouldBeProblemAsync(response, HttpStatusCode.Conflict, "VEHICLE_ALREADY_BOOKED");
    }

    [Theory]
    [InlineData("customer", "CUSTOMER_NOT_FOUND")]
    [InlineData("vehicle", "VEHICLE_NOT_FOUND")]
    [InlineData("dealership", "DEALERSHIP_NOT_FOUND")]
    [InlineData("serviceType", "SERVICE_TYPE_NOT_FOUND")]
    [Trait("AC", "AC-01.19")]
    public async Task Post_UnknownReference_Returns404WithMatchingCode(string unknown, string code)
    {
        var scenario = await _data.CreateDealershipAsync();
        var missing = Guid.NewGuid();

        var response = unknown switch
        {
            "customer" => await BookAsync(scenario, customerId: missing),
            "vehicle" => await BookAsync(scenario, vehicleId: missing),
            "dealership" => await BookAsync(scenario, dealershipId: missing),
            _ => await BookAsync(scenario, serviceTypeId: missing)
        };

        await ShouldBeProblemAsync(response, HttpStatusCode.NotFound, code);
    }

    [Fact]
    [Trait("AC", "AC-01.20")]
    public async Task Post_MissingFields_Returns400ValidationFailedWithFieldErrors()
    {
        var response = await _client.PostAsJsonAsync(Url, new { notes = "only notes" });

        var body = await ShouldBeProblemAsync(response, HttpStatusCode.BadRequest, "VALIDATION_FAILED");
        var fields = body.GetProperty("errors").EnumerateObject().Select(p => p.Name);
        fields.Should().Contain(new[] { "customerId", "vehicleId", "dealershipId", "serviceTypeId", "startTime" });
    }

    [Fact]
    [Trait("AC", "AC-01.20")]
    public async Task Post_MalformedGuid_Returns400ValidationFailed()
    {
        using var content = new StringContent(
            """{ "customerId": "not-a-guid", "startTime": "2026-11-10T09:00:00+07:00" }""",
            Encoding.UTF8,
            "application/json");

        var response = await _client.PostAsync(Url, content);

        var body = await ShouldBeProblemAsync(response, HttpStatusCode.BadRequest, "VALIDATION_FAILED");
        body.GetProperty("errors").EnumerateObject().Select(p => p.Name).Should().Contain("customerId");
    }

    [Fact]
    [Trait("AC", "AC-01.21")]
    public async Task Get_UnknownId_Returns404AppointmentNotFound()
    {
        var response = await _client.GetAsync($"{Url}/{Guid.NewGuid()}");

        await ShouldBeProblemAsync(response, HttpStatusCode.NotFound, "APPOINTMENT_NOT_FOUND");
    }

    [Fact]
    public async Task ErrorResponse_CarriesTheRequestCorrelationId()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{Url}/{Guid.NewGuid()}");
        request.Headers.Add("X-Correlation-Id", "api-test-correlation");

        var response = await _client.SendAsync(request);

        var body = await ReadJsonAsync(response);
        body.GetProperty("correlationId").GetString().Should().Be("api-test-correlation");
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }
}
