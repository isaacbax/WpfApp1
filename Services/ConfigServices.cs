using System.IO;

namespace WorkshopTracker.Services
{
    /// <summary>
    /// Central place for application configuration.
    /// Right now it hard-codes the workshop CSV folder to:
    ///     S:\Public\DesignData\
    /// and exposes a UsersCsvPath for users.csv in that folder.
    /// </summary>
    public class ConfigServices
    {
        private const string DefaultBaseFolder = @"S:\Public\DesignData\";

        /// <summary>
        /// Base folder where all workshop CSVs live.
        /// e.g. S:\Public\DesignData\
        /// </summary>
        public string BaseFolder { get; private set; }

        /// <summary>
        /// Full path to users.csv in the base folder.
        /// e.g. S:\Public\DesignData\users.csv
        /// </summary>
        public string UsersCsvPath => Path.Combine(BaseFolder, "users.csv");

        /// <summary>
        /// Default constructor – uses the hard-coded default base folder.
        /// </summary>
        public ConfigServices()
        {
            BaseFolder = NormalizeFolder(DefaultBaseFolder);
        }

        /// <summary>
        /// Optional constructor if you ever want to override the base folder.
        /// If baseFolder is null/empty, falls back to the default.
        /// </summary>
        public ConfigServices(string baseFolder)
        {
            if (string.IsNullOrWhiteSpace(baseFolder))
            {
                BaseFolder = NormalizeFolder(DefaultBaseFolder);
            }
            else
            {
                BaseFolder = NormalizeFolder(baseFolder);
            }
        }

        /// <summary>
        /// Path for the open works CSV for a given branch.
        /// e.g. headoffice -> S:\Public\DesignData\headofficeopen.csv
        /// </summary>
        public string GetOpenCsvPath(string branch)
        {
            branch ??= "headoffice";
            return Path.Combine(BaseFolder, $"{branch}open.csv");
        }

        /// <summary>
        /// Path for the closed works CSV for a given branch.
        /// e.g. headoffice -> S:\Public\DesignData\headofficeclosed.csv
        /// </summary>
        public string GetClosedCsvPath(string branch)
        {
            branch ??= "headoffice";
            return Path.Combine(BaseFolder, $"{branch}closed.csv");
        }

        private static string NormalizeFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder))
                folder = DefaultBaseFolder;

            if (!folder.EndsWith("\\") && !folder.EndsWith("/"))
            {
                folder += Path.DirectorySeparatorChar;
            }

            return folder;
        }
    }
}
