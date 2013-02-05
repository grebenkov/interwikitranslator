using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml;

namespace InterwikiTranslator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        BackgroundWorker worker;
        private string wikitext;
        private string pagename;

        private void DoWork(object sender,
            DoWorkEventArgs e)
        {
            e.Result = TranslateLinks((string)e.Argument);
        }


        private void ProcessURL(object sender, RoutedEventArgs e)
        {
            pagename = tURL.Text;
            GetWikitext(pagename);
            tSource.Text = wikitext;
            
            worker = new BackgroundWorker();
            worker.DoWork += DoWork;
            worker.RunWorkerCompleted += worker_RunWorkerCompleted;
            worker.ProgressChanged += worker_ProgressChanged;
            worker.WorkerReportsProgress = true;

            worker.RunWorkerAsync(wikitext);
        }

        private void GetWikitext(string pagename)
        {            
            wikitext = GetFromURL(@"http://en.wikipedia.org/w/index.php?action=raw&title=" + Uri.EscapeDataString(pagename));            
        }

        void worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            tResult.Text = (string)e.UserState + ": " + e.ProgressPercentage.ToString() + " %";
        }

        void worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            tResult.Text = (string)e.Result;
        }

        private string TranslateLinks(string wikitext)
        {
            var validlinks = LoadOriginalPage();

            Dictionary<string, string> pagetoiwiki = MakeTranslationsList(validlinks);

            return ReplaceWords(pagetoiwiki);
        }

        private Dictionary<string, string> MakeTranslationsList(IEnumerable<string> validlinks)
        {
            Dictionary<string, string> pagetoiwiki = new Dictionary<string, string>();
            int i = 0;
            foreach (string link in validlinks)
            {
                worker.ReportProgress(Convert.ToInt32(((decimal)i / (decimal)validlinks.Count()) * 100), "Processing interwiki");
                XmlDocument interwikipage = new XmlDocument();
                interwikipage.LoadXml(GetFromURL(@"http://en.wikipedia.org/w/api.php?action=parse&redirects&prop=langlinks&format=xml&page=" + link));
                XmlNodeList langlinks = interwikipage.GetElementsByTagName("ll");
                var rulist = from XmlNode c in langlinks
                             where c.Attributes["lang"].Value == "ru"
                             select c.InnerText;
                string rulink = rulist.Count() != 0 ? rulist.First() : "";
                pagetoiwiki.Add(link, rulink);
                ++i;
                Thread.Sleep(1000);
            }
            return pagetoiwiki;
        }

        private string ReplaceWords(Dictionary<string, string> pagetoiwiki)
        {
            string result = wikitext;
            foreach (var wordpair in pagetoiwiki)
            {
                if (wordpair.Value != "")
                {
                    string key = wordpair.Key.Replace('_', ' '); // normalize underscores
                    result = result.Replace(key, wordpair.Value);
                    // next lines are for lowercase link in wikitext (less dirty hack than before)
                    // only first letter is lowercased because other text may have any case
                    string lowercasekey = Char.ToLower(key[0]) + key.Substring(1);
                    string lowercasevalue = Char.ToLower(wordpair.Value[0]) + wordpair.Value.Substring(1);
                    result = result.Replace(lowercasekey, lowercasevalue); 
                }
            }
            return result;
        }

        private IEnumerable<string> LoadOriginalPage()
        {
            worker.ReportProgress(1, "Loading original page link list");

            XmlNodeList xmllinkscollection = GetNodeListByPropAndTagname("links", "pl");

            var validlinks = from XmlNode c in xmllinkscollection
                             orderby c.InnerText.Length descending
                             where c.Attributes["exists"] != null                             
                             select c.InnerText;

            // also process categories
            XmlNodeList xmlcatscollection = GetNodeListByPropAndTagname("categories", "cl");

            var catlinks = from XmlNode c in xmlcatscollection
                           orderby c.InnerText.Length descending                           
                           select "Category:" + c.InnerText;

            var alllinks = validlinks.Concat(catlinks).OrderByDescending(str => str.Length);

            return alllinks;
        }

        private XmlNodeList GetNodeListByPropAndTagname(string prop, string tagname)
        {
            XmlDocument xmlcatsdocument = new XmlDocument();
            xmlcatsdocument.LoadXml(GetFromURL(@"http://en.wikipedia.org/w/api.php?action=parse&prop=" + prop + @"&format=xml&page=" + Uri.EscapeDataString(pagename)));
            XmlNodeList xmlcatscollection = xmlcatsdocument.GetElementsByTagName(tagname);
            return xmlcatscollection;
        }

        private string GetFromURL(string url)
        {
            string wikitext;
            using (WebClient client = new WebClient())
            {
                client.Headers["User-Agent"] = "InterwikiTranslator/1.0";
                client.Encoding = System.Text.Encoding.UTF8;                
                wikitext = client.DownloadString(url);
            }
            return wikitext;
        }
    }
}
