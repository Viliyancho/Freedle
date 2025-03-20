namespace Freedle.Web.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class MessageViewModel
    {
        public int ConversationId { get; set; }

        public string SenderId { get; set; }

        public string SenderName { get; set; }

        public string Content { get; set; }

        public DateTime SentOn { get; set; }
    }
}
