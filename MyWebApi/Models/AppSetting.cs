namespace MyWebApi.Models
{
    public class AppSetting
    {
        public string Secret { get; set; }
        public string ValidIssuer { get; set; }
        public string ValidAudience { get; set; }
    }
}
