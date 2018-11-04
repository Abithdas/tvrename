// 
// Main website for TVRename is http://tvrename.com
// 
// Source code available at https://github.com/TV-Rename/tvrename
// 
// This code is released under GPLv3 https://github.com/TV-Rename/tvrename/blob/master/LICENSE.md
// 
using System;
using Alphaleonis.Win32.Filesystem;
using System.Xml;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using TVRename;

// These are what is used when processing folders for missing episodes, renaming, etc. of files.

// A "ProcessedEpisode" is generated by processing an Episode from thetvdb, and merging/renaming/etc.
//
// A "ShowItem" is a show the user has added on the "My Shows" tab

namespace TVRename
{
    public class ShowItem
    {
        public string AutoAddFolderBase; // TODO: use magical renaming tokens here
        public string AutoAddCustomFolderFormat;
        public AutomaticFolderType AutoAddType;

        public bool CountSpecials;
        public bool DvdOrder; // sort by DVD order, not the default sort we get
        public bool DoMissingCheck;
        public bool DoRename;
        public bool ForceCheckFuture;
        public bool ForceCheckNoAirdate;
        public List<int> IgnoreSeasons;
        public Dictionary<int, List<string>> ManualFolderLocations;
        public Dictionary<int, List<ProcessedEpisode>> SeasonEpisodes; // built up by applying rules.
        public Dictionary<int, List<ShowRule>> SeasonRules;
        public bool ShowNextAirdate;
        public int TvdbCode;
        public bool UseCustomShowName;
        public string CustomShowName;
        public bool UseCustomLanguage;
        public string CustomLanguageCode;
        public bool UseSequentialMatch;
        public readonly List<string> AliasNames = new List<string>();
        public bool UseCustomSearchUrl;
        public string CustomSearchUrl;

        public string ShowTimeZone;
        private TimeZone seriesTimeZone;
        private string lastFiguredTz;
        
        public DateTime? BannersLastUpdatedOnDisk { get; set; }

        #region AutomaticFolderType enum
        public enum AutomaticFolderType
        {
            none,
            baseOnly,
            libraryDefault,
            custom
        }
        #endregion

        public ShowItem()
        {
            SetDefaults();
        }

        public ShowItem(int tvdbCode)
        {
            SetDefaults();
            TvdbCode = tvdbCode;
        }

        private void FigureOutTimeZone()
        {
            string tzstr = ShowTimeZone;

            if (string.IsNullOrEmpty(tzstr))
                tzstr = TimeZone.DefaultTimeZone();

            seriesTimeZone = TimeZone.TimeZoneFor(tzstr);

            lastFiguredTz = tzstr;
        }

        public TimeZone GetTimeZone()
        {
            // we cache the timezone info, as the fetching is a bit slow, and we do this a lot
            if (lastFiguredTz != ShowTimeZone)
                FigureOutTimeZone();

            return seriesTimeZone;
        }

        public ShowItem(XElement xmlSettings)
        {
            SetDefaults();

            //These variables have been discontinued (JULY 2018).  If we have any then we should migrate to the new values
            bool upgradeFromOldAutoAddFunction = false;
            bool TEMP_AutoAddNewSeasons = true;
            bool TEMP_AutoAdd_FolderPerSeason = true;
            bool TEMP_PadSeasonToTwoDigits = true;
            string TEMP_AutoAdd_SeasonFolderName = string.Empty;

            CustomShowName = xmlSettings.ExtractString("ShowName");
            UseCustomShowName = xmlSettings.ExtractBool("UseCustomShowName")??false;

            UseCustomLanguage = xmlSettings.ExtractBool("UseCustomLanguage")??false;
            CustomLanguageCode = xmlSettings.ExtractString("CustomLanguageCode");
            CustomShowName = xmlSettings.ExtractString("CustomShowName");

            TvdbCode = xmlSettings.ExtractInt("TVDBID")??-1;

            upgradeFromOldAutoAddFunction = xmlSettings.Descendants("AutoAddNewSeasons").Any()
                                            || xmlSettings.Descendants("FolderPerSeason").Any()
                                            || xmlSettings.Descendants("SeasonFolderName").Any()
                                            || xmlSettings.Descendants("PadSeasonToTwoDigits").Any();
            TEMP_AutoAddNewSeasons = xmlSettings.ExtractBool("AutoAddNewSeasons") ?? false;
            TEMP_AutoAdd_FolderPerSeason = xmlSettings.ExtractBool("FolderPerSeason") ?? false;
            TEMP_AutoAdd_SeasonFolderName = xmlSettings.ExtractString("SeasonFolderName");
            TEMP_PadSeasonToTwoDigits = xmlSettings.ExtractBool("PadSeasonToTwoDigits") ?? false;
            CountSpecials = xmlSettings.ExtractBool("CountSpecials") ?? false;
            ShowNextAirdate = xmlSettings.ExtractBool("ShowNextAirdate")??true;
            AutoAddFolderBase = xmlSettings.ExtractString("FolderBase");
            DoRename = xmlSettings.ExtractBool("DoRename") ?? true;
            DoMissingCheck = xmlSettings.ExtractBool("DoMissingCheck") ?? true;
            DvdOrder = xmlSettings.ExtractBool("DVDOrder") ?? false;
            UseCustomSearchUrl = xmlSettings.ExtractBool("UseCustomSearchURL") ?? false;
            CustomSearchUrl = xmlSettings.ExtractString("CustomSearchURL");
            ShowTimeZone = xmlSettings.ExtractString("TimeZone")?? TimeZone.DefaultTimeZone(); // default, is correct for most shows;
            ForceCheckFuture = xmlSettings.ExtractBool("ForceCheckFuture")
                                       ?? xmlSettings.ExtractBool("ForceCheckAll")
                                       ?? false;
                    ForceCheckNoAirdate = xmlSettings.ExtractBool("ForceCheckNoAirdate")
                                          ?? xmlSettings.ExtractBool("ForceCheckAll")
                                          ?? false;
                    AutoAddCustomFolderFormat = xmlSettings.ExtractString("CustomFolderFormat") ?? "Season {Season:2}";
                    AutoAddType = xmlSettings.ExtractInt("AutoAddType")==null
                        ? AutomaticFolderType.libraryDefault
                        : (AutomaticFolderType)xmlSettings.ExtractInt("AutoAddType");
                    BannersLastUpdatedOnDisk = xmlSettings.ExtractDateTime("BannersLastUpdatedOnDisk");
                    UseSequentialMatch = xmlSettings.ExtractBool("UseSequentialMatch")??false;

            foreach (XElement ig in xmlSettings.Descendants("IgnoreSeasons").Descendants("Ignore"))
            {
                IgnoreSeasons.Add(XmlConvert.ToInt32(ig.Value));
            }
            foreach (XElement alias in xmlSettings.Descendants("AliasNames").Descendants("Alias"))
            {
                AliasNames.Add(alias.Value);
            }

            foreach (XElement rulesSet in xmlSettings.Descendants("Rules"))
            {
                int snum = int.Parse(rulesSet.Attribute("SeasonNumber")?.Value);
                SeasonRules[snum] = new List<ShowRule>();

                foreach (XElement ruleData in rulesSet.Descendants("Rule"))
                {
                    SeasonRules[snum].Add(new ShowRule(ruleData));
                }
            }

            foreach (XElement seasonFolder in xmlSettings.Descendants("SeasonFolders"))
            {
                int snum = int.Parse(seasonFolder.Attribute("SeasonNumber")?.Value);
                ManualFolderLocations[snum] = new List<string>();

                foreach (XElement folderData in seasonFolder.Descendants("Folder"))
                {
                    string ff = folderData.Attribute("Location")?.Value;
                    if (!string.IsNullOrWhiteSpace(ff) && AutoFolderNameForSeason(snum) != ff)
                    {
                        ManualFolderLocations[snum].Add(ff);
                    }
                }
            }

            if (upgradeFromOldAutoAddFunction)
            {
                if (TEMP_AutoAddNewSeasons)
                {
                    if (TEMP_AutoAdd_FolderPerSeason)
                    {
                        AutoAddCustomFolderFormat = TEMP_AutoAdd_SeasonFolderName + ((TEMP_PadSeasonToTwoDigits||TVSettings.Instance.LeadingZeroOnSeason)?"{Season:2}":"{Season}");
                        AutoAddType = (AutoAddCustomFolderFormat == TVSettings.Instance.SeasonFolderFormat)
                            ? AutomaticFolderType.libraryDefault
                            : AutomaticFolderType.custom;
                    }
                    else
                    {
                        AutoAddCustomFolderFormat = string.Empty;
                        AutoAddType = AutomaticFolderType.baseOnly;
                    }
                }
                else
                {
                    AutoAddCustomFolderFormat = string.Empty;
                    AutoAddType = AutomaticFolderType.none;
                }
            }
        }

        internal bool UsesManualFolders() => ManualFolderLocations.Count > 0;

        public SeriesInfo TheSeries() => TheTVDB.Instance.GetSeries(TvdbCode);

        public string ShowName
        {
            get
            {
                if (UseCustomShowName)
                    return CustomShowName;
                SeriesInfo ser = TheSeries();
                if (ser != null)
                    return ser.Name;
                return "<" + TvdbCode + " not downloaded>";
            }
        }

        public List<string> GetSimplifiedPossibleShowNames()
        {
            List<string> possibles = new List<string>();

            string simplifiedShowName = Helpers.SimplifyName(ShowName);
            if (simplifiedShowName != "") { possibles.Add( simplifiedShowName); }

            //Check the custom show name too
            if (UseCustomShowName)
            {
                string simplifiedCustomShowName = Helpers.SimplifyName(CustomShowName);
                if (simplifiedCustomShowName != "") { possibles.Add(simplifiedCustomShowName); }
            }

            //Also add the aliases provided
            possibles.AddRange(from alias in AliasNames select Helpers.SimplifyName(alias));

            return possibles;
        }

        public string ShowStatus
        {
            get{
                SeriesInfo ser = TheSeries();
                if (ser != null ) return ser.GetStatus();
                return "Unknown";
            }
        }

        public enum ShowAirStatus
        {
            noEpisodesOrSeasons,
            aired,
            partiallyAired,
            noneAired
        }

        public ShowAirStatus SeasonsAirStatus
        {
            get
            {
                if (HasSeasonsAndEpisodes)
                {
                    if (HasAiredEpisodes && !HasUnairedEpisodes)
                    {
                        return ShowAirStatus.aired;
                    }
                    else if (HasUnairedEpisodes && !HasAiredEpisodes)
                    {
                        return ShowAirStatus.noneAired;
                    }
                    else if (HasAiredEpisodes && HasUnairedEpisodes)
                    {
                        return ShowAirStatus.partiallyAired;
                    }
                    else
                    {
                        //System.Diagnostics.Debug.Assert(false, "That is weird ... we have 'seasons and episodes' but none are aired, nor unaired. That case shouldn't actually occur !");
                        return ShowAirStatus.noEpisodesOrSeasons;
                    }
                }
                else
                {
                    return ShowAirStatus.noEpisodesOrSeasons;
                }
            }
        }

        private bool HasSeasonsAndEpisodes
        {
            get {
                //We can use AiredSeasons as it does not matter which order we do this in Aired or DVD
                if (TheSeries() == null || TheSeries().AiredSeasons == null || TheSeries().AiredSeasons.Count <= 0)
                    return false;
                foreach (KeyValuePair<int, Season> s in TheSeries().AiredSeasons)
                {
                    if(IgnoreSeasons.Contains(s.Key))
                        continue;
                    if (s.Value.Episodes != null && s.Value.Episodes.Count > 0)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        private bool HasUnairedEpisodes
        {
            get
            {
                if (!HasSeasonsAndEpisodes) return false;

                foreach (KeyValuePair<int, Season> s in TheSeries().AiredSeasons)
                {
                    if (IgnoreSeasons.Contains(s.Key))
                        continue;
                    if (s.Value.Status(GetTimeZone()) == Season.SeasonStatus.noneAired ||
                        s.Value.Status(GetTimeZone()) == Season.SeasonStatus.partiallyAired)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        private bool HasAiredEpisodes
        {
                get{
                    if (!HasSeasonsAndEpisodes) return false;

                    foreach (KeyValuePair<int, Season> s in TheSeries().AiredSeasons)
                    {
                        if(IgnoreSeasons.Contains(s.Key))
                            continue;
                        if (s.Value.Status(GetTimeZone()) == Season.SeasonStatus.partiallyAired || s.Value.Status(GetTimeZone()) == Season.SeasonStatus.aired)
                        {
                            return true;
                        }
                    }
                    return false;
             }
        }

        public string[] Genres => TheSeries()?.GetGenres();

        public Language  PreferredLanguage
        {
            get
            {
                if (UseCustomLanguage) return TheTVDB.Instance.LanguageList.GetLanguageFromCode(CustomLanguageCode);
                return TheTVDB.Instance.PreferredLanuage;
            }
        }

        private void SetDefaults()
        {
            ManualFolderLocations = new Dictionary<int, List<string>>();
            IgnoreSeasons = new List<int>();
            UseCustomShowName = false;
            CustomShowName = "";
            UseCustomLanguage = false;
            UseSequentialMatch = false;
            SeasonRules = new Dictionary<int, List<ShowRule>>();
            SeasonEpisodes = new Dictionary<int, List<ProcessedEpisode>>();
            ShowNextAirdate = true;
            TvdbCode = -1;
            AutoAddFolderBase = "";
            AutoAddCustomFolderFormat = "Season {Season:2}";
            AutoAddType = ShowItem.AutomaticFolderType.libraryDefault;
            DoRename = true;
            DoMissingCheck = true;
            CountSpecials = false;
            DvdOrder = false;
            CustomSearchUrl = "";
            UseCustomSearchUrl = false;
            ForceCheckNoAirdate = false;
            ForceCheckFuture = false;
            BannersLastUpdatedOnDisk = null; //assume that the baners are old and have expired
            ShowTimeZone = TimeZone.DefaultTimeZone(); // default, is correct for most shows
            lastFiguredTz = "";
        }

        public List<ShowRule> RulesForSeason(int n)
        {
            return SeasonRules.ContainsKey(n) ? SeasonRules[n] : null;
        }

        private string AutoFolderNameForSeason(Season s)
        {
            string r = AutoAddFolderBase;
            if (string.IsNullOrEmpty(r))
                return string.Empty;

            if (s == null) return string.Empty;

            if (!r.EndsWith(System.IO.Path.DirectorySeparatorChar.ToString()))
                r += System.IO.Path.DirectorySeparatorChar.ToString();

            if (AutoAddType == ShowItem.AutomaticFolderType.none)
            {
                return r;
            }

            if (AutoAddType == AutomaticFolderType.baseOnly)
            {
                return r;
            }

            if (s.IsSpecial())
            {
                return r + TVSettings.Instance.SpecialsFolderName;
            }

            if (AutoAddType == AutomaticFolderType.libraryDefault)
            {
                return r + CustomSeasonName.NameFor(s, TVSettings.Instance.SeasonFolderFormat);
            }

            if (AutoAddType == AutomaticFolderType.custom)
            {
                return r + CustomSeasonName.NameFor(s, AutoAddCustomFolderFormat);
            }

            return r;
        }

        public int MaxSeason()
        {
            int max = 0;
            foreach (KeyValuePair<int, List<ProcessedEpisode>> kvp in SeasonEpisodes)
            {
                if (kvp.Key > max)
                    max = kvp.Key;
            }
            return max;
        }

        //StringNiceName(int season)
        //{
        //    // something like "Simpsons (S3)"
        //    return String.Concat(ShowName," (S",season,")");
        //}

        public void WriteXmlSettings(XmlWriter writer)
        {
            writer.WriteStartElement("ShowItem");

            XmlHelper.WriteElementToXml(writer,"UseCustomShowName",UseCustomShowName);
            XmlHelper.WriteElementToXml(writer,"CustomShowName",CustomShowName);
            XmlHelper.WriteElementToXml(writer, "UseCustomLanguage", UseCustomLanguage);
            XmlHelper.WriteElementToXml(writer, "CustomLanguageCode", CustomLanguageCode);
            XmlHelper.WriteElementToXml(writer,"ShowNextAirdate",ShowNextAirdate);
            XmlHelper.WriteElementToXml(writer,"TVDBID",TvdbCode);
            XmlHelper.WriteElementToXml(writer, "FolderBase", AutoAddFolderBase);
            XmlHelper.WriteElementToXml(writer,"DoRename",DoRename);
            XmlHelper.WriteElementToXml(writer,"DoMissingCheck",DoMissingCheck);
            XmlHelper.WriteElementToXml(writer,"CountSpecials",CountSpecials);
            XmlHelper.WriteElementToXml(writer,"DVDOrder",DvdOrder);
            XmlHelper.WriteElementToXml(writer,"ForceCheckNoAirdate",ForceCheckNoAirdate);
            XmlHelper.WriteElementToXml(writer,"ForceCheckFuture",ForceCheckFuture);
            XmlHelper.WriteElementToXml(writer,"UseSequentialMatch",UseSequentialMatch);
            XmlHelper.WriteElementToXml(writer, "CustomFolderFormat", AutoAddCustomFolderFormat);
            XmlHelper.WriteElementToXml(writer, "AutoAddType", (int)AutoAddType );
            XmlHelper.WriteElementToXml(writer, "BannersLastUpdatedOnDisk", BannersLastUpdatedOnDisk);
            XmlHelper.WriteElementToXml(writer, "TimeZone", ShowTimeZone);

            writer.WriteStartElement("IgnoreSeasons");
            foreach (int i in IgnoreSeasons)
            {
                XmlHelper.WriteElementToXml(writer,"Ignore",i);
            }
            writer.WriteEndElement();

            writer.WriteStartElement("AliasNames");
            foreach (string str in AliasNames)
            {
                XmlHelper.WriteElementToXml(writer,"Alias",str);
            }
            writer.WriteEndElement();

            XmlHelper.WriteElementToXml(writer, "UseCustomSearchURL", UseCustomSearchUrl);
            XmlHelper.WriteElementToXml(writer, "CustomSearchURL",CustomSearchUrl);

            foreach (KeyValuePair<int, List<ShowRule>> kvp in SeasonRules)
            {
                if (kvp.Value.Count > 0)
                {
                    writer.WriteStartElement("Rules");
                    XmlHelper.WriteAttributeToXml(writer ,"SeasonNumber",kvp.Key);

                    foreach (ShowRule r in kvp.Value)
                        r.WriteXml(writer);

                    writer.WriteEndElement(); // Rules
                }
            }
            foreach (KeyValuePair<int, List<string>> kvp in ManualFolderLocations)
            {
                if (kvp.Value.Count > 0)
                {
                    writer.WriteStartElement("SeasonFolders");

                    XmlHelper.WriteAttributeToXml(writer,"SeasonNumber",kvp.Key);

                    foreach (string s in kvp.Value)
                    {
                        writer.WriteStartElement("Folder");
                        XmlHelper.WriteAttributeToXml(writer,"Location",s);
                        writer.WriteEndElement(); // Folder
                    }

                    writer.WriteEndElement(); // Rules
                }
            }
            writer.WriteEndElement(); // ShowItem
        }

        public static List<ProcessedEpisode> ProcessedListFromEpisodes(IEnumerable<Episode> el, ShowItem si)
        {
            List<ProcessedEpisode> pel = new List<ProcessedEpisode>();
            foreach (Episode e in el)
                pel.Add(new ProcessedEpisode(e, si));
            return pel;
        }

        public Dictionary<int, List<ProcessedEpisode>> GetDvdSeasons()
        {
            //We will create this on the fly
            Dictionary<int, List<ProcessedEpisode>> returnValue = new Dictionary<int, List<ProcessedEpisode>>();
            foreach (KeyValuePair<int, List<ProcessedEpisode>> kvp in SeasonEpisodes)
            {
                foreach (ProcessedEpisode ep in kvp.Value)
                {
                    if (!returnValue.ContainsKey(ep.DvdSeasonNumber ))
                    {
                        returnValue.Add(ep.DvdSeasonNumber, new List<ProcessedEpisode>());
                    }
                    returnValue[ep.DvdSeasonNumber].Add(ep);
                }
            }

            return returnValue;
        }

        public Dictionary<int, List<string>> AllFolderLocations() => AllFolderLocations( true);

        public Dictionary<int, List<string>> AllFolderLocationsEpCheck(bool checkExist) => AllFolderLocations(true, checkExist);

        public Dictionary<int, List<string>> AllFolderLocations(bool manualToo)=> AllFolderLocations(manualToo,true);

        public Dictionary<int, List<string>> AllFolderLocations(bool manualToo,bool checkExist)
        {
            Dictionary<int, List<string>> fld = new Dictionary<int, List<string>>();

            if (manualToo)
            {
                foreach (KeyValuePair<int, List<string>> kvp in ManualFolderLocations)
                {
                    if (!fld.ContainsKey(kvp.Key))
                        fld[kvp.Key] = new List<string>();
                    foreach (string s in kvp.Value)
                        fld[kvp.Key].Add(s.TrimSlash());
                }
            }

            if (AutoAddNewSeasons() && (!string.IsNullOrEmpty(AutoAddFolderBase)))
            {
                foreach (int i in SeasonEpisodes.Keys)
                {
                    if (IgnoreSeasons.Contains(i)) continue;

                    string newName = AutoFolderNameForSeason(i);
                    if (string.IsNullOrEmpty(newName)) continue;

                    if (checkExist && !Directory.Exists(newName)) continue;

                    if (!fld.ContainsKey(i)) fld[i] = new List<string>();

                    if (!fld[i].Contains(newName)) fld[i].Add(newName.TrimSlash());
                }
            }
            return fld;
        }

        public static int CompareShowItemNames(ShowItem one, ShowItem two)
        {
            string ones = one.ShowName; 
            string twos = two.ShowName; 
            return string.Compare(ones, twos, StringComparison.Ordinal);
        }

        public Season GetSeason(int snum)
        {
            Dictionary<int, Season> ssn = AppropriateSeasons();
            return ssn.ContainsKey(snum) ? ssn[snum] : null;
        }

        public void AddSeasonRule(int snum, ShowRule sr)
        {
            if (!SeasonRules.ContainsKey(snum)) SeasonRules[snum] = new List<ShowRule>();

            SeasonRules[snum].Add(sr);
        }

        public Dictionary<int,Season> AppropriateSeasons()
        {
            SeriesInfo s = TheSeries();
            if (s==null)return new Dictionary<int, Season>();
            return DvdOrder ? TheSeries().DvdSeasons : TheSeries().AiredSeasons;
        }

        public Season GetFirstAvailableSeason()
        {
            foreach (KeyValuePair<int, Season> x in AppropriateSeasons())
            {
                return x.Value;
            }

            return null;
        }

        public bool InOneFolder()
        {
            return (AutoAddType == AutomaticFolderType.baseOnly);
        }

        public string AutoFolderNameForSeason(int snum) => AutoFolderNameForSeason(GetSeason(snum));

        public bool AutoAddNewSeasons()
        {
            return (AutoAddType != AutomaticFolderType.none);
        }
    }
}
