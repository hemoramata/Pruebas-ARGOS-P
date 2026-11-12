using ARGOS.Application.ProcessEngine.Execution;

namespace ARGOS.Application.ProcessEngine.Contracts;

public interface IActionDispatcher
{
    Task DispatchAsync(
        ProcessExecutionContext context,
        CancellationToken cancellationToken = default);
}