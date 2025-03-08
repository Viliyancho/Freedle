namespace Freedle.Data.Models
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class Conversation
    {
        public int Id { get; set; }

        public string User1Id { get; set; }

        public ApplicationUser User1 { get; set; }

        public string User2Id { get; set; }

        public ApplicationUser User2 { get; set; }

        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

        public ICollection<UserConversation> UserConversations { get; set; } = new HashSet<UserConversation>();

        public ICollection<Message> Messages { get; set; } = new HashSet<Message>();
    }
}
