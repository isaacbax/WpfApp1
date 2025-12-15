using System.IO;

namespace WorkshopTracker.Services
{
    public class ConfigServices
    {
        private readonly AppConfig _config;

        public ConfigServices()
        {
            _config = new AppConfig();
        }

        public string BaseFolder => _config.BaseFolder;

        public string UsersCsvPath => Path.Combine(BaseFolder, "users.csv");

        public string GetOpenCsvPath(string branch) =>
            Path.Combine(BaseFolder, $"{branch}open.csv");

        public string GetClosedCsvPath(string branch) =>
            Path.Combine(BaseFolder, $"{branch}closed.csv");
    }
}
