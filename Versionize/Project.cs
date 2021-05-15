using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Xml;

namespace Versionize
{
    public class Project
    {
        public string ProjectFile { get; }

        public VersionFile VersionFile { get; }

        public IEnumerable<ConventionalCommit> ConventionalCommits { get; set; }

        public Version NewVersion { get; private set; }

        public IEnumerable<string> Releases { get; set; }

        private Project(string projectFile, VersionFile version)
        {
            ProjectFile = projectFile;
            VersionFile = version;
        }

        public static Project Create(string projectFile)
        {
            var version = ReadVersion(projectFile);

            return new Project(projectFile, version);
        }

        public static bool IsVersionable(string projectFile)
        {
            try
            {
                ReadVersion(projectFile);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static VersionFile ReadVersion(string projectFile)
        {
            VersionFile versionFile;
            try
            {
                var jsonString = File.ReadAllText(projectFile);
                versionFile = JsonSerializer.Deserialize<VersionFile>(jsonString);
            }
            catch (Exception)
            {
                throw new InvalidOperationException($"Project {projectFile} is not a valid csproj file. Please make sure that you have a valid csproj file in place!");
            }

            if (string.IsNullOrWhiteSpace(versionFile.VersionString))
            {
                throw new InvalidOperationException($"Project {projectFile} contains no or an empty <Version> XML Element. Please add one if you want to version this project - for example use <Version>1.0.0</Version>");
            }

            try
            {
                versionFile.SetVersion();
                return versionFile;
            }
            catch (Exception)
            {
                throw new InvalidOperationException($"Project {projectFile} contains an invalid version {versionFile.VersionString}. Please fix the currently contained version - for example use <Version>1.0.0</Version>");
            }
        }

        public void WriteVersion(Version nextVersion)
        {
            var version = new VersionFile(
                nextVersion.ToString(),
                VersionFile.ScopeName,
                VersionFile.ParentScopes);

            var jsonString = JsonSerializer.Serialize(version, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ProjectFile, jsonString);
            NewVersion = nextVersion;
        }
    }
}
