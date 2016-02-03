using System.Collections.Generic;

namespace Data.Warehouse.Crawler
{
    public class WebcrawlingUtilityConstants
    {
        public const string BaseSitename = "base.sitename";
        public const string BaseUri = "base.uri";
        public const string CommandBrowse = "browse()";
        public const string CurrentUri = "current.uri";

        public const string Corellation = "correlation";
        public const string Id = "id";
        public const string Uri = "uri";

        public static List<string> Commands = new List<string>
        {
            CommandBrowse
        };
        public static List<string> BasicProperties = new List<string>
        {
            Corellation,
            Id,
            Uri
        };
        public static List<string> BasicVariables = new List<string>
        {
            BaseSitename,
            BaseUri,
            CurrentUri
        };
    }
}
