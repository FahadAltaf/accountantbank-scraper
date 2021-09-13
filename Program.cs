using CsvHelper;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace accountantbank_scraper
{
    public class DataModel
    {
        public string Name { get; set; }
        public string Address { get; set; }
        public string URL { get; set; }
        public string Website { get; set; }
        public string Emails { get; set; }
    }
    class Program
    {
        static void Main(string[] args)
        {
            List<DataModel> entries = new List<DataModel>();

            for (int i = 1; i <= 505; i++)
            {
                Console.WriteLine($"https://accountantbank.nl/overzicht/{i}");
                HtmlDocument doc = new HtmlWeb().Load($"https://accountantbank.nl/overzicht/{i}");
                var list = doc.DocumentNode.SelectSingleNode("//ul[@class='company-list company-list-clean']");
                foreach (var item in list.ChildNodes.Where(x => x.Name == "li").Skip(1))
                {
                    DataModel entry = new DataModel();
                    HtmlDocument sub = new HtmlDocument();
                    sub.LoadHtml(item.InnerHtml);

                    var name = sub.DocumentNode.SelectSingleNode("/div[1]/a[1]");
                    if (name != null)
                    {
                        entry.Name = HttpUtility.HtmlDecode(name.InnerText);
                        entry.URL = "https://accountantbank.nl" + name.Attributes.FirstOrDefault(x => x.Name == "href").Value;
                    }

                    var address = sub.DocumentNode.SelectSingleNode("/div[1]/p[1]");
                    if (address != null)
                    {
                        entry.Address = HttpUtility.HtmlDecode(address.InnerText);
                    }

                    if(!string.IsNullOrEmpty(entry.URL))
                    {
                        Console.WriteLine(entry.URL);
                        HtmlWeb web = new HtmlWeb();
                      var pageDoc=  web.Load(entry.URL);
                        var rows = pageDoc.DocumentNode.SelectNodes("//tr");
                        if (rows!=null && rows.Count > 0)
                        {
                            var page =rows.FirstOrDefault(x => !string.IsNullOrEmpty(x.InnerText) && x.InnerText.Contains("Website"));
                            if (page != null)
                            {
                                entry.Website = HttpUtility.HtmlDecode(page.ChildNodes[1].InnerText);
                                var mails = LookForEmail(entry.Website);
                                if (mails.Count > 0)
                                    entry.Emails = string.Join(";", mails);
                            }
                        }
                      
                    }
                   

                    entries.Add(entry);
                }

                using (var writer = new StreamWriter("file.csv"))
                using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
                {
                    csv.WriteRecords(entries);
                }
            }

            //var entriesWithWebsite = entries.Where(x => !string.IsNullOrEmpty(x.Website));
            //Console.WriteLine("Records with website: "+entriesWithWebsite.Count());
            //foreach (var item in entriesWithWebsite)
            //{
            //    var mails = LookForEmail(item.Website);
            //    if (mails.Count > 0)
            //        item.Emails = string.Join(";", mails);
            //}

            

        }

        private static List<string> LookForEmail(string websiteLink)
        {

            List<string> foundEmails = new List<string>();
            try
            {
                Uri url = new Uri(websiteLink);

                var domain = url.Host;
                Console.WriteLine("DOMAIN: {0}", domain);

                HtmlWeb web = new HtmlWeb();
                var mainPage = web.Load(websiteLink);
                List<string> pages = new List<string>();
                pages.Add(websiteLink);
                var node = mainPage.DocumentNode.SelectNodes("//a[@href]");
                if(node!=null)
                foreach (HtmlNode link in node)
                {
                    // Get the value of the HREF attribute
                    string hrefValue = link.GetAttributeValue("href", string.Empty);
                    if (!string.IsNullOrEmpty(hrefValue) && !pages.Exists(x => x == hrefValue) && hrefValue.Contains(domain))
                    {
                        pages.Add(hrefValue);
                        Console.WriteLine(hrefValue);
                    }
                }

                List<Task> taskList = new List<Task>();
                int num1 = (pages.Count + 3 - 1) / 3;
                for (int index = 1; index <= num1; ++index)
                {
                    int num2 = index - 1;
                    var data = pages.Skip(num2 * 3).Take(3).ToList();

                    Task task1 = Task.Factory.StartNew(() =>
                    {
                        var mails = ExtractEmails(data);
                        if (mails.Count > 0)
                        {
                            foundEmails.AddRange(mails);
                        }
                    });
                    taskList.Add(task1);
                    if (index % 10 == 0 || index == num1)
                    {
                        foreach (Task task2 in taskList)
                        {
                            while (!task2.IsCompleted)
                            { }
                        }
                    }
                }
                //   var emails = ExtractEmails(pages);
                foundEmails = foundEmails.GroupBy(x => x).Select(x => x.FirstOrDefault()).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return foundEmails;
        }

        public static List<string> ExtractEmails(List<string> links)
        {
            List<string> emails = new List<string>();

            foreach (var link in links)
            {
                try
                {
                    var web = new HtmlWeb();
                    var doc = web.Load(link);

                    var pageContent = doc.DocumentNode.InnerText;
                    //instantiate with this pattern 
                    Regex emailRegex = new Regex(@"\w+([-+.]\w+)*@\w+([-.]\w+)*\.\w+([-.]\w+)*",
                        RegexOptions.IgnoreCase);
                    //find items that matches with our pattern
                    MatchCollection emailMatches = emailRegex.Matches(pageContent);

                    StringBuilder sb = new StringBuilder();

                    foreach (Match emailMatch in emailMatches)
                    {
                        if (!string.IsNullOrEmpty(emailMatch.Value) && !emails.Exists(x => x.Equals(emailMatch.Value)))
                        {
                            Console.WriteLine(emailMatch.Value);
                            emails.Add(emailMatch.Value);
                        }
                    }
                }
                catch
                {

                }
            }

            //store to file
            return emails;
        }
    }
}
