namespace MaintenancePlanning.Api.Hosting;

public sealed class ApplicationLifecycleState
{
    private int _startupCompleted;
    private int _stopping;

    public bool StartupCompleted => Volatile.Read(ref _startupCompleted) == 1;

    public bool IsStopping => Volatile.Read(ref _stopping) == 1;

    public void MarkStartupComplete()
    {
        Volatile.Write(ref _startupCompleted, 1);
    }

    public void MarkStopping()
    {
        Volatile.Write(ref _stopping, 1);
    }
}
