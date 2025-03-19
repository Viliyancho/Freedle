namespace Freedle.Web.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class MessageViewModel
    {
        public string SenderId { get; set; }

        public string SenderName { get; set; }

        public string Content { get; set; }

        public string SentOn { get; set; }
    }
}
