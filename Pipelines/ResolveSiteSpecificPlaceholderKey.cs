using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Web;
using Sitecore;
using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Pipelines.GetPlaceholderRenderings;
using Sitecore.Sites;

namespace Sc.Commons.SitePlaceholders.Pipelines
{
    [UsedImplicitly]
    public class ResolveSiteSpecificPlaceholderKey
    {
        //copied and modified from Sitecore.Support.Pipelines.GetPlaceholderRenderings.GetDynamicKeyAllowedRenderings
        private readonly Regex _regex = new Regex("(.+)(_[\\d\\w]{8}\\-([\\d\\w]{4}\\-){3}[\\d\\w]{12})");

        [UsedImplicitly]
        public void Process(GetPlaceholderRenderingsArgs args)
        {
            Assert.IsNotNull(args, "args");
            var placeholderKey = args.PlaceholderKey ?? string.Empty;

            //filter out the guid if its a dynamically generated placeholder
            var match = _regex.Match(placeholderKey);
            var isDynamicPlaceholder = match.Success && match.Groups.Count > 0;
            if (isDynamicPlaceholder) placeholderKey = match.Groups[1].Value;

            //get current item's sitename and compile a site specific placeholderKey
            var siteName = GetSiteName(args);
            if (string.IsNullOrEmpty(siteName)) return;

            var placeholderKeySegments = placeholderKey.Split('/');
            placeholderKeySegments[placeholderKeySegments.Count() - 1] = string.Format("{0}-{1}", siteName, placeholderKeySegments[placeholderKeySegments.Count() - 1]);
            var siteSpecificPlaceholderKey = string.Join("/", placeholderKeySegments);

            //try to resolve the specific placeholderKey
            var page = Client.Page;
            Assert.IsNotNull(page, "page");

            Item placeholderItem;
            if (ID.IsNullOrEmpty(args.DeviceId))
            {
                placeholderItem = page.GetPlaceholderItem(siteSpecificPlaceholderKey, args.ContentDatabase, args.LayoutDefinition);
            }
            else
            {
                using (new DeviceSwitcher(args.DeviceId, args.ContentDatabase))
                    placeholderItem = page.GetPlaceholderItem(siteSpecificPlaceholderKey, args.ContentDatabase, args.LayoutDefinition);
            }

            //adjust the placeholderKey if we were able to resolve it
            //unfortunately the args.PlaceholderKey has no setter so for now this is fixed with some casual Reflection hacking
            if (placeholderItem != null)
            {
                //re-add the dynamic part because it ensures business as usual (ie. the support package expects it)
                if (isDynamicPlaceholder) siteSpecificPlaceholderKey = string.Format("{0}{1}", siteSpecificPlaceholderKey, match.Groups[2].Value);

                var placeholderKeyField = typeof (GetPlaceholderRenderingsArgs).GetField("placeholderKey", BindingFlags.Instance | BindingFlags.NonPublic);
                if (placeholderKeyField != null) placeholderKeyField.SetValue(args, siteSpecificPlaceholderKey);
                //args.PlaceholderKey = siteSpecificPlaceholderKey;
            }
        }

        private string GetSiteName(GetPlaceholderRenderingsArgs args)
        {
            //get the id from the querystring
            var queryString = HttpContext.Current.Request.QueryString;
            var itemId = queryString["sc_itemId"] ?? queryString["id"];
            var siteName = Context.GetSiteName();

            if (!string.IsNullOrEmpty(itemId))
            {
                //get the sitecore item, which is the context item of the page in the xEditor
                var pageItem = args.ContentDatabase.GetItem(itemId);
                if (pageItem != null)
                {
                    //match it with a content site
                    foreach (
                        var info in
                            SiteContextFactory.Sites.Where(
                                info =>
                                    !string.IsNullOrEmpty(info.RootPath) &&
                                    (info.RootPath != "/sitecore/content" || info.Name.Equals("website"))))
                    {
                        if (pageItem.Paths.FullPath.StartsWith(info.RootPath, StringComparison.OrdinalIgnoreCase))
                        {
                            siteName = info.Name;
                            break;
                        }
                    }
                }
            }

            return siteName.ToLowerInvariant();
        }
    }
}
