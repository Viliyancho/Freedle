using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace Freedle.Web.Hubs
{
    public class ChatHub : Hub
    {
        public async Task JoinConversation(string conversationId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, conversationId);
        }

        public async Task SendMessage(string conversationId, string senderName, string message)
        {
            await Clients.Group(conversationId).SendAsync("ReceiveMessage", senderName, message);
        }
    }
}
