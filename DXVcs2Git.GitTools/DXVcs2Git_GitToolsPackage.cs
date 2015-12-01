﻿//------------------------------------------------------------------------------
// <copyright file="DXVcs2Git_GitToolsPackage.cs" company="Company">
//     Copyright (c) Company.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using DevExpress.Xpf.Core;
using DXVcs2Git.Core;
using DXVcs2Git.GitTools.ViewModels;
using DXVcs2Git.GitTools.Views;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using EnvDTE;
using Process = System.Diagnostics.Process;

namespace DXVcs2Git.GitTools {
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the
    /// IVsPackage interface and uses the registration attributes defined in the framework to
    /// register itself and its components with the shell. These attributes tell the pkgdef creation
    /// utility what data to put into .pkgdef file.
    /// </para>
    /// <para>
    /// To get loaded into VS, the package must be referred by &lt;Asset Type="Microsoft.VisualStudio.VsPackage" ...&gt; in .vsixmanifest file.
    /// </para>
    /// </remarks>
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)] // Info on this package for Help/About
    [ProvideAutoLoad(UIContextGuids.NoSolution)]
    [Guid(DXVcs2Git_GitToolsPackage.PackageGuidString)]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "pkgdef, VS and vsixmanifest are valid VS terms")]
    public sealed class DXVcs2Git_GitToolsPackage : Package {
        /// <summary>
        /// DXVcs2Git_GitToolsPackage GUID string.
        /// </summary>
        public const string PackageGuidString = "5221f195-9141-42a3-bbd9-1018afc30ba3";
        readonly MenuBuilder menuBuilder;
        DTE dte;
        Options options;
        /// <summary>
        /// Initializes a new instance of the <see cref="DXVcs2Git_GitToolsPackage"/> class.
        /// </summary>        
        public DXVcs2Git_GitToolsPackage() {
            // Inside this method you can place any initialization code that does not require
            // any Visual Studio service because at this point the package object is created but
            // not sited yet inside Visual Studio environment. The place to do all the other
            // initialization is the Initialize method.
            dte = GetGlobalService(typeof(DTE)) as DTE;
            options = ConfigSerializer.GetOptions();
            this.menuBuilder = new MenuBuilder(this, dte);
        }

        #region Package Members

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        protected override void Initialize() {
            base.Initialize();
            Core.Configuration.LauncherHelper.UpdateLauncher(ConfigSerializer.AppPath);
            this.menuBuilder.GenerateDefault();
        }
        protected override object GetService(Type serviceType) {
            return base.GetService(serviceType);
        }

        #endregion

        public void ShowMergeRequestUI() {
            var config = DXVcs2Git.Core.Configuration.ConfigSerializer.GetConfig();
            var launcher = Path.Combine(config.InstallPath, "DXVcs2Git.Launcher.exe");
            if (File.Exists(launcher)) {
                Process.Start(launcher);
                return;
            }  
            ProcessStartInfo info = new ProcessStartInfo("DXVcs2Git.UI.exe");
            info.WorkingDirectory = ConfigSerializer.AppPath;
            Process.Start(info);
        }
        string CalcGitRepoPath() {
            string gitPath = null;
            var document = this.dte.ActiveDocument;
            if (document != null) {
                string dirPath = Path.GetDirectoryName(document.Path);
                gitPath = FindGitDir(dirPath);
            }
            if (gitPath != null)
                return gitPath;
            return null;
        }
        string FindGitDir(string dirPath) {
            if (!Path.IsPathRooted(dirPath))
                return null;
            do {
                if (DirectoryHelper.IsGitDir(dirPath))
                    return dirPath;
                dirPath = Path.GetDirectoryName(dirPath);
            }
            while (!string.IsNullOrEmpty(dirPath));
            return null;
        }
        public void ShowOptionsUI() {
            AssemblyLoadingGuard.Protect();

            DXDialogWindow dialogWindow = new DXDialogWindow("Options", MessageBoxButton.OKCancel);
            dialogWindow.Content = new EditOptionsControl() { DataContext = this.options };
            if (dialogWindow.ShowDialog() == true) {
                ConfigSerializer.SaveOptions(this.options);
            }
            else {
                this.options = ConfigSerializer.GetOptions();
            }
        }
    }
}
