using ARGOS.Application.ProcessEngine.Execution;

namespace ARGOS.Application.ProcessEngine.Contracts;

public interface IProcessEngine
{
    Task ExecuteAsync(
        ProcessExecutionContext context,
        CancellationToken cancellationToken = default);
}