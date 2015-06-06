using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using HtmlAgilityPack;
using System;
using Newtonsoft.Json;
using System.IO;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace BusStationCrawler
{
    public class BusData
    {
        public string busName;
        public string region;
        public string[] stations_go;
        public string[] stations_back;
    }


    class Program
    {
        static void Crawl(WebClient client, BusData bus)
        {
            string sHtml = Encoding.UTF8.GetString(client.DownloadData(
                "http://pda.5284.com.tw/MQS/businfo2.jsp?routename=" + Uri.EscapeUriString(bus.busName)));
            var doc = new HtmlDocument();
            doc.LoadHtml(sHtml);

            string xpathroot = "/html/body/center/table/tr[6]/td/table/";

            HtmlNodeCollection nodes = doc.DocumentNode.SelectNodes(
                xpathroot+"tr[2]/td[1]/table");
            if (nodes == null)
            {
                // get `go` dir station table 取得去程table
                nodes = doc.DocumentNode.SelectNodes(
                    xpathroot+"tr[1]/td[1]/table");
                if (nodes == null)
                    throw new WebException("No items found");

                Debug.Assert(nodes.Count > 0
                    && nodes[0].ChildNodes.Count > 0
                    && nodes[0].ChildNodes.Count % 2 == 1);
                int num_stations = (nodes[0].ChildNodes.Count - 1) / 2;
                bus.stations_go = new string[num_stations];
                for (int s = 1; s <= num_stations; ++s)
                {
                    nodes = doc.DocumentNode.SelectNodes(
                        xpathroot+"tr[1]/td[1]/table/tr[{0}]/td[1]".Fmt(s));
                    bus.stations_go[s-1] = nodes[0].InnerText;
                }

            }
            else
            {
                // dir = go
                Debug.Assert(nodes.Count > 0
                    && nodes[0].ChildNodes.Count > 0
                    && nodes[0].ChildNodes.Count % 2 == 1);
                int num_stations = (nodes[0].ChildNodes.Count - 1) / 2;
                bus.stations_go = new string[num_stations];
                for (int s = 1; s <= num_stations; ++s)
                {
                    nodes = doc.DocumentNode.SelectNodes(
                        xpathroot+"tr[2]/td[1]/table/tr[{0}]/td[1]".Fmt(s));
                    bus.stations_go[s-1] = nodes[0].InnerText;
                }

                //dir=back
                nodes = doc.DocumentNode.SelectNodes(
                    xpathroot+"tr[2]/td[2]/table");
                Debug.Assert(nodes.Count > 0
                    && nodes[0].ChildNodes.Count > 0
                    && nodes[0].ChildNodes.Count % 2 == 1);
                num_stations = (nodes[0].ChildNodes.Count - 1) / 2;
                bus.stations_back = new string[num_stations];
                for (int s = 1; s <= num_stations; ++s)
                {
                    nodes = doc.DocumentNode.SelectNodes(
                        xpathroot+"tr[2]/td[2]/table/tr[{0}]/td[1]".Fmt(s));
                    bus.stations_back[s-1] = nodes[0].InnerText;
                }
            }
        }

        public class BusData
        {
            public string busName;
            public string region;
            public string[] stations_go;
            public string[] stations_back;
        }

        public enum BusDir
        {
            go, back,
        };

        public class BusTag
        {
            public string busName { get; set; }
            public string tag { get; set; }
            public BusDir dir { get; set; }
            public string station { get; set; }
            public string timeToArrive { get; set; }
        }


        static BusTag[] m_busTags = new BusTag[]
        {
            new BusTag{busName="橘2", station="秀山國小", dir=BusDir.go, tag="上班", timeToArrive="快來了"}, 
            new BusTag{busName="275", station="秀景里", dir=BusDir.go, tag="上班", timeToArrive="20分"}, 
            new BusTag{busName="敦化幹線", station="秀景里", dir=BusDir.back, tag="上班", timeToArrive="未發車"},
            new BusTag{busName="橘2", station="捷運永安市場站", dir=BusDir.back, tag="回家", timeToArrive="6分"}, 
            new BusTag{busName="敦化幹線", station="忠孝敦化路口", dir=BusDir.go, tag="回家", timeToArrive="已過站"}, 
            new BusTag{busName="275", station="忠孝敦化路口", dir=BusDir.back, tag="回家", timeToArrive="未班車"}, 
        };
        static void Main_WriteMyBusTags(string[] param)
        {
            JsonSerializer serializer = new JsonSerializer();
            serializer.NullValueHandling = NullValueHandling.Ignore;
            using (StreamWriter sw = new StreamWriter(@"my_bustags.json"))
            using (JsonWriter writer = new JsonTextWriter(sw))
            {
                serializer.Serialize(writer, m_busTags);
            }
        }


        /// <summary>
        /// Read My bus tags
        /// </summary>
        /// <param name="param"></param>
        static void MainReadMyBusTags(string[] param)
        {
            BusTag[] busTags = null;

            JsonSerializer serializer = new JsonSerializer();
            serializer.NullValueHandling = NullValueHandling.Ignore;
            using (StreamReader sr = new StreamReader(@"my_bustags.json"))
            using (JsonReader reader = new JsonTextReader(sr))
            {
                busTags = serializer.Deserialize(reader, typeof(BusTag[])) as BusTag[];
            }
            Debug.Assert(busTags.Length>0);
            Debug.WriteLine("busTags[0].dir=" + busTags[0].dir);
        }

        /// <summary>
        /// ToLineBuses
        /// </summary>
        /// <param name="param"></param>
        static void MainToLineBuses(string[] param)
        {
            Dictionary<string, List<string>> all_buses = null;

            JsonSerializer serializer = new JsonSerializer();
            serializer.NullValueHandling = NullValueHandling.Ignore;
            using (StreamReader sr = new StreamReader(@"bus_dict.json"))
            using (JsonReader reader = new JsonTextReader(sr))
            {
                all_buses = serializer.Deserialize(reader, typeof(Dictionary<string, List<string>>)) as Dictionary<string, List<string>>;
            }
            var bus_names = new string[all_buses.Keys.Count];
            all_buses.Keys.CopyTo(bus_names, 0);
            Array.Sort(bus_names);

            using( var wr = File.CreateText(@"all_buses.txt"))
            {
                foreach (string b in bus_names)
                    wr.WriteLine(b);
            }
        }

        /// <summary>
        /// regex test to distinct their area
        /// </summary>
        /// <param name="param"></param>
        static void Main_RegexTestToDistinguishArea(string[] param)
        {
            // Debug.Assert(Regex.IsMatch("275", @"^\d{1,3}[^\d]*$"));
            // Debug.Assert(Regex.IsMatch("275副", @"^\d{1,3}[^\d]*$"));
            var m_all_buses = new Dictionary<string, StationPair>();
            using (StreamReader sr = new StreamReader(@"all_buses.txt"))
            {
                string busName;
                while ((busName = sr.ReadLine()) != null)
                {
                    var stp = new StationPair();
                    string stations_go_line = sr.ReadLine();
                    stp.stations_go = stations_go_line.Split(new string[] { field_separator }, StringSplitOptions.RemoveEmptyEntries);
                    string stations_back_line = sr.ReadLine();
                    stp.stations_back = stations_back_line.Split(new string[] { field_separator }, StringSplitOptions.RemoveEmptyEntries);
                    m_all_buses[busName] = stp;
                }
            }

            foreach(string busName in m_all_buses.Keys)
            {
                if (Regex.IsMatch(busName, @"^\d{4}") && busName.Contains("→"))
                    Debug.WriteLine("公路客運     " + busName);
                else if (Regex.IsMatch(busName, @"^\d{1,4}[^\d]*$"))
                    Debug.WriteLine("四數內 "+ busName );
                else if(Regex.IsMatch(busName, @"^紅"))
                    Debug.WriteLine("紅     " + busName);
                else if (Regex.IsMatch(busName, @"^藍"))
                    Debug.WriteLine("藍     " + busName);
                else if (Regex.IsMatch(busName, @"^棕"))
                    Debug.WriteLine("棕     " + busName);
                else if (Regex.IsMatch(busName, @"^綠"))
                    Debug.WriteLine("綠     " + busName);
                else if (Regex.IsMatch(busName, @"^橘"))
                    Debug.WriteLine("橘     " + busName);
                else if (Regex.IsMatch(busName, @"^小"))
                    Debug.WriteLine("小     " + busName);
                else if (Regex.IsMatch(busName, @"^市民"))
                    Debug.WriteLine("市民   " + busName);
                else if (Regex.IsMatch(busName, @"^F"))
                    Debug.WriteLine("Ｆ     " + busName);
                else if (Regex.IsMatch(busName, @"幹線"))
                    Debug.WriteLine("幹線   " + busName);
                else if (Regex.IsMatch(busName, @"先導") || Regex.IsMatch(busName, @"環狀"))
                    Debug.WriteLine("先導或環狀   " + busName);
                else if (Regex.IsMatch(busName, @"通勤"))
                    Debug.WriteLine("通勤   " + busName);
                else
                    Debug.WriteLine("其他     " + busName);
            }

        }


        const string field_separator = "   ";
        /// <summary>
        /// read buses_simple.json and convert to simple txt, which separate by triple-whitespace
        /// write all_buses.txt
        /// </summary>
        /// <param name="param"></param>
        static void Main_json_to_txt(string[] param)
        {
            JsonSerializer serializer = new JsonSerializer();
            serializer.NullValueHandling = NullValueHandling.Ignore;

            Dictionary<string, StationPair> busStatDict;
            using (StreamReader sr = new StreamReader(@"buses_simple.json"))
            using (JsonReader reader = new JsonTextReader(sr))
            {
                busStatDict = serializer.Deserialize(reader, typeof(Dictionary<string, StationPair>)) as Dictionary<string, StationPair>;
            }

            using(StreamWriter sw = new StreamWriter(@"all_buses.txt"))
            {
                foreach(var kv in busStatDict)
                {
                    // BusName
                    sw.WriteLine(kv.Key);
                    // stations_go
                    sw.WriteLine( field_separator + String.Join(field_separator, kv.Value.stations_go) );
                    // stations_back
                    sw.WriteLine(field_separator + String.Join(field_separator, kv.Value.stations_back));
                }
            }
        }

        class StationPair
        {
            public string[] stations_go;
            public string[] stations_back;
        }
        /// <summary>
        /// convert buses.json from list<BusData> to dict[busName] => (statitions_go, stations_back)
        /// </summary>
        /// <param name="param"></param>
        static void Main_Write_buses_simple(string[] param)
        {
            List<BusData> busData = null;

            JsonSerializer serializer = new JsonSerializer();
            serializer.NullValueHandling = NullValueHandling.Ignore;
            using (StreamReader sr = new StreamReader(@"buses.json"))
            using (JsonReader reader = new JsonTextReader(sr))
            {
                busData = serializer.Deserialize(reader, typeof(List<BusData>)) as List<BusData>;
            }

            Dictionary<string, StationPair> busStatDict = new Dictionary<string,StationPair>();
            foreach (BusData bd in busData)
            {
                var newStat = new StationPair { stations_go = bd.stations_go, stations_back=bd.stations_back };
                if( busStatDict.ContainsKey(bd.busName) )
                {
                    var oldStat  = busStatDict[bd.busName];
                    var oldLength = oldStat.stations_go.Length + oldStat.stations_back.Length;
                    var newLength = newStat.stations_go.Length + newStat.stations_back.Length;
                    if(oldLength <= newLength)
                        busStatDict[bd.busName] = newStat;
                    else
                    {
                        Debug.WriteLine("{0} has diffrent bus stations oldLength={1}, newLength={2}", 
                            bd.busName, oldLength, newLength);
                    }
                }
                else
                {
                    busStatDict[bd.busName] = newStat;
                }
            }

            using (StreamWriter sw = new StreamWriter(@"buses_simple.json"))
            using (JsonWriter writer = new JsonTextWriter(sw))
            {
                serializer.Serialize(writer, busStatDict);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="param"></param>
        static void MainFindDuplicateBus(string[] param)
        {
            JsonSerializer serializer = new JsonSerializer();
            serializer.NullValueHandling = NullValueHandling.Ignore;
            List<BusData> buses;

            using (var sr = new StreamReader(@"buses.json"))
            using (var reader = new JsonTextReader(sr))
            {
                buses = serializer.Deserialize(reader, typeof(List<BusData>)) as List<BusData>;
            }
            
            //var duplicateBuses = from b in buses group b by b.busName into g where g.Count()>1 select g.Key;
            //Debug.WriteLine("duplicateBuses.Count() = " + duplicateBuses.Count());

            var dict = new Dictionary<string, HashSet<string> >();
            foreach(var bus in buses){
                if(dict.ContainsKey(bus.busName)){
                    dict[bus.busName].Add(bus.region);
                    //entry.Item2.Add(bus.region);
                    //var regions = String.Join(",", dict[bus.busName].Item1);
                    //Debug.WriteLine("{0} contains {1}", bus.busName, regions );
                    //dict[bus.busName].Item2.UnionWith(bus.stations_go);
                    //if (bus.stations_back != null)
                    //    dict[bus.busName].Item2.UnionWith(bus.stations_back);
                }
                else
                {
                    dict[bus.busName] = new HashSet<string>(
                        new HashSet<string>{bus.region} );
                    //if (bus.stations_back != null)
                    //    dict[bus.busName].Item2.UnionWith(bus.stations_back);
                }
            }

            var dict2 = from e in dict select new { Name = e.Key, Count = e.Value.Count };
            using (StreamWriter sw = new StreamWriter(@"bus_dict2.json"))
            using (JsonWriter writer = new JsonTextWriter(sw))
            {
                serializer.Serialize(writer, dict2);
            }

            using (StreamWriter sw = new StreamWriter(@"bus_dict.json"))
            using (JsonWriter writer = new JsonTextWriter(sw))
            {
                serializer.Serialize(writer, dict);
            }
        }


        /// <summary>
        ///   Convert Bus-centered to Station-Centered
        /// </summary>
        /// <param name="param"></param>
        static void MainConvertFromBusToStation(string[] param)
        {
            JsonSerializer serializer = new JsonSerializer();
            serializer.NullValueHandling = NullValueHandling.Ignore;
            List<BusData> buses;

            using (var sr = new StreamReader(@"buses.json"))
            using (var reader = new JsonTextReader(sr))
            {
                buses = serializer.Deserialize(reader, typeof(List<BusData>)) as List<BusData>;
            }
            
            Debug.WriteLine("buses.Count="+ buses.Count);

            var stations = new Dictionary<string, HashSet<string>>();
            foreach(var bus in buses)
            {

                foreach(var st in bus.stations_go)
                {
                    if(!stations.ContainsKey(st))
                        stations.Add(st, new HashSet<string>{ bus.busName}  );
                    else
                        stations[st].Add(bus.busName);
                }
                foreach(var st in bus.stations_back)
                {
                    if(!stations.ContainsKey(st))
                        stations.Add(st, new HashSet<string>{ bus.busName} );
                    else
                        stations[st].Add( bus.busName );
                }
            }
            Debug.WriteLine("stations.Count = "+ stations.Count);

            foreach (var st in stations)
            {
                if (st.Value.Count > 1)
                    Debug.WriteLine("{0} has {1} buses", st.Key, st.Value.Count);                
            }

            serializer = new JsonSerializer();
            serializer.NullValueHandling = NullValueHandling.Ignore;

            using (StreamWriter sw = new StreamWriter(@"stations_noregion.json"))
            using (JsonWriter writer = new JsonTextWriter(sw))
            {
                serializer.Serialize(writer, stations);
            }
        }

        static void Main()
        {
            var client = new WebClient();
            string sHtml = Encoding.UTF8.GetString( client.DownloadData("http://pda.5284.com.tw/MQS/businfo1.jsp"));

            var doc = new HtmlDocument();
            HtmlNode.ElementsFlags.Remove("option");
            doc.LoadHtml(sHtml);

            var buses = new List<BusData>();
            for (int i = 5; i <= 14; ++i)
            {
                HtmlNodeCollection nodes = doc.DocumentNode.SelectNodes("/html/body/center/table[1]/tr[" + i + "]/td/select/option");
                string regionName = nodes[0].InnerText.Replace("->", "").Replace("選擇", "");
                nodes.RemoveAt(0);
                buses.AddRange(from node in nodes select new BusData { busName = node.InnerText, region = regionName });
            }

            foreach(var bus in buses)
            {
                do
                {
                    try
                    {
                        Debug.WriteLine("bus="+ bus.busName);
                        Crawl(client, bus);
                    }
                    catch (WebException wex)
                    {
                        Debug.WriteLine(wex);
                        continue;
                    }
                    catch (Exception x)
                    {
                        Debug.WriteLine(x);
                    }

                } while (false);
            }


            JsonSerializer serializer = new JsonSerializer();
            serializer.NullValueHandling = NullValueHandling.Ignore;

            using (StreamWriter sw = new StreamWriter(@"buses.json"))
            using (JsonWriter writer = new JsonTextWriter(sw))
            {
                writer.Formatting = Formatting.Indented;
                serializer.Serialize(writer, buses);
            }

        }
    }

    public static class ExtensionMethods
    {
        public static bool IsNone(this String s)
        {
            return String.IsNullOrEmpty(s);
        }
        public static string DumpStr(this Exception ex)
        {
            return String.Format("{{Msg=\"{0}\",StackTrace=\"{1}\"}}", ex.Message, ex.StackTrace);
        }

        public static TValue GetValue<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key, TValue defaultValue)
        {
            if (!dict.ContainsKey(key))
                return defaultValue;
            return dict[key];
        }

        public static string DumpStr<TKey, TValue>(this IDictionary<TKey, TValue> dict)
        {
            return "{" + String.Join(",", dict.Select(kv => kv.Key + "=" + kv.Value)) + "}";
        }

        public static string DumpArray<T>(this IEnumerable<T> arr)
        {
            return "[" + arr.Count() + "]{" + String.Join(", ", arr.Select(x => x.ToString())) + "}";
        }

        public static String Fmt(this String fmt, params object[] args)
        {
            return String.Format(fmt, args);
        }
        public static T LastElement<T>(this T[] arr)
        {
            return arr[arr.Length - 1];
        }

        public static String Joyn(this String separator, IEnumerable<object> values)
        {
            return String.Join(separator, values);
        }
    }
}
