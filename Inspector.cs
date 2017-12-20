using HtmlAgilityPack;
using ScrapySharp.Extensions;
using ScrapySharp.Network;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace USATTInspector
{
    public class Player
    {
        public long USATTID { get; set; }
        public string PictureURL { get; set; }
        public string Profile { get; set; }
        public string Name { get; set; }
        public string Location { get; set; }
        public int Rating { get; set; }
        public string Date { get; set; }
    }

    class Inspector
    {
        static ScrapingBrowser _browser;
        static string[] _query;
        static HashSet<long> _usattNumbers;
        const string USATT_QUERY_URL = "https://usatt.simplycompete.com/userAccount/s?searchBy=&query={0}&max={1}&offset={2}";
        const int PAGE_SIZE = 200;

        static StreamWriter _fileOutput;
        static TextWriter _consoleOutput;

        static Inspector()
        {
            _browser = new ScrapingBrowser();
            _browser.AllowAutoRedirect = true;
            _browser.AllowMetaRedirect = true;

            _query = new string[] { "a", "e", "i", "o", "u" };

            _usattNumbers = new HashSet<long>();
        }

        static void Main(string[] args)
        {
            #region Open or create output file
            string fileName = "USATTUsers";
            FileStream ostrm;
            try
            {
                ostrm = new FileStream("./" + fileName + ".txt", FileMode.OpenOrCreate, FileAccess.Write);
                _fileOutput = new StreamWriter(ostrm);
            }
            catch (Exception e)
            {
                Console.WriteLine("Cannot open " + fileName + ".txt for writing.");
                Console.WriteLine(e.Message);
                return;
            }
            #endregion

            _consoleOutput = Console.Out;

            ReadUSATTUsers(0, PAGE_SIZE, 0);

            _fileOutput.Close();
            ostrm.Close();
            Console.WriteLine("\n\n>>> Done!");

            Console.ReadLine();
        }

        private static void ReadUSATTUsers(int queryIndex, int pageSize, int pageIndex)
        {
            int i = 0;
            try
            {
                int pageCount = 0;
                WebPage page;
                for (i = queryIndex; i < _query.Length; i++)
                {
                    Console.WriteLine(">>> Query index: " + i);
                    do
                    {
                        page = _browser.NavigateToPage(new Uri(string.Format(USATT_QUERY_URL, _query[i], pageSize, pageIndex * pageSize)));
                        if (pageCount == 0)
                        {
                            IEnumerable<HtmlNode> stepNodes = page.Html.CssSelect(".pagination a.step");
                            if (stepNodes != null && stepNodes.Count() > 0)
                            {
                                pageCount = int.Parse(stepNodes.Last().InnerText);
                                Console.WriteLine(">>> Page count: " + pageCount);
                                Console.Write(">>> Page indexes:");
                            }
                        }

                        ReadRecords(page);

                        Console.Write(" " + pageIndex);

                    } while (++pageIndex < pageCount);
                    pageCount = 0;
                    pageIndex = 0;

                    Console.WriteLine("");
                }
            }
            catch (Exception e)
            {
                //Console.SetOut(_fileOutput);
                //Console.WriteLine(">>> An error has ocurred. Exception: " + e.Message);
                //Console.WriteLine(string.Format(">>> Query index: {0}, Page index: {1}.", i, pageIndex));
                //Console.SetOut(_consoleOutput);

                Console.WriteLine(">>> An error has ocurred. Exception: " + e.Message);
                Console.WriteLine(string.Format(">>> Query index: {0}, Page index: {1}.", i, pageIndex));
                Console.WriteLine(">>> Continue reading...");

                ReadUSATTUsers(i, PAGE_SIZE, pageIndex);
            }
        }

        private static void ReadRecords(WebPage page)
        {
            Console.SetOut(_fileOutput);

            string profileRelativeUrl;
            int iFirst;
            int iLast;
            string pictureRelativeUrl;
            string name;
            string location;
            int rating;
            long usattNumber;
            string expirationDate;
            List<Player> players = new List<Player>();
            foreach (HtmlNode item in page.Html.CssSelect(".list-area .list-item"))
            {
                #region Profile Relative Url
                profileRelativeUrl = item.GetAttributeValue("onclick");
                iFirst = profileRelativeUrl.IndexOf("'");
                iLast = profileRelativeUrl.LastIndexOf("'");
                profileRelativeUrl = profileRelativeUrl.Substring(iFirst + 1, iLast - iFirst - 1);
                #endregion

                #region Picture Relative Url
                pictureRelativeUrl = item.CssSelect("img.profile-photo").First().GetAttributeValue("src");
                #endregion

                #region Name | Location| Rating | USATT Number | Exp. Date
                List<HtmlNode> columns = item.CssSelect(".list-column").ToList();
                name = columns[0].CssSelect(".img-text a").Single().InnerText.Trim();
                location = RemoveWhitespaces(columns[1].InnerText);
                rating = 0;
                if (!string.IsNullOrWhiteSpace(columns[2].InnerText))
                    rating = int.Parse(columns[2].InnerText.Trim());
                usattNumber = 0;
                if (!string.IsNullOrWhiteSpace(columns[3].InnerText))
                    usattNumber = long.Parse(columns[3].InnerText.Trim());
                expirationDate = columns[4].InnerText.Trim();
                #endregion

                #region Writing User Data
                if (usattNumber > 0 && !_usattNumbers.Contains(usattNumber))
                {
                    _usattNumbers.Add(usattNumber);
                    players = new List<Player>();
                    players.Add(new Player()
                    {
                        USATTID = usattNumber,
                        Profile = profileRelativeUrl,
                        PictureURL = pictureRelativeUrl,
                        Name = name,
                        Location = location,
                        Rating = rating,
                        Date = expirationDate
                    });
                    //Console.WriteLine(string.Format("Profile: {0}|Pic: {1}|Name: {2}|Location: {3}|Rating: {4}|USATT#: {5}|Exp. Date: {6}.", profileRelativeUrl, pictureRelativeUrl, name, location, rating, usattNumber, expirationDate));
                }
                #endregion
            }
            string playersAsAHugeJson = JsonConvert.SerializeObject(players.ToArray(), Formatting.Indented);
            Console.WriteLine(playersAsAHugeJson);

            Console.SetOut(_consoleOutput);
        }

        private static string RemoveWhitespaces(string text)
        {
            if (text.Contains(","))
            {
                string[] split = text.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries);
                split[0] = split[0].Trim();
                for (int i = 1; i < split.Length; i++)
                {
                    split[i] = " " + split[i].Trim();
                }
                return string.Join(",", split);
            }
            return text.Trim();
        }
    }
}
