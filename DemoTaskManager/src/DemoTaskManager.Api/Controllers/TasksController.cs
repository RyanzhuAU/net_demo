using DemoTaskManager.Api.Infrastructure;
using DemoTaskManager.Application.Abstractions;
using DemoTaskManager.Contracts.Common;
using DemoTaskManager.Contracts.Messaging;
using DemoTaskManager.Contracts.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace DemoTaskManager.Api.Controllers;

[ApiController]
[Route("api/tasks")]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public sealed class TasksController(ITaskService taskService) : ControllerBase
{
    /// <summary>Returns a deterministically ordered, server-side paginated list of tasks.</summary>
    [HttpGet(Name = nameof(GetTasks))]
    [ProducesResponseType(typeof(PagedResultDto<TaskItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResultDto<TaskItemDto>>> GetTasks(
        [FromQuery] TaskQueryParameters query,
        CancellationToken cancellationToken)
    {
        var result = await taskService.GetPagedAsync(query, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    [HttpGet("{id:guid}", Name = nameof(GetTaskById))]
    [ProducesResponseType(typeof(TaskItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskItemDto>> GetTaskById(Guid id, CancellationToken cancellationToken)
    {
        var task = await taskService.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        return task is null ? TaskNotFound(id) : Ok(task);
    }

    [HttpPost(Name = nameof(CreateTask))]
    [ProducesResponseType(typeof(TaskItemDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<TaskItemDto>> CreateTask(
        [FromBody] CreateTaskItemRequest request,
        CancellationToken cancellationToken)
    {
        var created = await taskService.CreateAsync(request, cancellationToken).ConfigureAwait(false);
        return CreatedAtRoute(nameof(GetTaskById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}", Name = nameof(UpdateTask))]
    [ProducesResponseType(typeof(TaskItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskItemDto>> UpdateTask(
        Guid id,
        [FromBody] UpdateTaskItemRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await taskService.UpdateAsync(id, request, cancellationToken).ConfigureAwait(false);
        return Ok(updated);
    }

    [HttpDelete("{id:guid}", Name = nameof(DeleteTask))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTask(Guid id, CancellationToken cancellationToken)
    {
        await taskService.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>Enqueues a task-processing command on the Azure Service Bus queue.</summary>
    [HttpPost("{id:guid}/process", Name = nameof(ProcessTask))]
    [ProducesResponseType(typeof(ServiceBusTaskMessageDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ServiceBusTaskMessageDto>> ProcessTask(
        Guid id,
        [FromBody] ProcessTaskItemRequest request,
        CancellationToken cancellationToken)
    {
        var message = await taskService
            .RequestProcessingAsync(id, request, HttpContext.GetCorrelationId(), cancellationToken)
            .ConfigureAwait(false);

        return Accepted(message);
    }

    private ActionResult TaskNotFound(Guid id) =>
        Problem(
            title: "Resource not found.",
            detail: $"TaskItem '{id}' was not found.",
            statusCode: StatusCodes.Status404NotFound);
}
