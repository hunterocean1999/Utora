using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using uStora.Web.Helper;

namespace uStora.Web.Controllers
{
    public class RSSController : Controller
    {
        // GET: RSS
        public ActionResult Index()
        {
            string url = "http://purl.org/rss/1.0/modules/slash/";
            ViewBag.list = RSSHelper.read(url);
            ViewBag.price = "2000";
            return View();
        }
    }
}