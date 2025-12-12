using Newtonsoft.Json;

namespace HCMS_Api.Components.HCMS.Common.Models
{
    public class clsRootMenu
    {
        // [JsonProperty("Text")]
        public string? Text { get; set; }
        //[JsonProperty("ImageUrl")]
        public string? ImageUrl { get; set; }

        //  [JsonProperty("Value")]
        public string? Value { get; set; }

        // public List<clsChildMenu> Child { get; set; }

        //[JsonProperty("child")]
        public List<clsChildMenu> child { get; set; } = new List<clsChildMenu>();


    }

    public class clsChildMenu
    {
        //  [JsonProperty("Text")]
        public string? Text { get; set; }
        //  [JsonProperty("ImageUrl")]
        public string? ImageUrl { get; set; }
        //   [JsonProperty("Value")]
        public string? Value { get; set; }
        //  [JsonProperty("NavigateUrl")]
        public string? NavigateUrl { get; set; }
        //[JsonProperty("Class")]
        public string? Class { get; set; }

        // [JsonProperty("ClsSep")]
        public string? ClsSep { get; set; }



        //  [JsonProperty("formdescription")]
        public string? formdescription { get; set; }

        // [JsonProperty("subChild")]
        public List<clsSubChildMenu> subChild { get; set; } = new List<clsSubChildMenu>();


    }

    public class clsSubChildMenu
    {
        // [JsonProperty("Text")]
        public string? Text { get; set; }
        // [JsonProperty("ImageUrl")]
        public string? ImageUrl { get; set; }
        //  [JsonProperty("Value")]
        public string? Value { get; set; }
        //  [JsonProperty("NavigateUrl")]
        public string? NavigateUrl { get; set; }
        // [JsonProperty("Class")]
        public string? Class { get; set; }

        // [JsonProperty("CLSSEP")]
        public string? CLSSEP { get; set; }
        [JsonProperty("formdescription")]
        public string? formdescription { get; set; }


        //  [JsonProperty("subSubChild")]
        public List<clsSubSubChildMenu> subSubChild { get; set; } = new List<clsSubSubChildMenu>();

        //public List<clsSubSubChildMenu> SubSubChild { get; set; }



    }

    public class clsSubSubChildMenu
    {
        //  [JsonProperty("Text")]
        public string? Text { get; set; }
        //  [JsonProperty("ImageUrl")]
        public string? ImageUrl { get; set; }
        //  [JsonProperty("Value")]
        public string? Value { get; set; }
        //   [JsonProperty("NavigateUrl")]
        public string? NavigateUrl { get; set; }
        //   [JsonProperty("CLSSEP")]
        public string? CLSSEP { get; set; }
        //   [JsonProperty("formdescription")]
        public string? formdescription { get; set; }
    }
}
