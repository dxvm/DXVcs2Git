﻿using System;
using System.Collections.Generic;
using System.Linq;
using DXVcs2Git.Core;
using DXVcs2Git.Core.Git;
using LibGit2Sharp;
using Branch = LibGit2Sharp.Branch;
using Commit = LibGit2Sharp.Commit;
using Tag = LibGit2Sharp.Tag;
using User = DXVcs2Git.Core.User;

namespace DXVcs2Git {
    public class GitWrapper : IGitProcessExecutor, IDisposable {
        readonly string path;
        readonly Credentials credentials;
        readonly string repoPath;
        readonly string gitPath;
        readonly Repository repo;
        readonly GitLfsFilter gitLfsFilter;
        public bool IsEmpty {
            get { return !repo.Branches.Any(); }
        }
        public string GitDirectory {
            get { return repoPath; }
        }
        public Credentials Credentials {
            get { return credentials; }
        }

        public GitWrapper(string path, string gitPath, Credentials credentials) {
            this.path = path;
            this.credentials = credentials;
            this.gitPath = gitPath;
            this.repoPath = DirectoryHelper.IsGitDir(path) ? GitInit() : GitClone();
            repo = new Repository(repoPath);
            this.gitLfsFilter = new GitLfsFilter(this);

            CommandArguments commandArguments = new CommandArguments();
            commandArguments.AddArg("lfs");
            commandArguments.AddArg("init");
            commandArguments.AddArg("--force");
            Log.Message("Installing git lfs filters");
            GlobalSettings.RegisterFilter(gitLfsFilter);
            //this.gitShell.Execute(commandArguments.ToString(), true)
        }
        public string GitInit() {
            return Repository.Init(path);
        }
        string GitClone() {
            CloneOptions options = new CloneOptions();
            options.CredentialsProvider += (url, fromUrl, types) => credentials;
            string clonedRepoPath = Repository.Clone(gitPath, path, options);
            Log.Message($"Git repo {clonedRepoPath} initialized");
            return clonedRepoPath;
        }
        public void Dispose() {
        }
        public void Fetch(string remote = "", bool updateTags = false) {
            FetchOptions options = new FetchOptions();
            options.CredentialsProvider += (url, fromUrl, types) => credentials;
            if (updateTags)
                options.TagFetchMode = TagFetchMode.All;
            var network = string.IsNullOrEmpty(remote) ? repo.Network.Remotes.FirstOrDefault() : this.repo.Network.Remotes[remote];
            repo.Fetch(network.Name, options);
        }
        public MergeResult Pull(User user, string branchName) {
            Branch head = this.repo.Branches[branchName];
            if (!head.IsTracking)
                throw new LibGit2SharpException("There is no tracking information for the current branch.");
            if (head.Remote == null)
                throw new LibGit2SharpException("No upstream remote for the current branch.");
            this.Fetch(head.Remote.Name);
            MergeOptions options = new MergeOptions();
            options.MergeFileFavor = MergeFileFavor.Theirs;
            options.FileConflictStrategy = CheckoutFileConflictStrategy.Theirs;
            return this.repo.MergeFetchedRefs(ToSignature(user), options);
        }
        Signature ToSignature(User user, DateTimeOffset? dateTime = null) {
            return new Signature(user.UserName, user.Email, dateTime ?? DateTimeOffset.Now);
        }
        public void Stage(string path) {
            repo.Stage(path);
            Log.Message($"Git stage performed.");
        }
        public Commit Commit(string comment, User user, string committerName, DateTime timeStamp, bool allowEmpty = true) {
            CommitOptions commitOptions = new CommitOptions();
            commitOptions.AllowEmptyCommit = allowEmpty;
            DateTime localTime = timeStamp.ToLocalTime();
            var author = ToSignature(user, localTime);
            var comitter = ToSignature(user, localTime);
            var commit = repo.Commit(comment, author, comitter, commitOptions);
            Log.Message($"Git commit performed for {user} {localTime}");
            return commit;
        }
        public void Push(string branch) {
            Push($@"refs/heads/{branch}", false);
        }
        public void Push(string refspec, bool force) {
            PushOptions options = new PushOptions();
            options.CredentialsProvider += (url, fromUrl, types) => credentials;
            options.OnPushStatusError += errors => {
                Log.Message($"Push to refspec {refspec} failed.");
                Log.Error($"Error: {errors.Message} in repo {errors.Reference}.");
                throw new ArgumentException("error while push");
            };
            Remote remote = this.repo.Network.Remotes["origin"];
            repo.Network.Push(remote, force ? $@"+{refspec}" : refspec, refspec, options);
            Log.Message($"Push to refspec {refspec} completed.");
        }
        public void EnsureBranch(string name, Commit whereCreateBranch) {
            Branch localBranch = this.repo.Branches[name];
            string remoteName = GetOriginName(name);
            Branch remoteBranch = this.repo.Branches[remoteName];
            if (localBranch == null) {
                if (remoteBranch != null) {
                    localBranch = InitLocalBranch(name, remoteBranch);
                }
                else if (whereCreateBranch == null) {
                    localBranch = repo.CreateBranch(name);
                    Push(name);
                }
                else {
                    localBranch = CreateBranchFromCommit(name, whereCreateBranch);
                }
            }
            if (remoteBranch != null)
                repo.Branches.Update(localBranch, b => b.TrackedBranch = remoteBranch.CanonicalName);
        }
        Branch InitLocalBranch(string name, Branch remoteBranch) {
            return this.repo.CreateBranch(name, remoteBranch.CanonicalName);
        }
        Branch CreateBranchFromCommit(string name, Commit whereCreateBranch) {
            CheckOut(whereCreateBranch);
            return repo.CreateBranch(name);
        }
        string GetOriginName(string name) {
            return $"origin/{name}";
        }
        public Commit FindCommit(string branchName, Func<Commit, bool> handler = null) {
            var branch = repo.Branches[branchName];
            var checkHandler = handler ?? (x => true);
            return branch.Commits.FirstOrDefault(checkHandler);
        }
        public Commit FindCommit(string branchName, string comment) {
            return FindCommit(branchName, x => x.Message?.StartsWith(comment) ?? false);
        }
        public DateTime GetLastCommitTimeStamp(string branchName, User user) {
            var branch = this.repo.Branches[branchName];
            var commit = branch.Commits.FirstOrDefault(x => x.Committer.Name == user.UserName || x.Committer.Name == "Administrator");
            return commit?.Author.When.DateTime.ToUniversalTime() ?? DateTime.MinValue;
        }
        public bool CalcHasModification() {
            RepositoryStatus status = repo.RetrieveStatus();
            return status.IsDirty;
        }
        public void CheckOut(string branchName) {
            CheckoutOptions options = new CheckoutOptions();
            options.CheckoutModifiers = CheckoutModifiers.Force;
            repo.Checkout(repo.Branches[branchName], options);
        }
        public void CheckOut(Commit commit) {
            CheckoutOptions options = new CheckoutOptions();
            options.CheckoutModifiers = CheckoutModifiers.Force;
            repo.Checkout(commit, options);
            Log.Message($"Checkout commit {commit.Id} completed");
        }
        public void AddTag(string tagName, GitObject target, string commiterName, DateTime timeStamp, string message) {
            repo.Tags.Add(tagName, target, new Signature(commiterName, "test@mail.com", timeStamp), message, true);
            Push($@"refs/tags/{tagName}", true);
            Log.Message($"Apply tag commit {tagName} completed");
        }
        public Tag GetTag(string tagName) {
            return repo.Tags[tagName];
        }
        public Commit GetHead(string branchName) {
            var branch = repo.Branches[branchName];
            return branch.Commits.First();
        }
        public IEnumerable<TreeEntryChanges> GetChanges(Commit commit, Commit parent) {
            var treeChanges = repo.Diff.Compare<TreeChanges>(parent.Tree, commit.Tree);
            var changes = Enumerable.Empty<TreeEntryChanges>();
            changes = changes.Concat(treeChanges.Added);
            changes = changes.Concat(treeChanges.Deleted);
            changes = changes.Concat(treeChanges.Modified);
            changes = changes.Concat(treeChanges.Renamed);
            return changes;
        }
        public void Reset(string branchName) {
            CheckOut(branchName);
            Fetch();
            this.repo.Reset(ResetMode.Hard);
            this.repo.RemoveUntrackedFiles();
        }
        public MergeStatus Merge(string sourceBranch, User merger) {
            Branch branch = repo.Branches[sourceBranch];
            MergeOptions mergeOptions = new MergeOptions();
            mergeOptions.CommitOnSuccess = false;
            mergeOptions.FastForwardStrategy = FastForwardStrategy.NoFastForward;
            mergeOptions.FileConflictStrategy = CheckoutFileConflictStrategy.Normal;
            MergeResult result = repo.Merge(branch, ToSignature(merger), mergeOptions);
            return result.Status;
        }
        public RevertStatus Revert(string branchName, Commit revertCommit, User user) {
            return this.repo.Revert(revertCommit, ToSignature(user)).Status;
        }
    }
}
