using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DemoTaskManager.Contracts.Common;
using DemoTaskManager.Contracts.Messaging;
using DemoTaskManager.Contracts.Tasks;
using DemoTaskManager.Domain.Entities;
using FluentAssertions;
using TaskStatus = DemoTaskManager.Domain.Enums.TaskStatus;

namespace DemoTaskManager.Api.Tests;

public class TasksApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private HttpClient CreateClient() => factory.CreateClient();

    private async Task<Guid> SeedAsync(
        string title,
        TaskStatus status = TaskStatus.Todo,
        DateTime? dueDateUtc = null)
    {
        await using var context = factory.CreateDbContext();
        var entity = TaskItem.Create(title, null, status, dueDateUtc, DateTime.UtcNow);
        context.Tasks.Add(entity);
        await context.SaveChangesAsync();
        return entity.Id;
    }

    [Fact]
    public async Task Get_health_live_returns_healthy()
    {
        var response = await CreateClient().GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Healthy");
    }

    [Fact]
    public async Task Post_creates_a_task_and_returns_201_with_a_location_header()
    {
        var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/tasks", new CreateTaskItemRequest
        {
            Title = "Created through the API",
            Description = "Integration test",
            Status = TaskStatus.Todo
        }, Json);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var created = await response.Content.ReadFromJsonAsync<TaskItemDto>(Json);
        created!.Title.Should().Be("Created through the API");

        var fetched = await client.GetFromJsonAsync<TaskItemDto>($"/api/tasks/{created.Id}", Json);
        fetched!.Id.Should().Be(created.Id);
    }

    [Fact]
    public async Task Post_with_an_empty_title_returns_400_ProblemDetails()
    {
        var response = await CreateClient().PostAsJsonAsync("/api/tasks", new CreateTaskItemRequest
        {
            Title = "   "
        }, Json);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("validation");
        body.Should().Contain("correlationId");
    }

    [Fact]
    public async Task Get_by_id_returns_404_for_an_unknown_task()
    {
        var response = await CreateClient().GetAsync($"/api/tasks/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Put_updates_an_existing_task()
    {
        var id = await SeedAsync("Before update");
        var client = CreateClient();

        var response = await client.PutAsJsonAsync($"/api/tasks/{id}", new UpdateTaskItemRequest
        {
            Title = "After update",
            Status = TaskStatus.Completed
        }, Json);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await response.Content.ReadFromJsonAsync<TaskItemDto>(Json);
        updated!.Title.Should().Be("After update");
        updated.Status.Should().Be(TaskStatus.Completed);
    }

    [Fact]
    public async Task Delete_removes_the_task_and_returns_204()
    {
        var id = await SeedAsync("Delete through the API");
        var client = CreateClient();

        (await client.DeleteAsync($"/api/tasks/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetAsync($"/api/tasks/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_supports_status_filtering_and_paging()
    {
        await SeedAsync("Filtered A", TaskStatus.Completed);
        await SeedAsync("Filtered B", TaskStatus.Completed);
        await SeedAsync("Filtered C", TaskStatus.Todo);

        var client = CreateClient();

        var page = await client.GetFromJsonAsync<PagedResultDto<TaskItemDto>>(
            "/api/tasks?status=Completed&pageNumber=1&pageSize=1", Json);

        page!.PageSize.Should().Be(1);
        page.Items.Should().HaveCount(1);
        page.Items.Should().OnlyContain(t => t.Status == TaskStatus.Completed);
        page.TotalCount.Should().BeGreaterThanOrEqualTo(2);
        page.HasNextPage.Should().BeTrue();
    }

    [Fact]
    public async Task Post_process_publishes_a_Service_Bus_message_and_returns_202()
    {
        var id = await SeedAsync("Queue me");
        var client = CreateClient();

        var response = await client.PostAsJsonAsync($"/api/tasks/{id}/process", new ProcessTaskItemRequest
        {
            TargetStatus = TaskStatus.InProgress,
            Reason = "integration test"
        }, Json);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var message = await response.Content.ReadFromJsonAsync<ServiceBusTaskMessageDto>(Json);
        message!.TaskId.Should().Be(id);
        message.CorrelationId.Should().NotBeNullOrWhiteSpace();

        factory.Publisher.Messages.Should().Contain(m => m.TaskId == id);
    }

    [Fact]
    public async Task Api_routes_are_excluded_from_the_SPA_fallback()
    {
        var response = await CreateClient().GetAsync("/api/does-not-exist");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.Should().NotBe("text/html");
    }

    [Fact]
    public async Task Runtime_configuration_is_served_for_the_SPA()
    {
        var response = await CreateClient().GetAsync("/config.json");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("apiBaseUrl");
    }

    [Fact]
    public async Task Responses_carry_security_headers_and_a_correlation_id()
    {
        var response = await CreateClient().GetAsync("/api/tasks");

        response.Headers.GetValues("X-Content-Type-Options").Should().Contain("nosniff");
        response.Headers.GetValues("X-Correlation-Id").Should().NotBeEmpty();
    }
}
