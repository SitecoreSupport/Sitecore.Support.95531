using System;
using System.Threading;
using System.Web;
using Sitecore.Analytics;
using Sitecore.Analytics.Configuration;
using Sitecore.Analytics.Data;
using Sitecore.Analytics.Pipelines.EnsureSessionContext;
using Sitecore.Analytics.Pipelines.ExcludeRobots;
using Sitecore.Analytics.Pipelines.InitializeTracker;
using Sitecore.Analytics.Pipelines.StartTracking;
using Sitecore.Analytics.Tracking;
using Sitecore.Analytics.Tracking.Diagnostics.PerformanceCounters;
using Sitecore.Analytics.Web;
using Sitecore.Common;
using Sitecore.Data.Fields;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Sites;

namespace Sitecore.Support.Analytics
{
  public class DefaultTracker : ITracker
  {
    private const string TrackerActive = "SC_TRACKER_ACTIVE";

    public Contact Contact
    {
      get
      {
        if (this.Session != null)
        {
          return this.Session.Contact;
        }
        return null;
      }
    }

    public Session Session
    {
      get
      {
        return Switcher<Session, Session>.CurrentValue;
      }
    }

    public ICurrentPageContext CurrentPage
    {
      get
      {
        if (this.Interaction != null)
        {
          return this.Interaction.CurrentPage;
        }
        return null;
      }
    }

    public CurrentInteraction Interaction
    {
      get
      {
        if (this.Session != null)
        {
          return this.Session.Interaction;
        }
        return null;
      }
    }

    public bool IsActive
    {
      get
      {
        object obj = Context.Items["SC_TRACKER_ACTIVE"];
        return obj != null && (bool)obj;
      }
      set
      {
        Context.Items["SC_TRACKER_ACTIVE"] = value;
      }
    }

    public TrackerSamplingBase Sampling
    {
      get;
      private set;
    }

    public DefaultTracker()
    {
      this.Sampling = new TrackerSampling();
      this.EnsureSessionContext();
    }

    public void EndTracking()
    {
      this.IsActive = false;
    }

    public void EndVisit(bool clearVisitor)
    {
      if (clearVisitor)
      {
        new ContactKeyCookie("").Invalidate();
      }
    }

    public void StartTracking()
    {
      if (this.IsActive)
      {
        Log.Debug("Skipping of tracking, tracker is already active", typeof(Tracker));
        return;
      }
      if (!this.Sampling.IsSampling())
      {
        Log.Debug("Session is null or the session is not participate in analytics because was not chosen as a sample for tracking", typeof(Tracker));
        return;
      }
      SiteContext site = Context.Site;
      if (site != null && !site.Tracking().EnableTracking)
      {
        Log.Debug("Cannot start tracking, analytics is not enabled for site " + site.Name, typeof(Tracker));
        return;
      }
      if (this.ExcludeRequest())
      {
        Log.Debug("The request was excluded because the Agent or IP is determined as a robot, see Exclude robots configuration file", typeof(Tracker));
        AnalyticsTrackingCount.CollectionRobotRequests.Increment(1L);
        return;
      }
      if (DefaultTracker.IgnoreCurrentItem())
      {
        AnalyticsTrackingCount.CollectionRequestsIgnored.Increment(1L);
        return;
      }
      this.IsActive = true;
      StartTrackingArgs args = new StartTrackingArgs
      {
        HttpContext = new HttpContextWrapper(HttpContext.Current)
      };
      try
      {
        StartTrackingPipeline.Run(args);
      }
      catch (ThreadAbortException)
      {
        throw;
      }
      catch (Exception ex)
      {
        this.IsActive = false;
        if (!AnalyticsSettings.SuppressTrackingInitializationExceptions)
        {
          throw new Exception("Failed to start tracking", ex);
        }
        Log.Error("Cannot start analytics Tracker", ex, typeof(Tracker));
      }
    }

    private static bool IgnoreCurrentItem()
    {
      Item item = Context.Item;
      if (item != null)
      {
        TrackingField trackingField = FindTrackingField(item);
        if (trackingField != null)
        {
          return trackingField.Ignore;
        }
      }
      return false;
    }

    private void EnsureSessionContext()
    {
      InitializeTrackerArgs initializeTrackerArgs = new InitializeTrackerArgs();
      if (HttpContext.Current != null)
      {
        initializeTrackerArgs.HttpContext = new HttpContextWrapper(HttpContext.Current);
      }
      EnsureSessionContextPipeline.Run(initializeTrackerArgs);
      if (initializeTrackerArgs.Session != null)
      {
        Switcher<Session, Session>.Enter(initializeTrackerArgs.Session);
      }
    }

    private bool ExcludeRequest()
    {
      ExcludeRobotsArgs expr_05 = new ExcludeRobotsArgs();
      ExcludeRobotsPipeline.Run(expr_05);
      return expr_05.IsInExcludeList;
    }

    internal static TrackingField FindTrackingField(Item item)
    {
      Assert.ArgumentNotNull(item, "item");
      TrackingField trackingField = GetTrackingField(item);
      if (trackingField != null)
      {
        return trackingField;
      }
      TemplateItem template = item.Template;
      if (template == null)
      {
        return null;
      }
      return GetTrackingField(template.InnerItem);
    }

    internal static TrackingField GetTrackingField(Item item)
    {
      Assert.ArgumentNotNull(item, "item");
      Field field = item.Fields["__Tracking"];
      if (field != null)
      {
        return new TrackingField(field);
      }
      return null;
    }
  }
}
