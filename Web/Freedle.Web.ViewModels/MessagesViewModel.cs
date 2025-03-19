namespace Freedle.Web.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class MessagesViewModel
    {
        public string CurrentUserId { get; set; }

        public List<ConversationViewModel> Conversations { get; set; } = new();

        public int? SelectedConversationId { get; set; }

        public List<MessageViewModel> Messages { get; set; } = new();
    }
}
