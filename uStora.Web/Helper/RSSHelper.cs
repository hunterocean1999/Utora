using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Web;
using uStora.Web.Models;
using System.Xml.XPath;

namespace uStora.Web.Helper
{
    public class RSSHelper
    {
        public static List<Items> read(string url)
        {
            List<Items> list = new List<Items>();
            try
            {
                XPathDocument doc = new XPathDocument(url);
                XPathNavigator navigator = doc.CreateNavigator();
                XPathNodeIterator nodes = navigator.Select("//item");
                while (nodes.MoveNext())
                {
                    XPathNavigator node = nodes.Current;
                    list.Add(new Items
                    {
                        Title = node.SelectSingleNode("title").Value,
                        Category = node.SelectSingleNode("category").Value,
                        Description = node.SelectSingleNode("description").Value,
                        Guid = node.SelectSingleNode("guid").Value,
                        Link = node.SelectSingleNode("link").Value,
                        PubDate = node.SelectSingleNode("pubDate").Value
                    });
                }
            }
            catch
            {
                list = null;
            }
            return list;
        }
    }
}