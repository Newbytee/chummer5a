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
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using System.Xml.XPath;
using Chummer.Annotations;
using Chummer.Backend.Attributes;
using Chummer.Backend.Skills;

// ReSharper disable SpecifyACultureInStringConversionExplicitly

// ReSharper disable once CheckNamespace
namespace Chummer
{
    /// <summary>
    /// An Adept Power.
    /// </summary>
    [HubClassTag("SourceID", true, "Name", "Extra")]
    [DebuggerDisplay("{DisplayName(GlobalSettings.DefaultLanguage)}")]
    public sealed class Power : INotifyMultiplePropertyChanged, IHasInternalId, IHasName, IHasSourceId, IHasXmlDataNode, IHasNotes, IHasSource, IHasLockObject
    {
        private Guid _guiID;
        private Guid _guiSourceID = Guid.Empty;
        private string _strName = string.Empty;
        private string _strExtra = string.Empty;
        private string _strSource = string.Empty;
        private string _strPage = string.Empty;
        private string _strPointsPerLevel = "0";
        private string _strAction = string.Empty;
        private decimal _decExtraPointCost;
        private int _intMaxLevels;
        private bool _blnDiscountedAdeptWay;
        private bool _blnDiscountedGeas;
        private XPathNavigator _nodAdeptWayRequirements;
        private string _strNotes = string.Empty;
        private Color _colNotes = ColorManager.HasNotesColor;
        private string _strAdeptWayDiscount = "0";
        private string _strBonusSource = string.Empty;
        private decimal _decFreePoints;
        private string _strCachedPowerPoints = string.Empty;
        private bool _blnLevelsEnabled;
        private int _intRating = 1;
        private int _cachedLearnedRating;

        #region Constructor, Create, Save, Load, and Print Methods

        public Power(Character objCharacter)
        {
            // Create the GUID for the new Power.
            _guiID = Guid.NewGuid();
            CharacterObject = objCharacter;
            if (objCharacter != null)
            {
                using (objCharacter.LockObject.EnterWriteLock())
                    objCharacter.PropertyChanged += OnCharacterChanged;
                using (objCharacter.Settings.LockObject.EnterWriteLock())
                    objCharacter.Settings.PropertyChanged += OnCharacterSettingsChanged;
                if (objCharacter.Settings.MysAdeptSecondMAGAttribute && objCharacter.IsMysticAdept)
                {
                    MAGAttributeObject = objCharacter.MAGAdept;
                }
                else
                {
                    MAGAttributeObject = objCharacter.MAG;
                }
            }
        }

        public void DeletePower()
        {
            using (LockObject.EnterWriteLock())
            {
                ImprovementManager.RemoveImprovements(CharacterObject, Improvement.ImprovementSource.Power, InternalId);
                CharacterObject.Powers.Remove(this);
            }
            Dispose();
        }

        public async ValueTask DeletePowerAsync(CancellationToken token = default)
        {
            IAsyncDisposable objLocker = await LockObject.EnterWriteLockAsync(token).ConfigureAwait(false);
            try
            {
                await ImprovementManager
                      .RemoveImprovementsAsync(CharacterObject, Improvement.ImprovementSource.Power, InternalId, token)
                      .ConfigureAwait(false);
                await CharacterObject.Powers.RemoveAsync(this, token).ConfigureAwait(false);
            }
            finally
            {
                await objLocker.DisposeAsync().ConfigureAwait(false);
            }

            await DisposeAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Save the object's XML to the XmlWriter.
        /// </summary>
        /// <param name="objWriter">XmlTextWriter to write with.</param>
        public void Save(XmlWriter objWriter)
        {
            if (objWriter == null)
                return;
            using (EnterReadLock.Enter(LockObject))
            {
                objWriter.WriteStartElement("power");
                objWriter.WriteElementString("sourceid", SourceIDString);
                objWriter.WriteElementString("guid", InternalId);
                objWriter.WriteElementString("name", _strName);
                objWriter.WriteElementString("extra", Extra);
                objWriter.WriteElementString("pointsperlevel", _strPointsPerLevel);
                objWriter.WriteElementString("adeptway", _strAdeptWayDiscount);
                objWriter.WriteElementString("action", _strAction);
                objWriter.WriteElementString("rating", _intRating.ToString(GlobalSettings.InvariantCultureInfo));
                objWriter.WriteElementString("extrapointcost",
                                             _decExtraPointCost.ToString(GlobalSettings.InvariantCultureInfo));
                objWriter.WriteElementString("levels", _blnLevelsEnabled.ToString(GlobalSettings.InvariantCultureInfo));
                objWriter.WriteElementString("maxlevels", _intMaxLevels.ToString(GlobalSettings.InvariantCultureInfo));
                objWriter.WriteElementString("discounted",
                                             _blnDiscountedAdeptWay.ToString(GlobalSettings.InvariantCultureInfo));
                objWriter.WriteElementString("discountedgeas",
                                             _blnDiscountedGeas.ToString(GlobalSettings.InvariantCultureInfo));
                objWriter.WriteElementString("bonussource", _strBonusSource);
                objWriter.WriteElementString("freepoints",
                                             _decFreePoints.ToString(GlobalSettings.InvariantCultureInfo));
                objWriter.WriteElementString("source", _strSource);
                objWriter.WriteElementString("page", _strPage);
                if (Bonus != null)
                    objWriter.WriteRaw("<bonus>" + Bonus.InnerXml + "</bonus>");
                else
                    objWriter.WriteElementString("bonus", string.Empty);
                if (_nodAdeptWayRequirements != null)
                    objWriter.WriteRaw("<adeptwayrequires>" + _nodAdeptWayRequirements.InnerXml
                                                            + "</adeptwayrequires>");
                else
                    objWriter.WriteElementString("adeptwayrequires", string.Empty);
                objWriter.WriteStartElement("enhancements");
                foreach (Enhancement objEnhancement in Enhancements)
                {
                    objEnhancement.Save(objWriter);
                }

                objWriter.WriteEndElement();
                objWriter.WriteElementString("notes", _strNotes.CleanOfInvalidUnicodeChars());
                objWriter.WriteElementString("notesColor", ColorTranslator.ToHtml(_colNotes));
                objWriter.WriteEndElement();
            }
        }

        public bool Create(XmlNode objNode, int intRating = 1, XmlNode objBonusNodeOverride = null, bool blnCreateImprovements = true)
        {
            using (LockObject.EnterWriteLock())
            {
                objNode.TryGetStringFieldQuickly("name", ref _strName);
                objNode.TryGetField("id", Guid.TryParse, out _guiSourceID);
                _objCachedMyXmlNode = null;
                _objCachedMyXPathNode = null;
                objNode.TryGetStringFieldQuickly("points", ref _strPointsPerLevel);
                objNode.TryGetStringFieldQuickly("adeptway", ref _strAdeptWayDiscount);
                objNode.TryGetBoolFieldQuickly("levels", ref _blnLevelsEnabled);
                _intRating = intRating;
                if (!objNode.TryGetMultiLineStringFieldQuickly("altnotes", ref _strNotes))
                    objNode.TryGetMultiLineStringFieldQuickly("notes", ref _strNotes);

                string sNotesColor = ColorTranslator.ToHtml(ColorManager.HasNotesColor);
                objNode.TryGetStringFieldQuickly("notesColor", ref sNotesColor);
                _colNotes = ColorTranslator.FromHtml(sNotesColor);
                objNode.TryGetStringFieldQuickly("source", ref _strSource);
                objNode.TryGetStringFieldQuickly("page", ref _strPage);

                if (GlobalSettings.InsertPdfNotesIfAvailable && string.IsNullOrEmpty(Notes))
                {
                    Notes = CommonFunctions.GetBookNotes(objNode, Name, CurrentDisplayName, Source, Page,
                                                         DisplayPage(GlobalSettings.Language), CharacterObject);
                }

                if (!objNode.TryGetInt32FieldQuickly("maxlevel", ref _intMaxLevels))
                {
                    objNode.TryGetInt32FieldQuickly("maxlevels", ref _intMaxLevels);
                }

                objNode.TryGetBoolFieldQuickly("discounted", ref _blnDiscountedAdeptWay);
                objNode.TryGetBoolFieldQuickly("discountedgeas", ref _blnDiscountedGeas);
                objNode.TryGetStringFieldQuickly("bonussource", ref _strBonusSource);
                objNode.TryGetDecFieldQuickly("freepoints", ref _decFreePoints);
                objNode.TryGetDecFieldQuickly("extrapointcost", ref _decExtraPointCost);
                objNode.TryGetStringFieldQuickly("action", ref _strAction);
                Bonus = objNode["bonus"];
                if (objBonusNodeOverride != null)
                    Bonus = objBonusNodeOverride;
                _nodAdeptWayRequirements = objNode["adeptwayrequires"]?.CreateNavigator();
                XmlNode nodEnhancements = objNode["enhancements"];
                if (nodEnhancements != null)
                {
                    using (XmlNodeList xmlEnhancementList = nodEnhancements.SelectNodes("enhancement"))
                    {
                        if (xmlEnhancementList != null)
                        {
                            foreach (XmlNode nodEnhancement in xmlEnhancementList)
                            {
                                Enhancement objEnhancement = new Enhancement(CharacterObject);
                                objEnhancement.Load(nodEnhancement);
                                objEnhancement.Parent = this;
                                Enhancements.Add(objEnhancement);
                            }
                        }
                    }
                }

                if (blnCreateImprovements && Bonus?.HasChildNodes == true)
                {
                    string strOldForce = ImprovementManager.ForcedValue;
                    string strOldSelected = ImprovementManager.SelectedValue;
                    ImprovementManager.ForcedValue = Extra;
                    if (!ImprovementManager.CreateImprovements(CharacterObject, Improvement.ImprovementSource.Power,
                                                               InternalId, Bonus, TotalRating, CurrentDisplayNameShort))
                    {
                        ImprovementManager.ForcedValue = strOldForce;
                        return false;
                    }

                    Extra = ImprovementManager.SelectedValue;
                    ImprovementManager.SelectedValue = strOldSelected;
                    ImprovementManager.ForcedValue = strOldForce;
                }

                if (TotalMaximumLevels < Rating)
                {
                    Rating = TotalMaximumLevels;
                }

                return true;
            }
        }

        private SourceString _objCachedSourceDetail;

        public SourceString SourceDetail
        {
            get
            {
                using (EnterReadLock.Enter(LockObject))
                {
                    if (_objCachedSourceDetail == default)
                        _objCachedSourceDetail = SourceString.GetSourceString(Source,
                                                                              DisplayPage(GlobalSettings.Language),
                                                                              GlobalSettings.Language,
                                                                              GlobalSettings.CultureInfo,
                                                                              CharacterObject);
                    return _objCachedSourceDetail;
                }
            }
        }

        public async ValueTask<SourceString> GetSourceDetailAsync(CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
            {
                if (_objCachedSourceDetail == default)
                    _objCachedSourceDetail = await SourceString.GetSourceStringAsync(Source,
                        await DisplayPageAsync(GlobalSettings.Language, token).ConfigureAwait(false),
                        GlobalSettings.Language,
                        GlobalSettings.CultureInfo,
                        CharacterObject, token).ConfigureAwait(false);
                return _objCachedSourceDetail;
            }
        }

        /// <summary>
        /// Load the Power from the XmlNode.
        /// </summary>
        /// <param name="objNode">XmlNode to load.</param>
        public void Load(XmlNode objNode)
        {
            using (LockObject.EnterWriteLock())
            {
                if (!objNode.TryGetField("guid", Guid.TryParse, out _guiID))
                {
                    _guiID = Guid.NewGuid();
                }

                objNode.TryGetStringFieldQuickly("name", ref _strName);
                _objCachedMyXmlNode = null;
                _objCachedMyXPathNode = null;
                if (!objNode.TryGetGuidFieldQuickly("sourceid", ref _guiSourceID))
                {
                    if (this.GetNodeXPath().TryGetField("id", Guid.TryParse, out _guiSourceID))
                    {
                        _objCachedMyXmlNode = null;
                        _objCachedMyXPathNode = null;
                    }
                    else
                    {
                        string strPowerName = Name;
                        int intPos = strPowerName.IndexOf('(');
                        if (intPos != -1)
                            strPowerName = strPowerName.Substring(0, intPos - 1);
                        XPathNavigator xmlPower = CharacterObject.LoadDataXPath("powers.xml")
                                                                 .SelectSingleNode(
                                                                     "/chummer/powers/power[starts-with(./name, "
                                                                     + strPowerName.CleanXPath() + ")]");
                        if (xmlPower.TryGetField("id", Guid.TryParse, out _guiSourceID))
                        {
                            _objCachedMyXmlNode = null;
                            _objCachedMyXPathNode = null;
                        }
                    }
                }

                Extra = objNode["extra"]?.InnerText ?? string.Empty;
                _strPointsPerLevel = objNode["pointsperlevel"]?.InnerText;
                objNode.TryGetStringFieldQuickly("action", ref _strAction);
                _strAdeptWayDiscount = objNode["adeptway"]?.InnerText;
                if (string.IsNullOrEmpty(_strAdeptWayDiscount))
                {
                    string strPowerName = Name;
                    int intPos = strPowerName.IndexOf('(');
                    if (intPos != -1)
                        strPowerName = strPowerName.Substring(0, intPos - 1);
                    _strAdeptWayDiscount = CharacterObject.LoadDataXPath("powers.xml")
                                                          .SelectSingleNode(
                                                              "/chummer/powers/power[starts-with(./name, "
                                                              + strPowerName.CleanXPath() + ")]/adeptway")?.Value
                                           ?? string.Empty;
                }

                objNode.TryGetInt32FieldQuickly("rating", ref _intRating);
                objNode.TryGetBoolFieldQuickly("levels", ref _blnLevelsEnabled);
                if (!objNode.TryGetInt32FieldQuickly("maxlevel", ref _intMaxLevels))
                {
                    objNode.TryGetInt32FieldQuickly("maxlevels", ref _intMaxLevels);
                }

                objNode.TryGetBoolFieldQuickly("discounted", ref _blnDiscountedAdeptWay);
                objNode.TryGetBoolFieldQuickly("discountedgeas", ref _blnDiscountedGeas);
                objNode.TryGetStringFieldQuickly("bonussource", ref _strBonusSource);
                objNode.TryGetDecFieldQuickly("freepoints", ref _decFreePoints);
                objNode.TryGetDecFieldQuickly("extrapointcost", ref _decExtraPointCost);
                objNode.TryGetStringFieldQuickly("source", ref _strSource);
                objNode.TryGetStringFieldQuickly("page", ref _strPage);
                objNode.TryGetMultiLineStringFieldQuickly("notes", ref _strNotes);

                string sNotesColor = ColorTranslator.ToHtml(ColorManager.HasNotesColor);
                objNode.TryGetStringFieldQuickly("notesColor", ref sNotesColor);
                _colNotes = ColorTranslator.FromHtml(sNotesColor);

                Bonus = objNode["bonus"];
                if (objNode["adeptway"] != null)
                {
                    _nodAdeptWayRequirements = objNode["adeptwayrequires"]?.CreateNavigator()
                                               ?? this.GetNodeXPath()
                                                      ?.SelectSingleNodeAndCacheExpression("adeptwayrequires");
                }

                if (Name != "Improved Reflexes" && Name.StartsWith("Improved Reflexes", StringComparison.Ordinal))
                {
                    XmlNode objXmlPower = CharacterObject.LoadData("powers.xml")
                                                         .SelectSingleNode(
                                                             "/chummer/powers/power[starts-with(./name,\"Improved Reflexes\")]");
                    if (objXmlPower != null
                        && int.TryParse(Name.TrimStartOnce("Improved Reflexes", true).Trim(), out int intTemp))
                    {
                        Create(objXmlPower, intTemp, null, false);
                        objNode.TryGetMultiLineStringFieldQuickly("notes", ref _strNotes);

                        sNotesColor = ColorTranslator.ToHtml(ColorManager.HasNotesColor);
                        objNode.TryGetStringFieldQuickly("notesColor", ref sNotesColor);
                        _colNotes = ColorTranslator.FromHtml(sNotesColor);
                    }
                }
                else
                {
                    XmlNodeList nodEnhancements = objNode.SelectNodes("enhancements/enhancement");
                    if (nodEnhancements != null)
                    {
                        foreach (XmlNode nodEnhancement in nodEnhancements)
                        {
                            Enhancement objEnhancement = new Enhancement(CharacterObject);
                            objEnhancement.Load(nodEnhancement);
                            objEnhancement.Parent = this;
                            Enhancements.Add(objEnhancement);
                        }
                    }
                }

                //TODO: Seems that the MysAd Second Attribute house rule gets accidentally enabled sometimes?
                if (Rating > TotalMaximumLevels)
                {
                    Utils.BreakIfDebug();
                    Rating = TotalMaximumLevels;
                }
                else if (Rating + FreeLevels > TotalMaximumLevels)
                {
                    Utils.BreakIfDebug();
                    TotalRating = TotalMaximumLevels;
                }
            }
        }

        /// <summary>
        /// Print the object's XML to the XmlWriter.
        /// </summary>
        /// <param name="objWriter">XmlTextWriter to write with.</param>
        /// <param name="objCulture">Culture in which to print.</param>
        /// <param name="strLanguageToPrint">Language in which to print</param>
        /// <param name="token">Cancellation token to listen to.</param>
        public async ValueTask Print(XmlWriter objWriter, CultureInfo objCulture, string strLanguageToPrint, CancellationToken token = default)
        {
            if (objWriter == null)
                return;

            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
            {
                // <power>
                XmlElementWriteHelper objBaseElement
                    = await objWriter.StartElementAsync("power", token).ConfigureAwait(false);
                try
                {
                    await objWriter.WriteElementStringAsync("guid", InternalId, token).ConfigureAwait(false);
                    await objWriter.WriteElementStringAsync("sourceid", SourceIDString, token).ConfigureAwait(false);
                    await objWriter
                          .WriteElementStringAsync(
                              "name", await DisplayNameShortAsync(strLanguageToPrint, token).ConfigureAwait(false),
                              token).ConfigureAwait(false);
                    await objWriter
                          .WriteElementStringAsync(
                              "fullname", await DisplayNameAsync(strLanguageToPrint, token).ConfigureAwait(false),
                              token).ConfigureAwait(false);
                    await objWriter
                          .WriteElementStringAsync(
                              "extra",
                              await CharacterObject.TranslateExtraAsync(Extra, strLanguageToPrint, token: token)
                                                   .ConfigureAwait(false), token).ConfigureAwait(false);
                    await objWriter
                          .WriteElementStringAsync("pointsperlevel", PointsPerLevel.ToString(objCulture), token)
                          .ConfigureAwait(false);
                    await objWriter.WriteElementStringAsync("adeptway", AdeptWayDiscount.ToString(objCulture), token)
                                   .ConfigureAwait(false);
                    await objWriter
                          .WriteElementStringAsync("rating", LevelsEnabled ? TotalRating.ToString(objCulture) : "0",
                                                   token).ConfigureAwait(false);
                    await objWriter.WriteElementStringAsync("totalpoints", PowerPoints.ToString(objCulture), token)
                                   .ConfigureAwait(false);
                    await objWriter
                          .WriteElementStringAsync(
                              "action", await DisplayActionMethodAsync(strLanguageToPrint, token).ConfigureAwait(false),
                              token).ConfigureAwait(false);
                    await objWriter
                          .WriteElementStringAsync(
                              "source",
                              await CharacterObject.LanguageBookShortAsync(Source, strLanguageToPrint, token)
                                                   .ConfigureAwait(false), token).ConfigureAwait(false);
                    await objWriter
                          .WriteElementStringAsync(
                              "page", await DisplayPageAsync(strLanguageToPrint, token).ConfigureAwait(false), token)
                          .ConfigureAwait(false);
                    if (GlobalSettings.PrintNotes)
                        await objWriter.WriteElementStringAsync("notes", Notes, token).ConfigureAwait(false);
                    // <enhancements>
                    XmlElementWriteHelper objEnhancementsElement
                        = await objWriter.StartElementAsync("enhancements", token).ConfigureAwait(false);
                    try
                    {
                        foreach (Enhancement objEnhancement in Enhancements)
                        {
                            await objEnhancement.Print(objWriter, strLanguageToPrint, token).ConfigureAwait(false);
                        }
                    }
                    finally
                    {
                        // </enhancements>
                        await objEnhancementsElement.DisposeAsync().ConfigureAwait(false);
                    }
                }
                finally
                {
                    // </power>
                    await objBaseElement.DisposeAsync().ConfigureAwait(false);
                }
            }
        }

        #endregion Constructor, Create, Save, Load, and Print Methods

        #region Properties

        /// <summary>
        /// The Character object being used by the Power.
        /// </summary>
        public Character CharacterObject { get; }

        private CharacterAttrib _objMAGAttribute;

        /// <summary>
        /// MAG Attribute this skill primarily depends on
        /// </summary>
        public CharacterAttrib MAGAttributeObject
        {
            get
            {
                using (EnterReadLock.Enter(LockObject))
                    return _objMAGAttribute;
            }
            private set
            {
                using (EnterReadLock.Enter(LockObject))
                {
                    value?.LockObject.EnterReadLock();
                    try
                    {
                        CharacterAttrib objOldValue = Interlocked.Exchange(ref _objMAGAttribute, value);
                        objOldValue?.LockObject.EnterReadLock();
                        try
                        {
                            if (objOldValue == value)
                                return;
                            Utils.RunWithoutThreadLock(
                                () =>
                                {
                                    if (objOldValue == null)
                                        return;
                                    using (objOldValue.LockObject.EnterWriteLock())
                                        objOldValue.PropertyChanged -= OnLinkedAttributeChanged;
                                },
                                () =>
                                {
                                    if (value == null)
                                        return;
                                    using (value.LockObject.EnterWriteLock())
                                        value.PropertyChanged += OnLinkedAttributeChanged;
                                });
                        }
                        finally
                        {
                            objOldValue?.LockObject.ExitReadLock();
                        }
                    }
                    finally
                    {
                        value?.LockObject.ExitReadLock();
                    }

                    OnPropertyChanged();
                }
            }
        }

        private Skill _objBoostedSkill;

        public Skill BoostedSkill
        {
            get
            {
                using (EnterReadLock.Enter(LockObject))
                    return _objBoostedSkill;
            }
            private set
            {
                using (EnterReadLock.Enter(LockObject))
                {
                    value?.LockObject.EnterReadLock();
                    try
                    {
                        Skill objOldValue = Interlocked.Exchange(ref _objBoostedSkill, value);
                        objOldValue?.LockObject.EnterReadLock();
                        try
                        {
                            if (objOldValue == value)
                                return;
                            Utils.RunWithoutThreadLock(
                                () =>
                                {
                                    if (objOldValue == null)
                                        return;
                                    using (objOldValue.LockObject.EnterWriteLock())
                                        objOldValue.PropertyChanged -= OnBoostedSkillChanged;
                                },
                                () =>
                                {
                                    if (value == null)
                                        return;
                                    using (value.LockObject.EnterWriteLock())
                                        value.PropertyChanged += OnBoostedSkillChanged;
                                });
                        }
                        finally
                        {
                            objOldValue?.LockObject.ExitReadLock();
                        }
                    }
                    finally
                    {
                        value?.LockObject.ExitReadLock();
                    }

                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Internal identifier which will be used to identify this Power in the Improvement system.
        /// </summary>
        public string InternalId
        {
            get
            {
                using (EnterReadLock.Enter(LockObject))
                    return _guiID.ToString("D", GlobalSettings.InvariantCultureInfo);
            }
        }

        /// <summary>
        /// Identifier of the object within data files.
        /// </summary>
        public Guid SourceID
        {
            get
            {
                using (EnterReadLock.Enter(LockObject))
                    return _guiSourceID;
            }
        }

        /// <summary>
        /// String-formatted identifier of the <inheritdoc cref="SourceID"/> from the data files.
        /// </summary>
        public string SourceIDString
        {
            get
            {
                using (EnterReadLock.Enter(LockObject))
                    return _guiSourceID.ToString("D", GlobalSettings.InvariantCultureInfo);
            }
        }

        /// <summary>
        /// Power's name.
        /// </summary>
        public string Name
        {
            get
            {
                using (EnterReadLock.Enter(LockObject))
                    return _strName;
            }
            set
            {
                using (EnterReadLock.Enter(LockObject))
                {
                    string strOldValue = Interlocked.Exchange(ref _strName, value);
                    if (strOldValue == value)
                        return;
                    if (strOldValue == "Improved Ability (skill)")
                    {
                        BoostedSkill = null;
                    }
                    else if (value == "Improved Ability (skill)")
                    {
                        BoostedSkill = CharacterObject.SkillsSection.GetActiveSkill(Extra);
                    }

                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Extra information that should be applied to the name, like a linked CharacterAttribute.
        /// </summary>
        public string Extra
        {
            get
            {
                using (EnterReadLock.Enter(LockObject))
                    return _strExtra;
            }
            set
            {
                using (EnterReadLock.Enter(LockObject))
                {
                    if (Interlocked.Exchange(ref _strExtra, value) == value)
                        return;
                    if (Name == "Improved Ability (skill)")
                        BoostedSkill = CharacterObject.SkillsSection.GetActiveSkill(value);
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// The Enhancements currently applied to the Power.
        /// </summary>
        public TaggedObservableCollection<Enhancement> Enhancements { get; } = new TaggedObservableCollection<Enhancement>();

        /// <summary>
        /// The name of the object as it should be displayed on printouts (translated name only).
        /// </summary>
        public string CurrentDisplayNameShort => DisplayNameShort(GlobalSettings.Language);

        public ValueTask<string> GetCurrentDisplayNameShortAsync(CancellationToken token = default) =>
            DisplayNameAsync(GlobalSettings.Language, token);

        /// <summary>
        /// The name of the object as it should be displayed on printouts (translated name only).
        /// </summary>
        public string DisplayNameShort(string strLanguage)
        {
            using (EnterReadLock.Enter(LockObject))
            {
                string strReturn = Name;

                if (!strLanguage.Equals(GlobalSettings.DefaultLanguage, StringComparison.OrdinalIgnoreCase))
                {
                    strReturn = this.GetNodeXPath(strLanguage)?.SelectSingleNodeAndCacheExpression("translate")?.Value
                                ?? Name;
                }

                return strReturn;
            }
        }

        /// <summary>
        /// The name of the object as it should be displayed on printouts (translated name only).
        /// </summary>
        public async ValueTask<string> DisplayNameShortAsync(string strLanguage, CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
            {
                string strReturn = Name;

                if (!strLanguage.Equals(GlobalSettings.DefaultLanguage, StringComparison.OrdinalIgnoreCase))
                {
                    XPathNavigator objNode
                        = await this.GetNodeXPathAsync(strLanguage, token: token).ConfigureAwait(false);
                    strReturn = objNode != null
                        ? (await objNode.SelectSingleNodeAndCacheExpressionAsync("translate", token: token)
                                        .ConfigureAwait(false))?.Value ?? Name
                        : Name;
                }

                return strReturn;
            }
        }

        /// <summary>
        /// The translated name of the Power (Name + any Extra text).
        /// </summary>
        public string CurrentDisplayName => DisplayName(GlobalSettings.Language);

        public ValueTask<string> GetCurrentDisplayNameAsync(CancellationToken token = default) =>
            DisplayNameAsync(GlobalSettings.Language, token);

        /// <summary>
        /// The translated name of the Power (Name + any Extra text).
        /// </summary>
        public string DisplayName(string strLanguage)
        {
            using (EnterReadLock.Enter(LockObject))
            {
                string strReturn = DisplayNameShort(strLanguage);

                if (!string.IsNullOrEmpty(Extra))
                {
                    // Attempt to retrieve the CharacterAttribute name.
                    strReturn += LanguageManager.GetString("String_Space", strLanguage) + '('
                        + CharacterObject.TranslateExtra(Extra, strLanguage) + ')';
                }

                return strReturn;
            }
        }

        /// <summary>
        /// The translated name of the Power (Name + any Extra text).
        /// </summary>
        public async ValueTask<string> DisplayNameAsync(string strLanguage, CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
            {
                string strReturn = await DisplayNameShortAsync(strLanguage, token).ConfigureAwait(false);

                if (!string.IsNullOrEmpty(Extra))
                {
                    // Attempt to retrieve the CharacterAttribute name.
                    strReturn
                        += await LanguageManager.GetStringAsync("String_Space", strLanguage, token: token)
                                                .ConfigureAwait(false) + '(' + await CharacterObject
                            .TranslateExtraAsync(Extra, strLanguage, token: token).ConfigureAwait(false) + ')';
                }

                return strReturn;
            }
        }

        /// <summary>
        /// Power Point cost per level of the Power.
        /// </summary>
        public decimal PointsPerLevel
        {
            get
            {
                using (EnterReadLock.Enter(LockObject))
                {
                    decimal decReturn = Convert.ToDecimal(_strPointsPerLevel, GlobalSettings.InvariantCultureInfo);
                    return decReturn;
                }
            }
            set
            {
                string strNewValue = value.ToString(GlobalSettings.InvariantCultureInfo);
                using (EnterReadLock.Enter(LockObject))
                {
                    if (Interlocked.Exchange(ref _strPointsPerLevel, strNewValue) == strNewValue)
                        return;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// An additional cost on top of the power's PointsPerLevel.
        /// Example: Improved Reflexes is properly speaking Rating + 0.5, but the math for that gets weird.
        /// </summary>
        public decimal ExtraPointCost
        {
            get
            {
                using (EnterReadLock.Enter(LockObject))
                    return _decExtraPointCost;
            }
            set
            {
                using (EnterReadLock.Enter(LockObject))
                {
                    if (_decExtraPointCost == value)
                        return;
                    using (LockObject.EnterWriteLock())
                        _decExtraPointCost = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Power Point discount for an Adept Way.
        /// </summary>
        public decimal AdeptWayDiscount
        {
            get
            {
                using (EnterReadLock.Enter(LockObject))
                {
                    decimal decReturn = Convert.ToDecimal(_strAdeptWayDiscount, GlobalSettings.InvariantCultureInfo);
                    return decReturn;
                }
            }
            set
            {
                string strNewValue = value.ToString(GlobalSettings.InvariantCultureInfo);
                using (EnterReadLock.Enter(LockObject))
                {
                    if (Interlocked.Exchange(ref _strAdeptWayDiscount, strNewValue) == strNewValue)
                        return;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Calculated Power Point cost per level of the Power (including discounts).
        /// </summary>
        public decimal CalculatedPointsPerLevel => PointsPerLevel;

        /// <summary>
        /// Calculate the discount that is applied to the Power.
        /// </summary>
        private decimal Discount
        {
            get
            {
                using (EnterReadLock.Enter(LockObject))
                    return DiscountedAdeptWay ? AdeptWayDiscount : 0;
            }
        }

        /// <summary>
        /// The current 'paid' Rating of the Power.
        /// </summary>
        public int Rating
        {
            get
            {
                using (EnterReadLock.Enter(LockObject))
                {
                    //TODO: This isn't super safe, but it's more reliable than checking it at load as improvement effects like Essence Loss take effect after powers are loaded. Might need another solution.
                    return _intRating = Math.Min(_intRating, TotalMaximumLevels);
                }
            }
            set
            {
                using (EnterReadLock.Enter(LockObject))
                {
                    if (Interlocked.Exchange(ref _intRating, value) == value)
                        return;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// The current 'paid' Rating of the Power.
        /// </summary>
        public async ValueTask<int> GetRatingAsync(CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
            {
                //TODO: This isn't super safe, but it's more reliable than checking it at load as improvement effects like Essence Loss take effect after powers are loaded. Might need another solution.
                int intTotalMax = await GetTotalMaximumLevelsAsync(token).ConfigureAwait(false);
                if (_intRating <= intTotalMax)
                    return _intRating;
                return _intRating = intTotalMax;
            }
        }

        /// <summary>
        /// The current Rating of the Power, including any Free Levels.
        /// </summary>
        public int TotalRating
        {
            get
            {
                using (EnterReadLock.Enter(LockObject))
                    return Math.Min(Rating + FreeLevels, TotalMaximumLevels);
            }
            set
            {
                using (EnterReadLock.Enter(LockObject))
                    Rating = Math.Max(value - FreeLevels, 0);
            }
        }

        /// <summary>
        /// The current Rating of the Power, including any Free Levels.
        /// </summary>
        public async ValueTask<int> GetTotalRatingAsync(CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
            {
                return Math.Min(
                    await GetRatingAsync(token).ConfigureAwait(false)
                    + await GetFreeLevelsAsync(token).ConfigureAwait(false),
                    await GetTotalMaximumLevelsAsync(token).ConfigureAwait(false));
            }
        }

        public bool DoesNotHaveFreeLevels => FreeLevels == 0;

        private int _intCachedFreeLevels = int.MinValue;

        /// <summary>
        /// Free levels of the power.
        /// </summary>
        public int FreeLevels
        {
            get
            {
                using (EnterReadLock.Enter(LockObject))
                {
                    if (_intCachedFreeLevels != int.MinValue)
                        return _intCachedFreeLevels;

                    decimal decExtraCost = FreePoints;
                    // Rating does not include free levels from improvements, and those free levels can be used to buy the first level of a power so that Qi Foci, so need to check for those first
                    int intReturn = ImprovementManager
                                    .GetCachedImprovementListForValueOf(CharacterObject,
                                                                        Improvement.ImprovementType
                                                                            .AdeptPowerFreeLevels,
                                                                        Name)
                                    .Sum(objImprovement => objImprovement.UniqueName == Extra,
                                         objImprovement => objImprovement.Rating);
                    // The power has an extra cost, so free PP from things like Qi Foci have to be charged first.
                    if (Rating + intReturn == 0 && ExtraPointCost > 0)
                    {
                        decExtraCost -= (PointsPerLevel + ExtraPointCost);
                        if (decExtraCost >= 0)
                        {
                            ++intReturn;
                        }

                        for (decimal i = decExtraCost; i >= 1; --i)
                        {
                            ++intReturn;
                        }
                    }
                    else if (PointsPerLevel == 0)
                    {
                        Utils.BreakIfDebug();
                        // power costs no PP, just return free levels
                    }
                    //Either the first level of the power has been paid for with PP, or the power doesn't have an extra cost.
                    else
                    {
                        for (decimal i = decExtraCost; i >= PointsPerLevel; i -= PointsPerLevel)
                        {
                            ++intReturn;
                        }
                    }

                    return _intCachedFreeLevels = Math.Min(intReturn, MAGAttributeObject?.TotalValue ?? 0);
                }
            }
        }

        /// <summary>
        /// Free levels of the power.
        /// </summary>
        public async ValueTask<int> GetFreeLevelsAsync(CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
            {
                if (_intCachedFreeLevels != int.MinValue)
                    return _intCachedFreeLevels;

                decimal decExtraCost = FreePoints;
                // Rating does not include free levels from improvements, and those free levels can be used to buy the first level of a power so that Qi Foci, so need to check for those first
                int intReturn = (await ImprovementManager
                                       .GetCachedImprovementListForValueOfAsync(CharacterObject,
                                           Improvement.ImprovementType.AdeptPowerFreeLevels,
                                           Name, token: token).ConfigureAwait(false))
                    .Sum(objImprovement => objImprovement.UniqueName == Extra,
                         objImprovement => objImprovement.Rating, token);
                // The power has an extra cost, so free PP from things like Qi Foci have to be charged first.
                if (Rating + intReturn == 0 && ExtraPointCost > 0)
                {
                    decExtraCost -= (PointsPerLevel + ExtraPointCost);
                    if (decExtraCost >= 0)
                    {
                        ++intReturn;
                    }

                    for (decimal i = decExtraCost; i >= 1; --i)
                    {
                        ++intReturn;
                    }
                }
                else if (PointsPerLevel == 0)
                {
                    Utils.BreakIfDebug();
                    // power costs no PP, just return free levels
                }
                //Either the first level of the power has been paid for with PP, or the power doesn't have an extra cost.
                else
                {
                    for (decimal i = decExtraCost; i >= PointsPerLevel; i -= PointsPerLevel)
                    {
                        ++intReturn;
                    }
                }

                return _intCachedFreeLevels
                    = Math.Min(intReturn,
                               MAGAttributeObject != null
                                   ? await MAGAttributeObject.GetTotalValueAsync(token).ConfigureAwait(false)
                                   : 0);
            }
        }

        private decimal _decCachedPowerPoints = decimal.MinValue;

        /// <summary>
        /// Total number of Power Points the Power costs.
        /// </summary>
        public decimal PowerPoints
        {
            get
            {
                using (EnterReadLock.Enter(LockObject))
                {
                    if (_decCachedPowerPoints != decimal.MinValue)
                        return _decCachedPowerPoints;

                    if (Rating == 0 || !LevelsEnabled && FreeLevels > 0)
                    {
                        return _decCachedPowerPoints = 0;
                    }

                    decimal decReturn;
                    if (FreeLevels * PointsPerLevel >= FreePoints)
                    {
                        decReturn = Rating * PointsPerLevel;
                        decReturn += ExtraPointCost;
                    }
                    else
                    {
                        decReturn = TotalRating * PointsPerLevel + ExtraPointCost;
                        decReturn -= FreePoints;
                    }

                    decReturn -= Discount;
                    return _decCachedPowerPoints = Math.Max(decReturn, 0.0m);
                }
            }
        }

        /// <summary>
        /// Total number of Power Points the Power costs.
        /// </summary>
        public async ValueTask<decimal> GetPowerPointsAsync(CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
            {
                if (_decCachedPowerPoints != decimal.MinValue)
                    return _decCachedPowerPoints;

                int intFreeLevels = await GetFreeLevelsAsync(token).ConfigureAwait(false);
                int intRating = await GetRatingAsync(token).ConfigureAwait(false);
                if (intRating == 0 || !LevelsEnabled && intFreeLevels > 0)
                {
                    return _decCachedPowerPoints = 0;
                }

                decimal decReturn;
                decimal decFreePoints = await GetFreePointsAsync(token).ConfigureAwait(false);
                if (intFreeLevels * PointsPerLevel >= decFreePoints)
                {
                    decReturn = intRating * PointsPerLevel;
                    decReturn += ExtraPointCost;
                }
                else
                {
                    decReturn = await GetTotalRatingAsync(token).ConfigureAwait(false) * PointsPerLevel
                                + ExtraPointCost;
                    decReturn -= decFreePoints;
                }

                decReturn -= Discount;
                return _decCachedPowerPoints = Math.Max(decReturn, 0.0m);
            }
        }

        public string DisplayPoints
        {
            get
            {
                using (EnterReadLock.Enter(LockObject))
                {
                    if (string.IsNullOrEmpty(_strCachedPowerPoints))
                        _strCachedPowerPoints = PowerPoints.ToString(GlobalSettings.CultureInfo);
                    return _strCachedPowerPoints;
                }
            }
        }

        public async ValueTask<string> GetDisplayPointsAsync(CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
            {
                if (string.IsNullOrEmpty(_strCachedPowerPoints))
                    _strCachedPowerPoints
                        = (await GetPowerPointsAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.CultureInfo);
                return _strCachedPowerPoints;
            }
        }

        /// <summary>
        /// Bonus source.
        /// </summary>
        public string BonusSource
        {
            get
            {
                using (EnterReadLock.Enter(LockObject))
                    return _strBonusSource;
            }
            set
            {
                using (EnterReadLock.Enter(LockObject))
                {
                    if (Interlocked.Exchange(ref _strBonusSource, value) == value)
                        return;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Free Power Points that apply to the Power. Calculated as Improvement Rating * 0.25.
        /// Typically used for Qi Foci.
        /// </summary>
        public decimal FreePoints
        {
            get
            {
                using (EnterReadLock.Enter(LockObject))
                {
                    int intRating = ImprovementManager
                                    .GetCachedImprovementListForValueOf(CharacterObject,
                                                                        Improvement.ImprovementType
                                                                            .AdeptPowerFreePoints,
                                                                        Name)
                                    .Sum(objImprovement => objImprovement.UniqueName == Extra,
                                         objImprovement => objImprovement.Rating);
                    return intRating * 0.25m;
                }
            }
        }

        /// <summary>
        /// Free Power Points that apply to the Power. Calculated as Improvement Rating * 0.25.
        /// Typically used for Qi Foci.
        /// </summary>
        public async ValueTask<decimal> GetFreePointsAsync(CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
            {
                int intRating = (await ImprovementManager
                                       .GetCachedImprovementListForValueOfAsync(CharacterObject,
                                           Improvement.ImprovementType.AdeptPowerFreePoints,
                                           Name, token: token).ConfigureAwait(false))
                    .Sum(objImprovement => objImprovement.UniqueName == Extra,
                         objImprovement => objImprovement.Rating, token);
                return intRating * 0.25m;
            }
        }

        /// <summary>
        /// Sourcebook.
        /// </summary>
        public string Source
        {
            get
            {
                using (EnterReadLock.Enter(LockObject))
                    return _strSource;
            }
            set
            {
                using (EnterReadLock.Enter(LockObject))
                {
                    if (Interlocked.Exchange(ref _strSource, value) == value)
                        return;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Sourcebook Page Number.
        /// </summary>
        public string Page
        {
            get
            {
                using (EnterReadLock.Enter(LockObject))
                    return _strPage;
            }
            set
            {
                using (EnterReadLock.Enter(LockObject))
                {
                    if (Interlocked.Exchange(ref _strPage, value) == value)
                        return;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Sourcebook Page Number using a given language file.
        /// Returns Page if not found or the string is empty.
        /// </summary>
        /// <param name="strLanguage">Language file keyword to use.</param>
        /// <returns></returns>
        public string DisplayPage(string strLanguage)
        {
            using (EnterReadLock.Enter(LockObject))
            {
                if (strLanguage.Equals(GlobalSettings.DefaultLanguage, StringComparison.OrdinalIgnoreCase))
                    return Page;
                string s = this.GetNodeXPath(strLanguage)?.SelectSingleNodeAndCacheExpression("altpage")?.Value ?? Page;
                return !string.IsNullOrWhiteSpace(s) ? s : Page;
            }
        }

        /// <summary>
        /// Sourcebook Page Number using a given language file.
        /// Returns Page if not found or the string is empty.
        /// </summary>
        /// <param name="strLanguage">Language file keyword to use.</param>
        /// <param name="token">Cancellation token to listen to.</param>
        /// <returns></returns>
        public async ValueTask<string> DisplayPageAsync(string strLanguage, CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
            {
                if (strLanguage.Equals(GlobalSettings.DefaultLanguage, StringComparison.OrdinalIgnoreCase))
                    return Page;
                XPathNavigator objNode = await this.GetNodeXPathAsync(strLanguage, token: token).ConfigureAwait(false);
                string s = objNode != null
                    ? (await objNode.SelectSingleNodeAndCacheExpressionAsync("altpage", token: token)
                                    .ConfigureAwait(false))?.Value ?? Page
                    : Page;
                return !string.IsNullOrWhiteSpace(s) ? s : Page;
            }
        }

        /// <summary>
        /// Bonus node from the XML file.
        /// </summary>
        public XmlNode Bonus
        {
            get
            {
                using (EnterReadLock.Enter(LockObject))
                    return _xmlBonus;
            }
            set
            {
                using (EnterReadLock.Enter(LockObject))
                {
                    if (Interlocked.Exchange(ref _xmlBonus, value) == value)
                        return;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Whether or not Levels enabled for the Power.
        /// </summary>
        public bool LevelsEnabled
        {
            get
            {
                using (EnterReadLock.Enter(LockObject))
                    return _blnLevelsEnabled;
            }
            set
            {
                using (EnterReadLock.Enter(LockObject))
                {
                    if (_blnLevelsEnabled == value)
                        return;
                    using (LockObject.EnterWriteLock())
                        _blnLevelsEnabled = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Maximum Level for the Power.
        /// </summary>
        public int MaxLevels
        {
            get
            {
                using (EnterReadLock.Enter(LockObject))
                    return _intMaxLevels;
            }
            set
            {
                using (EnterReadLock.Enter(LockObject))
                {
                    if (Interlocked.Exchange(ref _intMaxLevels, value) == value)
                        return;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Whether or not the Power Cost is discounted by 50% from Adept Way.
        /// </summary>
        public bool DiscountedAdeptWay
        {
            get
            {
                using (EnterReadLock.Enter(LockObject))
                    return _blnDiscountedAdeptWay;
            }
            set
            {
                using (EnterReadLock.Enter(LockObject))
                {
                    if (_blnDiscountedAdeptWay == value)
                        return;
                    using (LockObject.EnterWriteLock())
                        _blnDiscountedAdeptWay = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Whether or not the Power Cost is discounted by 25% from Geas.
        /// </summary>
        public bool DiscountedGeas
        {
            get
            {
                using (EnterReadLock.Enter(LockObject))
                    return _blnDiscountedGeas;
            }
            set
            {
                using (EnterReadLock.Enter(LockObject))
                {
                    if (_blnDiscountedGeas == value)
                        return;
                    using (LockObject.EnterWriteLock())
                        _blnDiscountedGeas = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Notes.
        /// </summary>
        public string Notes
        {
            get
            {
                using (EnterReadLock.Enter(LockObject))
                    return _strNotes;
            }
            set
            {
                using (EnterReadLock.Enter(LockObject))
                {
                    if (Interlocked.Exchange(ref _strNotes, value) == value)
                        return;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Forecolor to use for Notes in treeviews.
        /// </summary>
        public Color NotesColor
        {
            get
            {
                using (EnterReadLock.Enter(LockObject))
                    return _colNotes;
            }
            set
            {
                using (EnterReadLock.Enter(LockObject))
                {
                    if (_colNotes == value)
                        return;
                    using (LockObject.EnterWriteLock())
                        _colNotes = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Action.
        /// </summary>
        public string Action
        {
            get
            {
                using (EnterReadLock.Enter(LockObject))
                    return _strAction;
            }
            set
            {
                using (EnterReadLock.Enter(LockObject))
                {
                    if (Interlocked.Exchange(ref _strAction, value) == value)
                        return;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Translated Action.
        /// </summary>
        public string DisplayAction => DisplayActionMethod(GlobalSettings.Language);

        public Task<string> GetDisplayActionAsync(CancellationToken token = default) =>
            DisplayActionMethodAsync(GlobalSettings.Language, token);

        /// <summary>
        /// Translated Action.
        /// </summary>
        public string DisplayActionMethod(string strLanguage)
        {
            switch (Action)
            {
                case "Auto":
                    return LanguageManager.GetString("String_ActionAutomatic", strLanguage);

                case "Free":
                    return LanguageManager.GetString("String_ActionFree", strLanguage);

                case "Simple":
                    return LanguageManager.GetString("String_ActionSimple", strLanguage);

                case "Complex":
                    return LanguageManager.GetString("String_ActionComplex", strLanguage);

                case "Interrupt":
                    return LanguageManager.GetString("String_ActionInterrupt", strLanguage);

                case "Special":
                    return LanguageManager.GetString("String_SpellDurationSpecial", strLanguage);
            }

            return string.Empty;
        }

        /// <summary>
        /// Translated Action.
        /// </summary>
        public Task<string> DisplayActionMethodAsync(string strLanguage, CancellationToken token = default)
        {
            switch (Action)
            {
                case "Auto":
                    return LanguageManager.GetStringAsync("String_ActionAutomatic", strLanguage, token: token);

                case "Free":
                    return LanguageManager.GetStringAsync("String_ActionFree", strLanguage, token: token);

                case "Simple":
                    return LanguageManager.GetStringAsync("String_ActionSimple", strLanguage, token: token);

                case "Complex":
                    return LanguageManager.GetStringAsync("String_ActionComplex", strLanguage, token: token);

                case "Interrupt":
                    return LanguageManager.GetStringAsync("String_ActionInterrupt", strLanguage, token: token);

                case "Special":
                    return LanguageManager.GetStringAsync("String_SpellDurationSpecial", strLanguage, token: token);
            }

            return Task.FromResult(string.Empty);
        }

        public Color PreferredColor
        {
            get
            {
                using (EnterReadLock.Enter(LockObject))
                    return !string.IsNullOrEmpty(Notes)
                        ? ColorManager.GenerateCurrentModeColor(NotesColor)
                        : ColorManager.WindowText;
            }
        }

        #endregion Properties

        #region Complex Properties

        public int TotalMaximumLevels
        {
            get
            {
                using (EnterReadLock.Enter(LockObject))
                {
                    if (!LevelsEnabled)
                        return 1;
                    int intReturn = MaxLevels;
                    if (intReturn == 0)
                    {
                        // if unspecified, max rating = MAG
                        intReturn = MAGAttributeObject?.TotalValue ?? 0;
                    }

                    if (BoostedSkill != null)
                    {
                        // +1 at the end so that division of 2 always rounds up, and integer division by 2 is significantly less expensive than decimal/double division
                        intReturn = Math.Min(intReturn, (BoostedSkill.LearnedRating + 1) / 2);
                        if (CharacterObject.Settings.IncreasedImprovedAbilityMultiplier)
                        {
                            intReturn += BoostedSkill.LearnedRating;
                        }
                    }

                    if (!CharacterObject.IgnoreRules)
                    {
                        intReturn = Math.Min(intReturn, MAGAttributeObject?.TotalValue ?? 0);
                    }

                    return intReturn;
                }
            }
        }

        public async ValueTask<int> GetTotalMaximumLevelsAsync(CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
            {
                if (!LevelsEnabled)
                    return 1;
                int intReturn = MaxLevels;
                if (intReturn == 0)
                {
                    // if unspecified, max rating = MAG
                    intReturn = MAGAttributeObject != null
                        ? await MAGAttributeObject.GetTotalValueAsync(token).ConfigureAwait(false)
                        : 0;
                }

                if (BoostedSkill != null)
                {
                    int intBoostedSkillLearnedRating
                        = await BoostedSkill.GetLearnedRatingAsync(token).ConfigureAwait(false);
                    // +1 at the end so that division of 2 always rounds up, and integer division by 2 is significantly less expensive than decimal/double division
                    intReturn = Math.Min(intReturn, (intBoostedSkillLearnedRating + 1) / 2);
                    if (await (await CharacterObject.GetSettingsAsync(token).ConfigureAwait(false))
                              .GetIncreasedImprovedAbilityMultiplierAsync(token).ConfigureAwait(false))
                    {
                        intReturn += intBoostedSkillLearnedRating;
                    }
                }

                if (!await CharacterObject.GetIgnoreRulesAsync(token).ConfigureAwait(false))
                {
                    intReturn = Math.Min(
                        intReturn,
                        MAGAttributeObject != null
                            ? await MAGAttributeObject.GetTotalValueAsync(token).ConfigureAwait(false)
                            : 0);
                }

                return intReturn;
            }
        }

        /// <summary>
        /// Whether the power can be discounted due to presence of an Adept Way.
        /// </summary>
        public bool AdeptWayDiscountEnabled
        {
            get
            {
                using (EnterReadLock.Enter(LockObject))
                {
                    if (AdeptWayDiscount == 0)
                    {
                        return false;
                    }

                    bool blnReturn = false;
                    //If the Adept Way Requirements node is missing OR the Adept Way Requirements node doesn't have magicianswayforbids, check for the magician's way discount.
                    if (_nodAdeptWayRequirements?.SelectSingleNodeAndCacheExpression("magicianswayforbids") == null)
                    {
                        blnReturn = ImprovementManager
                                    .GetCachedImprovementListForValueOf(
                                        CharacterObject, Improvement.ImprovementType.MagiciansWayDiscount).Count > 0;
                    }

                    if (!blnReturn && _nodAdeptWayRequirements?.HasChildren == true)
                    {
                        blnReturn = _nodAdeptWayRequirements.RequirementsMet(CharacterObject);
                    }

                    return blnReturn;
                }
            }
        }

        public void RefreshDiscountedAdeptWay(bool blnAdeptWayDiscountEnabled)
        {
            using (EnterReadLock.Enter(LockObject))
            {
                if (DiscountedAdeptWay && !blnAdeptWayDiscountEnabled)
                {
                    using (LockObject.EnterWriteLock())
                        DiscountedAdeptWay = false;
                }
            }
        }

        private static readonly PropertyDependencyGraph<Power> s_PowerDependencyGraph =
            new PropertyDependencyGraph<Power>(
                new DependencyGraphNode<string, Power>(nameof(DisplayPoints),
                    new DependencyGraphNode<string, Power>(nameof(PowerPoints),
                        new DependencyGraphNode<string, Power>(nameof(TotalRating),
                            new DependencyGraphNode<string, Power>(nameof(Rating)),
                            new DependencyGraphNode<string, Power>(nameof(FreeLevels),
                                new DependencyGraphNode<string, Power>(nameof(FreePoints)),
                                new DependencyGraphNode<string, Power>(nameof(ExtraPointCost)),
                                new DependencyGraphNode<string, Power>(nameof(PointsPerLevel))
                            ),
                            new DependencyGraphNode<string, Power>(nameof(TotalMaximumLevels),
                                new DependencyGraphNode<string, Power>(nameof(LevelsEnabled)),
                                new DependencyGraphNode<string, Power>(nameof(MaxLevels))
                            )
                        ),
                        new DependencyGraphNode<string, Power>(nameof(Rating)),
                        new DependencyGraphNode<string, Power>(nameof(LevelsEnabled)),
                        new DependencyGraphNode<string, Power>(nameof(FreeLevels)),
                        new DependencyGraphNode<string, Power>(nameof(PointsPerLevel)),
                        new DependencyGraphNode<string, Power>(nameof(FreePoints)),
                        new DependencyGraphNode<string, Power>(nameof(ExtraPointCost)),
                        new DependencyGraphNode<string, Power>(nameof(Discount),
                            new DependencyGraphNode<string, Power>(nameof(DiscountedAdeptWay)),
                            new DependencyGraphNode<string, Power>(nameof(AdeptWayDiscount))
                        )
                    )
                ),
                new DependencyGraphNode<string, Power>(nameof(ToolTip),
                    new DependencyGraphNode<string, Power>(nameof(Rating)),
                    new DependencyGraphNode<string, Power>(nameof(PointsPerLevel))
                ),
                new DependencyGraphNode<string, Power>(nameof(DoesNotHaveFreeLevels),
                    new DependencyGraphNode<string, Power>(nameof(FreeLevels))
                ),
                new DependencyGraphNode<string, Power>(nameof(AdeptWayDiscountEnabled),
                    new DependencyGraphNode<string, Power>(nameof(AdeptWayDiscount))
                ),
                new DependencyGraphNode<string, Power>(nameof(CurrentDisplayName),
                    new DependencyGraphNode<string, Power>(nameof(DisplayName),
                        new DependencyGraphNode<string, Power>(nameof(Extra)),
                        new DependencyGraphNode<string, Power>(nameof(DisplayNameShort),
                            new DependencyGraphNode<string, Power>(nameof(Name))
                        )
                    )
                )
            );

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        public void OnPropertyChanged([CallerMemberName] string strPropertyName = null)
        {
            this.OnMultiplePropertyChanged(strPropertyName);
        }

        public void OnMultiplePropertyChanged(IReadOnlyCollection<string> lstPropertyNames)
        {
            using (EnterReadLock.Enter(LockObject))
            {
                HashSet<string> setNamesOfChangedProperties = null;
                try
                {
                    foreach (string strPropertyName in lstPropertyNames)
                    {
                        if (setNamesOfChangedProperties == null)
                            setNamesOfChangedProperties
                                = s_PowerDependencyGraph.GetWithAllDependents(this, strPropertyName, true);
                        else
                        {
                            foreach (string strLoopChangedProperty in s_PowerDependencyGraph
                                         .GetWithAllDependentsEnumerable(
                                             this, strPropertyName))
                                setNamesOfChangedProperties.Add(strLoopChangedProperty);
                        }
                    }

                    if (setNamesOfChangedProperties == null || setNamesOfChangedProperties.Count == 0)
                        return;

                    using (LockObject.EnterWriteLock())
                    {
                        if (setNamesOfChangedProperties.Contains(nameof(DisplayPoints)))
                            _strCachedPowerPoints = string.Empty;
                        if (setNamesOfChangedProperties.Contains(nameof(FreeLevels)))
                            _intCachedFreeLevels = int.MinValue;
                        if (setNamesOfChangedProperties.Contains(nameof(PowerPoints)))
                            _decCachedPowerPoints = decimal.MinValue;

                        // If the Bonus contains "Rating", remove the existing Improvements and create new ones.
                        if (setNamesOfChangedProperties.Contains(nameof(TotalRating))
                            && Bonus?.InnerXml.Contains("Rating") == true)
                        {
                            // We cannot actually go with setting a rating here because of a load of technical debt involving bonus nodes feeding into `Value` indirectly through a parser
                            // that uses `Rating` instead of using only `Rating` and having the parser work off of whatever is in the `Rating` field
                            // TODO: Solve this bad code
                            ImprovementManager.RemoveImprovements(CharacterObject, Improvement.ImprovementSource.Power,
                                                                  InternalId);
                            int intTotalRating = TotalRating;
                            if (intTotalRating > 0)
                            {
                                ImprovementManager.ForcedValue = Extra;
                                ImprovementManager.CreateImprovements(CharacterObject,
                                                                      Improvement.ImprovementSource.Power,
                                                                      InternalId, Bonus, intTotalRating,
                                                                      CurrentDisplayNameShort);
                            }
                        }

                        if (setNamesOfChangedProperties.Contains(nameof(AdeptWayDiscountEnabled)))
                        {
                            RefreshDiscountedAdeptWay(AdeptWayDiscountEnabled);
                        }
                    }

                    if (PropertyChanged != null)
                    {
                        Utils.RunOnMainThread(() =>
                        {
                            if (PropertyChanged != null)
                            {
                                // ReSharper disable once AccessToModifiedClosure
                                foreach (string strPropertyToChange in setNamesOfChangedProperties)
                                {
                                    PropertyChanged.Invoke(this, new PropertyChangedEventArgs(strPropertyToChange));
                                }
                            }
                        });
                    }
                }
                finally
                {
                    if (setNamesOfChangedProperties != null)
                        Utils.StringHashSetPool.Return(ref setNamesOfChangedProperties);
                }
            }
        }

        private void OnLinkedAttributeChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e?.PropertyName == nameof(CharacterAttrib.TotalValue))
                OnPropertyChanged(nameof(TotalMaximumLevels));
        }

        private void OnBoostedSkillChanged(object sender, PropertyChangedEventArgs e)
        {
            using (EnterReadLock.Enter(LockObject))
            {
                if (e?.PropertyName == nameof(Skill.LearnedRating) && sender is Skill objSkill
                                                                   && BoostedSkill.LearnedRating != _cachedLearnedRating
                                                                   && _cachedLearnedRating != TotalMaximumLevels)
                {
                    using (LockObject.EnterWriteLock())
                        _cachedLearnedRating = objSkill.LearnedRating;
                    OnPropertyChanged(nameof(TotalMaximumLevels));
                }
            }
        }

        private void OnCharacterChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Character.IsMysticAdept))
            {
                MAGAttributeObject = CharacterObject.Settings.MysAdeptSecondMAGAttribute && CharacterObject.IsMysticAdept
                    ? CharacterObject.MAGAdept
                    : CharacterObject.MAG;
            }
        }

        private void OnCharacterSettingsChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(CharacterSettings.MysAdeptSecondMAGAttribute):
                    {
                        MAGAttributeObject = CharacterObject.Settings.MysAdeptSecondMAGAttribute && CharacterObject.IsMysticAdept
                            ? CharacterObject.MAGAdept
                            : CharacterObject.MAG;
                        break;
                    }
                case nameof(CharacterSettings.IncreasedImprovedAbilityMultiplier):
                    {
                        MAGAttributeObject = CharacterObject.Settings.MysAdeptSecondMAGAttribute && CharacterObject.IsMysticAdept
                            ? CharacterObject.MAGAdept
                            : CharacterObject.MAG;
                        break;
                    }
            }
        }

        private XmlNode _objCachedMyXmlNode;
        private string _strCachedXmlNodeLanguage = string.Empty;

        public async Task<XmlNode> GetNodeCoreAsync(bool blnSync, string strLanguage, CancellationToken token = default)
        {
            // ReSharper disable once MethodHasAsyncOverload
            using (blnSync ? EnterReadLock.Enter(LockObject, token) : await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
            {
                XmlNode objReturn = _objCachedMyXmlNode;
                if (objReturn != null && strLanguage == _strCachedXmlNodeLanguage
                                      && !GlobalSettings.LiveCustomData)
                    return objReturn;
                objReturn = (blnSync
                        // ReSharper disable once MethodHasAsyncOverload
                        ? CharacterObject.LoadData("powers.xml", strLanguage, token: token)
                        : await CharacterObject.LoadDataAsync("powers.xml", strLanguage, token: token)
                                               .ConfigureAwait(false))
                    .SelectSingleNode(SourceID == Guid.Empty
                                          ? "/chummer/powers/power[name = "
                                            + Name.CleanXPath() + ']'
                                          : "/chummer/powers/power[id = "
                                            + SourceIDString.CleanXPath() + " or id = "
                                            + SourceIDString.ToUpperInvariant()
                                                            .CleanXPath()
                                            + ']');
                _objCachedMyXmlNode = objReturn;
                _strCachedXmlNodeLanguage = strLanguage;
                return objReturn;
            }
        }

        private XPathNavigator _objCachedMyXPathNode;
        private string _strCachedXPathNodeLanguage = string.Empty;
        private XmlNode _xmlBonus;

        public async Task<XPathNavigator> GetNodeXPathCoreAsync(bool blnSync, string strLanguage, CancellationToken token = default)
        {
            // ReSharper disable once MethodHasAsyncOverload
            using (blnSync ? EnterReadLock.Enter(LockObject, token) : await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
            {
                XPathNavigator objReturn = _objCachedMyXPathNode;
                if (objReturn != null && strLanguage == _strCachedXPathNodeLanguage
                                      && !GlobalSettings.LiveCustomData)
                    return objReturn;
                objReturn = (blnSync
                        // ReSharper disable once MethodHasAsyncOverload
                        ? CharacterObject.LoadDataXPath("powers.xml", strLanguage, token: token)
                        : await CharacterObject.LoadDataXPathAsync("powers.xml", strLanguage, token: token)
                                               .ConfigureAwait(false))
                    .SelectSingleNode(SourceID == Guid.Empty
                                          ? "/chummer/powers/power[name = "
                                            + Name.CleanXPath() + ']'
                                          : "/chummer/powers/power[id = "
                                            + SourceIDString.CleanXPath() + " or id = "
                                            + SourceIDString.ToUpperInvariant()
                                                            .CleanXPath()
                                            + ']');
                _objCachedMyXPathNode = objReturn;
                _strCachedXPathNodeLanguage = strLanguage;
                return objReturn;
            }
        }

        /// <summary>
        /// ToolTip that shows how the Power is calculating its Modified Rating.
        /// </summary>
        public string ToolTip
        {
            get
            {
                string strSpace = LanguageManager.GetString("String_Space");
                using (EnterReadLock.Enter(LockObject))
                using (new FetchSafelyFromPool<StringBuilder>(Utils.StringBuilderPool, out StringBuilder sbdModifier))
                {
                    sbdModifier.Append(LanguageManager.GetString("String_Rating")).Append(strSpace).Append('(')
                               .Append(Rating.ToString(GlobalSettings.CultureInfo)).Append(strSpace).Append('×')
                               .Append(strSpace).Append(PointsPerLevel.ToString(GlobalSettings.CultureInfo))
                               .Append(')');
                    foreach (Improvement objImprovement in ImprovementManager
                                                           .GetCachedImprovementListForValueOf(
                                                               CharacterObject, Improvement.ImprovementType.AdeptPower,
                                                               Name).Where(objImprovement =>
                                                                               objImprovement.UniqueName == Extra))
                    {
                        sbdModifier.Append(strSpace).Append('+').Append(strSpace)
                                   .Append(CharacterObject.GetObjectName(objImprovement)).Append(strSpace).Append('(')
                                   .Append(objImprovement.Rating.ToString(GlobalSettings.CultureInfo)).Append(')');
                    }

                    return sbdModifier.ToString();
                }
            }
        }

        /// <summary>
        /// ToolTip that shows how the Power is calculating its Modified Rating.
        /// </summary>
        public async ValueTask<string> GetToolTipAsync(CancellationToken token = default)
        {
            string strSpace = await LanguageManager.GetStringAsync("String_Space", token: token).ConfigureAwait(false);
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
            using (new FetchSafelyFromPool<StringBuilder>(Utils.StringBuilderPool, out StringBuilder sbdModifier))
            {
                sbdModifier.Append(await LanguageManager.GetStringAsync("String_Rating", token: token)
                                                        .ConfigureAwait(false)).Append(strSpace).Append('(')
                           .Append((await GetRatingAsync(token).ConfigureAwait(false)).ToString(
                                       GlobalSettings.CultureInfo)).Append(strSpace)
                           .Append('×')
                           .Append(strSpace).Append(PointsPerLevel.ToString(GlobalSettings.CultureInfo))
                           .Append(')');
                foreach (Improvement objImprovement in (await ImprovementManager
                                                              .GetCachedImprovementListForValueOfAsync(
                                                                  CharacterObject,
                                                                  Improvement.ImprovementType.AdeptPower,
                                                                  Name, token: token).ConfigureAwait(false)).Where(
                             objImprovement =>
                                 objImprovement.UniqueName == Extra))
                {
                    sbdModifier.Append(strSpace).Append('+').Append(strSpace)
                               .Append(await CharacterObject.GetObjectNameAsync(objImprovement, token: token)
                                                            .ConfigureAwait(false)).Append(strSpace).Append('(')
                               .Append(objImprovement.Rating.ToString(GlobalSettings.CultureInfo)).Append(')');
                }

                return sbdModifier.ToString();
            }
        }

        #endregion Complex Properties

        public void SetSourceDetail(Control sourceControl)
        {
            using (EnterReadLock.Enter(LockObject))
            {
                if (_objCachedSourceDetail.Language != GlobalSettings.Language)
                    _objCachedSourceDetail = default;
                SourceDetail.SetControl(sourceControl);
            }
        }

        public async Task SetSourceDetailAsync(Control sourceControl, CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
            {
                if (_objCachedSourceDetail.Language != GlobalSettings.Language)
                    _objCachedSourceDetail = default;
                await SourceDetail.SetControlAsync(sourceControl, token).ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            using (LockObject.EnterWriteLock())
            {
                if (CharacterObject != null)
                {
                    using (CharacterObject.LockObject.EnterWriteLock())
                        CharacterObject.PropertyChanged -= OnCharacterChanged;
                    using (CharacterObject.Settings.LockObject.EnterWriteLock())
                        CharacterObject.Settings.PropertyChanged -= OnCharacterSettingsChanged;
                }

                MAGAttributeObject = null;
                BoostedSkill = null;
                Enhancements.Dispose();
            }

            LockObject.Dispose();
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            IAsyncDisposable objLocker = await LockObject.EnterWriteLockAsync().ConfigureAwait(false);
            try
            {
                if (CharacterObject != null)
                {
                    IAsyncDisposable objLocker2 = await CharacterObject.LockObject.EnterWriteLockAsync().ConfigureAwait(false);
                    try
                    {
                        CharacterObject.PropertyChanged -= OnCharacterChanged;
                    }
                    finally
                    {
                        await objLocker2.DisposeAsync().ConfigureAwait(false);
                    }
                    objLocker2 = await CharacterObject.Settings.LockObject.EnterWriteLockAsync().ConfigureAwait(false);
                    try
                    {
                        CharacterObject.Settings.PropertyChanged -= OnCharacterSettingsChanged;
                    }
                    finally
                    {
                        await objLocker2.DisposeAsync().ConfigureAwait(false);
                    }
                }

                MAGAttributeObject = null;
                BoostedSkill = null;
                await Enhancements.DisposeAsync().ConfigureAwait(false);
            }
            finally
            {
                await objLocker.DisposeAsync().ConfigureAwait(false);
            }

            await LockObject.DisposeAsync().ConfigureAwait(false);
        }

        /// <inheritdoc />
        public AsyncFriendlyReaderWriterLock LockObject { get; } = new AsyncFriendlyReaderWriterLock();
    }
}
