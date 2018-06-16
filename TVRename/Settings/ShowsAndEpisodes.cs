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

// These are what is used when processing folders for missing episodes, renaming, etc. of files.

// A "ProcessedEpisode" is generated by processing an Episode from thetvdb, and merging/renaming/etc.
//
// A "ShowItem" is a show the user has added on the "My Shows" tab

// TODO: C++ to C# conversion stopped it using some of the typedefs, such as "IgnoreSeasonList".  (a) probably should
// rename that to something more generic like IntegerList, and (b) then put it back into the classes & functions
// that use it (e.g. ShowItem.IgnoreSeasons)

namespace TVRename
{
    public class ProcessedEpisode : Episode
    {
        public int EpNum2; // if we are a concatenation of episodes, this is the last one in the series. Otherwise, same as EpNum
        public bool Ignore;
        public bool NextToAir;
        public int OverallNumber;
        public ShowItem SI;
        public ProcessedEpisodeType type;
        public List<Episode> sourceEpisodes;

        public enum ProcessedEpisodeType { single, split, merged}

        public ProcessedEpisode(SeriesInfo ser, Season airseas, Season dvdseas, ShowItem si)
            : base(ser, airseas,dvdseas)
        {
            NextToAir = false;
            OverallNumber = -1;
            Ignore = false;
            EpNum2 = si.DVDOrder? DvdEpNum: AiredEpNum;
            SI = si;
            type = ProcessedEpisodeType.single;
        }

        public ProcessedEpisode(ProcessedEpisode O)
            : base(O)
        {
            NextToAir = O.NextToAir;
            EpNum2 = O.EpNum2;
            Ignore = O.Ignore;
            SI = O.SI;
            OverallNumber = O.OverallNumber;
            type = O.type;
        }

        public ProcessedEpisode(Episode e, ShowItem si)
            : base(e)
        {
            OverallNumber = -1;
            NextToAir = false;
            EpNum2 = si.DVDOrder ? DvdEpNum : AiredEpNum;
            Ignore = false;
            SI = si;
            type = ProcessedEpisodeType.single;
        }
        public ProcessedEpisode(Episode e, ShowItem si, ProcessedEpisodeType t)
            : base(e)
        {
            OverallNumber = -1;
            NextToAir = false;
            EpNum2 = si.DVDOrder ? DvdEpNum : AiredEpNum;
            Ignore = false;
            SI = si;
            type = t;
        }

        public ProcessedEpisode(Episode e, ShowItem si, List<Episode> episodes)
            : base(e)
        {
            OverallNumber = -1;
            NextToAir = false;
            EpNum2 = si.DVDOrder ? DvdEpNum : AiredEpNum;
            Ignore = false;
            SI = si;
            sourceEpisodes = episodes;
            type = ProcessedEpisodeType.merged;
        }

        public int AppropriateSeasonNumber => SI.DVDOrder ? DvdSeasonNumber : AiredSeasonNumber;

        public Season AppropriateSeason => SI.DVDOrder ? TheDvdSeason : TheAiredSeason;

        public int AppropriateEpNum
        {
            get => SI.DVDOrder ? DvdEpNum : AiredEpNum;
            set
            {
                if (SI.DVDOrder) DvdEpNum = value;
                else AiredEpNum = value;
            }
        }

        public string NumsAsString()
        {
            if (AppropriateEpNum == EpNum2)
                return AppropriateEpNum.ToString();
            else
                return AppropriateEpNum + "-" + EpNum2;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1011:ConsiderPassingBaseTypesAsParameters")]
        public static int EPNumberSorter(ProcessedEpisode e1, ProcessedEpisode e2)
        {
            int ep1 = e1.AiredEpNum;
            int ep2 = e2.AiredEpNum;

            return ep1 - ep2;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1011:ConsiderPassingBaseTypesAsParameters")]
        public static int DVDOrderSorter(ProcessedEpisode e1, ProcessedEpisode e2)
        {
            int ep1 = e1.DvdEpNum;
            int ep2 = e2.DvdEpNum;

            return ep1 - ep2;
        }

        public DateTime? GetAirDateDT(bool inLocalTime)
        {
            if (!inLocalTime)
                return GetAirDateDt();
            // do timezone adjustment
            return GetAirDateDt(SI.GetTimeZone());
        }

        public string HowLong()
        {
            DateTime? airsdt = GetAirDateDT(true);
            if (airsdt == null)
                return "";
            DateTime dt = (DateTime)airsdt;

            TimeSpan ts = dt.Subtract(DateTime.Now); // how long...
            if (ts.TotalHours < 0)
                return "Aired";
            else
            {
                int h = ts.Hours;
                if (ts.TotalHours >= 1)
                {
                    if (ts.Minutes >= 30)
                        h += 1;
                    return ts.Days + "d " + h + "h"; // +ts->Minutes+"m "+ts->Seconds+"s";
                }
                else
                    return Math.Round(ts.TotalMinutes) + "min";
            }
        }

        public string DayOfWeek()
        {
            DateTime? dt = GetAirDateDT(true);
            return (dt != null) ? dt.Value.ToString("ddd") : "-";
        }

        public string TimeOfDay()
        {
            DateTime? dt = GetAirDateDT(true);
            return (dt != null) ? dt.Value.ToString("t") : "-";
        }
    }

public class ShowItem
    {
        public bool AutoAddNewSeasons;
        public string AutoAdd_FolderBase; // TODO: use magical renaming tokens here
        public bool AutoAdd_FolderPerSeason;
        public string AutoAdd_SeasonFolderName; // TODO: use magical renaming tokens here

        public bool CountSpecials;
        public string CustomShowName;
        public bool DVDOrder; // sort by DVD order, not the default sort we get
        public bool DoMissingCheck;
        public bool DoRename;
        public bool ForceCheckFuture;
        public bool ForceCheckNoAirdate;
        public List<int> IgnoreSeasons;
        public Dictionary<int, List<string>> ManualFolderLocations;
        public bool PadSeasonToTwoDigits;
        public Dictionary<int, List<ProcessedEpisode>> SeasonEpisodes; // built up by applying rules.
        public Dictionary<int, List<ShowRule>> SeasonRules;
        public bool ShowNextAirdate;
        public int TVDBCode;
        public bool UseCustomShowName;
        public bool UseSequentialMatch;
        public List<string> AliasNames = new List<string>();
        public bool UseCustomSearchURL;
        public string CustomSearchURL;

        public string ShowTimeZone;
        private TimeZone SeriesTZ;
        private string LastFiguredTZ;
        
        public DateTime? BannersLastUpdatedOnDisk { get; set; }

        public ShowItem()
        {
            SetDefaults();
        }

        public ShowItem(int tvDBCode)
        {
            SetDefaults();
            TVDBCode = tvDBCode;
        }

        private void FigureOutTimeZone()
        {
            string tzstr = ShowTimeZone;

            if (string.IsNullOrEmpty(tzstr))
                tzstr = TimeZone.DefaultTimeZone();

            SeriesTZ = TimeZone.TimeZoneFor(tzstr);

            LastFiguredTZ = tzstr;
        }

        public TimeZone GetTimeZone()
        {
            // we cache the timezone info, as the fetching is a bit slow, and we do this a lot
            if (LastFiguredTZ != ShowTimeZone)
                FigureOutTimeZone();

            return SeriesTZ;
        }

        public ShowItem(XmlReader reader)
        {
            SetDefaults();

            reader.Read();
            if (reader.Name != "ShowItem")
                return; // bail out

            reader.Read();
            while (!reader.EOF)
            {
                if ((reader.Name == "ShowItem") && !reader.IsStartElement())
                    break; // all done

                if (reader.Name == "ShowName")
                {
                    CustomShowName = reader.ReadElementContentAsString();
                    UseCustomShowName = true;
                }
                if (reader.Name == "UseCustomShowName")
                    UseCustomShowName = reader.ReadElementContentAsBoolean();
                if (reader.Name == "CustomShowName")
                    CustomShowName = reader.ReadElementContentAsString();
                else if (reader.Name == "TVDBID")
                    TVDBCode = reader.ReadElementContentAsInt();
                else if (reader.Name == "CountSpecials")
                    CountSpecials = reader.ReadElementContentAsBoolean();
                else if (reader.Name == "ShowNextAirdate")
                    ShowNextAirdate = reader.ReadElementContentAsBoolean();
                else if (reader.Name == "AutoAddNewSeasons")
                    AutoAddNewSeasons = reader.ReadElementContentAsBoolean();
                else if (reader.Name == "FolderBase")
                    AutoAdd_FolderBase = reader.ReadElementContentAsString();
                else if (reader.Name == "FolderPerSeason")
                    AutoAdd_FolderPerSeason = reader.ReadElementContentAsBoolean();
                else if (reader.Name == "SeasonFolderName")
                    AutoAdd_SeasonFolderName = reader.ReadElementContentAsString();
                else if (reader.Name == "DoRename")
                    DoRename = reader.ReadElementContentAsBoolean();
                else if (reader.Name == "DoMissingCheck")
                    DoMissingCheck = reader.ReadElementContentAsBoolean();
                else if (reader.Name == "DVDOrder")
                    DVDOrder = reader.ReadElementContentAsBoolean();
                else if (reader.Name == "UseCustomSearchURL")
                    UseCustomSearchURL = reader.ReadElementContentAsBoolean();
                else if (reader.Name == "CustomSearchURL")
                    CustomSearchURL = reader.ReadElementContentAsString();
                else if (reader.Name == "TimeZone")
                    ShowTimeZone = reader.ReadElementContentAsString();
                else if (reader.Name == "ForceCheckAll") // removed 2.2.0b2
                    ForceCheckNoAirdate = ForceCheckFuture = reader.ReadElementContentAsBoolean();
                else if (reader.Name == "ForceCheckFuture")
                    ForceCheckFuture = reader.ReadElementContentAsBoolean();
                else if (reader.Name == "ForceCheckNoAirdate")
                    ForceCheckNoAirdate = reader.ReadElementContentAsBoolean();
                else if (reader.Name == "PadSeasonToTwoDigits")
                    PadSeasonToTwoDigits = reader.ReadElementContentAsBoolean();
                else if (reader.Name == "BannersLastUpdatedOnDisk")
                {
                    if (!reader.IsEmptyElement)
                    {
                        BannersLastUpdatedOnDisk = reader.ReadElementContentAsDateTime();
                    }
                    else
                        reader.Read();
                }

                else if (reader.Name == "UseSequentialMatch")
                    UseSequentialMatch = reader.ReadElementContentAsBoolean();
                else if (reader.Name == "IgnoreSeasons")
                {
                    if (!reader.IsEmptyElement)
                    {
                        reader.Read();
                        while (reader.Name != "IgnoreSeasons")
                        {
                            if (reader.Name == "Ignore")
                                IgnoreSeasons.Add(reader.ReadElementContentAsInt());
                            else
                                reader.ReadOuterXml();
                        }
                    }
                    reader.Read();
                }
                else if (reader.Name == "AliasNames")
                {
                    if (!reader.IsEmptyElement)
                    {
                        reader.Read();
                        while (reader.Name != "AliasNames")
                        {
                            if (reader.Name == "Alias")
                                AliasNames.Add(reader.ReadElementContentAsString());
                            else
                                reader.ReadOuterXml();
                        }
                    }
                    reader.Read();
                }
                else if (reader.Name == "Rules")
                {
                    if (!reader.IsEmptyElement)
                    {
                        int snum = int.Parse(reader.GetAttribute("SeasonNumber"));
                        SeasonRules[snum] = new List<ShowRule>();
                        reader.Read();
                        while (reader.Name != "Rules")
                        {
                            if (reader.Name == "Rule")
                            {
                                SeasonRules[snum].Add(new ShowRule(reader.ReadSubtree()));
                                reader.Read();
                            }
                        }
                    }
                    reader.Read();
                }
                else if (reader.Name == "SeasonFolders")
                {
                    if (!reader.IsEmptyElement)
                    {
                        int snum = int.Parse(reader.GetAttribute("SeasonNumber"));
                        ManualFolderLocations[snum] = new List<string>();
                        reader.Read();
                        while (reader.Name != "SeasonFolders")
                        {
                            if ((reader.Name == "Folder") && reader.IsStartElement())
                            {
                                string ff = reader.GetAttribute("Location");
                                if (AutoFolderNameForSeason(snum) != ff)
                                    ManualFolderLocations[snum].Add(ff);
                            }
                            reader.Read();
                        }
                    }
                    reader.Read();
                }
                else
                    reader.ReadOuterXml();
            } // while
        }

        internal bool UsesManualFolders()
        {
            return ManualFolderLocations.Count>0;
        }

        public SeriesInfo TheSeries()
        {
            return TheTVDB.Instance.GetSeries(TVDBCode);
        }

        public string ShowName
        {
            get
            {
                if (UseCustomShowName)
                    return CustomShowName;
                SeriesInfo ser = TheSeries();
                if (ser != null)
                    return ser.Name;
                return "<" + TVDBCode + " not downloaded>";
            }
        }

        public List<string> getSimplifiedPossibleShowNames()
        {
            List<string> possibles = new List<string>();

            string simplifiedShowName = Helpers.SimplifyName(ShowName);
            if (!(simplifiedShowName == "")) { possibles.Add( simplifiedShowName); }

            //Check the custom show name too
            if (UseCustomShowName)
            {
                string simplifiedCustomShowName = Helpers.SimplifyName(CustomShowName);
                if (!(simplifiedCustomShowName == "")) { possibles.Add(simplifiedCustomShowName); }
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
            NoEpisodesOrSeasons,
            Aired,
            PartiallyAired,
            NoneAired
        }

        public ShowAirStatus SeasonsAirStatus
        {
            get
            {
                if (HasSeasonsAndEpisodes)
                {
                    if (HasAiredEpisodes && !HasUnairedEpisodes)
                    {
                        return ShowAirStatus.Aired;
                    }
                    else if (HasUnairedEpisodes && !HasAiredEpisodes)
                    {
                        return ShowAirStatus.NoneAired;
                    }
                    else if (HasAiredEpisodes && HasUnairedEpisodes)
                    {
                        return ShowAirStatus.PartiallyAired;
                    }
                    else
                    {
                        //System.Diagnostics.Debug.Assert(false, "That is weird ... we have 'seasons and episodes' but none are aired, nor unaired. That case shouldn't actually occur !");
                        return ShowAirStatus.NoEpisodesOrSeasons;
                    }
                }
                else
                {
                    return ShowAirStatus.NoEpisodesOrSeasons;
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

        public void SetDefaults()
        {
            ManualFolderLocations = new Dictionary<int, List<string>>();
            IgnoreSeasons = new List<int>();
            UseCustomShowName = false;
            CustomShowName = "";
            UseSequentialMatch = false;
            SeasonRules = new Dictionary<int, List<ShowRule>>();
            SeasonEpisodes = new Dictionary<int, List<ProcessedEpisode>>();
            ShowNextAirdate = true;
            TVDBCode = -1;
            AutoAddNewSeasons = true;
            PadSeasonToTwoDigits = false;
            AutoAdd_FolderBase = "";
            AutoAdd_FolderPerSeason = true;
            AutoAdd_SeasonFolderName = "Season ";
            DoRename = true;
            DoMissingCheck = true;
            CountSpecials = false;
            DVDOrder = false;
            CustomSearchURL = "";
            UseCustomSearchURL = false;
            ForceCheckNoAirdate = false;
            ForceCheckFuture = false;
            BannersLastUpdatedOnDisk = null; //assume that the baners are old and have expired
            ShowTimeZone = TimeZone.DefaultTimeZone(); // default, is correct for most shows
            LastFiguredTZ = "";
        }

        public List<ShowRule> RulesForSeason(int n)
        {
            return SeasonRules.ContainsKey(n) ? SeasonRules[n] : null;
        }

        public string AutoFolderNameForSeason(int n)
        {
            bool leadingZero = TVSettings.Instance.LeadingZeroOnSeason || PadSeasonToTwoDigits;
            string r = AutoAdd_FolderBase;
            if (string.IsNullOrEmpty(r))
                return "";

            if (!r.EndsWith(System.IO.Path.DirectorySeparatorChar.ToString()))
                r += System.IO.Path.DirectorySeparatorChar.ToString();
            if (AutoAdd_FolderPerSeason)
            {
                if (n == 0)
                    r += TVSettings.Instance.SpecialsFolderName;
                else
                {
                    r += AutoAdd_SeasonFolderName;
                    if ((n < 10) && leadingZero)
                        r += "0";
                    r += n.ToString();
                }
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

        public void WriteXMLSettings(XmlWriter writer)
        {
            writer.WriteStartElement("ShowItem");

            XmlHelper.WriteElementToXml(writer,"UseCustomShowName",UseCustomShowName);
            XmlHelper.WriteElementToXml(writer,"CustomShowName",CustomShowName);
            XmlHelper.WriteElementToXml(writer,"ShowNextAirdate",ShowNextAirdate);
            XmlHelper.WriteElementToXml(writer,"TVDBID",TVDBCode);
            XmlHelper.WriteElementToXml(writer,"AutoAddNewSeasons",AutoAddNewSeasons);
            XmlHelper.WriteElementToXml(writer,"FolderBase",AutoAdd_FolderBase);
            XmlHelper.WriteElementToXml(writer,"FolderPerSeason",AutoAdd_FolderPerSeason);
            XmlHelper.WriteElementToXml(writer,"SeasonFolderName",AutoAdd_SeasonFolderName);
            XmlHelper.WriteElementToXml(writer,"DoRename",DoRename);
            XmlHelper.WriteElementToXml(writer,"DoMissingCheck",DoMissingCheck);
            XmlHelper.WriteElementToXml(writer,"CountSpecials",CountSpecials);
            XmlHelper.WriteElementToXml(writer,"DVDOrder",DVDOrder);
            XmlHelper.WriteElementToXml(writer,"ForceCheckNoAirdate",ForceCheckNoAirdate);
            XmlHelper.WriteElementToXml(writer,"ForceCheckFuture",ForceCheckFuture);
            XmlHelper.WriteElementToXml(writer,"UseSequentialMatch",UseSequentialMatch);
            XmlHelper.WriteElementToXml(writer,"PadSeasonToTwoDigits",PadSeasonToTwoDigits);
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

            XmlHelper.WriteElementToXml(writer, "UseCustomSearchURL", UseCustomSearchURL);
            XmlHelper.WriteElementToXml(writer, "CustomSearchURL",CustomSearchURL);

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

        public static List<ProcessedEpisode> ProcessedListFromEpisodes(List<Episode> el, ShowItem si)
        {
            List<ProcessedEpisode> pel = new List<ProcessedEpisode>();
            foreach (Episode e in el)
                pel.Add(new ProcessedEpisode(e, si));
            return pel;
        }

        public Dictionary<int, List<ProcessedEpisode>> GetDVDSeasons()
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

        public Dictionary<int, List<string>> AllFolderLocations()
        {
            return AllFolderLocations( true);
        }

        public Dictionary<int, List<string>> AllFolderLocationsEpCheck(bool checkExist)
        {
            return AllFolderLocations(true, checkExist);
        }

        public Dictionary<int, List<string>> AllFolderLocations(bool manualToo,bool checkExist=true)
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

            if (AutoAddNewSeasons && (!string.IsNullOrEmpty(AutoAdd_FolderBase)))
            {
                int highestThereIs = -1;
                foreach (KeyValuePair<int, List<ProcessedEpisode>> kvp in SeasonEpisodes)
                {
                    if (kvp.Key > highestThereIs)
                        highestThereIs = kvp.Key;
                }
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
            string ones = one.ShowName; // + " " +one->SeasonNumber.ToString("D3");
            string twos = two.ShowName; // + " " +two->SeasonNumber.ToString("D3");
            return ones.CompareTo(twos);
        }

        public Season GetSeason(int snum)
        {
            return DVDOrder? TheSeries().DVDSeasons[snum]: TheSeries().AiredSeasons[snum];
        }

        public void AddSeasonRule(int snum, ShowRule sr)
        {
            if (!SeasonRules.ContainsKey(snum)) SeasonRules[snum] = new List<ShowRule>();

            SeasonRules[snum].Add(sr);
        }
    }
}