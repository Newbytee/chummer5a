/*  This file is part of Chummer5a.
 *
 *  Chummer5a is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  Chummer5a is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with Chummer5a.  If not, see <http://www.gnu.org/licenses/>.
 *
 *  You can obtain the full source code for Chummer5a at
 *  https://github.com/chummer5a/chummer5a
 */
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Chummer;
using Chummer.Controls.Shared;
using ChummerDataViewer.Model;

namespace ChummerDataViewer
{
    public partial class Mainform : Form
    {
        //Main display
        private readonly ThreadSafeObservableCollection<CrashReport> _lstCrashReports = new ThreadSafeObservableCollection<CrashReport>();

        private ObservableCollectionDisplay<CrashReport> _bldCrashReports;

        //Status strip
        private delegate void MainThreadDelegate(INotifyThreadStatus sender, StatusChangedEventArgs args);

        private MainThreadDelegate _mainThreadDelegate;
        private readonly Dictionary<INotifyThreadStatus, ToolStripItem> _statusLabels = new Dictionary<INotifyThreadStatus, ToolStripItem>();

        //background workers
        private DynamoDbLoader _loader;

        private DownloaderWorker _downloader;

        private readonly Dictionary<string, Action<StatusChangedEventArgs>> _specificHandlers;

        public Mainform()
        {
            _specificHandlers = new Dictionary<string, Action<StatusChangedEventArgs>>
            {
                {"DynamoDBConnection" , DynamoDbStatus}
            };
            InitializeComponent();
        }

        private void Mainform_Shown(object sender, EventArgs e)
        {
            if (!PersistentState.Setup)
            {
                SetupForm setupForm = new SetupForm();
                DialogResult result = setupForm.ShowDialog(this);

                if (result != DialogResult.OK)
                {
                    Application.Exit();
                    return;
                }

                PersistentState.Initialize(setupForm.Id, setupForm.Key, setupForm.BulkData);
            }

            _loader = new DynamoDbLoader();
            _loader.StatusChanged += OtherThreadNotificationHandler;

            _downloader = new DownloaderWorker();
            _downloader.StatusChanged += OtherThreadNotificationHandler;

            _mainThreadDelegate = MainThreadAction;

            //lstCrashes.View = View.Details;

            foreach (CrashReport crashReport in PersistentState.Database.GetAllCrashes())
            {
                _lstCrashReports.Add(crashReport);
            }

            _lstCrashReports.Sort(new CrashReportTimeStampFilter());

            _bldCrashReports = new ObservableCollectionDisplay<CrashReport>(_lstCrashReports, c => new CrashReportView(c, _downloader))
            {
                Anchor = AnchorStyles.Bottom | AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Left,
                Location = new Point(12, 69),
                Size = new Size(863, 277),
            };

            tabReports.Controls.Add(_bldCrashReports);

            string automation = PersistentState.Database.GetKey("autodownload_automation");
            if (automation != null)
            {
                cboAutomation.SelectedIndex = int.Parse(automation);
            }

            UpdateDBDependantControls();
        }

        private void UpdateDBDependantControls()
        {
            object o = cboBuild.SelectedItem;
            cboBuild.Items.Clear();
            foreach (string strBuildType in PersistentState.Database.GetAllBuildTypes())
                cboBuild.Items.Add(strBuildType);

            if (o != null) cboBuild.SelectedItem = o;

            o = cboVersion.SelectedItem;
            cboVersion.Items.Clear();
            foreach (Version objVersionType in PersistentState.Database.GetAllVersions().OrderByDescending(v => v))
                cboVersion.Items.Add(objVersionType);

            if (o != null) cboVersion.SelectedItem = o;
        }

        //This is used to subscribe to an action happening on another thread. Least ugly way i know to re-route it to ui thread
        private void OtherThreadNotificationHandler(INotifyThreadStatus sender, StatusChangedEventArgs args)
        {
            try
            {
                if (Disposing || IsDisposed) return;

                Invoke(_mainThreadDelegate, sender, args);
            }
            catch
            {
                // ignored
            }
        }

        private void MainThreadAction(INotifyThreadStatus sender, StatusChangedEventArgs args)
        {
            if (_statusLabels.TryGetValue(sender, out ToolStripItem item))
            {
                item.Text = $"{sender.Name}: {args.Status}";
            }
            else
            {
                item = tsBackground.Items.Add($"{sender.Name}: {args.Status}");
                _statusLabels.Add(sender, item);
            }

            if (_specificHandlers.TryGetValue(sender.Name, out Action<StatusChangedEventArgs> action))
            {
                action(args);
            }
        }

        private void DynamoDbStatus(StatusChangedEventArgs statusChangedEventArgs)
        {
            List<Guid> list = statusChangedEventArgs.AttachedData;

            if (list == null)
                return;

            foreach (Guid guid in list)
            {
                CrashReport item = PersistentState.Database.GetCrash(guid);
                if (item != null)
                    _lstCrashReports.Add(item);
            }

            UpdateDBDependantControls();
        }

        private void deleteDatabaserequiresRestartToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PersistentState.Database.Delete();
            Application.Restart();
        }

        private void cboAutomation_SelectedIndexChanged(object sender, EventArgs e)
        {
            PersistentState.Database.SetKey("autodownload_automation", cboAutomation.SelectedIndex.ToString());
        }

        private void SearchParameterChanged(object sender, EventArgs e)
        {
            _bldCrashReports.Filter(report => TextFilter(report, txtSearch.Text) && OtherFilter(report), true);
        }

        private bool OtherFilter(CrashReport report)
        {
            bool versionOk = true;
            bool buildOk = true;

            if (cboVersion.SelectedItem != null && !report.Version.Equals(cboVersion.SelectedItem))
            {
                versionOk = false;
            }

            if (cboBuild.SelectedItem != null && !report.BuildType.Equals(cboBuild.SelectedItem))
            {
                buildOk = false;
            }

            return buildOk && versionOk;
        }

        private static bool TextFilter(CrashReport report, string search)
        {
            if (report.Guid.ToString("D").Contains(search)) return true;

            if (report.ErrorFrindly.Contains(search)) return true;

            return report.StackTrace?.Contains(search) ?? false;
        }
    }

    public sealed class CrashReportTimeStampFilter : IComparer<CrashReport>
    {
        public int Compare(CrashReport x, CrashReport y)
        {
            return y?.Timestamp.CompareTo(x?.Timestamp) ?? ((x?.Timestamp == null) ? 0 : -1);
        }
    }
}
