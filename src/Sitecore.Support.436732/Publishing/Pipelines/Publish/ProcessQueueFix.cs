namespace Sitecore.Support.Publishing.Pipelines.Publish
{
  using Sitecore.Data;
  using Sitecore.Data.Items;
  using Sitecore.Globalization;
  using Sitecore.Publishing;
  using Sitecore.Publishing.Pipelines.Publish;
  using Sitecore.Publishing.Pipelines.PublishItem;
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Reflection;

  public class ProcessQueueFix : ProcessQueue
  {
    private static MethodInfo addToPublishQueue = null;
    private static Func<Database, ID, ItemUpdateType, DateTime, bool, bool> CachedAddToPublishQueue;

    static ProcessQueueFix()
    {
      addToPublishQueue = typeof(PublishManager).GetMethod("AddToPublishQueue", BindingFlags.NonPublic | BindingFlags.Static, null, new Type[] { typeof(Database), typeof(ID), typeof(ItemUpdateType), typeof(DateTime), typeof(bool) }, null);
      CachedAddToPublishQueue = (Func<Database, ID, ItemUpdateType, DateTime, bool, bool>)Delegate.CreateDelegate(typeof(Func<Database, ID, ItemUpdateType, DateTime, bool, bool>), addToPublishQueue);
    }

    protected override void ProcessEntries(IEnumerable<PublishingCandidate> entries, PublishContext context)
    {
      foreach (PublishingCandidate candidate in entries)
      {
        List<PublishingCandidate> source = new List<PublishingCandidate>();
        List<PublishingCandidate> list2 = new List<PublishingCandidate>();
        if (!context.ProcessedItems.Contains(candidate.ItemId.ToString()))
        {
          lock (context.ProcessedItems)
          {
            context.ProcessedItems.Add(candidate.ItemId.ToString());
          }
          foreach (Language language in context.Languages)
          {
            candidate.PublishOptions.Language = language;
            PublishItemContext context2 = this.CreateItemContext(candidate, context);
            context2.PublishOptions.Language = language;
            PublishItemResult result = PublishItemPipeline.Run(context2);
            if (((context.PublishOptions.Mode == PublishMode.Incremental) && (result.Operation == PublishOperation.Skipped)) && (result.ShouldBeReturnedToPublishQueue && (addToPublishQueue != null)))
            {
              CachedAddToPublishQueue(context.PublishOptions.SourceDatabase, context2.ItemId, ItemUpdateType.Skipped, DateTime.UtcNow, true);
            }
            if (!this.SkipReferrers(result, context))
            {
              source.AddRange(result.ReferredItems);
            }
            if (!this.SkipChildren(result, candidate, context) && !list2.Any<PublishingCandidate>())
            {
              list2.AddRange(candidate.ChildEntries);
            }
          }
          if (list2.Any<PublishingCandidate>())
          {
            this.ProcessEntries(list2, context);
          }
          if (source.Any<PublishingCandidate>())
          {
            this.ProcessEntries(this.RemoveDuplicateReferrers(source, context), context);
          }
        }
      }
    }
  }
}
