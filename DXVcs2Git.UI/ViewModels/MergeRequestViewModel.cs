﻿using System.Collections.Generic;
using System.Linq;
using DevExpress.Mvvm;
using DXVcs2Git.Git;
using NGitLab.Models;

namespace DXVcs2Git.UI.ViewModels {
    public class MergeRequestViewModel : BindableBase {
        GitLabWrapper gitLabWrapper;
        MergeRequest MergeRequest { get; }
        public IEnumerable<MergeRequestFileDataViewModel> Changes { get; private set; }
        public string Title { get; }
        public MergeRequestViewModel(GitLabWrapper gitLabWrapper, MergeRequest mergeRequest) {
            this.gitLabWrapper = gitLabWrapper;
            MergeRequest = mergeRequest;
            Changes = gitLabWrapper.GetMergeRequestChanges(mergeRequest).Select(x => new MergeRequestFileDataViewModel(x)).ToList();
            Title = MergeRequest.Title;
        }
    }
}
