namespace Freedle.Data.Models
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class UserConversation
    {
        public string UserId { get; set; }

        public int ConversationId { get; set; }

        public ApplicationUser User { get; set; }

        public Conversation Conversation { get; set; }

        public bool HasReadAllMessages { get; set; } = false; // Статус за прочетено съобщение
    }
}
