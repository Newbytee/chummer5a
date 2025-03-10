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
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using Chummer.Annotations;
using Chummer.Backend.Equipment;

namespace Chummer
{
    public partial class CreatePACKSKit : Form
    {
        [NotNull]
        private readonly Character _objCharacter;

        #region Control Events

        public CreatePACKSKit(Character objCharacter)
        {
            _objCharacter = objCharacter ?? throw new ArgumentNullException(nameof(objCharacter));
            InitializeComponent();
            this.UpdateLightDarkMode();
            this.TranslateWinForm();
        }

        private async void cmdOK_Click(object sender, EventArgs e)
        {
            // Make sure the kit and file name fields are populated.
            string strName = await txtName.DoThreadSafeFuncAsync(x => x.Text).ConfigureAwait(false);
            if (string.IsNullOrEmpty(strName))
            {
                Program.ShowScrollableMessageBox(this, await LanguageManager.GetStringAsync("Message_CreatePACKSKit_KitName").ConfigureAwait(false), await LanguageManager.GetStringAsync("MessageTitle_CreatePACKSKit_KitName").ConfigureAwait(false), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string strFileName = await txtFileName.DoThreadSafeFuncAsync(x => x.Text).ConfigureAwait(false);
            if (string.IsNullOrEmpty(strFileName))
            {
                Program.ShowScrollableMessageBox(this, await LanguageManager.GetStringAsync("Message_CreatePACKSKit_FileName").ConfigureAwait(false), await LanguageManager.GetStringAsync("MessageTitle_CreatePACKSKit_FileName").ConfigureAwait(false), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Make sure the file name starts with custom and ends with _packs.xml.
            if (!strFileName.StartsWith("custom_", StringComparison.OrdinalIgnoreCase) || !strFileName.EndsWith("_packs.xml", StringComparison.OrdinalIgnoreCase))
            {
                Program.ShowScrollableMessageBox(this, await LanguageManager.GetStringAsync("Message_CreatePACKSKit_InvalidFileName").ConfigureAwait(false), await LanguageManager.GetStringAsync("MessageTitle_CreatePACKSKit_InvalidFileName").ConfigureAwait(false), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // See if a Kit with this name already exists for the Custom category.
            // This was originally done without the XmlManager, but because amends and overrides and toggling custom data directories can change names, we need to use it.
            if ((await XmlManager.LoadXPathAsync("packs.xml", _objCharacter.Settings.EnabledCustomDataDirectoryPaths).ConfigureAwait(false))
                .SelectSingleNode("/chummer/packs/pack[name = " + strName.CleanXPath() + " and category = \"Custom\"]") != null)
            {
                Program.ShowScrollableMessageBox(this, string.Format(GlobalSettings.CultureInfo, await LanguageManager.GetStringAsync("Message_CreatePACKSKit_DuplicateName").ConfigureAwait(false), strName),
                                                 await LanguageManager.GetStringAsync("MessageTitle_CreatePACKSKit_DuplicateName").ConfigureAwait(false), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string strPath = Path.Combine(Utils.GetStartupPath, "data", strFileName);

            // If this is not a new file, read in the existing contents.
            XmlDocument objXmlCurrentDocument = null;
            if (File.Exists(strPath))
            {
                try
                {
                    objXmlCurrentDocument = new XmlDocument { XmlResolver = null };
                    await objXmlCurrentDocument.LoadStandardAsync(strPath).ConfigureAwait(false);
                }
                catch (IOException ex)
                {
                    Program.ShowScrollableMessageBox(this, ex.ToString());
                    return;
                }
                catch (XmlException ex)
                {
                    Program.ShowScrollableMessageBox(this, ex.ToString());
                    return;
                }
            }

            using (FileStream objStream = new FileStream(strPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                using (XmlWriter objWriter = Utils.GetStandardXmlWriter(objStream))
                {
                    await objWriter.WriteStartDocumentAsync().ConfigureAwait(false);

                    // <chummer>
                    await objWriter.WriteStartElementAsync("chummer").ConfigureAwait(false);
                    // <packs>
                    await objWriter.WriteStartElementAsync("packs").ConfigureAwait(false);

                    // If this is not a new file, write out the current contents.
                    if (objXmlCurrentDocument != null)
                    {
                        XmlNode xmlExistingPacksNode = objXmlCurrentDocument.SelectSingleNode("/chummer/packs");
                        xmlExistingPacksNode?.WriteContentTo(objWriter);
                    }

                    // <pack>
                    await objWriter.WriteStartElementAsync("pack").ConfigureAwait(false);
                    // <name />
                    await objWriter.WriteElementStringAsync("name", txtName.Text).ConfigureAwait(false);
                    // <category />
                    await objWriter.WriteElementStringAsync("category", "Custom").ConfigureAwait(false);

                    // Export Attributes.
                    if (await chkAttributes.DoThreadSafeFuncAsync(x => x.Checked).ConfigureAwait(false))
                    {
                        int intBOD = await _objCharacter.BOD.GetValueAsync().ConfigureAwait(false) - (await _objCharacter.BOD.GetMetatypeMinimumAsync().ConfigureAwait(false) - 1);
                        int intAGI = await _objCharacter.AGI.GetValueAsync().ConfigureAwait(false) - (await _objCharacter.AGI.GetMetatypeMinimumAsync().ConfigureAwait(false) - 1);
                        int intREA = await _objCharacter.REA.GetValueAsync().ConfigureAwait(false) - (await _objCharacter.REA.GetMetatypeMinimumAsync().ConfigureAwait(false) - 1);
                        int intSTR = await _objCharacter.STR.GetValueAsync().ConfigureAwait(false) - (await _objCharacter.STR.GetMetatypeMinimumAsync().ConfigureAwait(false) - 1);
                        int intCHA = await _objCharacter.CHA.GetValueAsync().ConfigureAwait(false) - (await _objCharacter.CHA.GetMetatypeMinimumAsync().ConfigureAwait(false) - 1);
                        int intINT = await _objCharacter.INT.GetValueAsync().ConfigureAwait(false) - (await _objCharacter.INT.GetMetatypeMinimumAsync().ConfigureAwait(false) - 1);
                        int intLOG = await _objCharacter.LOG.GetValueAsync().ConfigureAwait(false) - (await _objCharacter.LOG.GetMetatypeMinimumAsync().ConfigureAwait(false) - 1);
                        int intWIL = await _objCharacter.WIL.GetValueAsync().ConfigureAwait(false) - (await _objCharacter.WIL.GetMetatypeMinimumAsync().ConfigureAwait(false) - 1);
                        int intEDG = await _objCharacter.EDG.GetValueAsync().ConfigureAwait(false) - (await _objCharacter.EDG.GetMetatypeMinimumAsync().ConfigureAwait(false) - 1);
                        int intMAG = await _objCharacter.MAG.GetValueAsync().ConfigureAwait(false) - (await _objCharacter.MAG.GetMetatypeMinimumAsync().ConfigureAwait(false) - 1);
                        int intMAGAdept = await _objCharacter.MAGAdept.GetValueAsync().ConfigureAwait(false) - (await _objCharacter.MAGAdept.GetMetatypeMinimumAsync().ConfigureAwait(false) - 1);
                        int intDEP = await _objCharacter.DEP.GetValueAsync().ConfigureAwait(false) - (await _objCharacter.DEP.GetMetatypeMinimumAsync().ConfigureAwait(false) - 1);
                        int intRES = await _objCharacter.RES.GetValueAsync().ConfigureAwait(false) - (await _objCharacter.RES.GetMetatypeMinimumAsync().ConfigureAwait(false) - 1);
                        // <attributes>
                        await objWriter.WriteStartElementAsync("attributes").ConfigureAwait(false);
                        await objWriter.WriteElementStringAsync("bod", intBOD.ToString(GlobalSettings.InvariantCultureInfo)).ConfigureAwait(false);
                        await objWriter.WriteElementStringAsync("agi", intAGI.ToString(GlobalSettings.InvariantCultureInfo)).ConfigureAwait(false);
                        await objWriter.WriteElementStringAsync("rea", intREA.ToString(GlobalSettings.InvariantCultureInfo)).ConfigureAwait(false);
                        await objWriter.WriteElementStringAsync("str", intSTR.ToString(GlobalSettings.InvariantCultureInfo)).ConfigureAwait(false);
                        await objWriter.WriteElementStringAsync("cha", intCHA.ToString(GlobalSettings.InvariantCultureInfo)).ConfigureAwait(false);
                        await objWriter.WriteElementStringAsync("int", intINT.ToString(GlobalSettings.InvariantCultureInfo)).ConfigureAwait(false);
                        await objWriter.WriteElementStringAsync("log", intLOG.ToString(GlobalSettings.InvariantCultureInfo)).ConfigureAwait(false);
                        await objWriter.WriteElementStringAsync("wil", intWIL.ToString(GlobalSettings.InvariantCultureInfo)).ConfigureAwait(false);
                        await objWriter.WriteElementStringAsync("edg", intEDG.ToString(GlobalSettings.InvariantCultureInfo)).ConfigureAwait(false);
                        if (_objCharacter.MAGEnabled)
                        {
                            await objWriter.WriteElementStringAsync("mag", intMAG.ToString(GlobalSettings.InvariantCultureInfo)).ConfigureAwait(false);
                            if (_objCharacter.Settings.MysAdeptSecondMAGAttribute && _objCharacter.IsMysticAdept)
                                await objWriter.WriteElementStringAsync("magadept", intMAGAdept.ToString(GlobalSettings.InvariantCultureInfo)).ConfigureAwait(false);
                        }

                        if (_objCharacter.RESEnabled)
                            await objWriter.WriteElementStringAsync("res", intRES.ToString(GlobalSettings.InvariantCultureInfo)).ConfigureAwait(false);
                        if (_objCharacter.DEPEnabled)
                            await objWriter.WriteElementStringAsync("dep", intDEP.ToString(GlobalSettings.InvariantCultureInfo)).ConfigureAwait(false);
                        // </attributes>
                        await objWriter.WriteEndElementAsync().ConfigureAwait(false);
                    }

                    // Export Qualities.
                    if (await chkQualities.DoThreadSafeFuncAsync(x => x.Checked).ConfigureAwait(false))
                    {
                        bool blnPositive = false;
                        bool blnNegative = false;
                        // Determine if Positive or Negative Qualities exist.
                        foreach (Quality objQuality in _objCharacter.Qualities)
                        {
                            switch (objQuality.Type)
                            {
                                case QualityType.Positive:
                                    blnPositive = true;
                                    break;

                                case QualityType.Negative:
                                    blnNegative = true;
                                    break;
                            }

                            if (blnPositive && blnNegative)
                                break;
                        }

                        // <qualities>
                        await objWriter.WriteStartElementAsync("qualities").ConfigureAwait(false);

                        // Positive Qualities.
                        if (blnPositive)
                        {
                            // <positive>
                            await objWriter.WriteStartElementAsync("positive").ConfigureAwait(false);
                            foreach (Quality objQuality in _objCharacter.Qualities)
                            {
                                if (objQuality.Type == QualityType.Positive)
                                {
                                    await objWriter.WriteStartElementAsync("quality").ConfigureAwait(false);
                                    if (!string.IsNullOrEmpty(objQuality.Extra))
                                        await objWriter.WriteAttributeStringAsync("select", objQuality.Extra).ConfigureAwait(false);
                                    objWriter.WriteValue(objQuality.Name);
                                    await objWriter.WriteEndElementAsync().ConfigureAwait(false);
                                }
                            }

                            // </positive>
                            await objWriter.WriteEndElementAsync().ConfigureAwait(false);
                        }

                        // Negative Qualities.
                        if (blnPositive)
                        {
                            // <negative>
                            await objWriter.WriteStartElementAsync("negative").ConfigureAwait(false);
                            foreach (Quality objQuality in _objCharacter.Qualities)
                            {
                                if (objQuality.Type == QualityType.Negative)
                                {
                                    await objWriter.WriteStartElementAsync("quality").ConfigureAwait(false);
                                    if (!string.IsNullOrEmpty(objQuality.Extra))
                                        await objWriter.WriteAttributeStringAsync("select", objQuality.Extra).ConfigureAwait(false);
                                    objWriter.WriteValue(objQuality.Name);
                                    await objWriter.WriteEndElementAsync().ConfigureAwait(false);
                                }
                            }

                            // </negative>
                            await objWriter.WriteEndElementAsync().ConfigureAwait(false);
                        }

                        // </qualities>
                        await objWriter.WriteEndElementAsync().ConfigureAwait(false);
                    }

                    // Export Starting Nuyen.
                    if (await chkStartingNuyen.DoThreadSafeFuncAsync(x => x.Checked).ConfigureAwait(false))
                    {
                        decimal decNuyenBP = _objCharacter.NuyenBP;
                        if (!_objCharacter.EffectiveBuildMethodUsesPriorityTables)
                            decNuyenBP /= 2.0m;
                        await objWriter.WriteElementStringAsync("nuyenbp", decNuyenBP.ToString(GlobalSettings.InvariantCultureInfo)).ConfigureAwait(false);
                    }

                    /* TODO: Add support for active and knowledge skills and skill groups
                    // Export Active Skills.
                    if (await chkActiveSkills.DoThreadSafeFuncAsync(x => x.Checked))
                    {
                        // <skills>
                        await objWriter.WriteStartElementAsync("skills");

                        // Active Skills.
                        foreach (Skill objSkill in _objCharacter.SkillsSection.Skills)
                        {
                            if (!objSkill.IsKnowledgeSkill && objSkill.Rating > 0)
                            {
                                // <skill>
                                await objWriter.WriteStartElementAsync("skill");
                                await objWriter.WriteElementStringAsync("name", objSkill.Name);
                                await objWriter.WriteElementStringAsync("rating", objSkill.Rating.ToString());
                                if (!string.IsNullOrEmpty(objSkill.Specialization))
                                    await objWriter.WriteElementStringAsync("spec", objSkill.Specialization);
                                // </skill>
                                await objWriter.WriteEndElementAsync();
                            }
                        }

                        // Skill Groups.
                        foreach (SkillGroup objSkillGroup in _objCharacter.SkillsSection.SkillGroups)
                        {
                            if (objSkillGroup.BaseUnbroken && objSkillGroup.Rating > 0)
                            {
                                // <skillgroup>
                                await objWriter.WriteStartElementAsync("skillgroup");
                                await objWriter.WriteElementStringAsync("name", objSkillGroup.Name);
                                await objWriter.WriteElementStringAsync("rating", objSkillGroup.Rating.ToString());
                                // </skillgroup>
                                await objWriter.WriteEndElementAsync();
                            }
                        }
                        // </skills>
                        await objWriter.WriteEndElementAsync();
                    }

                    // Export Knowledge Skills.
                    if (await chkKnowledgeSkills.DoThreadSafeFuncAsync(x => x.Checked))
                    {
                        // <knowledgeskills>
                        await objWriter.WriteStartElementAsync("knowledgeskills");
                        foreach (KnowledgeSkill objSkill in _objCharacter.SkillsSection.Skills.OfType<KnowledgeSkill>())
                        {
                            // <skill>
                            await objWriter.WriteStartElementAsync("skill");
                            await objWriter.WriteElementStringAsync("name", objSkill.Name);
                            await objWriter.WriteElementStringAsync("rating", objSkill.Rating.ToString(GlobalSettings.InvariantCultureInfo));
                            if (!string.IsNullOrEmpty(objSkill.Specialization))
                                await objWriter.WriteElementStringAsync("spec", objSkill.Specialization);
                            await objWriter.WriteElementStringAsync("category", objSkill.SkillCategory);
                            // </skill>
                            await objWriter.WriteEndElementAsync();
                        }

                        // </knowledgeskills>
                        await objWriter.WriteEndElementAsync();
                    }
                    */

                    // Export Martial Arts.
                    if (await chkMartialArts.DoThreadSafeFuncAsync(x => x.Checked).ConfigureAwait(false))
                    {
                        // <martialarts>
                        await objWriter.WriteStartElementAsync("martialarts").ConfigureAwait(false);
                        foreach (MartialArt objArt in _objCharacter.MartialArts)
                        {
                            // <martialart>
                            await objWriter.WriteStartElementAsync("martialart").ConfigureAwait(false);
                            await objWriter.WriteElementStringAsync("name", objArt.Name).ConfigureAwait(false);
                            if (objArt.Techniques.Count > 0)
                            {
                                // <techniques>
                                await objWriter.WriteStartElementAsync("techniques").ConfigureAwait(false);
                                foreach (MartialArtTechnique objTechnique in objArt.Techniques)
                                    await objWriter.WriteElementStringAsync("technique", objTechnique.Name).ConfigureAwait(false);
                                // </techniques>
                                await objWriter.WriteEndElementAsync().ConfigureAwait(false);
                            }

                            // </martialart>
                            await objWriter.WriteEndElementAsync().ConfigureAwait(false);
                        }
                        // </martialarts>
                        await objWriter.WriteEndElementAsync().ConfigureAwait(false);
                    }

                    // Export Spells.
                    if (await chkSpells.DoThreadSafeFuncAsync(x => x.Checked).ConfigureAwait(false))
                    {
                        // <spells>
                        await objWriter.WriteStartElementAsync("spells").ConfigureAwait(false);
                        foreach (Spell objSpell in _objCharacter.Spells)
                        {
                            await objWriter.WriteStartElementAsync("spell").ConfigureAwait(false);
                            await objWriter.WriteStartElementAsync("name").ConfigureAwait(false);
                            if (!string.IsNullOrEmpty(objSpell.Extra))
                                await objWriter.WriteAttributeStringAsync("select", objSpell.Extra).ConfigureAwait(false);
                            objWriter.WriteValue(objSpell.Name);
                            await objWriter.WriteEndElementAsync().ConfigureAwait(false);
                            await objWriter.WriteElementStringAsync("category", objSpell.Category).ConfigureAwait(false);
                            await objWriter.WriteEndElementAsync().ConfigureAwait(false);
                        }

                        // </spells>
                        await objWriter.WriteEndElementAsync().ConfigureAwait(false);
                    }

                    // Export Complex Forms.
                    if (await chkComplexForms.DoThreadSafeFuncAsync(x => x.Checked).ConfigureAwait(false))
                    {
                        // <programs>
                        await objWriter.WriteStartElementAsync("complexforms").ConfigureAwait(false);
                        foreach (ComplexForm objComplexForm in _objCharacter.ComplexForms)
                        {
                            // <program>
                            await objWriter.WriteStartElementAsync("complexform").ConfigureAwait(false);
                            await objWriter.WriteStartElementAsync("name").ConfigureAwait(false);
                            if (!string.IsNullOrEmpty(objComplexForm.Extra))
                                await objWriter.WriteAttributeStringAsync("select", objComplexForm.Extra).ConfigureAwait(false);
                            objWriter.WriteValue(objComplexForm.Name);
                            await objWriter.WriteEndElementAsync().ConfigureAwait(false);
                            await objWriter.WriteEndElementAsync().ConfigureAwait(false);
                            // </program>
                            await objWriter.WriteEndElementAsync().ConfigureAwait(false);
                        }

                        // </programs>
                        await objWriter.WriteEndElementAsync().ConfigureAwait(false);
                    }

                    // Export Cyberware/Bioware.
                    if (await chkCyberware.DoThreadSafeFuncAsync(x => x.Checked).ConfigureAwait(false))
                    {
                        bool blnCyberware = false;
                        bool blnBioware = false;
                        foreach (Cyberware objCharacterCyberware in _objCharacter.Cyberware)
                        {
                            switch (objCharacterCyberware.SourceType)
                            {
                                case Improvement.ImprovementSource.Bioware:
                                    blnBioware = true;
                                    break;

                                case Improvement.ImprovementSource.Cyberware:
                                    blnCyberware = true;
                                    break;
                            }

                            if (blnCyberware && blnBioware)
                                break;
                        }

                        if (blnCyberware)
                        {
                            // <cyberwares>
                            await objWriter.WriteStartElementAsync("cyberwares").ConfigureAwait(false);
                            foreach (Cyberware objCyberware in _objCharacter.Cyberware)
                            {
                                if (objCyberware.SourceType == Improvement.ImprovementSource.Cyberware)
                                {
                                    // <cyberware>
                                    await objWriter.WriteStartElementAsync("cyberware").ConfigureAwait(false);
                                    await objWriter.WriteElementStringAsync("name", objCyberware.Name).ConfigureAwait(false);
                                    if (objCyberware.Rating > 0)
                                        await objWriter.WriteElementStringAsync("rating", objCyberware.Rating.ToString(GlobalSettings.InvariantCultureInfo)).ConfigureAwait(false);
                                    await objWriter.WriteElementStringAsync("grade", objCyberware.Grade.Name).ConfigureAwait(false);
                                    if (objCyberware.Children.Count > 0)
                                    {
                                        // <cyberwares>
                                        await objWriter.WriteStartElementAsync("cyberwares").ConfigureAwait(false);
                                        foreach (Cyberware objChildCyberware in objCyberware.Children)
                                        {
                                            if (objChildCyberware.Capacity != "[*]")
                                            {
                                                // <cyberware>
                                                await objWriter.WriteStartElementAsync("cyberware").ConfigureAwait(false);
                                                await objWriter.WriteElementStringAsync("name", objChildCyberware.Name).ConfigureAwait(false);
                                                if (objChildCyberware.Rating > 0)
                                                    await objWriter.WriteElementStringAsync("rating", objChildCyberware.Rating.ToString(GlobalSettings.InvariantCultureInfo)).ConfigureAwait(false);

                                                if (objChildCyberware.GearChildren.Count > 0)
                                                    await WriteGear(objWriter, objChildCyberware.GearChildren).ConfigureAwait(false);
                                                // </cyberware>
                                                await objWriter.WriteEndElementAsync().ConfigureAwait(false);
                                            }
                                        }

                                        // </cyberwares>
                                        await objWriter.WriteEndElementAsync().ConfigureAwait(false);
                                    }

                                    if (objCyberware.GearChildren.Count > 0)
                                        await WriteGear(objWriter, objCyberware.GearChildren).ConfigureAwait(false);

                                    // </cyberware>
                                    await objWriter.WriteEndElementAsync().ConfigureAwait(false);
                                }
                            }

                            // </cyberwares>
                            await objWriter.WriteEndElementAsync().ConfigureAwait(false);
                        }

                        if (blnBioware)
                        {
                            // <biowares>
                            await objWriter.WriteStartElementAsync("biowares").ConfigureAwait(false);
                            foreach (Cyberware objCyberware in _objCharacter.Cyberware)
                            {
                                if (objCyberware.SourceType == Improvement.ImprovementSource.Bioware)
                                {
                                    // <bioware>
                                    await objWriter.WriteStartElementAsync("bioware").ConfigureAwait(false);
                                    await objWriter.WriteElementStringAsync("name", objCyberware.Name).ConfigureAwait(false);
                                    if (objCyberware.Rating > 0)
                                        await objWriter.WriteElementStringAsync("rating", objCyberware.Rating.ToString(GlobalSettings.InvariantCultureInfo)).ConfigureAwait(false);
                                    await objWriter.WriteElementStringAsync("grade", objCyberware.Grade.ToString()).ConfigureAwait(false);

                                    if (objCyberware.GearChildren.Count > 0)
                                        await WriteGear(objWriter, objCyberware.GearChildren).ConfigureAwait(false);
                                    // </bioware>
                                    await objWriter.WriteEndElementAsync().ConfigureAwait(false);
                                }
                            }

                            // </biowares>
                            await objWriter.WriteEndElementAsync().ConfigureAwait(false);
                        }
                    }

                    // Export Lifestyle.
                    if (await chkLifestyle.DoThreadSafeFuncAsync(x => x.Checked).ConfigureAwait(false))
                    {
                        // <lifestyles>
                        await objWriter.WriteStartElementAsync("lifestyles").ConfigureAwait(false);
                        foreach (Lifestyle objLifestyle in _objCharacter.Lifestyles)
                        {
                            // <lifestyle>
                            await objWriter.WriteStartElementAsync("lifestyle").ConfigureAwait(false);
                            await objWriter.WriteElementStringAsync("name", objLifestyle.Name).ConfigureAwait(false);
                            await objWriter.WriteElementStringAsync("months", objLifestyle.Increments.ToString(GlobalSettings.InvariantCultureInfo)).ConfigureAwait(false);
                            if (!string.IsNullOrEmpty(objLifestyle.BaseLifestyle))
                            {
                                // This is an Advanced Lifestyle, so write out its properties.
                                await objWriter.WriteElementStringAsync("cost", objLifestyle.Cost.ToString(_objCharacter.Settings.NuyenFormat, GlobalSettings.CultureInfo)).ConfigureAwait(false);
                                await objWriter.WriteElementStringAsync("dice", objLifestyle.Dice.ToString(GlobalSettings.InvariantCultureInfo)).ConfigureAwait(false);
                                await objWriter.WriteElementStringAsync("multiplier", objLifestyle.Multiplier.ToString(_objCharacter.Settings.NuyenFormat, GlobalSettings.CultureInfo)).ConfigureAwait(false);
                                await objWriter.WriteElementStringAsync("baselifestyle", objLifestyle.BaseLifestyle).ConfigureAwait(false);
                                if (objLifestyle.LifestyleQualities.Count > 0)
                                {
                                    // <qualities>
                                    await objWriter.WriteStartElementAsync("qualities").ConfigureAwait(false);
                                    foreach (LifestyleQuality objQuality in objLifestyle.LifestyleQualities)
                                        await objWriter.WriteElementStringAsync("quality", objQuality.Name).ConfigureAwait(false);
                                    // </qualities>
                                    await objWriter.WriteEndElementAsync().ConfigureAwait(false);
                                }
                            }

                            // </lifestyle>
                            await objWriter.WriteEndElementAsync().ConfigureAwait(false);
                        }

                        // </lifestyles>
                        await objWriter.WriteEndElementAsync().ConfigureAwait(false);
                    }

                    // Export Armor.
                    if (await chkArmor.DoThreadSafeFuncAsync(x => x.Checked).ConfigureAwait(false))
                    {
                        // <armors>
                        await objWriter.WriteStartElementAsync("armors").ConfigureAwait(false);
                        foreach (Armor objArmor in _objCharacter.Armor)
                        {
                            // <armor>
                            await objWriter.WriteStartElementAsync("armor").ConfigureAwait(false);
                            await objWriter.WriteElementStringAsync("name", objArmor.Name).ConfigureAwait(false);
                            if (objArmor.ArmorMods.Count > 0)
                            {
                                // <mods>
                                await objWriter.WriteStartElementAsync("mods").ConfigureAwait(false);
                                foreach (ArmorMod objMod in objArmor.ArmorMods)
                                {
                                    // <mod>
                                    await objWriter.WriteStartElementAsync("mod").ConfigureAwait(false);
                                    await objWriter.WriteElementStringAsync("name", objMod.Name).ConfigureAwait(false);
                                    if (objMod.Rating > 0)
                                        await objWriter.WriteElementStringAsync("rating", objMod.Rating.ToString(GlobalSettings.InvariantCultureInfo)).ConfigureAwait(false);
                                    // </mod>
                                    await objWriter.WriteEndElementAsync().ConfigureAwait(false);
                                }

                                // </mods>
                                await objWriter.WriteEndElementAsync().ConfigureAwait(false);
                            }

                            if (objArmor.GearChildren.Count > 0)
                                await WriteGear(objWriter, objArmor.GearChildren).ConfigureAwait(false);

                            // </armor>
                            await objWriter.WriteEndElementAsync().ConfigureAwait(false);
                        }

                        // </armors>
                        await objWriter.WriteEndElementAsync().ConfigureAwait(false);
                    }

                    // Export Weapons.
                    if (await chkWeapons.DoThreadSafeFuncAsync(x => x.Checked).ConfigureAwait(false))
                    {
                        // <weapons>
                        await objWriter.WriteStartElementAsync("weapons").ConfigureAwait(false);
                        foreach (Weapon objWeapon in _objCharacter.Weapons)
                        {
                            // Don't attempt to export Cyberware and Gear Weapons since those are handled by those object types. The default Unarmed Attack Weapon should also not be exported.
                            if (objWeapon.Category != "Cyberware" && objWeapon.Category != "Gear" && objWeapon.Name != "Unarmed Attack")
                            {
                                // <weapon>
                                await objWriter.WriteStartElementAsync("weapon").ConfigureAwait(false);
                                await objWriter.WriteElementStringAsync("name", objWeapon.Name).ConfigureAwait(false);

                                // Weapon Accessories.
                                if (objWeapon.WeaponAccessories.Count > 0)
                                {
                                    // <accessories>
                                    await objWriter.WriteStartElementAsync("accessories").ConfigureAwait(false);
                                    foreach (WeaponAccessory objAccessory in objWeapon.WeaponAccessories)
                                    {
                                        // Don't attempt to export items included in the Weapon.
                                        if (!objAccessory.IncludedInWeapon)
                                        {
                                            // <accessory>
                                            await objWriter.WriteStartElementAsync("accessory").ConfigureAwait(false);
                                            await objWriter.WriteElementStringAsync("name", objAccessory.Name).ConfigureAwait(false);
                                            await objWriter.WriteElementStringAsync("mount", objAccessory.Mount).ConfigureAwait(false);
                                            await objWriter.WriteElementStringAsync("extramount", objAccessory.ExtraMount).ConfigureAwait(false);

                                            if (objAccessory.GearChildren.Count > 0)
                                                await WriteGear(objWriter, objAccessory.GearChildren).ConfigureAwait(false);

                                            // </accessory>
                                            await objWriter.WriteEndElementAsync().ConfigureAwait(false);
                                        }
                                    }

                                    // </accessories>
                                    await objWriter.WriteEndElementAsync().ConfigureAwait(false);
                                }

                                // Underbarrel Weapon.
                                if (objWeapon.UnderbarrelWeapons.Count > 0)
                                {
                                    foreach (Weapon objUnderbarrelWeapon in objWeapon.UnderbarrelWeapons)
                                    {
                                        if (!objUnderbarrelWeapon.IncludedInWeapon)
                                            await objWriter.WriteElementStringAsync("underbarrel", objUnderbarrelWeapon.Name).ConfigureAwait(false);
                                    }
                                }

                                // </weapon>
                                await objWriter.WriteEndElementAsync().ConfigureAwait(false);
                            }
                        }

                        // </weapons>
                        await objWriter.WriteEndElementAsync().ConfigureAwait(false);
                    }

                    // Export Gear.
                    if (await chkGear.DoThreadSafeFuncAsync(x => x.Checked).ConfigureAwait(false))
                    {
                        await WriteGear(objWriter, _objCharacter.Gear).ConfigureAwait(false);
                    }

                    // Export Vehicles.
                    if (await chkVehicles.DoThreadSafeFuncAsync(x => x.Checked).ConfigureAwait(false))
                    {
                        // <vehicles>
                        await objWriter.WriteStartElementAsync("vehicles").ConfigureAwait(false);
                        foreach (Vehicle objVehicle in _objCharacter.Vehicles)
                        {
                            bool blnWeapons = false;
                            // <vehicle>
                            await objWriter.WriteStartElementAsync("vehicle").ConfigureAwait(false);
                            await objWriter.WriteElementStringAsync("name", objVehicle.Name).ConfigureAwait(false);
                            if (objVehicle.Mods.Count > 0)
                            {
                                // <mods>
                                await objWriter.WriteStartElementAsync("mods").ConfigureAwait(false);
                                foreach (VehicleMod objVehicleMod in objVehicle.Mods)
                                {
                                    // Only write out the Mods that are not part of the base vehicle.
                                    if (!objVehicleMod.IncludedInVehicle)
                                    {
                                        // <mod>
                                        await objWriter.WriteStartElementAsync("mod").ConfigureAwait(false);
                                        await objWriter.WriteElementStringAsync("name", objVehicleMod.Name).ConfigureAwait(false);
                                        if (objVehicleMod.Rating > 0)
                                            await objWriter.WriteElementStringAsync("rating", objVehicleMod.Rating.ToString(GlobalSettings.InvariantCultureInfo)).ConfigureAwait(false);
                                        // </mod>
                                        await objWriter.WriteEndElementAsync().ConfigureAwait(false);

                                        // See if this is a Weapon Mount with Weapons.
                                        if (objVehicleMod.Weapons.Count > 0)
                                            blnWeapons = true;
                                    }
                                    else
                                    {
                                        // See if this is a Weapon Mount with Weapons.
                                        if (objVehicleMod.Weapons.Count > 0)
                                            blnWeapons = true;
                                    }
                                }

                                // </mods>
                                await objWriter.WriteEndElementAsync().ConfigureAwait(false);
                            }

                            // If there are Weapons, add them.
                            if (blnWeapons)
                            {
                                // <weapons>
                                await objWriter.WriteStartElementAsync("weapons").ConfigureAwait(false);
                                foreach (VehicleMod objVehicleMod in objVehicle.Mods)
                                {
                                    foreach (Weapon objWeapon in objVehicleMod.Weapons)
                                    {
                                        // <weapon>
                                        await objWriter.WriteStartElementAsync("weapon").ConfigureAwait(false);
                                        await objWriter.WriteElementStringAsync("name", objWeapon.Name).ConfigureAwait(false);

                                        // Weapon Accessories.
                                        if (objWeapon.WeaponAccessories.Count > 0)
                                        {
                                            // <accessories>
                                            await objWriter.WriteStartElementAsync("accessories").ConfigureAwait(false);
                                            foreach (WeaponAccessory objAccessory in objWeapon.WeaponAccessories)
                                            {
                                                // Don't attempt to export items included in the Weapon.
                                                if (!objAccessory.IncludedInWeapon)
                                                {
                                                    // <accessory>
                                                    await objWriter.WriteStartElementAsync("accessory").ConfigureAwait(false);
                                                    await objWriter.WriteElementStringAsync("name", objAccessory.Name).ConfigureAwait(false);
                                                    await objWriter.WriteElementStringAsync("mount", objAccessory.Mount).ConfigureAwait(false);
                                                    await objWriter.WriteElementStringAsync("extramount", objAccessory.ExtraMount).ConfigureAwait(false);
                                                    // </accessory>
                                                    await objWriter.WriteEndElementAsync().ConfigureAwait(false);
                                                }
                                            }

                                            // </accessories>
                                            await objWriter.WriteEndElementAsync().ConfigureAwait(false);
                                        }

                                        // Underbarrel Weapon.
                                        if (objWeapon.UnderbarrelWeapons.Count > 0)
                                        {
                                            foreach (Weapon objUnderbarrelWeapon in objWeapon.UnderbarrelWeapons)
                                                await objWriter.WriteElementStringAsync("underbarrel", objUnderbarrelWeapon.Name).ConfigureAwait(false);
                                        }

                                        // </weapon>
                                        await objWriter.WriteEndElementAsync().ConfigureAwait(false);
                                    }
                                }

                                // </weapons>
                                await objWriter.WriteEndElementAsync().ConfigureAwait(false);
                            }

                            // Gear.
                            if (objVehicle.GearChildren.Count > 0)
                            {
                                await WriteGear(objWriter, objVehicle.GearChildren).ConfigureAwait(false);
                            }

                            // </vehicle>
                            await objWriter.WriteEndElementAsync().ConfigureAwait(false);
                        }

                        // </vehicles>
                        await objWriter.WriteEndElementAsync().ConfigureAwait(false);
                    }

                    // </pack>
                    await objWriter.WriteEndElementAsync().ConfigureAwait(false);
                    // </packs>
                    await objWriter.WriteEndElementAsync().ConfigureAwait(false);
                    // </chummer>
                    await objWriter.WriteEndElementAsync().ConfigureAwait(false);

                    await objWriter.WriteEndDocumentAsync().ConfigureAwait(false);
                }
            }

            Program.ShowScrollableMessageBox(this, string.Format(GlobalSettings.CultureInfo, await LanguageManager.GetStringAsync("Message_CreatePACKSKit_SuiteCreated").ConfigureAwait(false), strName),
                                             await LanguageManager.GetStringAsync("MessageTitle_CreatePACKSKit_SuiteCreated").ConfigureAwait(false), MessageBoxButtons.OK, MessageBoxIcon.Information);
            await this.DoThreadSafeAsync(x =>
            {
                x.DialogResult = DialogResult.OK;
                x.Close();
            }).ConfigureAwait(false);
        }

        private void cmdCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        #endregion Control Events

        #region Methods

        /// <summary>
        /// Recursively write out all Gear information since these can be nested pretty deep.
        /// </summary>
        /// <param name="objWriter">XmlWriter to use.</param>
        /// <param name="lstGear">List of Gear to write.</param>
        /// <param name="token">Cancellation token to listen to.</param>
        private static async ValueTask WriteGear(XmlWriter objWriter, IEnumerable<Gear> lstGear, CancellationToken token = default)
        {
            // <gears>
            await objWriter.WriteStartElementAsync("gears", token: token).ConfigureAwait(false);
            foreach (Gear objGear in lstGear)
            {
                if (objGear.IncludedInParent)
                    continue;
                // <gear>
                await objWriter.WriteStartElementAsync("gear", token: token).ConfigureAwait(false);
                await objWriter.WriteStartElementAsync("name", token: token).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(objGear.Extra))
                    await objWriter.WriteAttributeStringAsync("select", objGear.Extra, token: token).ConfigureAwait(false);
                objWriter.WriteValue(objGear.Name);
                await objWriter.WriteEndElementAsync().ConfigureAwait(false);
                await objWriter.WriteElementStringAsync("category", objGear.Category, token: token).ConfigureAwait(false);
                if (objGear.Rating > 0)
                    await objWriter.WriteElementStringAsync("rating", objGear.Rating.ToString(GlobalSettings.InvariantCultureInfo), token: token).ConfigureAwait(false);
                if (objGear.Quantity != 1)
                    await objWriter.WriteElementStringAsync("qty", objGear.Quantity.ToString(GlobalSettings.InvariantCultureInfo), token: token).ConfigureAwait(false);
                if (objGear.Children.Count > 0)
                    await WriteGear(objWriter, objGear.Children, token).ConfigureAwait(false);
                // </gear>
                await objWriter.WriteEndElementAsync().ConfigureAwait(false);
            }
            // </gears>
            await objWriter.WriteEndElementAsync().ConfigureAwait(false);
        }

        #endregion Methods
    }
}
