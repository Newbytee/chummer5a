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
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.XPath;

namespace Chummer.Backend.Skills
{
    public class KnowledgeSkill : Skill
    {
        private ReadOnlyDictionary<string, string> _dicCategoriesSkillMap;  //Categories to their attribute

        private IReadOnlyDictionary<string, string> CategoriesSkillMap
        {
            get
            {
                using (EnterReadLock.Enter(LockObject))
                {
                    if (GlobalSettings.LiveCustomData)
                    {
                        XPathNodeIterator lstXmlSkills = CharacterObject.LoadDataXPath("skills.xml")
                                                                        .SelectAndCacheExpression(
                                                                            "/chummer/knowledgeskills/skill");
                        Dictionary<string, string> dicReturn = new Dictionary<string, string>(lstXmlSkills.Count);
                        foreach (XPathNavigator objXmlSkill in lstXmlSkills)
                        {
                            string strCategory = objXmlSkill.SelectSingleNodeAndCacheExpression("category")?.Value;
                            if (!string.IsNullOrWhiteSpace(strCategory))
                            {
                                dicReturn[strCategory] =
                                    objXmlSkill.SelectSingleNodeAndCacheExpression("attribute")?.Value;
                            }
                        }

                        return _dicCategoriesSkillMap = new ReadOnlyDictionary<string, string>(dicReturn);
                    }
                    if (_dicCategoriesSkillMap == null)
                    {
                        using (EnterReadLock.Enter(LockObject))
                        {
                            XPathNodeIterator lstXmlSkills = CharacterObject.LoadDataXPath("skills.xml")
                                                                            .SelectAndCacheExpression(
                                                                                "/chummer/knowledgeskills/skill");
                            Dictionary<string, string> dicReturn = new Dictionary<string, string>(lstXmlSkills.Count);
                            foreach (XPathNavigator objXmlSkill in lstXmlSkills)
                            {
                                string strCategory = objXmlSkill.SelectSingleNodeAndCacheExpression("category")?.Value;
                                if (!string.IsNullOrWhiteSpace(strCategory))
                                {
                                    dicReturn[strCategory] =
                                        objXmlSkill.SelectSingleNodeAndCacheExpression("attribute")?.Value;
                                }
                            }

                            return _dicCategoriesSkillMap = new ReadOnlyDictionary<string, string>(dicReturn);
                        }
                    }

                    return _dicCategoriesSkillMap;
                }
            }
        }

        private async ValueTask<IReadOnlyDictionary<string, string>> GetCategoriesSkillMapAsync(CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
            {
                if (GlobalSettings.LiveCustomData)
                {
                    XPathNodeIterator lstXmlSkills = await (await CharacterObject.LoadDataXPathAsync("skills.xml", token: token).ConfigureAwait(false))
                                                           .SelectAndCacheExpressionAsync(
                                                               "/chummer/knowledgeskills/skill", token).ConfigureAwait(false);
                    Dictionary<string, string> dicReturn = new Dictionary<string, string>(lstXmlSkills.Count);
                    foreach (XPathNavigator objXmlSkill in lstXmlSkills)
                    {
                        string strCategory = (await objXmlSkill.SelectSingleNodeAndCacheExpressionAsync("category", token).ConfigureAwait(false))?.Value;
                        if (!string.IsNullOrWhiteSpace(strCategory))
                        {
                            dicReturn[strCategory] =
                                (await objXmlSkill.SelectSingleNodeAndCacheExpressionAsync("attribute", token).ConfigureAwait(false))?.Value;
                        }
                    }

                    return _dicCategoriesSkillMap = new ReadOnlyDictionary<string, string>(dicReturn);
                }

                if (_dicCategoriesSkillMap == null)
                {
                    using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
                    {
                        XPathNodeIterator lstXmlSkills = await (await CharacterObject.LoadDataXPathAsync("skills.xml", token: token).ConfigureAwait(false))
                                                               .SelectAndCacheExpressionAsync(
                                                                   "/chummer/knowledgeskills/skill", token).ConfigureAwait(false);
                        Dictionary<string, string> dicReturn = new Dictionary<string, string>(lstXmlSkills.Count);
                        foreach (XPathNavigator objXmlSkill in lstXmlSkills)
                        {
                            string strCategory = (await objXmlSkill.SelectSingleNodeAndCacheExpressionAsync("category", token).ConfigureAwait(false))?.Value;
                            if (!string.IsNullOrWhiteSpace(strCategory))
                            {
                                dicReturn[strCategory] =
                                    (await objXmlSkill.SelectSingleNodeAndCacheExpressionAsync("attribute", token).ConfigureAwait(false))?.Value;
                            }
                        }

                        return _dicCategoriesSkillMap = new ReadOnlyDictionary<string, string>(dicReturn);
                    }
                }

                return _dicCategoriesSkillMap;
            }
        }

        public override bool IsKnowledgeSkill => true;

        public override bool AllowDelete
        {
            get
            {
                using (EnterReadLock.Enter(LockObject))
                    return (!ForcedName || FreeBase + FreeKarma + RatingModifiers(Attribute) <= 0) && !IsNativeLanguage;
            }
        }

        public override async ValueTask<bool> GetAllowDeleteAsync(CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
                return (!ForcedName || await GetFreeBaseAsync(token).ConfigureAwait(false)
                           + await GetFreeKarmaAsync(token).ConfigureAwait(false)
                           + await RatingModifiersAsync(Attribute, token: token).ConfigureAwait(false) <= 0)
                       && !await GetIsNativeLanguageAsync(token).ConfigureAwait(false);
        }

        public override bool AllowNameChange
        {
            get
            {
                using (EnterReadLock.Enter(LockObject))
                    return !ForcedName && (AllowUpgrade || IsNativeLanguage) &&
                           (!CharacterObject.Created || (Karma == 0 && Base == 0 && !IsNativeLanguage));
            }
        }

        public override async ValueTask<bool> GetAllowNameChangeAsync(CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
            {
                bool blnIsNativeLanguage = await GetIsNativeLanguageAsync(token).ConfigureAwait(false);
                return !ForcedName && (blnIsNativeLanguage || await GetAllowUpgradeAsync(token).ConfigureAwait(false))
                                   &&
                                   (!await CharacterObject.GetCreatedAsync(token).ConfigureAwait(false)
                                    || (await GetKarmaAsync(token).ConfigureAwait(false) == 0
                                        && await GetBaseAsync(token).ConfigureAwait(false) == 0
                                        && !blnIsNativeLanguage));
            }
        }

        public override bool AllowTypeChange
        {
            get
            {
                using (EnterReadLock.Enter(LockObject))
                    return (AllowNameChange || string.IsNullOrWhiteSpace(Type)) && !IsNativeLanguage;
            }
        }

        public override async ValueTask<bool> GetAllowTypeChangeAsync(CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
                return (await GetAllowNameChangeAsync(token).ConfigureAwait(false)
                        || string.IsNullOrWhiteSpace(await GetTypeAsync(token).ConfigureAwait(false)))
                       && !await GetIsNativeLanguageAsync(token).ConfigureAwait(false);
        }

        private string _strType = string.Empty;

        private int _intIsNativeLanguage;

        public bool ForcedName { get; }

        public KnowledgeSkill(Character objCharacter) : base(objCharacter)
        {
            if (objCharacter == null)
                throw new ArgumentNullException(nameof(objCharacter));
            DefaultAttribute = "LOG";
        }

        public KnowledgeSkill(Character objCharacter, string strForcedName, bool blnAllowUpgrade) : this(objCharacter)
        {
            WritableName = strForcedName;
            ForcedName = true;
            _blnAllowUpgrade = blnAllowUpgrade;
        }

        private bool _blnAllowUpgrade = true;

        /// <summary>
        /// Is the skill allowed to be upgraded through karma or points?
        /// </summary>
        public bool AllowUpgrade
        {
            get
            {
                using (EnterReadLock.Enter(LockObject))
                    return _blnAllowUpgrade && !IsNativeLanguage;
            }
            set
            {
                using (EnterReadLock.Enter(LockObject))
                {
                    if (_blnAllowUpgrade == value)
                        return;
                    using (LockObject.EnterWriteLock())
                    {
                        _blnAllowUpgrade = value;
                        OnPropertyChanged();
                    }
                }
            }
        }

        /// <summary>
        /// Is the skill allowed to be upgraded through karma or points?
        /// </summary>
        public async ValueTask<bool> GetAllowUpgradeAsync(CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
                return _blnAllowUpgrade && !await GetIsNativeLanguageAsync(token).ConfigureAwait(false);
        }

        public string WritableName
        {
            get => CurrentDisplayName;
            set
            {
                using (EnterReadLock.Enter(LockObject))
                {
                    if (ForcedName)
                        return;
                    if (string.Equals(CurrentDisplayName, value, StringComparison.Ordinal))
                        return;
                    using (LockObject.EnterWriteLock())
                    {
                        LoadSkillFromData(value);
                        Name = value;
                    }
                    OnPropertyChanged();
                }
            }
        }

        public ValueTask<string> GetWritableNameAsync(CancellationToken token = default) => GetCurrentDisplayNameAsync(token);

        public async ValueTask SetWritableNameAsync(string value, CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
            {
                if (ForcedName)
                    return;
                if (string.Equals(await GetCurrentDisplayNameAsync(token).ConfigureAwait(false), value, StringComparison.Ordinal))
                    return;
                IAsyncDisposable objLocker = await LockObject.EnterWriteLockAsync(token).ConfigureAwait(false);
                try
                {
                    await LoadSkillFromDataAsync(value, token).ConfigureAwait(false);
                    await SetNameAsync(value, token).ConfigureAwait(false);
                }
                finally
                {
                    await objLocker.DisposeAsync().ConfigureAwait(false);
                }
                OnPropertyChanged(nameof(WritableName));
            }
        }

        private void LoadSkillFromData(string strInputSkillName)
        {
            using (LockObject.EnterWriteLock())
            {
                string strSkillName = GetSkillNameFromData(strInputSkillName);
                XPathNavigator xmlSkillNode = CharacterObject.LoadDataXPath("skills.xml")
                    .SelectSingleNode("/chummer/knowledgeskills/skill[name = " + strSkillName.CleanXPath() + ']');

                if (xmlSkillNode == null)
                {
                    SkillId = Guid.Empty;
                    return;
                }

                SkillId = xmlSkillNode.TryGetField("id", Guid.TryParse, out Guid guidTemp)
                    ? guidTemp
                    : Guid.Empty;

                string strCategory = xmlSkillNode.SelectSingleNodeAndCacheExpression("category")?.Value;

                if (!string.IsNullOrEmpty(strCategory))
                {
                    Type = strCategory;
                }

                string strAttribute = xmlSkillNode.SelectSingleNodeAndCacheExpression("attribute")?.Value;

                if (!string.IsNullOrEmpty(strAttribute))
                {
                    DefaultAttribute = CharacterObject.GetAttribute(strAttribute) != null ? strAttribute : "LOG";
                }
            }
        }

        private async ValueTask LoadSkillFromDataAsync(string strInputSkillName, CancellationToken token = default)
        {
            IAsyncDisposable objLocker = await LockObject.EnterWriteLockAsync(token).ConfigureAwait(false);
            try
            {
                string strSkillName = await GetSkillNameFromDataAsync(strInputSkillName, token).ConfigureAwait(false);
                XPathNavigator xmlSkillNode = (await CharacterObject.LoadDataXPathAsync("skills.xml", token: token).ConfigureAwait(false))
                                                             .SelectSingleNode(
                                                                 "/chummer/knowledgeskills/skill[name = "
                                                                 + strSkillName.CleanXPath() + ']');

                if (xmlSkillNode == null)
                {
                    await SetSkillIdAsync(Guid.Empty, token).ConfigureAwait(false);
                    return;
                }

                await SetSkillIdAsync(xmlSkillNode.TryGetField("id", Guid.TryParse, out Guid guidTemp)
                                          ? guidTemp
                                          : Guid.Empty, token).ConfigureAwait(false);

                string strCategory = (await xmlSkillNode.SelectSingleNodeAndCacheExpressionAsync("category", token).ConfigureAwait(false))?.Value;

                if (!string.IsNullOrEmpty(strCategory))
                {
                    await SetTypeAsync(strCategory, token).ConfigureAwait(false);
                }

                string strAttribute = (await xmlSkillNode.SelectSingleNodeAndCacheExpressionAsync("attribute", token).ConfigureAwait(false))?.Value;

                if (!string.IsNullOrEmpty(strAttribute))
                {
                    await SetDefaultAttributeAsync(
                        await CharacterObject.GetAttributeAsync(strAttribute, token: token).ConfigureAwait(false) != null
                            ? strAttribute
                            : "LOG", token).ConfigureAwait(false);
                }
            }
            finally
            {
                await objLocker.DisposeAsync().ConfigureAwait(false);
            }
        }

        private string GetSkillNameFromData(string strInputSkillName)
        {
            if (GlobalSettings.Language.Equals(GlobalSettings.DefaultLanguage, StringComparison.OrdinalIgnoreCase))
            {
                return strInputSkillName;
            }

            using (EnterReadLock.Enter(CharacterObject.LockObject))
            {
                XPathNavigator xmlSkillTranslationNode = CharacterObject.LoadDataXPath("skills.xml")
                    .SelectSingleNode("/chummer/knowledgeskills/skill[translate = " + strInputSkillName.CleanXPath() +
                                      ']');

                if (xmlSkillTranslationNode == null)
                {
                    return CharacterObject.ReverseTranslateExtra(strInputSkillName, GlobalSettings.Language,
                        "skills.xml");
                }

                return xmlSkillTranslationNode.SelectSingleNodeAndCacheExpression("name")?.Value ?? strInputSkillName;
            }
        }

        private async ValueTask<string> GetSkillNameFromDataAsync(string strInputSkillName, CancellationToken token = default)
        {
            if (GlobalSettings.Language.Equals(GlobalSettings.DefaultLanguage, StringComparison.OrdinalIgnoreCase))
            {
                return strInputSkillName;
            }

            using (await EnterReadLock.EnterAsync(CharacterObject.LockObject, token).ConfigureAwait(false))
            {
                XPathNavigator xmlSkillTranslationNode = (await CharacterObject.LoadDataXPathAsync("skills.xml", token: token).ConfigureAwait(false))
                                                                        .SelectSingleNode("/chummer/knowledgeskills/skill[translate = " + strInputSkillName.CleanXPath() +
                                                                            ']');

                if (xmlSkillTranslationNode == null)
                {
                    return await CharacterObject.ReverseTranslateExtraAsync(strInputSkillName, GlobalSettings.Language,
                                                                            "skills.xml", token).ConfigureAwait(false);
                }

                return (await xmlSkillTranslationNode.SelectSingleNodeAndCacheExpressionAsync("name", token).ConfigureAwait(false))?.Value ?? strInputSkillName;
            }
        }

        public override string SkillCategory => Type;

        // ReSharper disable once InconsistentNaming
        private int _intCachedCyberwareRating = int.MinValue;

        protected override void ResetCachedCyberwareRating()
        {
            using (LockObject.EnterWriteLock())
                _intCachedCyberwareRating = int.MinValue;
        }

        /// <summary>
        /// The attributeValue this skill have from Skilljacks + Knowsoft
        /// </summary>
        /// <returns>Artificial skill attributeValue</returns>
        public override int CyberwareRating
        {
            get
            {
                using (EnterReadLock.Enter(LockObject))
                {
                    if (_intCachedCyberwareRating != int.MinValue)
                        return _intCachedCyberwareRating;
                    using (LockObject.EnterWriteLock())
                    {
                        if (_intCachedCyberwareRating != int.MinValue) // Just in case
                            return _intCachedCyberwareRating;
                        string strTranslatedName = CurrentDisplayName;
                        int intMaxHardwire = -1;
                        foreach (Improvement objImprovement in ImprovementManager
                                     .GetCachedImprovementListForValueOf(
                                         CharacterObject, Improvement.ImprovementType.Hardwire,
                                         DictionaryKey)
                                     .Concat(ImprovementManager.GetCachedImprovementListForValueOf(
                                         CharacterObject,
                                         Improvement.ImprovementType.Hardwire,
                                         strTranslatedName)))
                        {
                            intMaxHardwire = Math.Max(intMaxHardwire, objImprovement.Value.StandardRound());
                        }

                        if (intMaxHardwire >= 0)
                        {
                            return _intCachedCyberwareRating = intMaxHardwire;
                        }

                        int intMaxSkillsoftRating = ImprovementManager
                            .ValueOf(CharacterObject, Improvement.ImprovementType.SkillsoftAccess).StandardRound();
                        if (intMaxSkillsoftRating <= 0)
                            return _intCachedCyberwareRating = 0;
                        int intMax = 0;
                        foreach (Improvement objSkillsoftImprovement in ImprovementManager
                                     .GetCachedImprovementListForValueOf(
                                         CharacterObject, Improvement.ImprovementType.Skillsoft, InternalId))
                        {
                            intMax = Math.Max(intMax, objSkillsoftImprovement.Value.StandardRound());
                        }

                        return _intCachedCyberwareRating = Math.Min(intMax, intMaxSkillsoftRating);
                    }
                }
            }
        }

        /// <summary>
        /// The attributeValue this skill have from Skillwires + Skilljack or Active Hardwires
        /// </summary>
        /// <returns>Artificial skill attributeValue</returns>
        public override async ValueTask<int> GetCyberwareRatingAsync(CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
            {
                if (_intCachedCyberwareRating != int.MinValue)
                    return _intCachedCyberwareRating;

                IAsyncDisposable objLocker = await LockObject.EnterWriteLockAsync(token).ConfigureAwait(false);
                try
                {
                    if (_intCachedCyberwareRating != int.MinValue)
                        return _intCachedCyberwareRating;
                    string strTranslatedName = await GetCurrentDisplayNameAsync(token).ConfigureAwait(false);
                    int intMaxHardwire = -1;
                    foreach (Improvement objImprovement in (await ImprovementManager
                                 .GetCachedImprovementListForValueOfAsync(
                                     CharacterObject, Improvement.ImprovementType.Hardwire,
                                     await GetDictionaryKeyAsync(token).ConfigureAwait(false), token: token)
                                 .ConfigureAwait(false))
                             .Concat(await ImprovementManager.GetCachedImprovementListForValueOfAsync(
                                 CharacterObject,
                                 Improvement.ImprovementType.Hardwire,
                                 strTranslatedName, token: token).ConfigureAwait(false)))
                    {
                        intMaxHardwire = Math.Max(intMaxHardwire, objImprovement.Value.StandardRound());
                    }

                    if (intMaxHardwire >= 0)
                    {
                        return _intCachedCyberwareRating = intMaxHardwire;
                    }

                    int intMaxSkillsoftRating =
                        (await ImprovementManager
                            .ValueOfAsync(CharacterObject, Improvement.ImprovementType.SkillsoftAccess, token: token)
                            .ConfigureAwait(false)).StandardRound();
                    if (intMaxSkillsoftRating <= 0)
                        return _intCachedCyberwareRating = 0;
                    int intMax = 0;
                    foreach (Improvement objSkillsoftImprovement in await ImprovementManager
                                 .GetCachedImprovementListForValueOfAsync(
                                     CharacterObject, Improvement.ImprovementType.Skillsoft, InternalId, token: token)
                                 .ConfigureAwait(false))
                    {
                        intMax = Math.Max(intMax, objSkillsoftImprovement.Value.StandardRound());
                    }

                    return _intCachedCyberwareRating = Math.Min(intMax, intMaxSkillsoftRating);
                }
                finally
                {
                    await objLocker.DisposeAsync().ConfigureAwait(false);
                }
            }
        }

        public override string DisplaySpecialization(string strLanguage)
        {
            using (EnterReadLock.Enter(LockObject))
                return IsNativeLanguage ? string.Empty : base.DisplaySpecialization(strLanguage);
        }

        public override async ValueTask<string> DisplaySpecializationAsync(string strLanguage, CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
                return await GetIsNativeLanguageAsync(token).ConfigureAwait(false)
                    ? string.Empty
                    : await base.DisplaySpecializationAsync(strLanguage, token).ConfigureAwait(false);
        }

        public string Type
        {
            get
            {
                using (EnterReadLock.Enter(LockObject))
                    return _strType;
            }
            set
            {
                using (EnterReadLock.Enter(LockObject))
                {
                    // Interlocked guarantees thread safety here without write lock
                    if (Interlocked.Exchange(ref _strType, value) == value)
                        return;
                    using (LockObject.EnterWriteLock())
                    {
                        //2018-22-03: Causes any attempt to alter the Type for skills with names that match
                        //default skills to reset to the default Type for that skill. If we want to disable
                        //that behavior, better to disable it via the control.
                        /*
                        if (!LoadSkill())
                        {
                            if (s_CategoriesSkillMap.TryGetValue(value, out string strNewAttributeValue))
                            {
                                AttributeObject = CharacterObject.GetAttribute(strNewAttributeValue);
                            }
                        }
                        */
                        if (CategoriesSkillMap.TryGetValue(value, out string strNewAttributeValue))
                        {
                            DefaultAttribute = strNewAttributeValue;
                        }

                        if (!IsLanguage)
                            IsNativeLanguage = false;
                    }
                    OnPropertyChanged();
                }
            }
        }

        public async ValueTask<string> GetTypeAsync(CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
                return _strType;
        }

        public async ValueTask SetTypeAsync(string value, CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
            {
                // Interlocked guarantees thread safety here without write lock
                if (Interlocked.Exchange(ref _strType, value) == value)
                    return;
                IAsyncDisposable objLocker = await LockObject.EnterWriteLockAsync(token).ConfigureAwait(false);
                try
                {
                    //2018-22-03: Causes any attempt to alter the Type for skills with names that match
                    //default skills to reset to the default Type for that skill. If we want to disable
                    //that behavior, better to disable it via the control.
                    /*
                    if (!LoadSkill())
                    {
                        if (s_CategoriesSkillMap.TryGetValue(value, out string strNewAttributeValue))
                        {
                            AttributeObject = CharacterObject.GetAttribute(strNewAttributeValue);
                        }
                    }
                    */
                    if ((await GetCategoriesSkillMapAsync(token).ConfigureAwait(false)).TryGetValue(value, out string strNewAttributeValue))
                    {
                        await SetDefaultAttributeAsync(strNewAttributeValue, token).ConfigureAwait(false);
                    }

                    if (!await GetIsLanguageAsync(token).ConfigureAwait(false))
                        await SetIsNativeLanguageAsync(false, token).ConfigureAwait(false);
                }
                finally
                {
                    await objLocker.DisposeAsync().ConfigureAwait(false);
                }
                OnPropertyChanged(nameof(Type));
            }
        }

        public override bool IsLanguage => Type == "Language";

        public override async ValueTask<bool> GetIsLanguageAsync(CancellationToken token = default)
        {
            return await GetTypeAsync(token).ConfigureAwait(false) == "Language";
        }

        public override bool IsNativeLanguage
        {
            get
            {
                using (EnterReadLock.Enter(LockObject))
                    return _intIsNativeLanguage > 0;
            }
            set
            {
                int intNewValue = value.ToInt32();
                using (EnterReadLock.Enter(LockObject))
                {
                    // Interlocked guarantees thread safety here without write lock
                    if (Interlocked.Exchange(ref _intIsNativeLanguage, intNewValue) == intNewValue)
                        return;
                    using (LockObject.EnterWriteLock())
                    {
                        if (value)
                        {
                            Base = 0;
                            Karma = 0;
                            BuyWithKarma = false;
                            Specializations.Clear();
                        }
                    }
                    OnPropertyChanged();
                }
            }
        }

        public override async ValueTask<bool> GetIsNativeLanguageAsync(CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
                return _intIsNativeLanguage > 0;
        }

        public override async ValueTask SetIsNativeLanguageAsync(bool value, CancellationToken token = default)
        {
            int intNewValue = value.ToInt32();
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
            {
                // Interlocked guarantees thread safety here without write lock
                if (Interlocked.Exchange(ref _intIsNativeLanguage, intNewValue) == intNewValue)
                    return;
                IAsyncDisposable objLocker = await LockObject.EnterWriteLockAsync(token).ConfigureAwait(false);
                try
                {
                    if (value)
                    {
                        await SetBaseAsync(0, token).ConfigureAwait(false);
                        await SetKarmaAsync(0, token).ConfigureAwait(false);
                        await SetBuyWithKarmaAsync(false, token).ConfigureAwait(false);
                        await Specializations.ClearAsync(token).ConfigureAwait(false);
                    }
                }
                finally
                {
                    await objLocker.DisposeAsync().ConfigureAwait(false);
                }
                OnPropertyChanged(nameof(IsNativeLanguage));
            }
        }

        /// <summary>
        /// How much karma this costs. Return value during career mode is undefined
        /// </summary>
        public override int CurrentKarmaCost
        {
            get
            {
                using (EnterReadLock.Enter(LockObject))
                {
                    int intTotalBaseRating = TotalBaseRating;
                    decimal decCost = intTotalBaseRating * (intTotalBaseRating + 1);
                    int intLower = Base + FreeKarma + RatingModifiers(Attribute);
                    decCost -= intLower * (intLower + 1);

                    decCost /= 2;
                    decCost *= CharacterObject.Settings.KarmaImproveKnowledgeSkill;
                    // We have bought the first level with karma, too
                    if (intLower == 0 && decCost > 0)
                        decCost += CharacterObject.Settings.KarmaNewKnowledgeSkill -
                                   CharacterObject.Settings.KarmaImproveKnowledgeSkill;

                    decimal decMultiplier = 1.0m;
                    decimal decExtra = 0;
                    int intSpecCount = BuyWithKarma ? Specializations.Count(x => !x.Free) : 0;

                    decimal decSpecCost = CharacterObject.Settings.KarmaKnowledgeSpecialization * intSpecCount;
                    decimal decExtraSpecCost = 0;
                    decimal decSpecCostMultiplier = 1.0m;
                    foreach (Improvement objLoopImprovement in CharacterObject.Improvements)
                    {
                        if (objLoopImprovement.Minimum <= intTotalBaseRating &&
                            (string.IsNullOrEmpty(objLoopImprovement.Condition) ||
                             (objLoopImprovement.Condition == "career") == CharacterObject.Created ||
                             (objLoopImprovement.Condition == "create") != CharacterObject.Created) &&
                            objLoopImprovement.Enabled)
                        {
                            if (objLoopImprovement.ImprovedName == DictionaryKey ||
                                string.IsNullOrEmpty(objLoopImprovement.ImprovedName))
                            {
                                switch (objLoopImprovement.ImproveType)
                                {
                                    case Improvement.ImprovementType.KnowledgeSkillKarmaCost:
                                        decExtra += objLoopImprovement.Value *
                                                    (Math.Min(intTotalBaseRating,
                                                        objLoopImprovement.Maximum == 0
                                                            ? int.MaxValue
                                                            : objLoopImprovement.Maximum) - Math.Max(intLower,
                                                        objLoopImprovement.Minimum - 1));
                                        break;

                                    case Improvement.ImprovementType.KnowledgeSkillKarmaCostMultiplier:
                                        decMultiplier *= objLoopImprovement.Value / 100.0m;
                                        break;
                                }
                            }
                            else if (objLoopImprovement.ImprovedName == SkillCategory)
                            {
                                switch (objLoopImprovement.ImproveType)
                                {
                                    case Improvement.ImprovementType.SkillCategoryKarmaCost:
                                        decExtra += objLoopImprovement.Value *
                                                    (Math.Min(intTotalBaseRating,
                                                        objLoopImprovement.Maximum == 0
                                                            ? int.MaxValue
                                                            : objLoopImprovement.Maximum) - Math.Max(intLower,
                                                        objLoopImprovement.Minimum - 1));
                                        break;

                                    case Improvement.ImprovementType.SkillCategoryKarmaCostMultiplier:
                                        decMultiplier *= objLoopImprovement.Value / 100.0m;
                                        break;

                                    case Improvement.ImprovementType.SkillCategorySpecializationKarmaCost:
                                        decExtraSpecCost += objLoopImprovement.Value * intSpecCount;
                                        break;

                                    case Improvement.ImprovementType.SkillCategorySpecializationKarmaCostMultiplier:
                                        decSpecCostMultiplier *= objLoopImprovement.Value / 100.0m;
                                        break;
                                }
                            }
                        }
                    }

                    if (decMultiplier != 1.0m)
                        decCost *= decMultiplier;

                    if (decSpecCostMultiplier != 1.0m)
                        decSpecCost *= decSpecCostMultiplier;
                    decCost += decExtra;
                    decCost += decSpecCost + decExtraSpecCost; //Spec

                    return Math.Max(decCost.StandardRound(), 0);
                }
            }
        }

        /// <summary>
        /// How much karma this costs. Return value during career mode is undefined
        /// </summary>
        public override async ValueTask<int> GetCurrentKarmaCostAsync(CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
            {
                int intTotalBaseRating = await GetTotalBaseRatingAsync(token).ConfigureAwait(false);
                decimal decCost = intTotalBaseRating * (intTotalBaseRating + 1);
                int intLower = await GetBaseAsync(token).ConfigureAwait(false) +
                               await GetFreeKarmaAsync(token).ConfigureAwait(false) +
                               await RatingModifiersAsync(Attribute, token: token).ConfigureAwait(false);
                decCost -= intLower * (intLower + 1);

                decCost /= 2;
                CharacterSettings objSettings = await CharacterObject.GetSettingsAsync(token).ConfigureAwait(false);
                decCost *= await objSettings.GetKarmaImproveKnowledgeSkillAsync(token).ConfigureAwait(false);
                // We have bought the first level with karma, too
                if (intLower == 0 && decCost > 0)
                    decCost += await objSettings.GetKarmaNewKnowledgeSkillAsync(token).ConfigureAwait(false) -
                               await objSettings.GetKarmaImproveKnowledgeSkillAsync(token).ConfigureAwait(false);

                decimal decMultiplier = 1.0m;
                decimal decExtra = 0;
                int intSpecCount = await GetBuyWithKarmaAsync(token).ConfigureAwait(false)
                    ? await Specializations.CountAsync(objSpec => !objSpec.Free, token: token).ConfigureAwait(false)
                    : 0;
                decimal decSpecCost = intSpecCount *
                                      await (await CharacterObject.GetSettingsAsync(token).ConfigureAwait(false))
                                          .GetKarmaKnowledgeSpecializationAsync(token).ConfigureAwait(false);
                decimal decExtraSpecCost = 0;
                decimal decSpecCostMultiplier = 1.0m;
                bool blnCreated = await CharacterObject.GetCreatedAsync(token).ConfigureAwait(false);
                await (await CharacterObject.GetImprovementsAsync(token).ConfigureAwait(false)).ForEachAsync(
                    async objLoopImprovement =>
                    {
                        if (objLoopImprovement.Minimum > intTotalBaseRating ||
                            (!string.IsNullOrEmpty(objLoopImprovement.Condition)
                             && (objLoopImprovement.Condition == "career") != blnCreated
                             && (objLoopImprovement.Condition == "create") == blnCreated)
                            || !objLoopImprovement.Enabled)
                            return;
                        if (objLoopImprovement.ImprovedName == await GetDictionaryKeyAsync(token).ConfigureAwait(false)
                            || string.IsNullOrEmpty(objLoopImprovement.ImprovedName))
                        {
                            switch (objLoopImprovement.ImproveType)
                            {
                                case Improvement.ImprovementType.KnowledgeSkillKarmaCost:
                                    decExtra += objLoopImprovement.Value
                                                * (Math.Min(intTotalBaseRating,
                                                       objLoopImprovement.Maximum == 0
                                                           ? int.MaxValue
                                                           : objLoopImprovement.Maximum)
                                                   - Math.Max(intLower, objLoopImprovement.Minimum - 1));
                                    break;

                                case Improvement.ImprovementType.KnowledgeSkillKarmaCostMultiplier:
                                    decMultiplier *= objLoopImprovement.Value / 100.0m;
                                    break;
                            }
                        }
                        else if (objLoopImprovement.ImprovedName == SkillCategory)
                        {
                            switch (objLoopImprovement.ImproveType)
                            {
                                case Improvement.ImprovementType.SkillCategoryKarmaCost:
                                    decExtra += objLoopImprovement.Value
                                                * (Math.Min(intTotalBaseRating,
                                                       objLoopImprovement.Maximum == 0
                                                           ? int.MaxValue
                                                           : objLoopImprovement.Maximum)
                                                   - Math.Max(intLower, objLoopImprovement.Minimum - 1));
                                    break;

                                case Improvement.ImprovementType.SkillCategoryKarmaCostMultiplier:
                                    decMultiplier *= objLoopImprovement.Value / 100.0m;
                                    break;

                                case Improvement.ImprovementType.SkillCategorySpecializationKarmaCost:
                                    decExtraSpecCost += objLoopImprovement.Value * intSpecCount;
                                    break;

                                case Improvement.ImprovementType.SkillCategorySpecializationKarmaCostMultiplier:
                                    decSpecCostMultiplier *= objLoopImprovement.Value / 100.0m;
                                    break;
                            }
                        }
                    }, token: token).ConfigureAwait(false);

                if (decMultiplier != 1.0m)
                    decCost *= decMultiplier;

                if (decSpecCostMultiplier != 1.0m)
                    decSpecCost *= decSpecCostMultiplier;
                decCost += decExtra;
                decCost += decSpecCost + decExtraSpecCost; //Spec

                return Math.Max(decCost.StandardRound(), 0);
            }
        }

        /// <summary>
        /// Karma price to upgrade. Returns negative if impossible. Minimum value is always 1.
        /// </summary>
        /// <returns>Price in karma</returns>
        public override int UpgradeKarmaCost
        {
            get
            {
                using (EnterReadLock.Enter(LockObject))
                {
                    int intTotalBaseRating = TotalBaseRating;
                    if (intTotalBaseRating >= RatingMaximum)
                    {
                        return -1;
                    }

                    int intOptionsCost;
                    int intValue;
                    if (intTotalBaseRating == 0)
                    {
                        intOptionsCost = CharacterObject.Settings.KarmaNewKnowledgeSkill;
                        intValue = intOptionsCost;
                    }
                    else
                    {
                        intOptionsCost = CharacterObject.Settings.KarmaNewKnowledgeSkill;
                        intValue = (intTotalBaseRating + 1) * intOptionsCost;
                    }

                    decimal decMultiplier = 1.0m;
                    decimal decExtra = 0;
                    int intMinOverride = int.MaxValue;
                    foreach (Improvement objLoopImprovement in CharacterObject.Improvements)
                    {
                        if ((objLoopImprovement.Maximum == 0 || intTotalBaseRating + 1 <= objLoopImprovement.Maximum) &&
                            objLoopImprovement.Minimum <= intTotalBaseRating + 1 &&
                            (string.IsNullOrEmpty(objLoopImprovement.Condition) ||
                             (objLoopImprovement.Condition == "career") == CharacterObject.Created ||
                             (objLoopImprovement.Condition == "create") != CharacterObject.Created) &&
                            objLoopImprovement.Enabled)
                        {
                            if (objLoopImprovement.ImprovedName == DictionaryKey ||
                                string.IsNullOrWhiteSpace(objLoopImprovement.ImprovedName))
                            {
                                switch (objLoopImprovement.ImproveType)
                                {
                                    case Improvement.ImprovementType.KnowledgeSkillKarmaCost:
                                        decExtra += objLoopImprovement.Value;
                                        break;

                                    case Improvement.ImprovementType.KnowledgeSkillKarmaCostMultiplier:
                                        decMultiplier *= objLoopImprovement.Value / 100.0m;
                                        break;
                                }
                            }
                            else if (objLoopImprovement.ImprovedName == SkillCategory)
                            {
                                switch (objLoopImprovement.ImproveType)
                                {
                                    case Improvement.ImprovementType.SkillCategoryKarmaCost:
                                        decExtra += objLoopImprovement.Value;
                                        break;

                                    case Improvement.ImprovementType.SkillCategoryKarmaCostMultiplier:
                                        decMultiplier *= objLoopImprovement.Value / 100.0m;
                                        break;
                                }
                            }

                            if ((objLoopImprovement.ImprovedName == DictionaryKey ||
                                 string.IsNullOrWhiteSpace(objLoopImprovement.ImprovedName) ||
                                 objLoopImprovement.ImprovedName == SkillCategory) && objLoopImprovement.ImproveType ==
                                Improvement.ImprovementType.KnowledgeSkillKarmaCostMinimum)
                            {
                                intMinOverride = Math.Min(intMinOverride, objLoopImprovement.Value.StandardRound());
                            }
                        }
                    }

                    if (decMultiplier != 1.0m)
                        intValue = (intValue * decMultiplier + decExtra).StandardRound();
                    else
                        intValue += decExtra.StandardRound();
                    return Math.Max(intValue,
                        intMinOverride != int.MaxValue ? intMinOverride : Math.Min(1, intOptionsCost));
                }
            }
        }

        /// <summary>
        /// Karma price to upgrade. Returns negative if impossible. Minimum value is always 1.
        /// </summary>
        /// <returns>Price in karma</returns>
        public override async ValueTask<int> GetUpgradeKarmaCostAsync(CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
            {
                int intTotalBaseRating = await GetTotalBaseRatingAsync(token).ConfigureAwait(false);
                if (intTotalBaseRating >= await GetRatingMaximumAsync(token).ConfigureAwait(false))
                {
                    return -1;
                }

                int intOptionsCost;
                int intValue;
                CharacterSettings objSettings = await CharacterObject.GetSettingsAsync(token).ConfigureAwait(false);
                if (intTotalBaseRating == 0)
                {
                    intOptionsCost = objSettings.KarmaNewKnowledgeSkill;
                    intValue = intOptionsCost;
                }
                else
                {
                    intOptionsCost = objSettings.KarmaNewKnowledgeSkill;
                    intValue = (intTotalBaseRating + 1) * intOptionsCost;
                }

                decimal decMultiplier = 1.0m;
                decimal decExtra = 0;
                int intMinOverride = int.MaxValue;
                bool blnCreated = await CharacterObject.GetCreatedAsync(token).ConfigureAwait(false);
                string strDictionaryKey = await GetDictionaryKeyAsync(token).ConfigureAwait(false);
                await (await CharacterObject.GetImprovementsAsync(token).ConfigureAwait(false)).ForEachAsync(
                    objLoopImprovement =>
                    {
                        if ((objLoopImprovement.Maximum == 0 || intTotalBaseRating + 1 <= objLoopImprovement.Maximum) &&
                            objLoopImprovement.Minimum <= intTotalBaseRating + 1 &&
                            (string.IsNullOrEmpty(objLoopImprovement.Condition) ||
                             (objLoopImprovement.Condition == "career") == blnCreated ||
                             (objLoopImprovement.Condition == "create") != blnCreated) &&
                            objLoopImprovement.Enabled)
                        {
                            if (objLoopImprovement.ImprovedName == strDictionaryKey ||
                                string.IsNullOrWhiteSpace(objLoopImprovement.ImprovedName))
                            {
                                switch (objLoopImprovement.ImproveType)
                                {
                                    case Improvement.ImprovementType.KnowledgeSkillKarmaCost:
                                        decExtra += objLoopImprovement.Value;
                                        break;

                                    case Improvement.ImprovementType.KnowledgeSkillKarmaCostMultiplier:
                                        decMultiplier *= objLoopImprovement.Value / 100.0m;
                                        break;
                                }
                            }
                            else if (objLoopImprovement.ImprovedName == SkillCategory)
                            {
                                switch (objLoopImprovement.ImproveType)
                                {
                                    case Improvement.ImprovementType.SkillCategoryKarmaCost:
                                        decExtra += objLoopImprovement.Value;
                                        break;

                                    case Improvement.ImprovementType.SkillCategoryKarmaCostMultiplier:
                                        decMultiplier *= objLoopImprovement.Value / 100.0m;
                                        break;
                                }
                            }

                            if ((objLoopImprovement.ImprovedName == strDictionaryKey ||
                                 string.IsNullOrWhiteSpace(objLoopImprovement.ImprovedName) ||
                                 objLoopImprovement.ImprovedName == SkillCategory) && objLoopImprovement.ImproveType ==
                                Improvement.ImprovementType.KnowledgeSkillKarmaCostMinimum)
                            {
                                intMinOverride = Math.Min(intMinOverride, objLoopImprovement.Value.StandardRound());
                            }
                        }
                    }, token: token).ConfigureAwait(false);

                if (decMultiplier != 1.0m)
                    intValue = (intValue * decMultiplier + decExtra).StandardRound();
                else
                    intValue += decExtra.StandardRound();
                return Math.Max(intValue,
                                intMinOverride != int.MaxValue ? intMinOverride : Math.Min(1, intOptionsCost));
            }
        }

        /// <summary>
        /// How much Sp this costs. Price during career mode is undefined
        /// </summary>
        public override int CurrentSpCost
        {
            get
            {
                using (EnterReadLock.Enter(LockObject))
                {
                    int intPointCost = BasePoints;
                    if (!IsExoticSkill && !BuyWithKarma)
                        intPointCost += Specializations.Count(x => !x.Free);

                    decimal decExtra = 0;
                    decimal decMultiplier = 1.0m;
                    foreach (Improvement objLoopImprovement in CharacterObject.Improvements)
                    {
                        if (objLoopImprovement.Minimum <= BasePoints &&
                            (string.IsNullOrEmpty(objLoopImprovement.Condition) ||
                             (objLoopImprovement.Condition == "career") == CharacterObject.Created ||
                             (objLoopImprovement.Condition == "create") != CharacterObject.Created) &&
                            objLoopImprovement.Enabled)
                        {
                            if (objLoopImprovement.ImprovedName == DictionaryKey ||
                                string.IsNullOrEmpty(objLoopImprovement.ImprovedName))
                            {
                                switch (objLoopImprovement.ImproveType)
                                {
                                    case Improvement.ImprovementType.KnowledgeSkillPointCost:
                                        decExtra += objLoopImprovement.Value *
                                                    (Math.Min(BasePoints,
                                                        objLoopImprovement.Maximum == 0
                                                            ? int.MaxValue
                                                            : objLoopImprovement.Maximum) - objLoopImprovement.Minimum);
                                        break;

                                    case Improvement.ImprovementType.KnowledgeSkillPointCostMultiplier:
                                        decMultiplier *= objLoopImprovement.Value / 100.0m;
                                        break;
                                }
                            }
                            else if (objLoopImprovement.ImprovedName == SkillCategory)
                            {
                                switch (objLoopImprovement.ImproveType)
                                {
                                    case Improvement.ImprovementType.SkillCategoryPointCost:
                                        decExtra += objLoopImprovement.Value *
                                                    (Math.Min(BasePoints,
                                                        objLoopImprovement.Maximum == 0
                                                            ? int.MaxValue
                                                            : objLoopImprovement.Maximum) - objLoopImprovement.Minimum);
                                        break;

                                    case Improvement.ImprovementType.SkillCategoryPointCostMultiplier:
                                        decMultiplier *= objLoopImprovement.Value / 100.0m;
                                        break;
                                }
                            }
                        }
                    }

                    if (decMultiplier != 1.0m)
                        intPointCost = (intPointCost * decMultiplier + decExtra).StandardRound();
                    else
                        intPointCost += decExtra.StandardRound();

                    return Math.Max(intPointCost, 0);
                }
            }
        }

        /// <summary>
        /// How much Sp this costs. Price during career mode is undefined
        /// </summary>
        public override async ValueTask<int> GetCurrentSpCostAsync(CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
            {
                int intBasePoints = await GetBasePointsAsync(token).ConfigureAwait(false);
                int cost = intBasePoints;
                if (!IsExoticSkill && !await GetBuyWithKarmaAsync(token).ConfigureAwait(false))
                    cost += await Specializations.CountAsync(x => !x.Free, token: token).ConfigureAwait(false);

                decimal decExtra = 0;
                decimal decMultiplier = 1.0m;
                bool blnCreated = await CharacterObject.GetCreatedAsync(token).ConfigureAwait(false);
                await (await CharacterObject.GetImprovementsAsync(token).ConfigureAwait(false)).ForEachAsync(
                    async objLoopImprovement =>
                    {
                        if (objLoopImprovement.Minimum > intBasePoints ||
                            (!string.IsNullOrEmpty(objLoopImprovement.Condition)
                             && (objLoopImprovement.Condition == "career") != blnCreated
                             && (objLoopImprovement.Condition == "create") == blnCreated)
                            || !objLoopImprovement.Enabled)
                            return;
                        if (objLoopImprovement.ImprovedName == await GetDictionaryKeyAsync(token).ConfigureAwait(false)
                            || string.IsNullOrEmpty(objLoopImprovement.ImprovedName))
                        {
                            switch (objLoopImprovement.ImproveType)
                            {
                                case Improvement.ImprovementType.KnowledgeSkillPointCost:
                                    decExtra += objLoopImprovement.Value
                                                * (Math.Min(
                                                    intBasePoints,
                                                    objLoopImprovement.Maximum == 0
                                                        ? int.MaxValue
                                                        : objLoopImprovement.Maximum) - objLoopImprovement.Minimum);
                                    break;

                                case Improvement.ImprovementType.KnowledgeSkillPointCostMultiplier:
                                    decMultiplier *= objLoopImprovement.Value / 100.0m;
                                    break;
                            }
                        }
                        else if (objLoopImprovement.ImprovedName == SkillCategory)
                        {
                            switch (objLoopImprovement.ImproveType)
                            {
                                case Improvement.ImprovementType.SkillCategoryPointCost:
                                    decExtra += objLoopImprovement.Value
                                                * (Math.Min(
                                                    intBasePoints,
                                                    objLoopImprovement.Maximum == 0
                                                        ? int.MaxValue
                                                        : objLoopImprovement.Maximum) - objLoopImprovement.Minimum);
                                    break;

                                case Improvement.ImprovementType.SkillCategoryPointCostMultiplier:
                                    decMultiplier *= objLoopImprovement.Value / 100.0m;
                                    break;
                            }
                        }
                    }, token: token).ConfigureAwait(false);

                if (decMultiplier != 1.0m)
                    cost = (cost * decMultiplier + decExtra).StandardRound();
                else
                    cost += decExtra.StandardRound();

                return Math.Max(cost, 0);
            }
        }

        public override void WriteToDerived(XmlWriter objWriter)
        {
            objWriter.WriteElementString("type", Type);
            objWriter.WriteElementString("isnativelanguage", IsNativeLanguage.ToString(GlobalSettings.InvariantCultureInfo));
            if (ForcedName)
                objWriter.WriteElementString("forced", null);
        }

        public void Load(XmlNode xmlNode)
        {
            if (xmlNode == null)
                return;
            using (LockObject.EnterWriteLock())
            {
                string strTemp = Name;
                if (xmlNode.TryGetStringFieldQuickly("name", ref strTemp))
                    Name = strTemp;
                if (xmlNode.TryGetField("id", Guid.TryParse, out Guid guiTemp))
                    SkillId = guiTemp;
                else if (xmlNode.TryGetField("suid", Guid.TryParse, out Guid guiTemp2))
                    SkillId = guiTemp2;

                bool blnTemp = false;
                if (xmlNode.TryGetBoolFieldQuickly("disableupgrades", ref blnTemp))
                    _blnAllowUpgrade = !blnTemp;

                // Legacy shim
                if (SkillId.Equals(Guid.Empty))
                {
                    XPathNavigator objDataNode = CharacterObject.LoadDataXPath("skills.xml")
                        .SelectSingleNode("/chummer/knowledgeskills/skill[name = " + Name.CleanXPath() + ']');
                    if (objDataNode.TryGetField("id", Guid.TryParse, out Guid guidTemp))
                        SkillId = guidTemp;
                }

                string strCategoryString = string.Empty;
                if ((xmlNode.TryGetStringFieldQuickly("type", ref strCategoryString) &&
                     !string.IsNullOrEmpty(strCategoryString))
                    || (xmlNode.TryGetStringFieldQuickly("skillcategory", ref strCategoryString) &&
                        !string.IsNullOrEmpty(strCategoryString)))
                {
                    Type = strCategoryString;
                }

                // Legacy sweep for native language skills
                blnTemp = false;
                if (!xmlNode.TryGetBoolFieldQuickly("isnativelanguage", ref blnTemp) && IsLanguage &&
                    CharacterObject.LastSavedVersion <= new Version(5, 212, 72))
                {
                    int intKarma = 0;
                    int intBase = 0;
                    xmlNode.TryGetInt32FieldQuickly("karma", ref intKarma);
                    xmlNode.TryGetInt32FieldQuickly("base", ref intBase);
                    if (intKarma == 0 && intBase == 0 &&
                        CharacterObject.SkillsSection.KnowledgeSkills.Count(x => x.IsNativeLanguage) < 1 +
                        ImprovementManager.ValueOf(CharacterObject, Improvement.ImprovementType.NativeLanguageLimit))
                        blnTemp = true;
                }

                _intIsNativeLanguage = blnTemp.ToInt32();
            }
        }
    }
}
