namespace DemoTaskManager.Application.Common.Exceptions;

public sealed class NotFoundException(string resource, object key)
    : Exception($"{resource} '{key}' was not found.")
{
    public string Resource { get; } = resource;

    public object Key { get; } = key;
}
