using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Chummer;
using Microsoft.Rest;
using System.Net;
using SINners.Models;
using ChummerHub.Client.Backend;
using Chummer.Plugins;
using NLog;

namespace ChummerHub.Client.UI
{
    public partial class ucSINnersBasic : UserControl
    {
        private readonly Logger Log = LogManager.GetCurrentClassLogger();
        private ucSINnersUserControl myUC { get; set; }

        public ucSINnersBasic()
        {
            SINnersBasicConstructor(null);
        }

        public ucSINnersBasic(ucSINnersUserControl parent)
        {
            if (parent != null)
                SINnersBasicConstructor(parent);
        }

        private bool _inConstructor;

        private void SINnersBasicConstructor(ucSINnersUserControl parent)
        {
            _inConstructor = true;
            InitializeComponent();

            TagValueArchetype.DataSource = ContactControl.ContactArchetypes;
            Name = "SINnersBasic";
            bGroupSearch.Enabled = false;
            AutoSize = true;
            myUC = parent;
            myUC.MyCE = parent.MyCE;
            if (myUC.MyCE?.MySINnerFile?.Id != null)
                tbID.Text = myUC.MyCE?.MySINnerFile?.Id?.ToString();
            string tip =
                "Assigning this SINner a new Id enables you to save multiple versions of this chummer on SINnersHub." +
                Environment.NewLine;
            tip += "";
            bGenerateNewId.SetToolTip(tip);
            CheckSINnerStatus().ContinueWith(a =>
            {
                if (!a.Result)
                {
                    Log.Error("somehow I couldn't check the onlinestatus of " +
                                                        myUC.MyCE.MySINnerFile.Id);
                }
            });
            foreach (var cb in gpTags.Controls)
            {
                if (cb is Control cont)
                    cont.Click += OnGroupBoxTagsClick;
                if (cb is CheckBox cccb)
                {
                    cccb.CheckedChanged += OnGroupBoxTagsClick;
                    cccb.CheckStateChanged += OnGroupBoxTagsClick;
                }

                if (cb is ComboBox ccb)
                    ccb.TextChanged += OnGroupBoxTagsClick;
                if (cb is TextBox ctb)
                    ctb.TextChanged += OnGroupBoxTagsClick;
            }

            _inConstructor = false;
        }

        public async Task<bool> CheckSINnerStatus()
        {

            try
            {
                if (myUC?.MyCE?.MySINnerFile?.Id == null || myUC.MyCE.MySINnerFile.Id == Guid.Empty)
                {
                    PluginHandler.MainForm.DoThreadSafe(() =>
                    {
                        bUpload.Text = "SINless Character/Error";
                    });
                    return false;
                }

                using (new CursorWait(true, this))
                {
                    var client = StaticUtils.GetClient();
                    var response = await client.GetSINnerGroupFromSINerByIdWithHttpMessagesAsync(myUC.MyCE.MySINnerFile.Id.Value).ConfigureAwait(true);
                    HttpStatusCode eResponseStatus = response.Response.StatusCode;
                    SINnerGroup objMySiNnerGroup = eResponseStatus == HttpStatusCode.OK ? response.Body.MySINnerGroup : null;
                    response.Dispose();
                    PluginHandler.MainForm.DoThreadSafe(() =>
                    {
                        if (eResponseStatus == HttpStatusCode.OK)
                        {
                            myUC.MyCE.MySINnerFile.MyGroup = objMySiNnerGroup;
                            bUpload.Text = "Remove from SINners";
                            bGroupSearch.Enabled = true;
                            lUploadStatus.Text = "online";
                            bUpload.Enabled = true;
                        }
                        else if (eResponseStatus == HttpStatusCode.NotFound)
                        {
                            myUC.MyCE.MySINnerFile.MyGroup = null;
                            lUploadStatus.Text = "not online";
                            bGroupSearch.Enabled = false;
                            bGroupSearch.SetToolTip(
                                "SINner needs to be uploaded first, before he/she can join a group.");
                            bUpload.Enabled = true;
                            bUpload.Text = "Upload";
                        }
                        else if (eResponseStatus == HttpStatusCode.NoContent)
                        {
                            myUC.MyCE.MySINnerFile.MyGroup = null;
                            lUploadStatus.Text = "Statuscode: " + eResponseStatus;
                            bGroupSearch.Enabled = true;
                            bGroupSearch.SetToolTip(
                                "SINner does not belong to a group.");
                            bUpload.Text = "Remove from SINners";
                            lUploadStatus.Text = "online";
                            bUpload.Enabled = true;
                        }
                        cbTagCustom.Enabled = false;
                        TagValueCustomName.Enabled = false;
                    });
                    PluginHandler.MainForm.DoThreadSafe(UpdateTags);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                PluginHandler.MainForm.DoThreadSafe(() =>
                {
                    bUpload.Text = "unknown Status";
                });
                return false;
            }
            return true;
        }

        private void UpdateTags()
        {
            if (StaticUtils.UserRoles.Any(a => string.Equals(a, "ArchetypeAdmin", StringComparison.InvariantCultureIgnoreCase)))
            {
                cbTagCustom.Enabled = true;
                TagValueCustomName.Enabled = true;
            }
            if (myUC?.MyCE?.MySINnerFile?.MyGroup != null)
                lGourpForSinner.Text = myUC.MyCE.MySINnerFile.MyGroup.Groupname;

            var gpControlSeq = GetAllControls(gpTags).Where(x => x.Name.Contains("cbTag")).ToList();
            var gpControlValueSeq = GetAllControls(gpTags).Where(x => x.Name.Contains("TagValue")).ToList();

            foreach (var cb in gpControlSeq)
            {
                if (cb is CheckBox cbTag)
                    cbTag.CheckState = CheckState.Unchecked;
            }

            if (myUC.MyCE?.MySINnerFile != null)
            {
                foreach (Tag tag in myUC.MyCE.MySINnerFile.SiNnerMetaData.Tags)
                {
                    if (tag == null)
                        continue;
                    //search for a CheckBox that is named like the tag
                    string checkBoxKey = "cbTag" + tag.TagName;
                    Control objMatchingCheckBox = gpControlSeq.FirstOrDefault(x => x.Name == checkBoxKey);
                    if (objMatchingCheckBox == null)
                        continue;
                    if (!(objMatchingCheckBox is CheckBox cbTag))
                        throw new ArgumentNullException("Control " + checkBoxKey + " is NOT a Checkbox!");

                    cbTag.Checked = bool.TryParse(tag.TagValue, out var value) && value;
                    //search for the value-control (whatever that may be)
                    Control objMatchingControl = gpControlValueSeq.FirstOrDefault(x => x.Name == "TagValue" + tag.TagName);
                    if (objMatchingControl == null)
                        continue;

                    cbTag.Checked = true;
                    if (objMatchingControl is TextBox tbTagValue)
                    {
                        tbTagValue.Text = tag.TagValue;
                    }
                    else if (objMatchingControl is ComboBox comboTagValue)
                    {
                        if (tag.TagValue != null)
                        {
                            if (!comboTagValue.Items.Contains(tag.TagValue))
                                comboTagValue.Items.Add(tag.TagValue);
                            comboTagValue.SelectedItem = tag.TagValue;
                        }
                    }
                    else if (objMatchingControl is NumericUpDown upDownTagValue)
                    {
                        if (decimal.TryParse(tag.TagValue, out var val))
                            upDownTagValue.Value = val;
                    }
                }
            }
        }

        private void OnGroupBoxTagsClick(object sender, object eventargs)
        {
            // Call the base class
            //base.OnClick(eventargs);
            SaveTagsToSinner();
        }

        private IEnumerable<Control> GetAllControls(Control container)
        {
            foreach (Control objLoopChild in container.Controls)
            {
                yield return objLoopChild;
                foreach (Control objLoopSubchild in GetAllControls(objLoopChild))
                    yield return objLoopSubchild;
            }
        }

        private void SaveTagsToSinner()
        {
            if (_inConstructor)
                return;
            if (myUC == null)
                return;
            var gpControlValueSeq = GetAllControls(gpTags).Where(x => x.Name.Contains("TagValue")).ToList();

            myUC.MyCE.MySINnerFile.SiNnerMetaData.Tags = myUC.MyCE.MySINnerFile.SiNnerMetaData.Tags.Where(a => a != null).ToList();

            foreach (var cb in GetAllControls(gpTags).Where(x => x.Name.Contains("cbTag")))
            {
                if (!(cb is CheckBox cbTag))
                    continue;

                string tagName = cbTag.Name.Substring("cbTag".Length);
                Tag tag;
                if (myUC.MyCE.MySINnerFile.SiNnerMetaData.Tags.All(a => a != null && a.TagName != tagName))
                {
                    tag = new Tag(true)
                    {
                        SiNnerId = myUC.MyCE.MySINnerFile.Id,
                        TagName = tagName
                    };
                }
                else
                {
                    tag = myUC.MyCE.MySINnerFile.SiNnerMetaData.Tags.FirstOrDefault(a => a != null &&  a.TagName == tagName);
                }
                if (tag == null)
                    continue;
                if (myUC.MyCE.MySINnerFile.SiNnerMetaData.Tags.Contains(tag))
                    myUC.MyCE.MySINnerFile.SiNnerMetaData.Tags.Remove(tag);
                if (cbTag.Checked == false)
                    continue;
                if (cbTag.CheckState == CheckState.Checked)
                    tag.TagValue = bool.TrueString;
                myUC.MyCE.MySINnerFile.SiNnerMetaData.Tags.Add(tag);
                //search for the value
                Control objMatchingControl = gpControlValueSeq.FirstOrDefault(x => x.Name == "TagValue" + tag.TagName);
                if (objMatchingControl == null)
                    continue;
                if (objMatchingControl is TextBox tbTagValue)
                {
                    tag.TagValue = tbTagValue.Text;
                }
                else if (objMatchingControl is ComboBox comboTagValue)
                {
                    tag.TagValue = comboTagValue.SelectedItem?.ToString();
                }
                else if (objMatchingControl is NumericUpDown upDownTagValue)
                {
                    tag.TagValue = upDownTagValue.Value.ToString(CultureInfo.InvariantCulture);
                }
            }
        }


        private async void bUpload_Click(object sender, EventArgs e)
        {
            using (new CursorWait(true, this))
            {
                try
                {
                    if (bUpload.Text.Contains("Upload"))
                    {
                        lUploadStatus.Text = @"uploading";
                        await myUC.MyCE.Upload().ConfigureAwait(true);
                    }
                    else
                    {
                        lUploadStatus.Text = @"removing";
                        await myUC.RemoveSINnerAsync().ConfigureAwait(true);
                    }

                }
                catch (Exception exception)
                {
                    Program.MainForm.ShowMessageBox(exception.Message);
                }
            }
            await CheckSINnerStatus().ConfigureAwait(true);
        }

        private void bGroupSearch_Click(object sender, EventArgs e)
        {
            frmSINnerGroupSearch gs = new frmSINnerGroupSearch(myUC.MyCE, this);
            gs.MySINnerGroupSearch.OnGroupJoinCallback += (o, group) =>
            {
                PluginHandler.MainForm.DoThreadSafe(() =>
                {
                    PluginHandler.MainForm.CharacterRoster.LoadCharacters(false, false, false);
                });
            };
            gs.ShowDialog();
            gs.Close();
        }

        private async void BVisibility_Click(object sender, EventArgs e)
        {
            var visfrm = new frmSINnerVisibility();
            using (new CursorWait(true, this))
            {
                if (!myUC.MyCE.MySINnerFile.SiNnerMetaData.Visibility.UserRights.Any())
                {
                    var client = StaticUtils.GetClient();
                    if (myUC.MyCE.MySINnerFile.Id != null)
                    {
                        HttpOperationResponse<ResultSinnerGetSINnerVisibilityById> res =
                            await client.GetSINnerVisibilityByIdWithHttpMessagesAsync(
                                myUC.MyCE.MySINnerFile.Id.Value).ConfigureAwait(true);
                        await Backend.Utils.HandleError(res, res.Body).ConfigureAwait(true);
                        if (res.Body?.CallSuccess == true)
                        {
                            myUC.MyCE.MySINnerFile.SiNnerMetaData.Visibility.UserRights = res.Body.UserRights;
                        }
                        res.Dispose();
                    }
                }
            }

            visfrm.MyVisibility = myUC.MyCE.MySINnerFile.SiNnerMetaData.Visibility;
            var result = visfrm.ShowDialog(this);
            if (result == DialogResult.OK)
            {
                myUC.MyCE.MySINnerFile.SiNnerMetaData.Visibility = visfrm.MyVisibility;
            }
            visfrm.Close();
        }

        private void BGenerateNewId_Click(object sender, EventArgs e)
        {
            var oldId = myUC.MyCE.MySINnerFile.Id;
            myUC.MyCE.MySINnerFile.Id = Guid.NewGuid();
            myUC.MyCE.MySINnerFile.SiNnerMetaData.Id = Guid.NewGuid();
            myUC.MyCE.MySINnerFile.SiNnerMetaData.Visibility.Id = Guid.NewGuid();
            foreach (var user in myUC.MyCE.MySINnerFile.SiNnerMetaData.Visibility.UserRights)
            {
                user.Id = Guid.NewGuid();
            }
            myUC.MyCE.PopulateTags();
            //this.myUC.MyCE.MySINnerFile.MyExtendedAttributes.Id = Guid.NewGuid();
            if (oldId != null)
            {
                myUC.CharacterObject.FileName =  myUC.CharacterObject.FileName.Replace(oldId.ToString(), myUC.MyCE.MySINnerFile.Id.ToString());
            }
            myUC.CharacterObject.Save(myUC.MyCE.MySINnerFile.Id + ".chum5", false);
            tbID.Text = myUC.MyCE.MySINnerFile.Id.ToString();
        }

        private void TlpTags_MouseLeave(object sender, EventArgs e)
        {
            SaveTagsToSinner();
        }
    }
}
