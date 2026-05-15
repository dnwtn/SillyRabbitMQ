using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using SillyRabbitMQ.Core.Models;

namespace SillyRabbitMQ.Core.Services
{
    public class ProfileManager
    {
        private readonly string _profileDirectory;
        private readonly string _profileFilePath;
        private readonly byte[] _entropy = Encoding.UTF8.GetBytes("SillyRabbitMQ_DPAPI_Salt");

        public ProfileManager()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _profileDirectory = Path.Combine(appData, "SillyRabbitMQ");
            _profileFilePath = Path.Combine(_profileDirectory, "profiles.json");
        }

        public List<ConnectionProfile> LoadProfiles()
        {
            if (!File.Exists(_profileFilePath))
            {
                return new List<ConnectionProfile>();
            }

            try
            {
                var json = File.ReadAllText(_profileFilePath);
                var profiles = JsonConvert.DeserializeObject<List<ConnectionProfile>>(json) ?? new List<ConnectionProfile>();

                // Decrypt passwords
                foreach (var profile in profiles)
                {
                    if (!string.IsNullOrEmpty(profile.Password))
                    {
                        profile.Password = DecryptString(profile.Password);
                    }
                }

                return profiles;
            }
            catch (Exception ex)
            {
                // In a real app we would log this
                Console.WriteLine($"Error loading profiles: {ex.Message}");
                return new List<ConnectionProfile>();
            }
        }

        public void SaveProfiles(List<ConnectionProfile> profiles)
        {
            if (!Directory.Exists(_profileDirectory))
            {
                Directory.CreateDirectory(_profileDirectory);
            }

            // Clone to avoid modifying the in-memory passwords during save
            var profilesToSave = JsonConvert.DeserializeObject<List<ConnectionProfile>>(JsonConvert.SerializeObject(profiles));

            if (profilesToSave != null)
            {
                foreach (var profile in profilesToSave)
                {
                    if (!string.IsNullOrEmpty(profile.Password))
                    {
                        profile.Password = EncryptString(profile.Password);
                    }
                }

                var json = JsonConvert.SerializeObject(profilesToSave, Formatting.Indented);
                File.WriteAllText(_profileFilePath, json);
            }
        }

        private string EncryptString(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return plainText;

            try
            {
                var plainBytes = Encoding.UTF8.GetBytes(plainText);
                var encryptedBytes = ProtectedData.Protect(plainBytes, _entropy, DataProtectionScope.CurrentUser);
                return Convert.ToBase64String(encryptedBytes);
            }
            catch
            {
                return string.Empty;
            }
        }

        private string DecryptString(string encryptedText)
        {
            if (string.IsNullOrEmpty(encryptedText)) return encryptedText;

            try
            {
                var encryptedBytes = Convert.FromBase64String(encryptedText);
                var plainBytes = ProtectedData.Unprotect(encryptedBytes, _entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch
            {
                // If decryption fails (e.g., copied from another machine), return empty or handle gracefully
                return string.Empty;
            }
        }
    }
}
