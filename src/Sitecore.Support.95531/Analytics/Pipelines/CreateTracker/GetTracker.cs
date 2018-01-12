using Sitecore.Analytics.Pipelines.CreateTracker;
using Sitecore.Diagnostics;

namespace Sitecore.Support.Analytics.Pipelines.CreateTracker
{
  public class GetTracker : CreateTrackerProcessor
  {
    public override void Process(CreateTrackerArgs args)
    {
      Assert.ArgumentNotNull(args, "args");
      args.Tracker = new Sitecore.Support.Analytics.DefaultTracker();
    }
  }
}