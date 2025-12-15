namespace WorkshopTracker.Services
{
    /// <summary>
    /// Simple singleton-style access to configuration.
    /// Use AppConfig.Current anywhere you need the shared ConfigServices.
    /// </summary>
    public static class AppConfig
    {
        // Single shared instance for the whole app
        private static readonly ConfigServices _current = new ConfigServices();

        /// <summary>
        /// Global configuration instance.
        /// </summary>
        public static ConfigServices Current => _current;
    }
}
