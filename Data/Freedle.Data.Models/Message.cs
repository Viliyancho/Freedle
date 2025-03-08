namespace Freedle.Data.Models
{
    using Freedle.Data.Common.Models;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class Message : BaseDeletableModel<int>
    {
        public string Content { get; set; }

        public DateTime SentOn { get; set; } = DateTime.UtcNow;

        public string SenderId { get; set; }

        public ApplicationUser Sender { get; set; }

        public int ConversationId { get; set; }

        public Conversation Conversation { get; set; }
    }
}
