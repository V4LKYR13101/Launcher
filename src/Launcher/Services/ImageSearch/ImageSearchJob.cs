using Android.App;
using Android.App.Job;
using Android.Content;
using LauncherApp.Services.DocumentSearch;

namespace LauncherApp.Services.ImageSearch;

[Service(Exported = true, Permission = "android.permission.BIND_JOB_SERVICE")]
internal sealed class ImageSearchJob : JobService
{
    private const int Periodic = 7261;
    private const int Initial = 7262;
    private readonly Dictionary<int, CancellationTokenSource> runs = new();

    internal static void Schedule(Context context, bool initial)
    {
        using var scheduler = (JobScheduler?)context.GetSystemService(JobSchedulerService);
        if (scheduler is null)
            return;
        using var component = new ComponentName(context, Java.Lang.Class.FromType(typeof(ImageSearchJob)));
        void Add(int id, bool repeat)
        {
            using var existing = scheduler.GetPendingJob(id);
            if (existing is not null)
                return;
            using var builder = new JobInfo.Builder(id, component);
            builder.SetRequiresCharging(true);
            if (OperatingSystem.IsAndroidVersionAtLeast(26))
                builder.SetRequiresBatteryNotLow(true);
            if (repeat)
            {
                builder.SetPeriodic((long)TimeSpan.FromHours(6).TotalMilliseconds);
                builder.SetPersisted(true);
            }
            using var job = builder.Build()!;
            scheduler.Schedule(job);
        }
        Add(Periodic, repeat: true);
        if (initial)
            Add(Initial, repeat: false);
    }

    private static bool HasEnabledSearch(Context context) => ImageSearchService.For(context).Enabled ||
        PhotoSearchPack.Text(context).Enabled || PhotoSearchPack.Faces(context).Enabled || DocumentSearchService.For(context).Enabled;

    internal static void CancelIfUnused(Context context)
    {
        if (HasEnabledSearch(context))
            return;
        using var scheduler = (JobScheduler?)context.GetSystemService(JobSchedulerService);
        scheduler?.Cancel(Initial);
        scheduler?.Cancel(Periodic);
    }

    public override bool OnStartJob(JobParameters? parameters)
    {
        if (parameters is null)
            return false;
        var run = new CancellationTokenSource();
        runs[parameters.JobId] = run;
        _ = Index(parameters, run);
        return true;
    }

    public override bool OnStopJob(JobParameters? parameters)
    {
        if (parameters is null || !runs.Remove(parameters.JobId, out var run))
            return false;
        run.Cancel();
        return HasEnabledSearch(this);
    }

    private async Task Index(JobParameters parameters, CancellationTokenSource run)
    {
        // Ensure even a no-op pass completes after OnStartJob has returned.
        await Task.Yield();
        try
        {
            await ImageSearchService.For(this).Index(run.Token);
            await PhotoSearchPack.Text(this).Index(run.Token);
            await PhotoSearchPack.Faces(this).Index(run.Token);
            await DocumentSearchService.For(this).Index(run.Token);
        }
        finally
        {
            if (runs.TryGetValue(parameters.JobId, out var active) && active == run)
            {
                runs.Remove(parameters.JobId);
                JobFinished(parameters, false);
            }
            run.Dispose();
        }
    }
}
