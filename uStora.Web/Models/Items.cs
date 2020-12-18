using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace uStora.Web.Models
{
    public class Items
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string Link { get; set; }
        public string Guid { get; set; }
        public string Category { get; set; }
        public string PubDate { get; set; }
    }
}