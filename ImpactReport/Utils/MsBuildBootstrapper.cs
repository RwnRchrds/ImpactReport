namespace ImpactReport.Utils;
using Microsoft.Build.Locator;

public static class MsBuildBootstrapper
{
    public static void Register()
    {
        if (MSBuildLocator.IsRegistered)
            return;

        var instances = MSBuildLocator.QueryVisualStudioInstances().ToArray();
        var instance = instances.OrderByDescending(i => i.Version).FirstOrDefault();

        if (instance is not null)
            MSBuildLocator.RegisterInstance(instance);
        else
            MSBuildLocator.RegisterDefaults();
    }
}