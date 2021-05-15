using System;
using System.IO;
using System.Linq;
using LibGit2Sharp;
using static Versionize.CommandLine.CommandLineUI;

namespace Versionize
{
    public class WorkingCopy
    {
        private readonly DirectoryInfo _directory;

        private WorkingCopy(DirectoryInfo directory)
        {
            _directory = directory;
        }

        public System.Version Versionize(bool dryrun = false,
            bool skipDirtyCheck = false,
            bool skipCommit = false,
            string releaseVersion = null,
            bool ignoreInsignificant = false, 
            bool includeAllCommitsInChangelog = false)
        {
            var workingDirectory = _directory.FullName;

            using (var repo = new Repository(workingDirectory))
            {
                var isDirty = repo.RetrieveStatus(new StatusOptions()).IsDirty;

                if (!skipDirtyCheck && isDirty)
                {
                    Exit($"Repository {workingDirectory} is dirty. Please commit your changes.", 1);
                }

                var projects = Projects.Discover(workingDirectory);

                if (projects.IsEmpty())
                {
                    Exit($"Could not find any projects files in {workingDirectory} that have a <Version> defined in their csproj file.", 1);
                }

                Information($"Discovered {projects.GetProjectFiles().Count()} versionable projects");
                foreach (var project in projects.GetProjectFiles())
                {
                    Information($"  * {project}");
                }

                var versionTag = repo.SelectLatestVersionTag();
                var commitsInVersion = repo.GetCommitsSinceLastVersion(versionTag);

                var commitParser = new ConventionalCommitParser();
                var conventionalCommits = commitParser.Parse(commitsInVersion);

                if (conventionalCommits.Count == 0)
                {
                    Exit("No commits found", 0);
                }

                projects.GroupCommitsAndReleases(
                    conventionalCommits, 
                    repo.Tags.Select(x => x.FriendlyName));
                

                var versionTime = DateTimeOffset.Now;

                // Commit changelog and version source
                if (!dryrun)
                {
                    projects.WriteVersion(versionTag != null, ignoreInsignificant: true);

                    foreach (var projectFile in projects.GetProjectFiles())
                    {
                        Commands.Stage(repo, projectFile);
                    }
                }

                if (!projects.UpdatedProjects.Any())
                {
                    Exit($"Version was not affected by commits since last release ({versionTag?.FriendlyName}), since you specified to ignore insignificant changes, no action will be performed.", 0);
                }

                var changelog = Changelog.Discover(workingDirectory);

                foreach (var project in projects.UpdatedProjects)
                {
                    Step($"bumping version from {project.VersionFile.Version} to {project.NewVersion} in projects");

                    if (!dryrun)
                    {
                        var changelogLinkBuilder = ChangelogLinkBuilderFactory.CreateFor(repo);
                        changelog.Write(project.VersionFile.ScopeName, project.NewVersion, versionTime, changelogLinkBuilder, project.ConventionalCommits, includeAllCommitsInChangelog);
                    }
                }

                Step($"updated CHANGELOG.md");

                if (!dryrun && !skipCommit)
                {
                    Commands.Stage(repo, changelog.FilePath);

                    foreach (var projectFile in projects.GetProjectFiles())
                    {
                        Commands.Stage(repo, projectFile);
                    }
                }

                if (!dryrun && !skipCommit)
                {
                    var author = repo.Config.BuildSignature(versionTime);
                    var committer = author;
                    var commitMessages = projects.UpdatedProjects.Select(x =>
                        $"{x.VersionFile.ScopeName}: {x.NewVersion}");
                    var releaseCommitMessage = $"chore(release): {string.Join(Environment.NewLine, commitMessages)}";
                    var versionCommit = repo.Commit(releaseCommitMessage, author, committer);
                    foreach (var project in projects.UpdatedProjects)
                    {
                        // TODO: Check if tag exists before commit
                        repo.Tags.Add($"{project.VersionFile.ScopeName}/v{project.NewVersion}", versionCommit, author, $"{project.NewVersion}");
                        Step($"tagged release as {project.NewVersion}");
                    }
                    Information("");
                    Information($"i Run `git push --follow-tags origin master` to push all changes including tags");
                }
                else if (skipCommit)
                {
                    Information("");
                    Information($"i Commit and tagging of release was skipped. Tag this release as  to make versionize detect the release");
                }

                return null;
            }
        }

        public static WorkingCopy Discover(string workingDirectory)
        {
            var workingCopyCandidate = new DirectoryInfo(workingDirectory);

            if (!workingCopyCandidate.Exists)
            {
                Exit($"Directory {workingDirectory} does not exist", 2);
            }

            do
            {
                var isWorkingCopy = workingCopyCandidate.GetDirectories(".git").Any();

                if (isWorkingCopy)
                {
                    return new WorkingCopy(workingCopyCandidate);
                }

                workingCopyCandidate = workingCopyCandidate.Parent;
            }
            while (workingCopyCandidate.Parent != null);

            Exit($"Directory {workingDirectory} or any parent directory do not contain a git working copy", 3);

            return null;
        }
    }
}
