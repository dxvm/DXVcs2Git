﻿using DXVcs2Git.Core;
using DXVcs2Git.Core.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.ServiceModel.Syndication;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using System.Xml;

namespace DXVcs2Git.UI.AtomFeed {
    public static class FeedWorker {
        public const string VSIXId = "DXVcs2Git.GitTools.Xarlot.e4313842-75d7-4bf3-9516-fccdb00bec7d";
        public static Uri NewVersionUri { get; private set; }
        public static Version NewVersion { get; private set; }
        public static bool HasNewVersion { get { return NewVersionUri != null; } }
        static Dispatcher Dispatcher { get; set; }
        public static void Initialize() {
            Uri galleryUri = new Uri("http://idetester-sv.corp.devexpress.com/atomfeed.html");
            var downloader = new WebClient();
            downloader.OpenReadCompleted += OnOpenReadCompleted;
            Dispatcher = Dispatcher.CurrentDispatcher;
            DispatcherTimer timer = new DispatcherTimer(DispatcherPriority.ApplicationIdle, Dispatcher);
            timer.Interval = TimeSpan.FromMinutes(5);
            timer.Tick += (o, e) => downloader.OpenReadAsync(galleryUri);            
            timer.Start();
            downloader.OpenReadAsync(galleryUri);
        }
        static void OnOpenReadCompleted(object sender, OpenReadCompletedEventArgs e) {
            if (e.Error != null)
                return;
            using (var xmlReader = XmlReader.Create(e.Result)) {
                SyndicationFeed feed = SyndicationFeed.Load(xmlReader);
                foreach(SyndicationItem item in feed.Items) {
                    if (item.Id == VSIXId)
                        ProcessItem(item);
                }
            }
        }

        static void ProcessItem(SyndicationItem item) {
            var vsixExtension = item.ElementExtensions.ReadElementExtensions<Vsix>(Vsix.ExtensionName, Vsix.ExtensionNamespace).First();
            if (vsixExtension.Version > VersionInfo.Version) {
                OnNewVersion((item.Content as UrlSyndicationContent).Url, vsixExtension.Version);
            } else if(vsixExtension.Version == VersionInfo.Version) {
                var config = ConfigSerializer.GetConfig();
                config.InstallPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                ConfigSerializer.SaveConfig(config);
            }
        }

        static void OnNewVersion(Uri url, Version version) {
            NewVersionUri = new Uri(new Uri("http://idetester-sv.corp.devexpress.com/"), url);
            NewVersion = version;
        }        
    }
}
