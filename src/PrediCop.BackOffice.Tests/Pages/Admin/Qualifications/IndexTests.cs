using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using PrediCop.BackOffice.Pages.Admin.Qualifications;
using PrediCop.BackOffice.Tests.Helpers;
using PrediCop.Core.DTOs;
using PrediCop.Core.Enums;
using RichardSzalay.MockHttp;
using Xunit;

namespace PrediCop.BackOffice.Tests.Pages.Admin.Qualifications;

public class IndexTests
{
    private static readonly JsonSerializerOptions EnumOpts = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static string SerializeEnums<T>(T value) =>
        JsonSerializer.Serialize(value, EnumOpts);

    private static List<QualificationResponse> MakeSampleQualifications(int count) =>
        Enumerable.Range(1, count).Select(i => new QualificationResponse(
            Id: Guid.NewGuid(),
            AgentId: Guid.NewGuid(),
            AgentFullName: $"Agent {i}",
            AgentBadgeNumber: $"PM-{i:000}",
            Type: QualificationType.APJA,
            Reference: $"REF-{i:000}",
            IssuingAuthority: "Mairie",
            IssuedAt: DateTime.UtcNow.AddYears(-1),
            ExpiresAt: DateTime.UtcNow.AddYears(2),
            Notes: null,
            IsExpired: false,
            ExpiresWithin30Days: false
        )).ToList();

    private static string EmptyAgentsJson() =>
        SerializeEnums(new List<IndexModel.AgentItem>());

    [Fact]
    public async Task OnGetAsync_ApiReturnsQualifications_PopulatesList()
    {
        var (handler, factory) = MockHttpHelper.Create();

        var qualifications = MakeSampleQualifications(2);

        handler.When("https://api.test/api/users")
               .Respond("application/json", EmptyAgentsJson());

        handler.When("https://api.test/api/qualifications")
               .Respond("application/json", SerializeEnums(qualifications));

        handler.When("https://api.test/api/qualifications/expiring")
               .Respond("application/json", SerializeEnums(new List<QualificationResponse>()));

        var model = new IndexModel(factory, MockHttpHelper.NullLogger<IndexModel>());
        await model.OnGetAsync();

        Assert.Equal(2, model.Qualifications.Count);
        Assert.Equal(QualificationType.APJA, model.Qualifications[0].Type);
    }

    [Fact]
    public async Task OnGetAsync_ApiFails_ReturnsEmptyListWithoutException()
    {
        var (handler, factory) = MockHttpHelper.Create();

        handler.When("*").Respond(HttpStatusCode.InternalServerError);

        var model = new IndexModel(factory, MockHttpHelper.NullLogger<IndexModel>())
            .WithPageContext();
        var exception = await Record.ExceptionAsync(() => model.OnGetAsync());

        Assert.Null(exception);
        Assert.Empty(model.Qualifications);
        Assert.Empty(model.Agents);
        Assert.Equal(0, model.ExpiringCount);
    }

    [Fact]
    public async Task OnGetAsync_ExpiringCount_IsSetCorrectly()
    {
        var (handler, factory) = MockHttpHelper.Create();

        var expiring = MakeSampleQualifications(3);

        handler.When("https://api.test/api/users")
               .Respond("application/json", EmptyAgentsJson());

        handler.When("https://api.test/api/qualifications")
               .Respond("application/json", SerializeEnums(new List<QualificationResponse>()));

        handler.When("https://api.test/api/qualifications/expiring")
               .Respond("application/json", SerializeEnums(expiring));

        var model = new IndexModel(factory, MockHttpHelper.NullLogger<IndexModel>());
        await model.OnGetAsync();

        Assert.Equal(3, model.ExpiringCount);
    }
}
