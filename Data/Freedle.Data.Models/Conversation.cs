namespace Freedle.Data.Models
{
    using Freedle.Data.Common.Models;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class Conversation : BaseDeletableModel<int>
    {
        public string User1Id { get; set; }

        public ApplicationUser User1 { get; set; }

        public string User2Id { get; set; }

        public ApplicationUser User2 { get; set; }

        public ICollection<UserConversation> UserConversations { get; set; } = new HashSet<UserConversation>();

        public ICollection<Message> Messages { get; set; } = new HashSet<Message>();
    }
}
