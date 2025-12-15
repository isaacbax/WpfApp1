using System.IO;

namespace WorkshopTracker.Services
{
    public class WorkFileService
    {
        public string BaseFolder { get; }

        public WorkFileService()
            : this(@"S:\Public\DesignData\")
        {
        }

        public WorkFileService(string baseFolder)
        {
            BaseFolder = baseFolder;
        }

        public string GetOpenFilePath(string branch)
        {
            return Path.Combine(BaseFolder, $"{branch}open.csv");
        }

        public string GetClosedFilePath(string branch)
        {
            return Path.Combine(BaseFolder, $"{branch}closed.csv");
        }
    }
}
