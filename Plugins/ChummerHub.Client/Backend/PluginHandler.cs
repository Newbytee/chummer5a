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
using System.Composition;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ChummerHub.Client.Sinners;
using ChummerHub.Client.Backend;
using ChummerHub.Client.Properties;
using ChummerHub.Client.UI;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.Rest;
using Newtonsoft.Json;
using NLog;
using Resources = Chummer.Properties.Resources;

namespace Chummer.Plugins
{
    [Export(typeof(IPlugin))]
    //[ExportMetadata("Name", "SINners")]
    //[ExportMetadata("frmCareer", "true")]
    public class PluginHandler : IPlugin
    {
        private static readonly Lazy<Logger> s_ObjLogger = new Lazy<Logger>(LogManager.GetCurrentClassLogger);
        private static Logger Log => s_ObjLogger.Value;
        public static UploadClient MyUploadClient { get; private set; }
        public static IPlugin MyPluginHandlerInstance { get; private set; }
        public static ChummerMainForm MainForm { get; private set; }

        [ImportingConstructor]
        public PluginHandler()
        {
            if (Settings.Default.UpgradeRequired)
            {
                Settings.Default.Upgrade();
                Settings.Default.UpgradeRequired = false;
                Settings.Default.Save();
            }
            Trace.TraceInformation("Plugin ChummerHub.Client importing (Constructor).");
            MyUploadClient = new UploadClient();
            if (Properties.Settings.Default.UploadClientId == Guid.Empty)
            {
                Properties.Settings.Default.UploadClientId = Guid.NewGuid();
                Properties.Settings.Default.Save();
            }
            MyUploadClient.Id = Properties.Settings.Default.UploadClientId;
            MyPluginHandlerInstance = this;
        }

        public override string ToString()
        {
            return "SINners";
        }

        public bool SetCharacterRosterNode(TreeNode objNode)
        {
            if (objNode?.Tag == null)
                return false;
            if (objNode.ContextMenuStrip == null)
            {
                string strTag = objNode.Tag?.ToString() ?? string.Empty;
                objNode.ContextMenuStrip = MainForm?.CharacterRoster.CreateContextMenuStrip(strTag.EndsWith(".chum5", StringComparison.OrdinalIgnoreCase)
                    && MainForm?.OpenCharacterEditorForms?.Any(x => x.CharacterObject?.FileName == strTag) == true);
            }

            ContextMenuStrip cmsRoster = objNode.ContextMenuStrip ?? new ContextMenuStrip();
            try
            {
                DpiFriendlyToolStripMenuItem tsShowMySINners = new DpiFriendlyToolStripMenuItem
                {
                    Name = "tsShowMySINners",
                    Tag = "Menu_ShowMySINners",
                    Text = "Show all my SINners",
                    Size = new Size(177, 22),
                    ImageDpi96 = Resources.group,
                    ImageDpi192 = Resources.group1,
                };
                tsShowMySINners.Click += ShowMySINnersOnClick;
                tsShowMySINners.UpdateLightDarkMode();
                tsShowMySINners.TranslateToolStripItemsRecursively();
                cmsRoster.Items.Add(tsShowMySINners);

                DpiFriendlyToolStripMenuItem tsSINnersCreateGroup = new DpiFriendlyToolStripMenuItem
                {
                    Name = "tsSINnersCreateGroup",
                    Tag = "Menu_SINnersCreateGroup",
                    Text = "Create Group",
                    Size = new Size(177, 22),
                    ImageDpi96 = Resources.group,
                    ImageDpi192 = Resources.group1,
                };
                tsSINnersCreateGroup.Click += SINnersCreateGroupOnClick;
                tsSINnersCreateGroup.UpdateLightDarkMode();
                tsSINnersCreateGroup.TranslateToolStripItemsRecursively();
                cmsRoster.Items.Add(tsSINnersCreateGroup);

                if (objNode.Tag is CharacterCache member)
                {
                    DpiFriendlyToolStripMenuItem newShare = new DpiFriendlyToolStripMenuItem("Share")
                    {
                        Name = "tsShareChummer",
                        Tag = "Menu_ShareChummer",
                        Text = "Share chummer",
                        Size = new Size(177, 22),
                        ImageDpi96 = Resources.link_add,
                        ImageDpi192 = Resources.link_add1
                    };
                    newShare.Click += NewShareOnClick;
                    newShare.UpdateLightDarkMode();
                    newShare.TranslateToolStripItemsRecursively();
                    cmsRoster.Items.Add(newShare);

                    //is it a favorite sinner?
                    if (member.MyPluginDataDic.TryGetValue("IsSINnerFavorite", out object objFavorite))
                    {
                        DpiFriendlyToolStripMenuItem newFavorite;
                        if (objFavorite is bool isFavorite && isFavorite)
                        {
                            newFavorite = new DpiFriendlyToolStripMenuItem("RemovePinned")
                            {
                                Name = "tsRemovePinnedChummer",
                                Tag = "Menu_RemovePinnedChummer",
                                Text = "remove from pinned Chummers",
                                Size = new Size(177, 22),
                                ImageDpi96 = Resources.user_delete,
                                ImageDpi192 = Resources.user_delete1
                            };
                            newFavorite.Click += RemovePinnedOnClick;
                        }
                        else
                        {
                            newFavorite = new DpiFriendlyToolStripMenuItem("AddPinned")
                            {
                                Name = "tsAddPinnedChummer",
                                Tag = "Menu_AddPinnedChummer",
                                Text = "add to pinned Chummers",
                                Size = new Size(177, 22),
                                ImageDpi96 = Resources.user_add,
                                ImageDpi192 = Resources.user_add1
                            };
                            newFavorite.Click += AddPinnedOnClick;
                        }

                        newFavorite.UpdateLightDarkMode();
                        newFavorite.TranslateToolStripItemsRecursively();
                        cmsRoster.Items.Add(newFavorite);
                    }

                    DpiFriendlyToolStripMenuItem newDelete = new DpiFriendlyToolStripMenuItem("DeleteFromSINners")
                    {
                        Name = "tsDeleteFromSINners",
                        Tag = "Menu_DeleteFromSINners",
                        Text = "delete chummer from SINners registry",
                        Size = new Size(177, 22),
                        ImageDpi96 = Resources.delete,
                        ImageDpi192 = Resources.delete1
                    };
                    if (MainForm != null)
                        newDelete.Click += MainForm.CharacterRoster.tsDelete_Click;
                    newDelete.UpdateLightDarkMode();
                    newDelete.TranslateToolStripItemsRecursively();
                    cmsRoster.Items.Add(newDelete);
                }

                bool isPluginNode = false;
                TreeNode checkNode = objNode;
                while (!isPluginNode && checkNode != null)
                {
                    if (checkNode.Tag is PluginHandler)
                        isPluginNode = true;
                    checkNode = checkNode.Parent;
                }

                if (!isPluginNode)
                    return true;

                if (objNode.Tag is SINnerSearchGroup)
                {
                    DpiFriendlyToolStripMenuItem newShare = new DpiFriendlyToolStripMenuItem("Share")
                    {
                        Name = "tsShareChummerGroup",
                        Tag = "Menu_ShareChummerGroup",
                        Text = "Share chummer group",
                        Size = new Size(177, 22),
                        ImageDpi96 = Resources.link_add,
                        ImageDpi192 = Resources.link_add1
                    };
                    newShare.Click += NewShareOnClick;
                    newShare.UpdateLightDarkMode();
                    newShare.TranslateToolStripItemsRecursively();
                    cmsRoster.Items.Add(newShare);

                    //is it a favorite sinner?
                    DpiFriendlyToolStripMenuItem newFavorite;
                    //if (group.IsFavorite == true)
                    //{
                    //    newFavorite = new DpiFriendlyToolStripMenuItem("RemovePinned")
                    //    {
                    //        Name = "tsRemovePinnedGroup",
                    //        Tag = "Menu_RemovePinnedGroup",
                    //        Text = "remove from pinned",
                    //        Size = new Size(177, 22),
                    //        ImageDpi96 = Resources.user_delete,
                    //        ImageDpi192 = Resources.user_delete1
                    //    };
                    //    newFavorite.Click += RemovePinnedOnClick;
                    //}
                    //else
                    {
                        newFavorite = new DpiFriendlyToolStripMenuItem("AddPinned")
                        {
                            Name = "tsAddPinnedGroup",
                            Tag = "Menu_AddPinnedGroup",
                            Text = "Pin Chummer",
                            Size = new Size(177, 22),
                            ImageDpi96 = Resources.user_add,
                            ImageDpi192 = Resources.user_add1
                        };
                        newFavorite.Click += AddPinnedOnClick;
                    }
                    newFavorite.UpdateLightDarkMode();
                    newFavorite.TranslateToolStripItemsRecursively();
                    cmsRoster.Items.Add(newFavorite);
                }
                DpiFriendlyToolStripMenuItem[] workItems = cmsRoster.Items.OfType<DpiFriendlyToolStripMenuItem>().ToArray();
                foreach (DpiFriendlyToolStripMenuItem item in workItems)
                {
                    switch (item.Name)
                    {
                        case "tsToggleFav":
                        case "tsCloseOpenCharacter":
                        case "tsSort":
                            cmsRoster.Items.Remove(item);
                            break;
                        case "tsDelete":
                            cmsRoster.Items.Remove(item);
                            DpiFriendlyToolStripMenuItem newDelete = new DpiFriendlyToolStripMenuItem(item.Text)
                            {
                                ImageDpi96 = item.ImageDpi96,
                                ImageDpi192 = item.ImageDpi192,
                                Tag = item.Tag,
                                Name = item.Name
                            };
                            if (MainForm != null)
                                newDelete.Click += MainForm.CharacterRoster.tsDelete_Click;
                            newDelete.UpdateLightDarkMode();
                            newDelete.TranslateToolStripItemsRecursively();
                            cmsRoster.Items.Add(newDelete);
                            break;
                    }
                }

                return true;
            }
            finally
            {
                objNode.ContextMenuStrip = cmsRoster;
            }
        }

        public ITelemetry SetTelemetryInitialize(ITelemetry telemetry)
        {
            if (!string.IsNullOrEmpty(Settings.Default.UserEmail) && telemetry?.Context?.User != null)
            {
                telemetry.Context.User.AccountId = Settings.Default.UserEmail;
            }
            return telemetry;
        }

        bool IPlugin.ProcessCommandLine(string parameter)
        {
            Log.Debug("ChummerHub.Client.PluginHandler ProcessCommandLine: " + parameter);
            string argument = string.Empty;
            string onlyparameter = parameter;
            if (parameter.Contains(':'))
            {
                argument = parameter.Substring(parameter.IndexOf(':'));
                argument = argument.TrimStart(':');
                onlyparameter = parameter.Substring(0, parameter.IndexOf(':'));
            }
            switch (onlyparameter)
            {
                case "Load":
                    return Utils.SafelyRunSynchronously(() => HandleLoadCommand(argument));
            }
            Log.Warn("Unknown command line parameter: " + parameter);
            return true;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing || PipeManager == null)
                return;
            //only stop the server if this is the last instance!
            PipeManager.StopServer(!HasDuplicate);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private async Task<bool> HandleLoadCommand(string argument, CancellationToken token = default)
        {
            //check global mutex
            bool blnHasDuplicate;
            try
            {
                blnHasDuplicate = !Program.GlobalChummerMutex.WaitOne(0, false);
            }
            catch (AbandonedMutexException ex)
            {
                Log.Error(ex);
                Utils.BreakIfDebug();
                blnHasDuplicate = true;
            }
            await Task.Run(async () =>
            {
                if (!blnHasDuplicate)
                {
                    TimeSpan uptime = DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime();
                    if (uptime < TimeSpan.FromSeconds(2))
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1), token).ConfigureAwait(false);
                    }
                }
                if (PipeManager != null)
                {
                    try
                    {
                        string SINnerIdvalue = argument.Substring(5).Trim('/');
                        int transactionInt = SINnerIdvalue.IndexOf(':');
                        if (transactionInt != -1)
                        {
                            string transaction = SINnerIdvalue.Substring(transactionInt).TrimStart(':');
                            SINnerIdvalue = SINnerIdvalue.Substring(0, transactionInt).TrimEnd(':');
                            string callback = string.Empty;
                            int callbackInt = transaction.IndexOf(':');
                            if (callbackInt != -1)
                            {
                                callback = transaction.Substring(callbackInt).TrimStart(':');
                                transaction = transaction.Substring(0, callbackInt).TrimEnd(':');
                                callback = WebUtility.UrlDecode(callback);
                            }
                            await StaticUtils.WebCall(callback, 10, "Sending Open Character Request", token);
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Error(e);
                        Program.ShowMessageBox("Error loading SINner: " + e.Message);
                    }
                    string msg = "Load:" + argument;
                    Log.Trace("Sending argument to Pipeserver: " + msg);
                    PipeManager.Write(msg);
                }
            }, token);
            if (blnHasDuplicate)
            {
                Environment.ExitCode = -1;
                return false;
            }

            return true;
        }

        public Task<ICollection<TabPage>> GetTabPages(CharacterCareer input, CancellationToken token = default)
        {
            return !Settings.Default.UserModeRegistered
                ? Task.FromResult<ICollection<TabPage>>(null)
                : GetTabPagesCommon(input, token).ContinueWith(x => (ICollection<TabPage>) x.Result.Yield().ToArray(), token, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default);
        }

        public Task<ICollection<TabPage>> GetTabPages(CharacterCreate input, CancellationToken token = default)
        {
            return !Settings.Default.UserModeRegistered
                ? Task.FromResult<ICollection<TabPage>>(null)
                : GetTabPagesCommon(input, token).ContinueWith(x => (ICollection<TabPage>)x.Result.Yield().ToArray(), token, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default);
        }

        private static async Task<TabPage> GetTabPagesCommon(CharacterShared input, CancellationToken token = default)
        {
            ucSINnersUserControl uc = new ucSINnersUserControl();
            try
            {
                Stopwatch sw = new Stopwatch();
                sw.Start();
                try
                {
                    await uc.SetCharacterFrom(input, token);
                }
                finally
                {
                    sw.Stop();
                    Log.Trace("ucSINnersUserControl SetCharacterFrom finished in " + sw.ElapsedMilliseconds + "ms.");
                }
            }
            catch (Exception e)
            {
                ChummerHub.Client.Backend.Utils.HandleError(e);
                return null;
            }
            TabPage page = new TabPage("SINners")
            {
                Name = "SINners"
            };
            page.Controls.Add(uc);
            return page;
        }

        public static SINner MySINnerLoading { get; internal set; }

        private NamedPipeManager _objPipeManager;
        public NamedPipeManager PipeManager => _objPipeManager;

        public string GetSaveToFileElement(Character input)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));
            string returnme = string.Empty;
            using (CharacterExtended ce = GetMyCe(input))
            {
                PropertyRenameAndIgnoreSerializerContractResolver jsonResolver = new PropertyRenameAndIgnoreSerializerContractResolver();
                JsonSerializerSettings settings = new JsonSerializerSettings
                {
                    ContractResolver = jsonResolver
                };
                //remove the reflection tag - no need to save it
                Tag refTag = ce?.MySINnerFile?.SiNnerMetaData?.Tags?.FirstOrDefault(x => x?.TagName == "Reflection");
                if (refTag != null)
                {
                    ce.MySINnerFile.SiNnerMetaData.Tags.Remove(refTag);
                    returnme = JsonConvert.SerializeObject(ce.MySINnerFile, Formatting.Indented, settings);
                    ce.MySINnerFile.SiNnerMetaData.Tags.Add(refTag);
                }
                else if (ce != null)
                    returnme = JsonConvert.SerializeObject(ce.MySINnerFile, Formatting.Indented, settings);
            }

            return returnme;
        }

        public static async Task<bool> MyOnSaveUpload(Character input, CancellationToken token = default)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));
            if (!Settings.Default.UserModeRegistered)
            {
                string msg = "Public Mode currently does not save to the SINners Plugin by default, even if \"onlinemode\" is enabled!" + Environment.NewLine;
                msg += "If you want to use SINners as online store, please register!";
                Log.Warn(msg);
            }
            else if (await input.DoOnSaveCompletedAsync.RemoveAsync(MyOnSaveUpload, token)) // Makes we only run this if we haven't already triggered the callback
            {
                try
                {
                    using (await CursorWait.NewAsync(MainForm, true, token))
                    {
                        using (CharacterExtended ce = await GetMyCeAsync(input, token))
                        {
                            //ce = new CharacterExtended(input, null);
                            if (ce.MySINnerFile.SiNnerMetaData.Tags.All(a => a?.TagName != "Reflection"))
                            {
                                ce.MySINnerFile.SiNnerMetaData.Tags = ce.PopulateTags();
                            }

                            await ce.Upload(token: token);
                        }

                        TabPage tabPage = null;
                        ThreadSafeObservableCollection<CharacterShared>
                            lstToProcess = MainForm.OpenCharacterEditorForms;
                        if (lstToProcess != null)
                        {
                            CharacterShared found
                                = await lstToProcess.FirstOrDefaultAsync(
                                    x => x.CharacterObject == input, token: token);
                            switch (found)
                            {
                                case CharacterCreate frm when frm.TabCharacterTabs.TabPages.ContainsKey("SINners"):
                                {
                                    await Utils.RunOnMainThreadAsync(() =>
                                    {
                                        int index = frm.TabCharacterTabs.TabPages.IndexOfKey("SINners");
                                        tabPage = frm.TabCharacterTabs.TabPages[index];
                                    }, token);
                                    break;
                                }
                                case CharacterCareer frm2 when frm2.TabCharacterTabs.TabPages.ContainsKey("SINners"):
                                {
                                    await Utils.RunOnMainThreadAsync(() =>
                                    {
                                        int index = frm2.TabCharacterTabs.TabPages.IndexOfKey("SINners");
                                        tabPage = frm2.TabCharacterTabs.TabPages[index];
                                    }, token);
                                    break;
                                }
                            }
                        }

                        if (tabPage == null)
                            return true;
                        Control[] ucseq = tabPage.Controls.Find("SINnersBasic", true);
                        foreach (Control uc in ucseq)
                        {
                            if (uc is ucSINnersBasic sb)
                                await sb.CheckSINnerStatus(token);
                        }

                        Control[] ucseq2 = tabPage.Controls.Find("SINnersAdvanced", true);
                    }
                }
                catch (Exception e)
                {
                    Trace.TraceError(e.ToString());
                }
                finally
                {
                    await input.DoOnSaveCompletedAsync.AddAsync(MyOnSaveUpload, token);
                }
            }
            return true;
        }

        private static CharacterExtended GetMyCe(Character input, CancellationToken token = default)
        {
            return Utils.SafelyRunSynchronously(() => GetMyCeCoreAsync(true, input, token), token);
        }

        private static Task<CharacterExtended> GetMyCeAsync(Character input, CancellationToken token = default)
        {
            return GetMyCeCoreAsync(false, input, token);
        }

        private static async Task<CharacterExtended> GetMyCeCoreAsync(bool blnSync, Character input, CancellationToken token = default)
        {
            CharacterShared found = null;
            ThreadSafeObservableCollection<CharacterShared> lstForms = MainForm?.OpenCharacterEditorForms;
            if (lstForms != null)
            {
                foreach (CharacterShared a in lstForms)
                {
                    if (a?.CharacterObject != input)
                        continue;
                    found = a;
                    break;
                }
            }

            TabPage sinnertab = null;
            if (found != null)
            {
                TabControl.TabPageCollection myCollection = null;
                switch (found)
                {
                    case CharacterCreate foundcreate:
                        myCollection = foundcreate.TabCharacterTabs.TabPages;
                        break;
                    case CharacterCareer foundcareer:
                        myCollection = foundcareer.TabCharacterTabs.TabPages;
                        break;
                }

                if (myCollection == null)
                    return null;

                sinnertab = myCollection.OfType<TabPage>().FirstOrDefault(x => x.Name == "SINners");
            }

            CharacterCache myCharacterCache;
            if (blnSync)
                // ReSharper disable once MethodHasAsyncOverload
                myCharacterCache = new CharacterCache(input?.FileName);
            else
                myCharacterCache = await CharacterCache.CreateFromFileAsync(input?.FileName, token);
            if (sinnertab == null)
                return new CharacterExtended(input, null, myCharacterCache);
            ucSINnersUserControl myUcSIN = sinnertab.Controls.OfType<ucSINnersUserControl>().FirstOrDefault();
            return myUcSIN == null ? new CharacterExtended(input, null, myCharacterCache) : myUcSIN.MyCE;
        }

        public void LoadFileElement(Character input, string fileElement)
        {
            try
            {
                CharacterExtended.SaveFromPluginFile(fileElement, input, MySINnerLoading);
            }
            catch (Exception e)
            {
                Log.Error(e);
#if DEBUG
                throw;
#endif
            }
        }

        public Task<ICollection<ToolStripMenuItem>> GetMenuItems(ToolStripMenuItem menu, CancellationToken token = default)
        {
            List<ToolStripMenuItem> lstReturn = new List<ToolStripMenuItem>(3);
#if DEBUG
            if (Settings.Default.UserModeRegistered)
            {
                DpiFriendlyToolStripMenuItem mnuSINnerSearchs = new DpiFriendlyToolStripMenuItem
                {
                    Name = "mnuSINSearch",
                    Text = "&SINner Search",
                    ImageDpi96 = Resources.group,
                    ImageTransparentColor = Color.Black,
                    Size = new Size(148, 22),
                    Tag = "Menu_Tools_SINnerSearch",
                    ImageDpi192 = Resources.group1,
                };
                mnuSINnerSearchs.Click += mnuSINnerSearchs_Click;
                mnuSINnerSearchs.UpdateLightDarkMode(token: token);
                mnuSINnerSearchs.TranslateToolStripItemsRecursively(token: token);
                lstReturn.Add(mnuSINnerSearchs);
            }

            DpiFriendlyToolStripMenuItem mnuSINnersArchetypes = new DpiFriendlyToolStripMenuItem
            {
                Name = "mnuSINnersArchetypes",
                Text = "&Archetypes",
                ImageDpi96 = Resources.group,
                ImageTransparentColor = Color.Black,
                Size = new Size(148, 22),
                Tag = "Menu_Tools_SINnersArchetypes",
                ImageDpi192 = Resources.group1,
            };
            mnuSINnersArchetypes.Click += mnuSINnersArchetypes_Click;
            mnuSINnersArchetypes.UpdateLightDarkMode(token: token);
            mnuSINnersArchetypes.TranslateToolStripItemsRecursively(token: token);
            lstReturn.Add(mnuSINnersArchetypes);
#endif
            if (Settings.Default.UserModeRegistered)
            {
                DpiFriendlyToolStripMenuItem mnuSINners = new DpiFriendlyToolStripMenuItem
                {
                    Name = "mnuSINners",
                    Text = "&SINners",
                    ImageDpi96 = Resources.group,
                    ImageTransparentColor = Color.Black,
                    Size = new Size(148, 22),
                    Tag = "Menu_Tools_SINners",
                    ImageDpi192 = Resources.group1,
                };
                mnuSINners.Click += mnuSINners_Click;
                mnuSINners.UpdateLightDarkMode(token: token);
                mnuSINners.TranslateToolStripItemsRecursively(token: token);
                lstReturn.Add(mnuSINners);
            }

            return Task.FromResult<ICollection<ToolStripMenuItem>>(lstReturn);
        }

        private void mnuSINnerSearchs_Click(object sender, EventArgs e)
        {
            frmSINnerSearch search = new frmSINnerSearch();
            search.Show();
        }

        private async void mnuSINnersArchetypes_Click(object sender, EventArgs e)
        {
            ResultGroupGetSearchGroups res = null;
            try
            {
                using (await CursorWait.NewAsync(MainForm, true))
                {
                    SinnersClient client = StaticUtils.GetClient();
                    res = await client.GetPublicGroupAsync("Archetypes", string.Empty);
                    if (!(await ChummerHub.Client.Backend.Utils.ShowErrorResponseFormAsync(res) is ResultGroupGetSearchGroups result))
                        return;
                    if (!result.CallSuccess)
                        return;
                    SINSearchGroupResult ssgr = result.MySearchGroupResult;
                    if (ssgr != null && ssgr.SinGroups?.Count > 0)
                    {
                        List<TreeNode> nodelist = await MainForm.CharacterRoster.DoThreadSafeFuncAsync(
                            () => ChummerHub.Client.Backend.Utils
                                            .CharacterRosterTreeNodifyGroupList(
                                                ssgr.SinGroups.Where(a => a.Groupname == "Archetypes")).ToList());
                        foreach (TreeNode node in nodelist)
                        {
                            await MyTreeNodes2Add.AddOrUpdateAsync(node.Name, node,
                                                                   (key, oldValue) => node);
                        }

                        await MainForm.CharacterRoster.RefreshPluginNodesAsync(this);
                        await MainForm.DoThreadSafeAsync(x =>
                        {
                            x.CharacterRoster.treCharacterList.SelectedNode =
                                nodelist.Find(a => a.Name == "Archetypes");
                            x.BringToFront();
                        });
                    }
                    else
                    {
                        Program.ShowMessageBox("No archetypes found!");
                    }
                }
            }
            catch (ArgumentNullException)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (!(await ChummerHub.Client.Backend.Utils.ShowErrorResponseFormAsync(res, ex) is ResultGroupGetSearchGroups))
                    return;
            }
            finally
            {
                //res?.Dispose();
            }
        }

        public static readonly LockingDictionary<string, TreeNode> MyTreeNodes2Add = new LockingDictionary<string, TreeNode>();

        private void mnuSINners_Click(object sender, EventArgs ea)
        {
            try
            {
                using (CursorWait.New(MainForm, true))
                {
                    using (frmSINnerGroupSearch frmSearch = new frmSINnerGroupSearch(null, null)
                    {
                        TopMost = true
                    })
                    {
                        frmSearch.ShowDialog();
                    }
                }

            }
            catch (SerializationException e)
            {
                if (e.Content.Contains("Log in - ChummerHub"))
                {
                    TreeNode node = new TreeNode("Online, but not logged in!")
                    {
                        ToolTipText = "Please log in (Options -> Plugins -> Sinners (Cloud) -> Login",
                        Tag = e
                    };
                    Log.Warn("Online, but not logged in!");
                }
                else
                {
                    Log.Warn(e);
                    TreeNode node = new TreeNode("Error: " + e.Message)
                    {
                        ToolTipText = e.ToString(),
                        Tag = e
                    };
                }
            }
            catch (Exception e)
            {
                Log.Warn(e);
                TreeNode node = new TreeNode("SINners Error: please log in")
                {
                    ToolTipText = e.ToString(),
                    Tag = e
                };
            }
        }


        public System.Reflection.Assembly GetPluginAssembly()
        {
            return typeof(ucSINnersUserControl).Assembly;
        }

        public void SetIsUnitTest(bool isUnitTest)
        {
            StaticUtils.MyUtils.IsUnitTest = isUnitTest;
            MyUploadClient.ChummerVersion = !StaticUtils.MyUtils.IsUnitTest
                ? System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version.ToString() ?? string.Empty
                : System.Reflection.Assembly.GetCallingAssembly().GetName().Version.ToString();
        }

        public UserControl GetOptionsControl()
        {
            return new ucSINnersOptions();
        }

        public async Task<ICollection<TreeNode>> GetCharacterRosterTreeNode(CharacterRoster frmCharRoster, bool forceUpdate, CancellationToken token = default)
        {
            try
            {
                using (await CursorWait.NewAsync(frmCharRoster, true, token))
                {
                    IEnumerable<TreeNode> res = null;
                    if (Settings.Default.UserModeRegistered)
                    {
                        Log.Info("Loading CharacterRoster from SINners...");

                        async Task<ResultAccountGetSinnersByAuthorization> getSINnersFunction()
                        {
                            SinnersClient client = null;
                            ResultAccountGetSinnersByAuthorization ret = null;
                            try
                            {
                                client = StaticUtils.GetClient();
                                if (String.IsNullOrEmpty(Settings.Default.BearerToken))
                                {
                                    ret = await client.GetSINnersByTokenAsync(Settings.Default.IdentityToken, token);
                                    Settings.Default.BearerToken = ret.BearerToken;
                                    Settings.Default.Save();
                                }
                                else
                                    ret = await client.GetSINnersByAuthorizationAsync(token);
                                return ret;
                            }
                            catch (Exception)
                            {
                                if (client != null)
                                    client.ReadResponseAsString = !client.ReadResponseAsString;
                                try
                                {
                                    ret = client != null ? await client.GetSINnersByAuthorizationAsync(token) : null;
                                }
                                catch(ApiException e1)
                                {
                                    if (e1.Response?.Contains("<li><a href=\"/Identity/Account/Login\">Login</a></li>") == true)
                                    {
                                        Log.Info(e1, "User is not logged in.");
                                        throw new ArgumentException("User not logged in.");
                                    }
                                    else {
                                        Log.Error(e1);
                                        throw;
                                    }
                                }
                                finally
                                {
                                    if (client != null)
                                        client.ReadResponseAsString = !client.ReadResponseAsString;
                                }
                                if (ret == null)
                                    throw;
                            }
                            return ret;
                        }

                        res = await ChummerHub.Client.Backend.Utils.GetCharacterRosterTreeNode(forceUpdate, getSINnersFunction, token);
                        if (res == null)
                        {
                            throw new ArgumentException("Could not load owned SINners from WebService.");
                        }
                    }
                    //AddContextMenuStripRecursive(list, myContextMenuStrip);
                    return res?.Concat(MyTreeNodes2Add.Select(x => x.Value).OrderBy(x => x.Text)).ToList()
                           ?? MyTreeNodes2Add.Select(x => x.Value).OrderBy(x => x.Text).ToList();
                }
            }
            catch(SerializationException e)
            {
                if (e.Content.Contains("Log in - ChummerHub"))
                {
                    Log.Warn(e, "Online, but not logged in!");
                    return new List<TreeNode>
                    {
                        new TreeNode("Online, but not logged in!")
                        {
                            ToolTipText = "Please log in (Options -> Plugins -> Sinners (Cloud) -> Login",
                            Tag = e
                        }
                    };
                }

                Log.Error(e);
                return new List<TreeNode>
                {
                    new TreeNode("Error: " + e.Message)
                    {
                        ToolTipText = e.ToString(),
                        Tag = e
                    }
                };
            }
            catch(ApiException e)
            {
                TreeNode node = null;
                Log.Error(e);
                switch (e.StatusCode)
                {
                    case 500:
                        node = new TreeNode("SINers seems to be down (Error 500)")
                        {
                            ToolTipText = e.Message,
                            Tag = e

                        };
                        break;
                    case 200:
                        node = new TreeNode("SINersplugin encounterd an error: " + e.StatusCode)
                        {
                            ToolTipText = e.Message,
                            Tag = e
                        };
                        break;
                    default:
                        node = new TreeNode("SINers encounterd an error: " + e.StatusCode)
                        {
                            ToolTipText = e.Message,
                            Tag = e
                        };
                        break;
                }
                return new List<TreeNode>
                    {
                        node
                    };
            }
            catch(Exception e)
            {
                Log.Info(e);
                TreeNode node = new TreeNode("SINers: please log in")
                {
                    ToolTipText = e.Message,
                    Tag = e

                };

                return new List<TreeNode>
                {
                    node
                };
            }
        }




        private async void SINnersCreateGroupOnClick(object sender, EventArgs e)
        {
            try
            {
                await ChummerHub.Client.Backend.Utils.CreateGroupOnClickAsync();
                await ShowMySINners();
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                Program.ShowMessageBox(ex.Message);
            }
        }

        private async void ShowMySINnersOnClick(object sender, EventArgs e)
        {
            await ShowMySINners();
        }

        private async ValueTask ShowMySINners(CancellationToken token = default)
        {
            try
            {
                using (await CursorWait.NewAsync(MainForm.CharacterRoster, true, token))
                {
                    SINSearchGroupResult MySINSearchGroupResult = await ucSINnerGroupSearch.SearchForGroups(null, token);
                    SINnerSearchGroup item = MySINSearchGroupResult.SinGroups.FirstOrDefault(x => x.Groupname?.Contains("My Data") == true);
                    if (item != null)
                    {
                        List<SINnerSearchGroup> list = new List<SINnerSearchGroup> { item };
                        IEnumerable<TreeNode> nodelist = ChummerHub.Client.Backend.Utils.CharacterRosterTreeNodifyGroupList(list);
                        foreach (TreeNode node in nodelist)
                        {
                            await MyTreeNodes2Add.AddOrUpdateAsync(node.Name, node, (key, oldValue) => node, token);
                        }
                        await MainForm.CharacterRoster.RefreshPluginNodesAsync(this, token);
                        MainForm.CharacterRoster.BringToFront();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                Program.ShowMessageBox(ex.Message);
            }
        }

        private async void AddPinnedOnClick(object sender, EventArgs e)
        {
            TreeNode t = MainForm.CharacterRoster.treCharacterList.SelectedNode;

            switch (t?.Tag)
            {
                case CharacterCache objCache:
                    try
                    {
                        string sinneridstring = null;
                        SinnersClient client = StaticUtils.GetClient();
                        if (objCache.MyPluginDataDic.TryGetValue("SINnerId", out object sinneridobj))
                        {
                            sinneridstring = sinneridobj?.ToString();
                        }

                        if (Guid.TryParse(sinneridstring, out Guid sinnerid))
                        {
                            try
                            {
                                SINner res = await client.PutSINerInGroupAsync(Guid.Empty, sinnerid, null);
                                object response = await ChummerHub.Client.Backend.Utils.ShowErrorResponseFormAsync(res);
                                if (response != null)
                                    await MainForm.CharacterRoster.RefreshPluginNodesAsync(this);
                            }
                            catch (Exception exception)
                            {
                                ChummerHub.Client.Backend.Utils.HandleError(exception);
                            }
                            finally
                            {
                                //res?.Dispose();
                            }
                        }
                    }
                    catch (Exception exception)
                    {
                        Log.Error(exception);
                        Program.ShowMessageBox("Error sharing SINner: " + exception.Message);
                    }

                    break;
                case SINnerSearchGroup ssg:
                    try
                    {
                        SinnersClient client = StaticUtils.GetClient();
                        ResultGroupPutGroupInGroup res = await client.PutGroupInGroupAsync(ssg.Id, null, Guid.Empty, null, null);
                        object response = await ChummerHub.Client.Backend.Utils.ShowErrorResponseFormAsync(res);
                        if (response != null)
                        {
                            await MainForm.CharacterRoster.RefreshPluginNodesAsync(this);
                        }
                    }
                    catch (Exception exception)
                    {
                        ChummerHub.Client.Backend.Utils.HandleError(exception);
                    }
                    finally
                    {
                        //res?.Dispose();
                    }

                    break;
            }
        }

        private async void RemovePinnedOnClick(object sender, EventArgs e)
        {
            TreeNode t = MainForm.CharacterRoster.treCharacterList.SelectedNode;

            switch (t?.Tag)
            {
                case CharacterCache objCache:
                    try
                    {
                        string sinneridstring = null;
                        SinnersClient client = StaticUtils.GetClient();
                        if (objCache.MyPluginDataDic.TryGetValue("SINnerId", out object sinneridobj))
                        {
                            sinneridstring = sinneridobj?.ToString();
                        }

                        if (Guid.TryParse(sinneridstring, out Guid sinnerid))
                        {
                            SINner res = null;
                            try
                            {
                                res = await client.PutSINerInGroupAsync(null, sinnerid, null);

                                object response = await ChummerHub.Client.Backend.Utils.ShowErrorResponseFormAsync(res);
                                if (response != null)
                                {
                                    await MainForm.CharacterRoster.RefreshPluginNodesAsync(this);
                                }

                            }
                            catch(ApiException e1)
                            {
                                if (res != null)
                                {
                                    object response = await ChummerHub.Client.Backend.Utils.ShowErrorResponseFormAsync(res);
                                    if (response != null)
                                    {
                                        await MainForm.CharacterRoster.RefreshPluginNodesAsync(this);
                                    }
                                }
                                else
                                {
                                    ChummerHub.Client.Backend.Utils.HandleError(e1);
                                }

                            }
                            catch (Exception exception)
                            {
                                ChummerHub.Client.Backend.Utils.HandleError(exception);
                            }
                        }
                    }
                    catch (Exception exception)
                    {
                        Log.Error(exception);
                        Program.ShowMessageBox("Error sharing SINner: " + exception.Message);
                    }

                    break;
                case SINnerSearchGroup ssg:
                    try
                    {
                        SinnersClient client = StaticUtils.GetClient();
                        ResultGroupPutGroupInGroup res = await client.PutGroupInGroupAsync(ssg.Id, null, null, null, null);
                        object response = await ChummerHub.Client.Backend.Utils.ShowErrorResponseFormAsync(res);
                        if (response != null)
                        {
                            await MainForm.CharacterRoster.RefreshPluginNodesAsync(this);
                        }
                    }
                    catch (Exception exception)
                    {
                        ChummerHub.Client.Backend.Utils.HandleError(exception);
                    }

                    break;
            }
        }


        private async void NewShareOnClick(object sender, EventArgs e)
        {
            TreeNode t = MainForm.CharacterRoster.treCharacterList.SelectedNode;

            switch (t?.Tag)
            {
                case CharacterCache objCache:
                {
                    using (frmSINnerShare share = new frmSINnerShare
                    {
                        TopMost = true
                    })
                    {
                        share.MyUcSINnerShare.MyCharacterCache = objCache;
                        share.Show();
                        try
                        {
                            await share.MyUcSINnerShare.DoWork();
                        }
                        catch (Exception exception)
                        {
                            Log.Error(exception);
                            Program.ShowMessageBox("Error sharing SINner: " + exception.Message);
                        }
                    }

                    break;
                }
                case SINnerSearchGroup ssg:
                {
                    using (frmSINnerShare share = new frmSINnerShare
                    {
                        TopMost = true
                    })
                    {
                        share.MyUcSINnerShare.MySINnerSearchGroup = ssg;
                        share.Show();
                        try
                        {
                            await share.MyUcSINnerShare.DoWork();
                        }
                        catch (Exception exception)
                        {
                            Log.Error(exception);
                            Program.ShowMessageBox("Error sharing Group: " + exception.Message);
                        }
                    }

                    break;
                }
            }
        }

        //private void AddContextMenuStripRecursive(List<TreeNode> list, ContextMenuStrip myCmsRoster)
        //{
        //    foreach (var node in list)
        //    {
        //        if (node.Parent != null)
        //        {
        //            if (node.Tag is SINnerSearchGroup group)
        //            {

        //            }
        //            else if (node.Tag is frmCharacterRoster.CharacterCache member)
        //            {
        //                PluginHandler.MainForm.DoThreadSafe(() => { node.ContextMenuStrip = myCmsRoster; });
        //            }
        //        }

        //        if (node.Nodes.Count > 0)
        //        {
        //            var myList = node.Nodes.Cast<TreeNode>().ToList();
        //            AddContextMenuStripRecursive(myList, myCmsRoster);
        //        }
        //    }
        //}

        public bool HasDuplicate { get; set; }

        public void CustomInitialize(ChummerMainForm mainControl)
        {
            Log.Info("CustomInitialize for Plugin ChummerHub.Client entered.");
            MainForm = mainControl;
            if (string.IsNullOrEmpty(Settings.Default.TempDownloadPath))
            {
                Settings.Default.TempDownloadPath = Path.GetTempPath();
            }

            //check global mutex
            HasDuplicate = false;
            try
            {
                HasDuplicate = !Program.GlobalChummerMutex.WaitOne(0, false);
            }
            catch (AbandonedMutexException ex)
            {
                Log.Error(ex);
                Utils.BreakIfDebug();
                HasDuplicate = true;
            }

            if (_objPipeManager != null)
                return;
            NamedPipeManager objNewNamedPipeManager = new NamedPipeManager();
            if (Interlocked.CompareExchange(ref _objPipeManager, objNewNamedPipeManager, null) != null)
            {
                objNewNamedPipeManager.Dispose();
                return;
            }
            Log.Info("blnHasDuplicate = " + HasDuplicate.ToString(CultureInfo.InvariantCulture));
            // If there is more than 1 instance running, do not let the application start a receiving server.
            if (HasDuplicate)
            {
                Log.Info("More than one instance, not starting NamedPipe-Server...");
                throw new InvalidOperationException("More than one instance is running.");
            }

            Log.Info("Only one instance, starting NamedPipe-Server...");
            Utils.SafelyRunSynchronously(() => objNewNamedPipeManager.StartServer());
            objNewNamedPipeManager.ReceiveString += x => Utils.SafelyRunSynchronously(() => HandleNamedPipe_OpenRequest(x));
        }

        private static string fileNameToLoad = string.Empty;

        public static async Task HandleNamedPipe_OpenRequest(string argument, CancellationToken token = default)
        {
            Log.Trace("Pipeserver receiced a request: " + argument);
            if (!string.IsNullOrEmpty(argument))
            {
                //make sure the mainform is visible ...
                TimeSpan uptime = DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime();
                if (uptime < TimeSpan.FromSeconds(5))
                    await Task.Delay(TimeSpan.FromSeconds(4), token).ConfigureAwait(false);
                if (!MainForm.Visible)
                {
                    await MainForm.DoThreadSafeAsync(x =>
                    {
                        ChummerMainForm objMainForm = x;
                        if (objMainForm.WindowState == FormWindowState.Minimized)
                            objMainForm.WindowState = FormWindowState.Normal;
                    }, token: token);
                }

                await MainForm.DoThreadSafeAsync(x =>
                {
                    ChummerMainForm objMainForm = x;
                    objMainForm.Activate();
                    objMainForm.BringToFront();
                }, token: token);
                SinnersClient client = StaticUtils.GetClient();
                while (!MainForm.Visible)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), token).ConfigureAwait(false);
                }
                if (argument.StartsWith("Load:", StringComparison.Ordinal))
                {
                    try
                    {
                        string SINnerIdvalue = argument.Substring(5);
                        SINnerIdvalue = SINnerIdvalue.Trim('/');
                        int transactionInt = SINnerIdvalue.IndexOf(':');
                        string callback = null;
                        if (transactionInt != -1)
                        {
                            string transaction = SINnerIdvalue.Substring(transactionInt);
                            SINnerIdvalue = SINnerIdvalue.Substring(0, transactionInt).TrimEnd(':');
                            transaction = transaction.TrimStart(':');
                            int callbackInt = transaction.IndexOf(':');
                            if (callbackInt != -1)
                            {
                                callback = transaction.Substring(callbackInt);
                                transaction = transaction.Substring(0, callbackInt).TrimEnd(':');
                                callback = callback.TrimStart(':');
                                callback = WebUtility.UrlDecode(callback);
                            }

                            await StaticUtils.WebCall(callback, 30,
                                "Open Character Request received!", token);
                        }

                        if (Guid.TryParse(SINnerIdvalue, out Guid SINnerId))
                        {

                            ResultSinnerGetSINById found = await client.GetSINByIdAsync(SINnerId, token);
                            await ChummerHub.Client.Backend.Utils.ShowErrorResponseFormAsync(found, token: token);
                            if (found?.CallSuccess == true)
                            {
                                await StaticUtils.WebCall(callback, 40,
                                    "Character found online", token);
                                fileNameToLoad =
                                    await ChummerHub.Client.Backend.Utils.DownloadFileTask(found.MySINner, null, token);
                                await StaticUtils.WebCall(callback, 70,
                                    "Character downloaded", token);
                                await MainFormLoadChar(fileNameToLoad, token);
                                await StaticUtils.WebCall(callback, 100,
                                    "Character opened", token);
                            }
                            else if (found == null || !found.CallSuccess)
                            {
                                await StaticUtils.WebCall(callback, 0,
                                    "Character not found", token);
                                Program.ShowMessageBox("Could not find a SINner with Id " + SINnerId + " online!");
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Error(e);
                        Program.ShowMessageBox("Error loading SINner: " + e.Message);
                    }

                }
                else
                {
                    throw new ArgumentException("Unkown command received: " + argument, nameof(argument));
                }
            }
        }

        private static async ValueTask MainFormLoadChar(string fileToLoad, CancellationToken token = default)
        {
            //already open
            Character objCharacter = await Program.OpenCharacters.FirstOrDefaultAsync(a => a.FileName == fileToLoad, token: token);
            if (objCharacter == null)
            {
                objCharacter = new Character
                {
                    FileName = fileToLoad
                };
                using (ThreadSafeForm<LoadingBar> frmLoadingForm = await Program.CreateAndShowProgressBarAsync(Path.GetFileName(fileToLoad), Character.NumLoadingSections, token))
                {
                    if (await objCharacter.LoadAsync(frmLoadingForm: frmLoadingForm.MyForm, showWarnings: Settings.Default.IgnoreWarningsOnOpening, token: token))
                        await Program.OpenCharacters.AddAsync(objCharacter, token);
                    else
                        return;
                }
            }
            using (await CursorWait.NewAsync(MainForm, token: token))
            {
                if (!await Program.SwitchToOpenCharacter(objCharacter, token))
                    await Program.OpenCharacter(objCharacter, false, token);
                await MainForm.DoThreadSafeAsync(x => x.BringToFront(), token: token);
            }
        }

        public async Task<bool> DoCharacterList_DragDrop(object sender, DragEventArgs dragEventArgs, TreeView treCharacterList, CancellationToken token = default)
        {
            if (dragEventArgs == null)
                throw new ArgumentNullException(nameof(dragEventArgs));
            if (treCharacterList == null)
                throw new ArgumentNullException(nameof(treCharacterList));
            try
            {
                // Do not allow the root element to be moved.
                if (treCharacterList.SelectedNode == null || treCharacterList.SelectedNode.Level == 0 ||
                    treCharacterList.SelectedNode.Parent?.Tag?.ToString() == "Watch")
                    return false;

                if (dragEventArgs.Data.GetDataPresent("System.Windows.Forms.TreeNode", false))
                {
                    if (!(sender is TreeView treSenderView))
                        return false;
                    Point pt = treSenderView.PointToClient(new Point(dragEventArgs.X, dragEventArgs.Y));
                    TreeNode nodDestinationNode = treSenderView.GetNodeAt(pt);
                    if (nodDestinationNode.Level > 0)
                        nodDestinationNode = nodDestinationNode.Parent;
                    string strDestinationNode = nodDestinationNode.Tag?.ToString();
                    if (strDestinationNode != "Watch")
                    {
                        if (!(dragEventArgs.Data.GetData("System.Windows.Forms.TreeNode") is TreeNode nodNewNode))
                            return false;
                        SinnersClient client = StaticUtils.GetClient();
                        Guid? mySiNnerId = null;
                        switch (nodNewNode.Tag)
                        {
                            case SINnerSearchGroup sinGroup when nodDestinationNode.Tag == MyPluginHandlerInstance:
                            {
                                ResultGroupPutGroupInGroup res = await client.PutGroupInGroupAsync(sinGroup.Id, sinGroup.Groupname, null, null, null, token);
                                await ChummerHub.Client.Backend.Utils.ShowErrorResponseFormAsync(res, token: token);
                                return true;
                            }
                            case SINnerSearchGroup sinGroup when nodDestinationNode.Tag is SINnerSearchGroup destGroup:
                            {
                                ResultGroupPutGroupInGroup res = await client.PutGroupInGroupAsync(sinGroup.Id, sinGroup.Groupname, destGroup.Id, sinGroup.MyAdminIdentityRole, sinGroup.IsPublic, token);
                                await ChummerHub.Client.Backend.Utils.ShowErrorResponseFormAsync(res, token: token);
                                return true;
                            }
                            case CharacterCache objCache:
                            {
                                if (objCache.MyPluginDataDic.TryGetValue("SINnerId", out object sinidob, token))
                                {
                                    mySiNnerId = (Guid?) sinidob;
                                }
                                else
                                {
                                    using (CharacterExtended ce = await ChummerHub.Client.Backend.Utils.UploadCharacterFromFile(objCache.FilePath, token))
                                        mySiNnerId = ce?.MySINnerFile?.Id;
                                }

                                break;
                            }
                            case SINner sinner:
                                mySiNnerId = sinner.Id;
                                break;
                        }

                        if (mySiNnerId != null)
                        {
                            if (nodDestinationNode.Tag == MyPluginHandlerInstance)
                            {
                                SINner res = await client.PutSINerInGroupAsync(null, mySiNnerId, null, token);
                                await ChummerHub.Client.Backend.Utils.ShowErrorResponseFormAsync(res, token: token);
                                return true;
                            }
                            else if (nodDestinationNode.Tag is SINnerSearchGroup destGroup)
                            {
                                string passwd = null;
                                if (destGroup.HasPassword)
                                {
                                    using (frmSINnerPassword getPWD = new frmSINnerPassword())
                                    {
                                        string pwdquestion = await LanguageManager.GetStringAsync("String_SINners_EnterGroupPassword", true, token);
                                        string pwdcaption = await LanguageManager.GetStringAsync("String_SINners_EnterGroupPasswordTitle", true, token);
                                        passwd = getPWD.ShowDialog(Program.MainForm, pwdquestion, pwdcaption);
                                    }
                                }
                                SINner res = await client.PutSINerInGroupAsync(destGroup.Id, mySiNnerId, passwd, token);
                                await ChummerHub.Client.Backend.Utils.ShowErrorResponseFormAsync(res, token: token);
                                return true;
                            }
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                Log.Error(exception);
            }
            finally
            {
                await MainForm.CharacterRoster.RefreshPluginNodesAsync(this, token);
            }
            return true;
        }
    }
}
