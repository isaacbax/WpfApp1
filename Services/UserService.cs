using System;
using System.Collections.Generic;
using System.IO;
using WorkshopTracker.Models;

namespace WorkshopTracker.Services
{
    public class UserService
    {
        private readonly ConfigServices _config;

        public UserService(ConfigServices config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// Reads users.csv from S:\Public\DesignData\
        /// Expected header: username,password,branch
        /// </summary>
        public List<UserRecord> LoadUsers()
        {
            var list = new List<UserRecord>();
            var path = _config.UsersCsvPath;

            if (!File.Exists(path))
                return list;

            var lines = File.ReadAllLines(path);
            if (lines.Length == 0)
                return list;

            // Assume first row is header
            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var parts = line.Split(',');
                if (parts.Length < 3)
                    continue;

                var record = new UserRecord
                {
                    Username = parts[0].Trim(),
                    Password = parts[1].Trim(),
                    Branch = parts[2].Trim()
                };

                list.Add(record);
            }

            return list;
        }
    }
}
