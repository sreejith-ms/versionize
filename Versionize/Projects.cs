using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Versionize
{
    public class Projects
    {
        private readonly List<Project> _projects;

        private Projects(List<Project> projects)
        {
            _projects = projects;
        }

        public bool IsEmpty()
        {
            return _projects.Count == 0;
        }

        public IEnumerable<Project> UpdatedProjects => _projects.Where(x => x.NewVersion != null);

        //public Version Version { get => _projects.First().Version; }

        public static Projects Discover(string workingDirectory)
        {
            var projects = Directory
                .GetFiles(workingDirectory, "version.json", SearchOption.AllDirectories)
                .Where(Project.IsVersionable)
                .Select(Project.Create)
                .ToList();

            return new Projects(projects);
        }

        public void WriteVersion(Version nextVersion)
        {
            foreach (var project in _projects)
            {
                project.WriteVersion(nextVersion);
            }
        }

        public void WriteVersion(bool hasVersionTag, bool ignoreInsignificant)
        {
            for (int index = 0; index < _projects.Count; index++)
            {
                var project = _projects[index];
                var versionIncrement = VersionIncrementStrategy.CreateFrom(project.ConventionalCommits);
                var isInitialVersion = !hasVersionTag || !project.Releases.Any();
                var nextVersion = !isInitialVersion ? 
                    versionIncrement.NextVersion(project.VersionFile.Version, ignoreInsignificant)
                    : project.VersionFile.Version;
                if (nextVersion != project.VersionFile.Version || isInitialVersion)
                {
                    project.WriteVersion(nextVersion);
                }
            }
        }

        public IEnumerable<string> GetProjectFiles()
        {
            return _projects.Select(project => project.ProjectFile);
        }

        public void GroupCommitsAndReleases(
            List<ConventionalCommit> conventionalCommits,
            IEnumerable<string> tags)
        {
            if (conventionalCommits.Count == 0)
            {
                return;
            }

            for (int index = 0; index < _projects.Count; index++)
            {
                var project = _projects[index];
                project.ConventionalCommits =
                    conventionalCommits.Where(commit =>
                        commit.Scope == project.VersionFile.ScopeName ||
                        project.VersionFile.ParentScopes.Contains(commit.Scope));
                // TODO: multiple scopes in a commit
                project.Releases = tags.Where(t =>
                    project.VersionFile.ScopeName == t.Split('/')?[0]);
            }
        }
    }
}
