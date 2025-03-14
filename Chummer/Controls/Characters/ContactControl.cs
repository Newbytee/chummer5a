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
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.XPath;
using Chummer.Properties;
using Timer = System.Windows.Forms.Timer;

namespace Chummer
{
    public partial class ContactControl : UserControl
    {
        private readonly Contact _objContact;
        private int _intLoading = 1;

        private int _intStatBlockIsLoaded;
        //private readonly int _intLowHeight = 25;
        //private readonly int _intFullHeight = 156;

        private int _intUpdatingRole = 1;
        private readonly Timer _tmrRoleChangeTimer;
        private int _intUpdatingMetatype = 1;
        private readonly Timer _tmrMetatypeChangeTimer;
        private int _intUpdatingGender = 1;
        private readonly Timer _tmrGenderChangeTimer;
        private int _intUpdatingAge = 1;
        private readonly Timer _tmrAgeChangeTimer;
        private int _intUpdatingType = 1;
        private readonly Timer _tmrTypeChangeTimer;
        private int _intUpdatingPersonalLife = 1;
        private readonly Timer _tmrPersonalLifeChangeTimer;
        private int _intUpdatingPreferredPayment = 1;
        private readonly Timer _tmrPreferredPaymentChangeTimer;
        private int _intUpdatingHobbiesVice = 1;
        private readonly Timer _tmrHobbiesViceChangeTimer;

        // Events.
        public event EventHandler<TextEventArgs> ContactDetailChanged;

        public event EventHandler DeleteContact;

        #region Control Events

        public ContactControl(Contact objContact)
        {
            _objContact = objContact ?? throw new ArgumentNullException(nameof(objContact));

            InitializeComponent();

            _tmrRoleChangeTimer = new Timer { Interval = 1000 };
            _tmrRoleChangeTimer.Tick += UpdateContactRole;
            _tmrMetatypeChangeTimer = new Timer { Interval = 1000 };
            _tmrMetatypeChangeTimer.Tick += UpdateMetatype;
            _tmrGenderChangeTimer = new Timer { Interval = 1000 };
            _tmrGenderChangeTimer.Tick += UpdateGender;
            _tmrAgeChangeTimer = new Timer { Interval = 1000 };
            _tmrAgeChangeTimer.Tick += UpdateAge;
            _tmrTypeChangeTimer = new Timer { Interval = 1000 };
            _tmrTypeChangeTimer.Tick += UpdateType;
            _tmrPersonalLifeChangeTimer = new Timer { Interval = 1000 };
            _tmrPersonalLifeChangeTimer.Tick += UpdatePersonalLife;
            _tmrPreferredPaymentChangeTimer = new Timer { Interval = 1000 };
            _tmrPreferredPaymentChangeTimer.Tick += UpdatePreferredPayment;
            _tmrHobbiesViceChangeTimer = new Timer { Interval = 1000 };
            _tmrHobbiesViceChangeTimer.Tick += UpdateHobbiesVice;

            Disposed += (sender, args) => UnbindContactControl();

            this.UpdateLightDarkMode();
            this.TranslateWinForm();

            foreach (ToolStripItem tssItem in cmsContact.Items)
            {
                tssItem.UpdateLightDarkMode();
                tssItem.TranslateToolStripItemsRecursively();
            }
        }

        private async void ContactControl_Load(object sender, EventArgs e)
        {
            if (this.IsNullOrDisposed())
                return;
            await LoadContactList().ConfigureAwait(false);

            await DoDataBindings().ConfigureAwait(false);

            if (_objContact.IsEnemy)
            {
                if (cmdLink != null)
                {
                    string strText = !string.IsNullOrEmpty(_objContact.FileName)
                        ? await LanguageManager.GetStringAsync("Tip_Enemy_OpenLinkedEnemy").ConfigureAwait(false)
                        : await LanguageManager.GetStringAsync("Tip_Enemy_LinkEnemy").ConfigureAwait(false);
                    await cmdLink.SetToolTipTextAsync(strText).ConfigureAwait(false);
                }

                string strTooltip = await LanguageManager.GetStringAsync("Tip_Enemy_EditNotes").ConfigureAwait(false);
                if (!string.IsNullOrEmpty(_objContact.Notes))
                    strTooltip += Environment.NewLine + Environment.NewLine + _objContact.Notes;
                await cmdNotes.SetToolTipTextAsync(strTooltip.WordWrap()).ConfigureAwait(false);
            }
            else
            {
                if (cmdLink != null)
                {
                    string strText = !string.IsNullOrEmpty(_objContact.FileName)
                        ? await LanguageManager.GetStringAsync("Tip_Contact_OpenLinkedContact").ConfigureAwait(false)
                        : await LanguageManager.GetStringAsync("Tip_Contact_LinkContact").ConfigureAwait(false);
                    await cmdLink.SetToolTipTextAsync(strText).ConfigureAwait(false);
                }

                string strTooltip = await LanguageManager.GetStringAsync("Tip_Contact_EditNotes").ConfigureAwait(false);
                if (!string.IsNullOrEmpty(_objContact.Notes))
                    strTooltip += Environment.NewLine + Environment.NewLine + _objContact.Notes;
                await cmdNotes.SetToolTipTextAsync(strTooltip.WordWrap()).ConfigureAwait(false);
            }

            Interlocked.Decrement(ref _intUpdatingRole);
            Interlocked.Decrement(ref _intLoading);
        }

        public void UnbindContactControl()
        {
            _tmrRoleChangeTimer.Dispose();
            _tmrMetatypeChangeTimer.Dispose();
            _tmrGenderChangeTimer.Dispose();
            _tmrAgeChangeTimer.Dispose();
            _tmrTypeChangeTimer.Dispose();
            _tmrPersonalLifeChangeTimer.Dispose();
            _tmrPreferredPaymentChangeTimer.Dispose();
            _tmrHobbiesViceChangeTimer.Dispose();
            foreach (Control objControl in Controls)
            {
                objControl.DataBindings.Clear();
            }
        }

        private void nudConnection_ValueChanged(object sender, EventArgs e)
        {
            // Raise the ContactDetailChanged Event when the NumericUpDown's Value changes.
            if (_intLoading == 0 && _intStatBlockIsLoaded > 1)
                ContactDetailChanged?.Invoke(this, new TextEventArgs("Connection"));
        }

        private async void nudLoyalty_ValueChanged(object sender, EventArgs e)
        {
            // Raise the ContactDetailChanged Event when the NumericUpDown's Value changes.
            // The entire ContactControl is passed as an argument so the handling event can evaluate its contents.
            if (_intLoading > 0 || _intStatBlockIsLoaded < 1)
                return;
            while (_intStatBlockIsLoaded == 1)
                await Utils.SafeSleepAsync().ConfigureAwait(false);
            ContactDetailChanged?.Invoke(this, new TextEventArgs("Loyalty"));
        }

        private void cmdDelete_Click(object sender, EventArgs e)
        {
            // Raise the DeleteContact Event when the user has confirmed their desire to delete the Contact.
            // The entire ContactControl is passed as an argument so the handling event can evaluate its contents.
            DeleteContact?.Invoke(this, e);
        }

        private async void chkGroup_CheckedChanged(object sender, EventArgs e)
        {
            if (_intLoading > 0 || _intStatBlockIsLoaded < 1)
                return;
            while (_intStatBlockIsLoaded == 1)
                await Utils.SafeSleepAsync().ConfigureAwait(false);
            ContactDetailChanged?.Invoke(this, new TextEventArgs("Group"));
        }

        private async void cmdExpand_Click(object sender, EventArgs e)
        {
            await SetExpandedAsync(!await GetExpandedAsync().ConfigureAwait(false)).ConfigureAwait(false);
        }

        private void txtContactName_TextChanged(object sender, EventArgs e)
        {
            if (_intLoading > 0)
                return;
            ContactDetailChanged?.Invoke(this, new TextEventArgs("Name"));
        }

        private void txtContactLocation_TextChanged(object sender, EventArgs e)
        {
            if (_intLoading > 0)
                return;
            ContactDetailChanged?.Invoke(this, new TextEventArgs("Location"));
        }

        private async void UpdateMetatype(object sender, EventArgs e)
        {
            if (_intLoading > 0 || _intStatBlockIsLoaded < 1)
                return;
            while (_intStatBlockIsLoaded == 1)
                await Utils.SafeSleepAsync().ConfigureAwait(false);
            if (_intUpdatingMetatype > 0)
                return;
            _tmrMetatypeChangeTimer.Stop();
            string strNew = await cboMetatype.DoThreadSafeFuncAsync(x => x.Text).ConfigureAwait(false);
            string strOld = await _objContact.GetDisplayMetatypeAsync().ConfigureAwait(false);
            if (strOld == strNew)
                return;
            await _objContact.SetDisplayMetatypeAsync(strNew).ConfigureAwait(false);
            strOld = await _objContact.GetDisplayMetatypeAsync().ConfigureAwait(false);
            if (strOld != strNew)
            {
                Interlocked.Increment(ref _intUpdatingMetatype);
                try
                {
                    await cboMetatype.DoThreadSafeAsync(x => x.Text = strOld).ConfigureAwait(false);
                }
                finally
                {
                    Interlocked.Decrement(ref _intUpdatingMetatype);
                }
            }

            ContactDetailChanged?.Invoke(this, new TextEventArgs("Metatype"));
        }

        private async void UpdateGender(object sender, EventArgs e)
        {
            if (_intLoading > 0 || _intStatBlockIsLoaded < 1)
                return;
            while (_intStatBlockIsLoaded == 1)
                await Utils.SafeSleepAsync().ConfigureAwait(false);
            if (_intUpdatingGender > 0)
                return;
            _tmrGenderChangeTimer.Stop();
            string strNew = await cboGender.DoThreadSafeFuncAsync(x => x.Text).ConfigureAwait(false);
            string strOld = await _objContact.GetDisplayGenderAsync().ConfigureAwait(false);
            if (strOld == strNew)
                return;
            await _objContact.SetDisplayGenderAsync(strNew).ConfigureAwait(false);
            strOld = await _objContact.GetDisplayGenderAsync().ConfigureAwait(false);
            if (strOld != strNew)
            {
                Interlocked.Increment(ref _intUpdatingGender);
                try
                {
                    await cboGender.DoThreadSafeAsync(x => x.Text = strOld).ConfigureAwait(false);
                }
                finally
                {
                    Interlocked.Decrement(ref _intUpdatingGender);
                }
            }

            ContactDetailChanged?.Invoke(this, new TextEventArgs("Gender"));
        }

        private async void UpdateAge(object sender, EventArgs e)
        {
            if (_intLoading > 0 || _intStatBlockIsLoaded <= 1)
                return;
            while (_intStatBlockIsLoaded == 1)
                await Utils.SafeSleepAsync().ConfigureAwait(false);
            if (_intUpdatingAge > 0)
                return;
            _tmrAgeChangeTimer.Stop();
            string strNew = await cboAge.DoThreadSafeFuncAsync(x => x.Text).ConfigureAwait(false);
            string strOld = await _objContact.GetDisplayAgeAsync().ConfigureAwait(false);
            if (strOld == strNew)
                return;
            await _objContact.SetDisplayAgeAsync(strNew).ConfigureAwait(false);
            strOld = await _objContact.GetDisplayAgeAsync().ConfigureAwait(false);
            if (strOld != strNew)
            {
                Interlocked.Increment(ref _intUpdatingAge);
                try
                {
                    await cboAge.DoThreadSafeAsync(x => x.Text = strOld).ConfigureAwait(false);
                }
                finally
                {
                    Interlocked.Decrement(ref _intUpdatingAge);
                }
            }

            ContactDetailChanged?.Invoke(this, new TextEventArgs("Age"));
        }

        private async void UpdatePersonalLife(object sender, EventArgs e)
        {
            if (_intLoading > 0 || _intStatBlockIsLoaded < 1)
                return;
            while (_intStatBlockIsLoaded == 1)
                await Utils.SafeSleepAsync().ConfigureAwait(false);
            if (_intUpdatingPersonalLife > 0)
                return;
            _tmrPersonalLifeChangeTimer.Stop();
            string strNew = await cboPersonalLife.DoThreadSafeFuncAsync(x => x.Text).ConfigureAwait(false);
            string strOld = await _objContact.GetDisplayPersonalLifeAsync().ConfigureAwait(false);
            if (strOld == strNew)
                return;
            await _objContact.SetDisplayPersonalLifeAsync(strNew).ConfigureAwait(false);
            strOld = await _objContact.GetDisplayPersonalLifeAsync().ConfigureAwait(false);
            if (strOld != strNew)
            {
                Interlocked.Increment(ref _intUpdatingPersonalLife);
                try
                {
                    await cboPersonalLife.DoThreadSafeAsync(x => x.Text = strOld).ConfigureAwait(false);
                }
                finally
                {
                    Interlocked.Decrement(ref _intUpdatingPersonalLife);
                }
            }

            ContactDetailChanged?.Invoke(this, new TextEventArgs("PersonalLife"));
        }

        private async void UpdateType(object sender, EventArgs e)
        {
            if (_intLoading > 0 || _intStatBlockIsLoaded < 1)
                return;
            while (_intStatBlockIsLoaded == 1)
                await Utils.SafeSleepAsync().ConfigureAwait(false);
            if (_intUpdatingType > 0)
                return;
            _tmrTypeChangeTimer.Stop();
            string strNew = await cboType.DoThreadSafeFuncAsync(x => x.Text).ConfigureAwait(false);
            string strOld = await _objContact.GetDisplayTypeAsync().ConfigureAwait(false);
            if (strOld == strNew)
                return;
            await _objContact.SetDisplayTypeAsync(strNew).ConfigureAwait(false);
            strOld = await _objContact.GetDisplayTypeAsync().ConfigureAwait(false);
            if (strOld != strNew)
            {
                Interlocked.Increment(ref _intUpdatingType);
                try
                {
                    await cboType.DoThreadSafeAsync(x => x.Text = strOld).ConfigureAwait(false);
                }
                finally
                {
                    Interlocked.Decrement(ref _intUpdatingType);
                }
            }

            ContactDetailChanged?.Invoke(this, new TextEventArgs("Type"));
        }

        private async void UpdatePreferredPayment(object sender, EventArgs e)
        {
            if (_intLoading > 0 || _intStatBlockIsLoaded < 1)
                return;
            while (_intStatBlockIsLoaded == 1)
                await Utils.SafeSleepAsync().ConfigureAwait(false);
            if (_intUpdatingPreferredPayment > 0)
                return;
            _tmrPreferredPaymentChangeTimer.Stop();
            string strNew = await cboPreferredPayment.DoThreadSafeFuncAsync(x => x.Text).ConfigureAwait(false);
            string strOld = await _objContact.GetDisplayPreferredPaymentAsync().ConfigureAwait(false);
            if (strOld == strNew)
                return;
            await _objContact.SetDisplayPreferredPaymentAsync(strNew).ConfigureAwait(false);
            strOld = await _objContact.GetDisplayPreferredPaymentAsync().ConfigureAwait(false);
            if (strOld != strNew)
            {
                Interlocked.Increment(ref _intUpdatingPreferredPayment);
                try
                {
                    await cboPreferredPayment.DoThreadSafeAsync(x => x.Text = strOld).ConfigureAwait(false);
                }
                finally
                {
                    Interlocked.Decrement(ref _intUpdatingPreferredPayment);
                }
            }

            ContactDetailChanged?.Invoke(this, new TextEventArgs("PreferredPayment"));
        }

        private async void UpdateHobbiesVice(object sender, EventArgs e)
        {
            if (_intLoading > 0 || _intStatBlockIsLoaded < 1)
                return;
            while (_intStatBlockIsLoaded == 1)
                await Utils.SafeSleepAsync().ConfigureAwait(false);
            if (_intUpdatingHobbiesVice > 0)
                return;
            _tmrHobbiesViceChangeTimer.Stop();
            string strNew = await cboHobbiesVice.DoThreadSafeFuncAsync(x => x.Text).ConfigureAwait(false);
            string strOld = await _objContact.GetDisplayHobbiesViceAsync().ConfigureAwait(false);
            if (strOld == strNew)
                return;
            await _objContact.SetDisplayHobbiesViceAsync(strNew).ConfigureAwait(false);
            strOld = await _objContact.GetDisplayHobbiesViceAsync().ConfigureAwait(false);
            if (strOld != strNew)
            {
                Interlocked.Increment(ref _intUpdatingHobbiesVice);
                try
                {
                    await cboHobbiesVice.DoThreadSafeAsync(x => x.Text = strOld).ConfigureAwait(false);
                }
                finally
                {
                    Interlocked.Decrement(ref _intUpdatingHobbiesVice);
                }
            }

            ContactDetailChanged?.Invoke(this, new TextEventArgs("HobbiesVice"));
        }

        private async void UpdateContactRole(object sender, EventArgs e)
        {
            if (_intLoading > 0 || _intUpdatingRole > 0)
                return;
            _tmrRoleChangeTimer.Stop();
            string strNew = await cboContactRole.DoThreadSafeFuncAsync(x => x.Text).ConfigureAwait(false);
            string strOld = await _objContact.GetDisplayRoleAsync().ConfigureAwait(false);
            if (strOld == strNew)
                return;
            await _objContact.SetDisplayRoleAsync(strNew).ConfigureAwait(false);
            strOld = await _objContact.GetDisplayRoleAsync().ConfigureAwait(false);
            if (strOld != strNew)
            {
                Interlocked.Increment(ref _intUpdatingRole);
                try
                {
                    await cboContactRole.DoThreadSafeAsync(x => x.Text = strOld).ConfigureAwait(false);
                }
                finally
                {
                    Interlocked.Decrement(ref _intUpdatingRole);
                }
            }

            ContactDetailChanged?.Invoke(this, new TextEventArgs("Role"));
        }

        private void cmdLink_Click(object sender, EventArgs e)
        {
            // Determine which options should be shown based on the FileName value.
            if (!string.IsNullOrEmpty(_objContact.FileName))
            {
                tsAttachCharacter.Visible = false;
                tsContactOpen.Visible = true;
                tsRemoveCharacter.Visible = true;
            }
            else
            {
                tsAttachCharacter.Visible = true;
                tsContactOpen.Visible = false;
                tsRemoveCharacter.Visible = false;
            }

            cmsContact.Show(cmdLink, cmdLink.Left - cmsContact.PreferredSize.Width, cmdLink.Top);
        }

        private async void tsContactOpen_Click(object sender, EventArgs e)
        {
            if (_objContact.LinkedCharacter != null)
            {
                Character objOpenCharacter = await Program.OpenCharacters.ContainsAsync(_objContact.LinkedCharacter).ConfigureAwait(false)
                    ? _objContact.LinkedCharacter
                    : null;
                CursorWait objCursorWait = await CursorWait.NewAsync(ParentForm).ConfigureAwait(false);
                try
                {
                    if (objOpenCharacter == null)
                    {
                        using (ThreadSafeForm<LoadingBar> frmLoadingBar
                               = await Program.CreateAndShowProgressBarAsync(
                                   _objContact.LinkedCharacter.FileName, Character.NumLoadingSections).ConfigureAwait(false))
                            objOpenCharacter = await Program.LoadCharacterAsync(
                                _objContact.LinkedCharacter.FileName, frmLoadingBar: frmLoadingBar.MyForm).ConfigureAwait(false);
                    }

                    if (!await Program.SwitchToOpenCharacter(objOpenCharacter).ConfigureAwait(false))
                        await Program.OpenCharacter(objOpenCharacter).ConfigureAwait(false);
                }
                finally
                {
                    await objCursorWait.DisposeAsync().ConfigureAwait(false);
                }
            }
            else
            {
                bool blnUseRelative = false;

                // Make sure the file still exists before attempting to load it.
                if (!File.Exists(_objContact.FileName))
                {
                    bool blnError = false;
                    // If the file doesn't exist, use the relative path if one is available.
                    if (string.IsNullOrEmpty(_objContact.RelativeFileName))
                        blnError = true;
                    else if (!File.Exists(Path.GetFullPath(_objContact.RelativeFileName)))
                        blnError = true;
                    else
                        blnUseRelative = true;

                    if (blnError)
                    {
                        Program.ShowScrollableMessageBox(
                            string.Format(GlobalSettings.CultureInfo,
                                          await LanguageManager.GetStringAsync("Message_FileNotFound").ConfigureAwait(false),
                                          _objContact.FileName),
                            await LanguageManager.GetStringAsync("MessageTitle_FileNotFound").ConfigureAwait(false), MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                        return;
                    }
                }

                string strFile = blnUseRelative ? Path.GetFullPath(_objContact.RelativeFileName) : _objContact.FileName;
                Process.Start(strFile);
            }
        }

        private async void tsAttachCharacter_Click(object sender, EventArgs e)
        {
            string strFilter = await LanguageManager.GetStringAsync("DialogFilter_Chummer").ConfigureAwait(false) + '|'
                +
                await LanguageManager.GetStringAsync("DialogFilter_Chum5").ConfigureAwait(false) + '|' +
                await LanguageManager.GetStringAsync("DialogFilter_Chum5lz").ConfigureAwait(false) + '|' +
                await LanguageManager.GetStringAsync("DialogFilter_All").ConfigureAwait(false);
            string strFileName = string.Empty;
            DialogResult eResult = await this.DoThreadSafeFuncAsync(x =>
            {
                // Prompt the user to select a save file to associate with this Contact.
                using (OpenFileDialog dlgOpenFile = new OpenFileDialog())
                {
                    dlgOpenFile.Filter = strFilter;
                    if (!string.IsNullOrEmpty(_objContact.FileName) && File.Exists(_objContact.FileName))
                    {
                        dlgOpenFile.InitialDirectory = Path.GetDirectoryName(_objContact.FileName);
                        dlgOpenFile.FileName = Path.GetFileName(_objContact.FileName);
                    }

                    DialogResult eReturn = dlgOpenFile.ShowDialog(x);
                    strFileName = dlgOpenFile.FileName;
                    return eReturn;
                }
            }).ConfigureAwait(false);
            if (eResult != DialogResult.OK)
                return;
            _objContact.FileName = strFileName;
            if (cmdLink != null)
            {
                string strText = _objContact.IsEnemy
                    ? await LanguageManager.GetStringAsync("Tip_Enemy_OpenFile").ConfigureAwait(false)
                    : await LanguageManager.GetStringAsync("Tip_Contact_OpenFile").ConfigureAwait(false);
                await cmdLink.SetToolTipTextAsync(strText).ConfigureAwait(false);
            }

            // Set the relative path.
            Uri uriApplication = new Uri(Utils.GetStartupPath);
            Uri uriFile = new Uri(_objContact.FileName);
            Uri uriRelative = uriApplication.MakeRelativeUri(uriFile);
            _objContact.RelativeFileName = "../" + uriRelative;

            ContactDetailChanged?.Invoke(this, new TextEventArgs("File"));
        }

        private async void tsRemoveCharacter_Click(object sender, EventArgs e)
        {
            // Remove the file association from the Contact.
            if (Program.ShowScrollableMessageBox(await LanguageManager.GetStringAsync("Message_RemoveCharacterAssociation").ConfigureAwait(false),
                                                 await LanguageManager.GetStringAsync("MessageTitle_RemoveCharacterAssociation").ConfigureAwait(false),
                                                 MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                _objContact.FileName = string.Empty;
                _objContact.RelativeFileName = string.Empty;
                if (cmdLink != null)
                {
                    string strText = _objContact.IsEnemy
                        ? await LanguageManager.GetStringAsync("Tip_Enemy_LinkFile").ConfigureAwait(false)
                        : await LanguageManager.GetStringAsync("Tip_Contact_LinkFile").ConfigureAwait(false);
                    await cmdLink.SetToolTipTextAsync(strText).ConfigureAwait(false);
                }

                ContactDetailChanged?.Invoke(this, new TextEventArgs("File"));
            }
        }

        private async void cmdNotes_Click(object sender, EventArgs e)
        {
            using (ThreadSafeForm<EditNotes> frmContactNotes
                   = await ThreadSafeForm<EditNotes>.GetAsync(
                       () => new EditNotes(_objContact.Notes, _objContact.NotesColor)).ConfigureAwait(false))
            {
                if (await frmContactNotes.ShowDialogSafeAsync(this).ConfigureAwait(false) != DialogResult.OK)
                    return;
                _objContact.Notes = frmContactNotes.MyForm.Notes;
            }

            string strTooltip
                = await LanguageManager.GetStringAsync(_objContact.IsEnemy
                                                           ? "Tip_Enemy_EditNotes"
                                                           : "Tip_Contact_EditNotes").ConfigureAwait(false);
            if (!string.IsNullOrEmpty(_objContact.Notes))
                strTooltip += Environment.NewLine + Environment.NewLine + _objContact.Notes;
            await cmdNotes.SetToolTipTextAsync(strTooltip.WordWrap()).ConfigureAwait(false);
            ContactDetailChanged?.Invoke(this, new TextEventArgs("Notes"));
        }

        private async void chkFree_CheckedChanged(object sender, EventArgs e)
        {
            if (_intLoading > 0 || _intStatBlockIsLoaded < 1)
                return;
            while (_intStatBlockIsLoaded == 1)
                await Utils.SafeSleepAsync().ConfigureAwait(false);
            ContactDetailChanged?.Invoke(this, new TextEventArgs("Free"));
        }

        private async void chkBlackmail_CheckedChanged(object sender, EventArgs e)
        {
            if (_intLoading > 0 || _intStatBlockIsLoaded < 1)
                return;
            while (_intStatBlockIsLoaded == 1)
                await Utils.SafeSleepAsync().ConfigureAwait(false);
            ContactDetailChanged?.Invoke(this, new TextEventArgs("Blackmail"));
        }

        private async void chkFamily_CheckedChanged(object sender, EventArgs e)
        {
            if (_intLoading > 0 || _intStatBlockIsLoaded < 1)
                return;
            while (_intStatBlockIsLoaded == 1)
                await Utils.SafeSleepAsync().ConfigureAwait(false);
            ContactDetailChanged?.Invoke(this, new TextEventArgs("Family"));
        }

        #endregion Control Events

        #region Properties

        /// <summary>
        /// Contact object this is linked to.
        /// </summary>
        public Contact ContactObject => _objContact;

        public bool Expanded => tlpStatBlock?.DoThreadSafeFunc(x => x.Visible) == true;

        public async ValueTask<bool> GetExpandedAsync(CancellationToken token = default)
        {
            if (tlpStatBlock != null)
                return await tlpStatBlock.DoThreadSafeFuncAsync(x => x.Visible, token).ConfigureAwait(false);
            return false;
        }

        public async ValueTask SetExpandedAsync(bool value, CancellationToken token = default)
        {
            await cmdExpand.DoThreadSafeAsync(x =>
            {
                x.ImageDpi96 = value ? Resources.toggle : Resources.toggle_expand;
                x.ImageDpi192 = value ? Resources.toggle1 : Resources.toggle_expand1;
            }, token).ConfigureAwait(false);
            if (value)
            {
                int intOld = Interlocked.CompareExchange(ref _intStatBlockIsLoaded, 1, 0);
                try
                {
                    while (tlpStatBlock == null || intOld < 2)
                    {
                        while (intOld == 1)
                        {
                            await Utils.SafeSleepAsync(token).ConfigureAwait(false);
                            intOld = Interlocked.CompareExchange(ref _intStatBlockIsLoaded, 1, 0);
                        }

                        if (tlpStatBlock == null || intOld < 2)
                        {
                            // Create second row and statblock only on the first expansion to save on handles and load times
                            await CreateSecondRowAsync(token).ConfigureAwait(false);
                            await CreateStatBlockAsync(token).ConfigureAwait(false);
                            intOld = Interlocked.CompareExchange(ref _intStatBlockIsLoaded, 2, 1);
                        }
                    }
                }
                catch
                {
                    Interlocked.CompareExchange(ref _intStatBlockIsLoaded, intOld, 1);
                }
            }

            CursorWait objCursorWait = await CursorWait.NewAsync(this, token: token).ConfigureAwait(false);
            try
            {
                await this.DoThreadSafeAsync(x => x.SuspendLayout(), token).ConfigureAwait(false);
                try
                {
                    await lblConnection.DoThreadSafeAsync(x => x.Visible = value, token).ConfigureAwait(false);
                    await lblLoyalty.DoThreadSafeAsync(x => x.Visible = value, token).ConfigureAwait(false);
                    await nudConnection.DoThreadSafeAsync(x => x.Visible = value, token).ConfigureAwait(false);
                    await nudLoyalty.DoThreadSafeAsync(x => x.Visible = value, token).ConfigureAwait(false);
                    await chkGroup.DoThreadSafeAsync(x => x.Visible = value, token).ConfigureAwait(false);
                    //We don't actually pay for contacts in play so everyone is free
                    //Don't present a useless field
                    if (value && _objContact != null)
                    {
                        bool blnCreated = await _objContact.CharacterObject.GetCreatedAsync(token).ConfigureAwait(false);
                        await chkFree.DoThreadSafeAsync(x => x.Visible = !blnCreated, token: token).ConfigureAwait(false);
                    }
                    else
                        await chkFree.DoThreadSafeAsync(x => x.Visible = false, token).ConfigureAwait(false);
                    await chkBlackmail.DoThreadSafeAsync(x => x.Visible = value, token).ConfigureAwait(false);
                    await chkFamily.DoThreadSafeAsync(x => x.Visible = value, token).ConfigureAwait(false);
                    await cmdLink.DoThreadSafeAsync(x => x.Visible = value, token).ConfigureAwait(false);
                    await tlpStatBlock.DoThreadSafeAsync(x => x.Visible = value, token).ConfigureAwait(false);
                }
                finally
                {
                    await this.DoThreadSafeAsync(x => x.ResumeLayout(), token).ConfigureAwait(false);
                }
            }
            finally
            {
                await objCursorWait.DisposeAsync().ConfigureAwait(false);
            }
        }

        #endregion Properties

        #region Methods

        private async ValueTask LoadContactList(CancellationToken token = default)
        {
            if (_objContact.IsEnemy)
            {
                string strContactRole = _objContact.DisplayRole;
                if (!string.IsNullOrEmpty(strContactRole))
                    await cboContactRole.DoThreadSafeAsync(x => x.Text = strContactRole, token: token)
                                        .ConfigureAwait(false);
                return;
            }

            //the values are now loaded direct in the (new) property lstContactArchetypes (see above).
            //I only left this in here for better understanding what happend before (and because of bug #3566)
            //using (XmlNodeList xmlNodeList = xmlContactsBaseNode.SelectNodes("contacts/contact"))
            //    if (xmlNodeList != null)
            //        foreach (XmlNode xmlNode in xmlNodeList)
            //        {
            //            string strName = xmlNode.InnerText;
            //            ContactProfession.Add(new ListItem(strName, xmlNode.Attributes?["translate"]?.InnerText ?? strName));
            //        }

            await cboContactRole
                  .PopulateWithListItemsAsync(
                      await _objContact.CharacterObject.ContactArchetypesAsync(token: token).ConfigureAwait(false),
                      token: token).ConfigureAwait(false);
            await cboContactRole.DoThreadSafeAsync(x =>
            {
                x.SelectedValue = _objContact.Role;
                if (x.SelectedIndex < 0)
                    x.Text = _objContact.DisplayRole;
            }, token: token).ConfigureAwait(false);
        }

        private async ValueTask DoDataBindings(CancellationToken token = default)
        {
            await lblQuickStats.RegisterOneWayAsyncDataBindingAsync((x, y) => x.Text = y, _objContact,
                                                               nameof(Contact.QuickText),
                                                               // ReSharper disable once MethodSupportsCancellation
                                                               x => x.GetQuickTextAsync().AsTask(), token: token).ConfigureAwait(false);
            await txtContactName.DoDataBindingAsync("Text", _objContact, nameof(_objContact.Name), token).ConfigureAwait(false);
            await txtContactLocation.DoDataBindingAsync("Text", _objContact, nameof(_objContact.Location), token).ConfigureAwait(false);
            await cmdDelete.DoOneWayNegatableDataBindingAsync("Visible", _objContact, nameof(_objContact.ReadOnly), token).ConfigureAwait(false);
            await this.DoOneWayDataBindingAsync("BackColor", _objContact, nameof(_objContact.PreferredColor), token).ConfigureAwait(false);

            // Properties controllable by the character themselves
            await txtContactName.DoOneWayDataBindingAsync("Enabled", _objContact, nameof(_objContact.NoLinkedCharacter), token).ConfigureAwait(false);
        }

        private Label lblConnection;
        private Label lblLoyalty;
        private NumericUpDownEx nudConnection;
        private NumericUpDownEx nudLoyalty;
        private ColorableCheckBox chkGroup;
        private ColorableCheckBox chkFree;
        private ColorableCheckBox chkBlackmail;
        private ColorableCheckBox chkFamily;
        private ButtonWithToolTip cmdLink;

        private async ValueTask CreateSecondRowAsync(CancellationToken token = default)
        {
            CursorWait objCursorWait = await CursorWait.NewAsync(this, token: token).ConfigureAwait(false);
            try
            {
                await this.DoThreadSafeAsync(x =>
                {
                    x.lblConnection = new Label
                    {
                        Anchor = AnchorStyles.Right,
                        AutoSize = true,
                        Name = "lblConnection",
                        Tag = "Label_Contact_Connection",
                        Text = "Connection:",
                        TextAlign = ContentAlignment.MiddleRight
                    };
                    x.nudConnection = new NumericUpDownEx
                    {
                        Anchor = AnchorStyles.Left | AnchorStyles.Right,
                        AutoSize = true,
                        Maximum = new decimal(new[] {12, 0, 0, 0}),
                        Minimum = new decimal(new[] {1, 0, 0, 0}),
                        Name = "nudConnection",
                        Value = new decimal(new[] {1, 0, 0, 0})
                    };
                    x.lblLoyalty = new Label
                    {
                        Anchor = AnchorStyles.Right,
                        AutoSize = true,
                        Name = "lblLoyalty",
                        Tag = "Label_Contact_Loyalty",
                        Text = "Loyalty:",
                        TextAlign = ContentAlignment.MiddleRight
                    };
                    x.nudLoyalty = new NumericUpDownEx
                    {
                        Anchor = AnchorStyles.Left | AnchorStyles.Right,
                        AutoSize = true,
                        Maximum = new decimal(new[] {6, 0, 0, 0}),
                        Minimum = new decimal(new[] {1, 0, 0, 0}),
                        Name = "nudLoyalty",
                        Value = new decimal(new[] {1, 0, 0, 0})
                    };
                    x.chkFree = new ColorableCheckBox
                    {
                        Anchor = AnchorStyles.Left,
                        AutoSize = true,
                        DefaultColorScheme = true,
                        Name = "chkFree",
                        Tag = "Checkbox_Contact_Free",
                        Text = "Free",
                        UseVisualStyleBackColor = true
                    };
                    x.chkGroup = new ColorableCheckBox
                    {
                        Anchor = AnchorStyles.Left,
                        AutoSize = true,
                        DefaultColorScheme = true,
                        Name = "chkGroup",
                        Tag = "Checkbox_Contact_Group",
                        Text = "Group",
                        UseVisualStyleBackColor = true
                    };
                    x.chkBlackmail = new ColorableCheckBox
                    {
                        Anchor = AnchorStyles.Left,
                        AutoSize = true,
                        DefaultColorScheme = true,
                        Name = "chkBlackmail",
                        Tag = "Checkbox_Contact_Blackmail",
                        Text = "Blackmail",
                        UseVisualStyleBackColor = true
                    };
                    x.chkFamily = new ColorableCheckBox
                    {
                        Anchor = AnchorStyles.Left,
                        AutoSize = true,
                        DefaultColorScheme = true,
                        Name = "chkFamily",
                        Tag = "Checkbox_Contact_Family",
                        Text = "Family",
                        UseVisualStyleBackColor = true
                    };
                    x.cmdLink = new ButtonWithToolTip
                    {
                        Anchor = AnchorStyles.Right,
                        AutoSize = true,
                        AutoSizeMode = AutoSizeMode.GrowAndShrink,
                        FlatAppearance = {BorderSize = 0},
                        FlatStyle = FlatStyle.Flat,
                        Padding = new Padding(1),
                        MinimumSize = new Size(24, 24),
                        ImageDpi96 = Resources.link,
                        ImageDpi192 = Resources.link1,
                        Name = "cmdLink",
                        UseVisualStyleBackColor = true,
                        TabStop = false
                    };
                    x.nudConnection.ValueChanged += nudConnection_ValueChanged;
                    x.nudLoyalty.ValueChanged += nudLoyalty_ValueChanged;
                    x.chkFree.CheckedChanged += chkFree_CheckedChanged;
                    x.chkGroup.CheckedChanged += chkGroup_CheckedChanged;
                    x.chkBlackmail.CheckedChanged += chkBlackmail_CheckedChanged;
                    x.chkFamily.CheckedChanged += chkFamily_CheckedChanged;
                    x.cmdLink.Click += cmdLink_Click;
                }, token).ConfigureAwait(false);
                if (_objContact != null)
                {
                    //We don't actually pay for contacts in play so everyone is free
                    //Don't present a useless field
                    if (_objContact.CharacterObject != null)
                        await chkFree.DoThreadSafeAsync(x => x.Visible = !_objContact.CharacterObject.Created, token)
                                     .ConfigureAwait(false);
                    else
                        await chkFree.DoThreadSafeAsync(x => x.Visible = false, token).ConfigureAwait(false);
                    await chkGroup.DoDataBindingAsync("Checked", _objContact, nameof(Contact.IsGroup), token)
                                  .ConfigureAwait(false);
                    await chkFree.DoDataBindingAsync("Checked", _objContact, nameof(Contact.Free), token)
                                 .ConfigureAwait(false);
                    await chkFamily.DoDataBindingAsync("Checked", _objContact, nameof(Contact.Family), token)
                                   .ConfigureAwait(false);
                    await chkFamily
                          .DoOneWayNegatableDataBindingAsync("Visible", _objContact, nameof(Contact.IsEnemy), token)
                          .ConfigureAwait(false);
                    await chkBlackmail.DoDataBindingAsync("Checked", _objContact, nameof(Contact.Blackmail), token)
                                      .ConfigureAwait(false);
                    await chkBlackmail
                          .DoOneWayNegatableDataBindingAsync("Visible", _objContact, nameof(Contact.IsEnemy), token)
                          .ConfigureAwait(false);
                    await nudLoyalty.DoDataBindingAsync("Value", _objContact, nameof(Contact.Loyalty), token)
                                    .ConfigureAwait(false);
                    await nudConnection.DoDataBindingAsync("Value", _objContact, nameof(Contact.Connection), token)
                                       .ConfigureAwait(false);
                    await nudConnection
                          .DoOneWayNegatableDataBindingAsync("Enabled", _objContact, nameof(Contact.ReadOnly), token)
                          .ConfigureAwait(false);
                    await chkGroup.RegisterOneWayAsyncDataBindingAsync((x, y) => x.Enabled = y, _objContact,
                                                                       nameof(Contact.GroupEnabled),
                                                                       // ReSharper disable once MethodSupportsCancellation
                                                                       x => x.GetGroupEnabledAsync().AsTask(),
                                                                       token: token).ConfigureAwait(false);
                    await chkFree.RegisterOneWayAsyncDataBindingAsync((x, y) => x.Enabled = y, _objContact,
                                                                      nameof(Contact.FreeEnabled),
                                                                      // ReSharper disable once MethodSupportsCancellation
                                                                      x => x.GetFreeEnabledAsync().AsTask(),
                                                                      token: token).ConfigureAwait(false);
                    await nudLoyalty.RegisterOneWayAsyncDataBindingAsync((x, y) => x.Enabled = y, _objContact,
                                                                         nameof(Contact.LoyaltyEnabled),
                                                                         // ReSharper disable once MethodSupportsCancellation
                                                                         x => x.GetLoyaltyEnabledAsync().AsTask(),
                                                                         token: token).ConfigureAwait(false);
                    await nudConnection.RegisterOneWayAsyncDataBindingAsync((x, y) => x.Maximum = y, _objContact,
                                                                            nameof(Contact.ConnectionMaximum),
                                                                            // ReSharper disable once MethodSupportsCancellation
                                                                            x => x.GetConnectionMaximumAsync().AsTask(),
                                                                            token: token).ConfigureAwait(false);
                    string strToolTipText;
                    if (_objContact.IsEnemy)
                    {
                        strToolTipText = !string.IsNullOrEmpty(_objContact.FileName)
                            ? await LanguageManager.GetStringAsync("Tip_Enemy_OpenLinkedEnemy", token: token)
                                                   .ConfigureAwait(false)
                            : await LanguageManager.GetStringAsync("Tip_Enemy_LinkEnemy", token: token)
                                                   .ConfigureAwait(false);
                    }
                    else
                    {
                        strToolTipText = !string.IsNullOrEmpty(_objContact.FileName)
                            ? await LanguageManager.GetStringAsync("Tip_Contact_OpenLinkedContact", token: token)
                                                   .ConfigureAwait(false)
                            : await LanguageManager.GetStringAsync("Tip_Contact_LinkContact", token: token)
                                                   .ConfigureAwait(false);
                    }

                    await cmdLink.DoThreadSafeAsync(x => x.ToolTipText = strToolTipText, token).ConfigureAwait(false);
                }

                await this.DoThreadSafeAsync(x =>
                {
                    x.tlpMain.SetColumnSpan(x.lblConnection, 2);
                    x.tlpMain.SetColumnSpan(x.chkFamily, 3);
                    x.SuspendLayout();
                    try
                    {
                        x.tlpMain.SuspendLayout();
                        try
                        {
                            x.tlpMain.Controls.Add(x.lblConnection, 0, 2);
                            x.tlpMain.Controls.Add(x.nudConnection, 2, 2);
                            x.tlpMain.Controls.Add(x.lblLoyalty, 3, 2);
                            x.tlpMain.Controls.Add(x.nudLoyalty, 4, 2);
                            x.tlpMain.Controls.Add(x.chkFree, 6, 2);
                            x.tlpMain.Controls.Add(x.chkGroup, 7, 2);
                            x.tlpMain.Controls.Add(x.chkBlackmail, 8, 2);
                            x.tlpMain.Controls.Add(x.chkFamily, 9, 2);
                            x.tlpMain.Controls.Add(x.cmdLink, 12, 2);
                        }
                        finally
                        {
                            x.tlpMain.ResumeLayout();
                        }
                    }
                    finally
                    {
                        x.ResumeLayout(true);
                    }
                }, token).ConfigureAwait(false);
            }
            finally
            {
                await objCursorWait.DisposeAsync().ConfigureAwait(false);
            }
        }

        private TableLayoutPanel tlpStatBlock;
        private Label lblHobbiesVice;
        private Label lblPreferredPayment;
        private Label lblPersonalLife;
        private Label lblType;
        private Label lblMetatype;
        private Label lblGender;
        private Label lblAge;
        private ElasticComboBox cboMetatype;
        private ElasticComboBox cboGender;
        private ElasticComboBox cboType;
        private ElasticComboBox cboAge;
        private ElasticComboBox cboPersonalLife;
        private ElasticComboBox cboPreferredPayment;
        private ElasticComboBox cboHobbiesVice;

        /// <summary>
        /// Method to dynamically create stat block is separated out so that we only create it if the control is expanded
        /// </summary>
        private void CreateStatBlock()
        {
            using (CursorWait.New(this))
            {
                this.DoThreadSafe(x =>
                {
                    x.cboMetatype = new ElasticComboBox
                    {
                        Anchor = AnchorStyles.Left | AnchorStyles.Right,
                        FormattingEnabled = true,
                        Name = "cboMetatype"
                    };
                    x.cboGender = new ElasticComboBox
                    { Anchor = AnchorStyles.Left | AnchorStyles.Right, FormattingEnabled = true, Name = "cboGender" };
                    x.cboAge = new ElasticComboBox
                    { Anchor = AnchorStyles.Left | AnchorStyles.Right, FormattingEnabled = true, Name = "cboAge" };
                    x.cboType = new ElasticComboBox
                    { Anchor = AnchorStyles.Left | AnchorStyles.Right, FormattingEnabled = true, Name = "cboType" };
                    x.cboPersonalLife = new ElasticComboBox
                    {
                        Anchor = AnchorStyles.Left | AnchorStyles.Right,
                        FormattingEnabled = true,
                        Name = "cboPersonalLife"
                    };
                    x.cboPreferredPayment = new ElasticComboBox
                    {
                        Anchor = AnchorStyles.Left | AnchorStyles.Right,
                        FormattingEnabled = true,
                        Name = "cboPreferredPayment"
                    };
                    x.cboHobbiesVice = new ElasticComboBox
                    {
                        Anchor = AnchorStyles.Left | AnchorStyles.Right,
                        FormattingEnabled = true,
                        Name = "cboHobbiesVice"
                    };
                });

                LoadStatBlockLists();

                this.DoThreadSafe(x =>
                {
                    if (x._objContact != null)
                    {
                        // Properties controllable by the character themselves
                        x.cboMetatype.DoOneWayDataBinding("Enabled", x._objContact, nameof(Contact.NoLinkedCharacter));
                        x.cboGender.DoOneWayDataBinding("Enabled", x._objContact, nameof(Contact.NoLinkedCharacter));
                        x.cboAge.DoOneWayDataBinding("Enabled", x._objContact, nameof(Contact.NoLinkedCharacter));
                    }

                    x.lblType = new Label
                    {
                        Anchor = AnchorStyles.Right,
                        AutoSize = true,
                        Margin = new Padding(3, 6, 3, 6),
                        Name = "lblType",
                        Tag = "Label_Type",
                        Text = "Type:",
                        TextAlign = ContentAlignment.MiddleRight
                    };
                    x.lblMetatype = new Label
                    {
                        Anchor = AnchorStyles.Right,
                        AutoSize = true,
                        Margin = new Padding(3, 6, 3, 6),
                        Name = "lblMetatype",
                        Tag = "Label_Metatype",
                        Text = "Metatype:",
                        TextAlign = ContentAlignment.MiddleRight
                    };
                    x.lblGender = new Label
                    {
                        Anchor = AnchorStyles.Right,
                        AutoSize = true,
                        Margin = new Padding(3, 6, 3, 6),
                        Name = "lblGender",
                        Tag = "Label_Gender",
                        Text = "Gender:",
                        TextAlign = ContentAlignment.MiddleRight
                    };
                    x.lblAge = new Label
                    {
                        Anchor = AnchorStyles.Right,
                        AutoSize = true,
                        Margin = new Padding(3, 6, 3, 6),
                        Name = "lblAge",
                        Tag = "Label_Age",
                        Text = "Age:",
                        TextAlign = ContentAlignment.MiddleRight
                    };
                    x.lblPersonalLife = new Label
                    {
                        Anchor = AnchorStyles.Right,
                        AutoSize = true,
                        Margin = new Padding(3, 6, 3, 6),
                        Name = "lblPersonalLife",
                        Tag = "Label_Contact_PersonalLife",
                        Text = "Personal Life:",
                        TextAlign = ContentAlignment.MiddleRight
                    };
                    x.lblPreferredPayment = new Label
                    {
                        Anchor = AnchorStyles.Right,
                        AutoSize = true,
                        Margin = new Padding(3, 6, 3, 6),
                        Name = "lblPreferredPayment",
                        Tag = "Label_Contact_PreferredPayment",
                        Text = "Preferred Payment:",
                        TextAlign = ContentAlignment.MiddleRight
                    };
                    x.lblHobbiesVice = new Label
                    {
                        Anchor = AnchorStyles.Right,
                        AutoSize = true,
                        Margin = new Padding(3, 6, 3, 6),
                        Name = "lblHobbiesVice",
                        Tag = "Label_Contact_HobbiesVice",
                        Text = "Hobbies/Vice:",
                        TextAlign = ContentAlignment.MiddleRight
                    };

                    x.tlpStatBlock = new TableLayoutPanel
                    {
                        AutoSize = true,
                        AutoSizeMode = AutoSizeMode.GrowAndShrink,
                        ColumnCount = 4,
                        RowCount = 5,
                        Dock = DockStyle.Fill,
                        Name = "tlpStatBlock"
                    };
                    x.tlpStatBlock.ColumnStyles.Add(new ColumnStyle());
                    x.tlpStatBlock.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
                    x.tlpStatBlock.ColumnStyles.Add(new ColumnStyle());
                    x.tlpStatBlock.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
                    x.tlpStatBlock.RowStyles.Add(new RowStyle());
                    x.tlpStatBlock.RowStyles.Add(new RowStyle());
                    x.tlpStatBlock.RowStyles.Add(new RowStyle());
                    x.tlpStatBlock.RowStyles.Add(new RowStyle());
                    x.tlpStatBlock.Controls.Add(x.lblMetatype, 0, 0);
                    x.tlpStatBlock.Controls.Add(x.cboMetatype, 1, 0);
                    x.tlpStatBlock.Controls.Add(x.lblGender, 0, 1);
                    x.tlpStatBlock.Controls.Add(x.cboGender, 1, 1);
                    x.tlpStatBlock.Controls.Add(x.lblAge, 0, 2);
                    x.tlpStatBlock.Controls.Add(x.cboAge, 1, 2);
                    x.tlpStatBlock.Controls.Add(x.lblType, 0, 3);
                    x.tlpStatBlock.Controls.Add(x.cboType, 1, 3);
                    x.tlpStatBlock.Controls.Add(x.lblPersonalLife, 2, 0);
                    x.tlpStatBlock.Controls.Add(x.cboPersonalLife, 3, 0);
                    x.tlpStatBlock.Controls.Add(x.lblPreferredPayment, 2, 1);
                    x.tlpStatBlock.Controls.Add(x.cboPreferredPayment, 3, 1);
                    x.tlpStatBlock.Controls.Add(x.lblHobbiesVice, 2, 2);
                    x.tlpStatBlock.Controls.Add(x.cboHobbiesVice, 3, 2);

                    x.tlpStatBlock.TranslateWinForm();
                    x.tlpStatBlock.UpdateLightDarkMode();

                    x.SuspendLayout();
                    try
                    {
                        x.tlpMain.SuspendLayout();
                        try
                        {
                            x.tlpMain.SetColumnSpan(x.tlpStatBlock, 13);
                            x.tlpMain.Controls.Add(x.tlpStatBlock, 0, 3);
                        }
                        finally
                        {
                            x.tlpMain.ResumeLayout();
                        }
                    }
                    finally
                    {
                        x.ResumeLayout();
                    }

                    // Need these as separate instead of as simple data bindings so that we don't get annoying live partial translations

                    if (x._objContact != null)
                    {
                        x.cboMetatype.SelectedValue = x._objContact.Metatype;
                        x.cboGender.SelectedValue = x._objContact.Gender;
                        x.cboAge.SelectedValue = x._objContact.Age;
                        x.cboPersonalLife.SelectedValue = x._objContact.PersonalLife;
                        x.cboType.SelectedValue = x._objContact.Type;
                        x.cboPreferredPayment.SelectedValue = x._objContact.PreferredPayment;
                        x.cboHobbiesVice.SelectedValue = x._objContact.HobbiesVice;
                        if (x.cboMetatype.SelectedIndex < 0)
                            x.cboMetatype.Text = x._objContact.DisplayMetatype;
                        if (x.cboGender.SelectedIndex < 0)
                            x.cboGender.Text = x._objContact.DisplayGender;
                        if (x.cboAge.SelectedIndex < 0)
                            x.cboAge.Text = x._objContact.DisplayAge;
                        if (x.cboPersonalLife.SelectedIndex < 0)
                            x.cboPersonalLife.Text = x._objContact.DisplayPersonalLife;
                        if (x.cboType.SelectedIndex < 0)
                            x.cboType.Text = x._objContact.DisplayType;
                        if (x.cboPreferredPayment.SelectedIndex < 0)
                            x.cboPreferredPayment.Text = x._objContact.DisplayPreferredPayment;
                        if (x.cboHobbiesVice.SelectedIndex < 0)
                            x.cboHobbiesVice.Text = x._objContact.DisplayHobbiesVice;
                    }

                    x.cboMetatype.TextChanged += MetatypeOnTextChanged;
                    x.cboGender.TextChanged += GenderOnTextChanged;
                    x.cboAge.TextChanged += AgeOnTextChanged;
                    x.cboType.TextChanged += TypeOnTextChanged;
                    x.cboPersonalLife.TextChanged += PersonalLifeOnTextChanged;
                    x.cboPreferredPayment.TextChanged += PreferredPaymentOnTextChanged;
                    x.cboHobbiesVice.TextChanged += HobbiesViceOnTextChanged;
                });

                Interlocked.Decrement(ref _intUpdatingMetatype);
                Interlocked.Decrement(ref _intUpdatingGender);
                Interlocked.Decrement(ref _intUpdatingAge);
                Interlocked.Decrement(ref _intUpdatingType);
                Interlocked.Decrement(ref _intUpdatingPersonalLife);
                Interlocked.Decrement(ref _intUpdatingPreferredPayment);
                Interlocked.Decrement(ref _intUpdatingHobbiesVice);
            }
        }

        /// <summary>
        /// Method to dynamically create stat block is separated out so that we only create it if the control is expanded
        /// </summary>
        private async ValueTask CreateStatBlockAsync(CancellationToken token = default)
        {
            CursorWait objCursorWait = await CursorWait.NewAsync(this, token: token).ConfigureAwait(false);
            try
            {
                await this.DoThreadSafeAsync(x =>
                {
                    x.cboMetatype = new ElasticComboBox
                    {
                        Anchor = AnchorStyles.Left | AnchorStyles.Right,
                        FormattingEnabled = true,
                        Name = "cboMetatype"
                    };
                    x.cboGender = new ElasticComboBox
                        {Anchor = AnchorStyles.Left | AnchorStyles.Right, FormattingEnabled = true, Name = "cboGender"};
                    x.cboAge = new ElasticComboBox
                        {Anchor = AnchorStyles.Left | AnchorStyles.Right, FormattingEnabled = true, Name = "cboAge"};
                    x.cboType = new ElasticComboBox
                        {Anchor = AnchorStyles.Left | AnchorStyles.Right, FormattingEnabled = true, Name = "cboType"};
                    x.cboPersonalLife = new ElasticComboBox
                    {
                        Anchor = AnchorStyles.Left | AnchorStyles.Right,
                        FormattingEnabled = true,
                        Name = "cboPersonalLife"
                    };
                    x.cboPreferredPayment = new ElasticComboBox
                    {
                        Anchor = AnchorStyles.Left | AnchorStyles.Right,
                        FormattingEnabled = true,
                        Name = "cboPreferredPayment"
                    };
                    x.cboHobbiesVice = new ElasticComboBox
                    {
                        Anchor = AnchorStyles.Left | AnchorStyles.Right,
                        FormattingEnabled = true,
                        Name = "cboHobbiesVice"
                    };
                }, token).ConfigureAwait(false);

                await LoadStatBlockListsAsync(token).ConfigureAwait(false);

                await this.DoThreadSafeAsync(x =>
                {
                    if (x._objContact != null)
                    {
                        // Properties controllable by the character themselves
                        x.cboMetatype.DoOneWayDataBinding("Enabled", x._objContact, nameof(Contact.NoLinkedCharacter));
                        x.cboGender.DoOneWayDataBinding("Enabled", x._objContact, nameof(Contact.NoLinkedCharacter));
                        x.cboAge.DoOneWayDataBinding("Enabled", x._objContact, nameof(Contact.NoLinkedCharacter));
                    }

                    x.lblType = new Label
                    {
                        Anchor = AnchorStyles.Right,
                        AutoSize = true,
                        Margin = new Padding(3, 6, 3, 6),
                        Name = "lblType",
                        Tag = "Label_Type",
                        Text = "Type:",
                        TextAlign = ContentAlignment.MiddleRight
                    };
                    x.lblMetatype = new Label
                    {
                        Anchor = AnchorStyles.Right,
                        AutoSize = true,
                        Margin = new Padding(3, 6, 3, 6),
                        Name = "lblMetatype",
                        Tag = "Label_Metatype",
                        Text = "Metatype:",
                        TextAlign = ContentAlignment.MiddleRight
                    };
                    x.lblGender = new Label
                    {
                        Anchor = AnchorStyles.Right,
                        AutoSize = true,
                        Margin = new Padding(3, 6, 3, 6),
                        Name = "lblGender",
                        Tag = "Label_Gender",
                        Text = "Gender:",
                        TextAlign = ContentAlignment.MiddleRight
                    };
                    x.lblAge = new Label
                    {
                        Anchor = AnchorStyles.Right,
                        AutoSize = true,
                        Margin = new Padding(3, 6, 3, 6),
                        Name = "lblAge",
                        Tag = "Label_Age",
                        Text = "Age:",
                        TextAlign = ContentAlignment.MiddleRight
                    };
                    x.lblPersonalLife = new Label
                    {
                        Anchor = AnchorStyles.Right,
                        AutoSize = true,
                        Margin = new Padding(3, 6, 3, 6),
                        Name = "lblPersonalLife",
                        Tag = "Label_Contact_PersonalLife",
                        Text = "Personal Life:",
                        TextAlign = ContentAlignment.MiddleRight
                    };
                    x.lblPreferredPayment = new Label
                    {
                        Anchor = AnchorStyles.Right,
                        AutoSize = true,
                        Margin = new Padding(3, 6, 3, 6),
                        Name = "lblPreferredPayment",
                        Tag = "Label_Contact_PreferredPayment",
                        Text = "Preferred Payment:",
                        TextAlign = ContentAlignment.MiddleRight
                    };
                    x.lblHobbiesVice = new Label
                    {
                        Anchor = AnchorStyles.Right,
                        AutoSize = true,
                        Margin = new Padding(3, 6, 3, 6),
                        Name = "lblHobbiesVice",
                        Tag = "Label_Contact_HobbiesVice",
                        Text = "Hobbies/Vice:",
                        TextAlign = ContentAlignment.MiddleRight
                    };

                    x.tlpStatBlock = new TableLayoutPanel
                    {
                        AutoSize = true,
                        AutoSizeMode = AutoSizeMode.GrowAndShrink,
                        ColumnCount = 4,
                        RowCount = 5,
                        Dock = DockStyle.Fill,
                        Name = "tlpStatBlock"
                    };
                    x.tlpStatBlock.ColumnStyles.Add(new ColumnStyle());
                    x.tlpStatBlock.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
                    x.tlpStatBlock.ColumnStyles.Add(new ColumnStyle());
                    x.tlpStatBlock.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
                    x.tlpStatBlock.RowStyles.Add(new RowStyle());
                    x.tlpStatBlock.RowStyles.Add(new RowStyle());
                    x.tlpStatBlock.RowStyles.Add(new RowStyle());
                    x.tlpStatBlock.RowStyles.Add(new RowStyle());
                    x.tlpStatBlock.Controls.Add(x.lblMetatype, 0, 0);
                    x.tlpStatBlock.Controls.Add(x.cboMetatype, 1, 0);
                    x.tlpStatBlock.Controls.Add(x.lblGender, 0, 1);
                    x.tlpStatBlock.Controls.Add(x.cboGender, 1, 1);
                    x.tlpStatBlock.Controls.Add(x.lblAge, 0, 2);
                    x.tlpStatBlock.Controls.Add(x.cboAge, 1, 2);
                    x.tlpStatBlock.Controls.Add(x.lblType, 0, 3);
                    x.tlpStatBlock.Controls.Add(x.cboType, 1, 3);
                    x.tlpStatBlock.Controls.Add(x.lblPersonalLife, 2, 0);
                    x.tlpStatBlock.Controls.Add(x.cboPersonalLife, 3, 0);
                    x.tlpStatBlock.Controls.Add(x.lblPreferredPayment, 2, 1);
                    x.tlpStatBlock.Controls.Add(x.cboPreferredPayment, 3, 1);
                    x.tlpStatBlock.Controls.Add(x.lblHobbiesVice, 2, 2);
                    x.tlpStatBlock.Controls.Add(x.cboHobbiesVice, 3, 2);
                }, token).ConfigureAwait(false);
                await tlpStatBlock.TranslateWinFormAsync(token: token).ConfigureAwait(false);
                await tlpStatBlock.UpdateLightDarkModeAsync(token: token).ConfigureAwait(false);
                await this.DoThreadSafeAsync(x =>
                {
                    x.SuspendLayout();
                    try
                    {
                        x.tlpMain.SuspendLayout();
                        try
                        {
                            x.tlpMain.SetColumnSpan(x.tlpStatBlock, 13);
                            x.tlpMain.Controls.Add(x.tlpStatBlock, 0, 3);
                        }
                        finally
                        {
                            x.tlpMain.ResumeLayout();
                        }
                    }
                    finally
                    {
                        x.ResumeLayout();
                    }
                }, token: token).ConfigureAwait(false);

                // Need these as separate instead of as simple data bindings so that we don't get annoying live partial translations
                if (_objContact != null)
                {
                    string strMetatype = await _objContact.GetMetatypeAsync(token).ConfigureAwait(false);
                    string strGender = await _objContact.GetGenderAsync(token).ConfigureAwait(false);
                    string strAge = await _objContact.GetAgeAsync(token).ConfigureAwait(false);
                    string strPersonalLife = _objContact.PersonalLife;
                    string strType = _objContact.Type;
                    string strPreferredPayment = _objContact.PreferredPayment;
                    string strHobbiesVice = _objContact.HobbiesVice;
                    await this.DoThreadSafeAsync(x =>
                    {
                        x.cboMetatype.SelectedValue = strMetatype;
                        x.cboGender.SelectedValue = strGender;
                        x.cboAge.SelectedValue = strAge;
                        x.cboPersonalLife.SelectedValue = strPersonalLife;
                        x.cboType.SelectedValue = strType;
                        x.cboPreferredPayment.SelectedValue = strPreferredPayment;
                        x.cboHobbiesVice.SelectedValue = strHobbiesVice;
                    }, token: token).ConfigureAwait(false);
                    if (await cboMetatype.DoThreadSafeFuncAsync(x => x.SelectedIndex, token: token)
                                         .ConfigureAwait(false) < 0)
                    {
                        string strTemp = await _objContact.GetDisplayMetatypeAsync(token).ConfigureAwait(false);
                        await cboMetatype.DoThreadSafeAsync(x => x.Text = strTemp, token: token).ConfigureAwait(false);
                    }

                    if (await cboGender.DoThreadSafeFuncAsync(x => x.SelectedIndex, token: token).ConfigureAwait(false)
                        < 0)
                    {
                        string strTemp = await _objContact.GetDisplayGenderAsync(token).ConfigureAwait(false);
                        await cboGender.DoThreadSafeAsync(x => x.Text = strTemp, token: token).ConfigureAwait(false);
                    }

                    if (await cboAge.DoThreadSafeFuncAsync(x => x.SelectedIndex, token: token).ConfigureAwait(false)
                        < 0)
                    {
                        string strTemp = await _objContact.GetDisplayAgeAsync(token).ConfigureAwait(false);
                        await cboAge.DoThreadSafeAsync(x => x.Text = strTemp, token: token).ConfigureAwait(false);
                    }

                    if (await cboPersonalLife.DoThreadSafeFuncAsync(x => x.SelectedIndex, token: token)
                                             .ConfigureAwait(false) < 0)
                    {
                        string strTemp = await _objContact.GetDisplayPersonalLifeAsync(token).ConfigureAwait(false);
                        await cboPersonalLife.DoThreadSafeAsync(x => x.Text = strTemp, token: token)
                                             .ConfigureAwait(false);
                    }

                    if (await cboType.DoThreadSafeFuncAsync(x => x.SelectedIndex, token: token).ConfigureAwait(false)
                        < 0)
                    {
                        string strTemp = await _objContact.GetDisplayTypeAsync(token).ConfigureAwait(false);
                        await cboType.DoThreadSafeAsync(x => x.Text = strTemp, token: token).ConfigureAwait(false);
                    }

                    if (await cboPreferredPayment.DoThreadSafeFuncAsync(x => x.SelectedIndex, token: token)
                                                 .ConfigureAwait(false) < 0)
                    {
                        string strTemp = await _objContact.GetDisplayTypeAsync(token).ConfigureAwait(false);
                        await cboPreferredPayment.DoThreadSafeAsync(x => x.Text = strTemp, token: token)
                                                 .ConfigureAwait(false);
                    }

                    if (await cboHobbiesVice.DoThreadSafeFuncAsync(x => x.SelectedIndex, token: token)
                                            .ConfigureAwait(false) < 0)
                    {
                        string strTemp = await _objContact.GetDisplayTypeAsync(token).ConfigureAwait(false);
                        await cboHobbiesVice.DoThreadSafeAsync(x => x.Text = strTemp, token: token)
                                            .ConfigureAwait(false);
                    }
                }

                // Need these as separate instead of as simple data bindings so that we don't get annoying live partial translations
                await this.DoThreadSafeAsync(x =>
                {
                    x.cboMetatype.TextChanged += MetatypeOnTextChanged;
                    x.cboGender.TextChanged += GenderOnTextChanged;
                    x.cboAge.TextChanged += AgeOnTextChanged;
                    x.cboType.TextChanged += TypeOnTextChanged;
                    x.cboPersonalLife.TextChanged += PersonalLifeOnTextChanged;
                    x.cboPreferredPayment.TextChanged += PreferredPaymentOnTextChanged;
                    x.cboHobbiesVice.TextChanged += HobbiesViceOnTextChanged;
                }, token).ConfigureAwait(false);

                Interlocked.Decrement(ref _intUpdatingMetatype);
                Interlocked.Decrement(ref _intUpdatingGender);
                Interlocked.Decrement(ref _intUpdatingAge);
                Interlocked.Decrement(ref _intUpdatingType);
                Interlocked.Decrement(ref _intUpdatingPersonalLife);
                Interlocked.Decrement(ref _intUpdatingPreferredPayment);
                Interlocked.Decrement(ref _intUpdatingHobbiesVice);
            }
            finally
            {
                await objCursorWait.DisposeAsync().ConfigureAwait(false);
            }
        }

        private void cboContactRole_TextChanged(object sender, EventArgs e)
        {
            if (_tmrRoleChangeTimer == null)
                return;
            if (_tmrRoleChangeTimer.Enabled)
                _tmrRoleChangeTimer.Stop();
            if (_intUpdatingRole > 0)
                return;
            _tmrRoleChangeTimer.Start();
        }

        private void MetatypeOnTextChanged(object sender, EventArgs e)
        {
            if (_tmrMetatypeChangeTimer == null)
                return;
            if (_tmrMetatypeChangeTimer.Enabled)
                _tmrMetatypeChangeTimer.Stop();
            if (_intUpdatingMetatype > 0)
                return;
            _tmrMetatypeChangeTimer.Start();
        }

        private void GenderOnTextChanged(object sender, EventArgs e)
        {
            if (_tmrGenderChangeTimer == null)
                return;
            if (_tmrGenderChangeTimer.Enabled)
                _tmrGenderChangeTimer.Stop();
            if (_intUpdatingGender > 0)
                return;
            _tmrGenderChangeTimer.Start();
        }

        private void AgeOnTextChanged(object sender, EventArgs e)
        {
            if (_tmrAgeChangeTimer == null)
                return;
            if (_tmrAgeChangeTimer.Enabled)
                _tmrAgeChangeTimer.Stop();
            if (_intUpdatingAge > 0)
                return;
            _tmrAgeChangeTimer.Start();
        }

        private void TypeOnTextChanged(object sender, EventArgs e)
        {
            if (_tmrTypeChangeTimer == null)
                return;
            if (_tmrTypeChangeTimer.Enabled)
                _tmrTypeChangeTimer.Stop();
            if (_intUpdatingType > 0)
                return;
            _tmrTypeChangeTimer.Start();
        }

        private void PersonalLifeOnTextChanged(object sender, EventArgs e)
        {
            if (_tmrPersonalLifeChangeTimer == null)
                return;
            if (_tmrPersonalLifeChangeTimer.Enabled)
                _tmrPersonalLifeChangeTimer.Stop();
            if (_intUpdatingPersonalLife > 0)
                return;
            _tmrPersonalLifeChangeTimer.Start();
        }

        private void PreferredPaymentOnTextChanged(object sender, EventArgs e)
        {
            if (_tmrPreferredPaymentChangeTimer == null)
                return;
            if (_tmrPreferredPaymentChangeTimer.Enabled)
                _tmrPreferredPaymentChangeTimer.Stop();
            if (_intUpdatingPreferredPayment > 0)
                return;
            _tmrPreferredPaymentChangeTimer.Start();
        }

        private void HobbiesViceOnTextChanged(object sender, EventArgs e)
        {
            if (_tmrHobbiesViceChangeTimer == null)
                return;
            if (_tmrHobbiesViceChangeTimer.Enabled)
                _tmrHobbiesViceChangeTimer.Stop();
            if (_intUpdatingHobbiesVice > 0)
                return;
            _tmrHobbiesViceChangeTimer.Start();
        }

        private void LoadStatBlockLists()
        {
            // Read the list of Categories from the XML file.
            using (new FetchSafelyFromPool<List<ListItem>>(Utils.ListItemListPool, out List<ListItem> lstMetatypes))
            using (new FetchSafelyFromPool<List<ListItem>>(Utils.ListItemListPool, out List<ListItem> lstGenders))
            using (new FetchSafelyFromPool<List<ListItem>>(Utils.ListItemListPool, out List<ListItem> lstAges))
            using (new FetchSafelyFromPool<List<ListItem>>(Utils.ListItemListPool, out List<ListItem> lstPersonalLives))
            using (new FetchSafelyFromPool<List<ListItem>>(Utils.ListItemListPool, out List<ListItem> lstTypes))
            using (new FetchSafelyFromPool<List<ListItem>>(Utils.ListItemListPool, out List<ListItem> lstPreferredPayments))
            using (new FetchSafelyFromPool<List<ListItem>>(Utils.ListItemListPool, out List<ListItem> lstHobbiesVices))
            {
                lstMetatypes.Add(ListItem.Blank);
                lstGenders.Add(ListItem.Blank);
                lstAges.Add(ListItem.Blank);
                lstPersonalLives.Add(ListItem.Blank);
                lstTypes.Add(ListItem.Blank);
                lstPreferredPayments.Add(ListItem.Blank);
                lstHobbiesVices.Add(ListItem.Blank);

                XPathNavigator xmlContactsBaseNode = _objContact.CharacterObject.LoadDataXPath("contacts.xml")
                                                                .SelectSingleNodeAndCacheExpression("/chummer");
                if (xmlContactsBaseNode != null)
                {
                    foreach (XPathNavigator xmlNode in xmlContactsBaseNode.SelectAndCacheExpression("genders/gender"))
                    {
                        string strName = xmlNode.Value;
                        lstGenders.Add(new ListItem(
                                           strName,
                                           xmlNode.SelectSingleNodeAndCacheExpression("@translate")?.Value ?? strName));
                    }

                    foreach (XPathNavigator xmlNode in xmlContactsBaseNode.SelectAndCacheExpression("ages/age"))
                    {
                        string strName = xmlNode.Value;
                        lstAges.Add(new ListItem(
                                        strName,
                                        xmlNode.SelectSingleNodeAndCacheExpression("@translate")?.Value ?? strName));
                    }

                    foreach (XPathNavigator xmlNode in xmlContactsBaseNode.SelectAndCacheExpression(
                                 "personallives/personallife"))
                    {
                        string strName = xmlNode.Value;
                        lstPersonalLives.Add(new ListItem(
                                                 strName,
                                                 xmlNode.SelectSingleNodeAndCacheExpression("@translate")?.Value
                                                 ?? strName));
                    }

                    foreach (XPathNavigator xmlNode in xmlContactsBaseNode.SelectAndCacheExpression("types/type"))
                    {
                        string strName = xmlNode.Value;
                        lstTypes.Add(new ListItem(
                                         strName,
                                         xmlNode.SelectSingleNodeAndCacheExpression("@translate")?.Value ?? strName));
                    }

                    foreach (XPathNavigator xmlNode in xmlContactsBaseNode.SelectAndCacheExpression(
                                 "preferredpayments/preferredpayment"))
                    {
                        string strName = xmlNode.Value;
                        lstPreferredPayments.Add(new ListItem(
                                                     strName,
                                                     xmlNode.SelectSingleNodeAndCacheExpression("@translate")?.Value
                                                     ?? strName));
                    }

                    foreach (XPathNavigator xmlNode in xmlContactsBaseNode.SelectAndCacheExpression(
                                 "hobbiesvices/hobbyvice"))
                    {
                        string strName = xmlNode.Value;
                        lstHobbiesVices.Add(new ListItem(
                                                strName,
                                                xmlNode.SelectSingleNodeAndCacheExpression("@translate")?.Value
                                                ?? strName));
                    }
                }

                string strSpace = LanguageManager.GetString("String_Space");
                foreach (XPathNavigator xmlMetatypeNode in _objContact.CharacterObject.LoadDataXPath("metatypes.xml")
                                                                      .SelectAndCacheExpression(
                                                                          "/chummer/metatypes/metatype"))
                {
                    string strName = xmlMetatypeNode.SelectSingleNodeAndCacheExpression("name")?.Value;
                    string strMetatypeDisplay = xmlMetatypeNode.SelectSingleNodeAndCacheExpression("translate")?.Value
                                                ?? strName;
                    lstMetatypes.Add(new ListItem(strName, strMetatypeDisplay));
                    XPathNodeIterator xmlMetavariantsList
                        = xmlMetatypeNode.SelectAndCacheExpression("metavariants/metavariant");
                    if (xmlMetavariantsList.Count > 0)
                    {
                        string strMetavariantFormat = strMetatypeDisplay + strSpace + "({0})";
                        foreach (XPathNavigator objXmlMetavariantNode in xmlMetavariantsList)
                        {
                            string strMetavariantName
                                = objXmlMetavariantNode.SelectSingleNodeAndCacheExpression("name")?.Value
                                  ?? string.Empty;
                            if (lstMetatypes.All(
                                    x => strMetavariantName.Equals(x.Value.ToString(),
                                                                   StringComparison.OrdinalIgnoreCase)))
                                lstMetatypes.Add(new ListItem(strMetavariantName,
                                                              string.Format(
                                                                  GlobalSettings.CultureInfo, strMetavariantFormat,
                                                                  objXmlMetavariantNode
                                                                      .SelectSingleNodeAndCacheExpression("translate")
                                                                      ?.Value ?? strMetavariantName)));
                        }
                    }
                }

                lstMetatypes.Sort(CompareListItems.CompareNames);
                lstGenders.Sort(CompareListItems.CompareNames);
                lstAges.Sort(CompareListItems.CompareNames);
                lstPersonalLives.Sort(CompareListItems.CompareNames);
                lstTypes.Sort(CompareListItems.CompareNames);
                lstHobbiesVices.Sort(CompareListItems.CompareNames);
                lstPreferredPayments.Sort(CompareListItems.CompareNames);

                cboMetatype.PopulateWithListItems(lstMetatypes);
                cboGender.PopulateWithListItems(lstGenders);
                cboAge.PopulateWithListItems(lstAges);
                cboPersonalLife.PopulateWithListItems(lstPersonalLives);
                cboType.PopulateWithListItems(lstTypes);
                cboPreferredPayment.PopulateWithListItems(lstPreferredPayments);
                cboHobbiesVice.PopulateWithListItems(lstHobbiesVices);
            }
        }

        private async ValueTask LoadStatBlockListsAsync(CancellationToken token = default)
        {
            // Read the list of Categories from the XML file.
            using (new FetchSafelyFromPool<List<ListItem>>(Utils.ListItemListPool, out List<ListItem> lstMetatypes))
            using (new FetchSafelyFromPool<List<ListItem>>(Utils.ListItemListPool, out List<ListItem> lstGenders))
            using (new FetchSafelyFromPool<List<ListItem>>(Utils.ListItemListPool, out List<ListItem> lstAges))
            using (new FetchSafelyFromPool<List<ListItem>>(Utils.ListItemListPool, out List<ListItem> lstPersonalLives))
            using (new FetchSafelyFromPool<List<ListItem>>(Utils.ListItemListPool, out List<ListItem> lstTypes))
            using (new FetchSafelyFromPool<List<ListItem>>(Utils.ListItemListPool, out List<ListItem> lstPreferredPayments))
            using (new FetchSafelyFromPool<List<ListItem>>(Utils.ListItemListPool, out List<ListItem> lstHobbiesVices))
            {
                lstMetatypes.Add(ListItem.Blank);
                lstGenders.Add(ListItem.Blank);
                lstAges.Add(ListItem.Blank);
                lstPersonalLives.Add(ListItem.Blank);
                lstTypes.Add(ListItem.Blank);
                lstPreferredPayments.Add(ListItem.Blank);
                lstHobbiesVices.Add(ListItem.Blank);

                XPathNavigator xmlContactsBaseNode = await (await _objContact.CharacterObject.LoadDataXPathAsync("contacts.xml", token: token).ConfigureAwait(false))
                                                           .SelectSingleNodeAndCacheExpressionAsync("/chummer", token).ConfigureAwait(false);
                if (xmlContactsBaseNode != null)
                {
                    foreach (XPathNavigator xmlNode in await xmlContactsBaseNode.SelectAndCacheExpressionAsync("genders/gender", token).ConfigureAwait(false))
                    {
                        string strName = xmlNode.Value;
                        lstGenders.Add(new ListItem(
                                           strName,
                                           (await xmlNode.SelectSingleNodeAndCacheExpressionAsync("@translate", token).ConfigureAwait(false))?.Value ?? strName));
                    }

                    foreach (XPathNavigator xmlNode in await xmlContactsBaseNode.SelectAndCacheExpressionAsync("ages/age", token).ConfigureAwait(false))
                    {
                        string strName = xmlNode.Value;
                        lstAges.Add(new ListItem(
                                        strName,
                                        (await xmlNode.SelectSingleNodeAndCacheExpressionAsync("@translate", token).ConfigureAwait(false))?.Value ?? strName));
                    }

                    foreach (XPathNavigator xmlNode in await xmlContactsBaseNode.SelectAndCacheExpressionAsync(
                                 "personallives/personallife", token).ConfigureAwait(false))
                    {
                        string strName = xmlNode.Value;
                        lstPersonalLives.Add(new ListItem(
                                                 strName,
                                                 (await xmlNode.SelectSingleNodeAndCacheExpressionAsync("@translate", token).ConfigureAwait(false))?.Value
                                                 ?? strName));
                    }

                    foreach (XPathNavigator xmlNode in await xmlContactsBaseNode.SelectAndCacheExpressionAsync("types/type", token).ConfigureAwait(false))
                    {
                        string strName = xmlNode.Value;
                        lstTypes.Add(new ListItem(
                                         strName,
                                         (await xmlNode.SelectSingleNodeAndCacheExpressionAsync("@translate", token).ConfigureAwait(false))?.Value ?? strName));
                    }

                    foreach (XPathNavigator xmlNode in await xmlContactsBaseNode.SelectAndCacheExpressionAsync(
                                 "preferredpayments/preferredpayment", token).ConfigureAwait(false))
                    {
                        string strName = xmlNode.Value;
                        lstPreferredPayments.Add(new ListItem(
                                                     strName,
                                                     (await xmlNode.SelectSingleNodeAndCacheExpressionAsync("@translate", token).ConfigureAwait(false))?.Value
                                                     ?? strName));
                    }

                    foreach (XPathNavigator xmlNode in await xmlContactsBaseNode.SelectAndCacheExpressionAsync(
                                 "hobbiesvices/hobbyvice", token).ConfigureAwait(false))
                    {
                        string strName = xmlNode.Value;
                        lstHobbiesVices.Add(new ListItem(
                                                strName,
                                                (await xmlNode.SelectSingleNodeAndCacheExpressionAsync("@translate", token).ConfigureAwait(false))?.Value
                                                ?? strName));
                    }
                }

                string strSpace = await LanguageManager.GetStringAsync("String_Space", token: token).ConfigureAwait(false);
                foreach (XPathNavigator xmlMetatypeNode in await (await _objContact.CharacterObject.LoadDataXPathAsync("metatypes.xml", token: token).ConfigureAwait(false))
                                                                 .SelectAndCacheExpressionAsync(
                                                                     "/chummer/metatypes/metatype", token).ConfigureAwait(false))
                {
                    string strName = (await xmlMetatypeNode.SelectSingleNodeAndCacheExpressionAsync("name", token).ConfigureAwait(false))?.Value;
                    string strMetatypeDisplay = (await xmlMetatypeNode.SelectSingleNodeAndCacheExpressionAsync("translate", token).ConfigureAwait(false))?.Value
                                                ?? strName;
                    lstMetatypes.Add(new ListItem(strName, strMetatypeDisplay));
                    XPathNodeIterator xmlMetavariantsList
                        = await xmlMetatypeNode.SelectAndCacheExpressionAsync("metavariants/metavariant", token).ConfigureAwait(false);
                    if (xmlMetavariantsList.Count > 0)
                    {
                        string strMetavariantFormat = strMetatypeDisplay + strSpace + "({0})";
                        foreach (XPathNavigator objXmlMetavariantNode in xmlMetavariantsList)
                        {
                            string strMetavariantName
                                = (await objXmlMetavariantNode.SelectSingleNodeAndCacheExpressionAsync("name", token).ConfigureAwait(false))?.Value
                                  ?? string.Empty;
                            if (lstMetatypes.All(
                                    x => strMetavariantName.Equals(x.Value.ToString(),
                                                                   StringComparison.OrdinalIgnoreCase)))
                                lstMetatypes.Add(new ListItem(strMetavariantName,
                                                              string.Format(
                                                                  GlobalSettings.CultureInfo, strMetavariantFormat,
                                                                  (await objXmlMetavariantNode
                                                                         .SelectSingleNodeAndCacheExpressionAsync("translate", token).ConfigureAwait(false))
                                                                      ?.Value ?? strMetavariantName)));
                        }
                    }
                }

                lstMetatypes.Sort(CompareListItems.CompareNames);
                lstGenders.Sort(CompareListItems.CompareNames);
                lstAges.Sort(CompareListItems.CompareNames);
                lstPersonalLives.Sort(CompareListItems.CompareNames);
                lstTypes.Sort(CompareListItems.CompareNames);
                lstHobbiesVices.Sort(CompareListItems.CompareNames);
                lstPreferredPayments.Sort(CompareListItems.CompareNames);

                await cboMetatype.PopulateWithListItemsAsync(lstMetatypes, token).ConfigureAwait(false);
                await cboGender.PopulateWithListItemsAsync(lstGenders, token).ConfigureAwait(false);
                await cboAge.PopulateWithListItemsAsync(lstAges, token).ConfigureAwait(false);
                await cboPersonalLife.PopulateWithListItemsAsync(lstPersonalLives, token).ConfigureAwait(false);
                await cboType.PopulateWithListItemsAsync(lstTypes, token).ConfigureAwait(false);
                await cboPreferredPayment.PopulateWithListItemsAsync(lstPreferredPayments, token).ConfigureAwait(false);
                await cboHobbiesVice.PopulateWithListItemsAsync(lstHobbiesVices, token).ConfigureAwait(false);
            }
        }

        #endregion Methods
    }
}
