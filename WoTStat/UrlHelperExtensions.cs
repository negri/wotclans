namespace Negri.Wot
{
    public static class UrlHelperExtensions
    {
        //Builds URL by finding the best matching route that corresponds to the current URL,
        //with given parameters added or replaced.
        public static System.Web.Mvc.MvcHtmlString Current(this System.Web.Mvc.UrlHelper helper, object substitutes)
        {
            //get the route data for the current URL e.g. /Research/InvestmentModelling/RiskComparison
            //this is needed because unlike UrlHelper.Action, UrlHelper.RouteUrl sets includeImplicitMvcValues to false
            //which causes it to ignore current ViewContext.RouteData.Values
            var rd = new System.Web.Routing.RouteValueDictionary(helper.RequestContext.RouteData.Values);

            //get the current query string e.g. ?BucketID=17371&amp;compareTo=123
            var qs = helper.RequestContext.HttpContext.Request.QueryString;

            //add query string parameters to the route value dictionary
            foreach (string param in qs)
            {
                if (!string.IsNullOrEmpty(qs[param]))
                {
                    rd[param] = qs[param];
                }
            }

            //override parameters we're changing
            foreach (
                System.ComponentModel.PropertyDescriptor property in
                    System.ComponentModel.TypeDescriptor.GetProperties(substitutes.GetType()))
            {
                var value = property.GetValue(substitutes);
                if (value == null)
                {
                    rd.Remove(property.Name);
                }
                else
                {
                    rd[property.Name] = value;
                }                
            }

            //UrlHelper will find the first matching route
            //(the routes are searched in the order they were registered).
            //The unmatched parameters will be added as query string.
            var url = helper.RouteUrl(rd);
            return new System.Web.Mvc.MvcHtmlString(url);            
        }
    }
}