using Freedle.Data.Models;
using Freedle.Data;
using Freedle.Web.Hubs;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Freedle.Web.ViewModels;
using System.Threading.Tasks;
using System;
using System.Security.Claims;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace Freedle.Web.Controllers
{
    public class MessageController : BaseController
    {
        private readonly ApplicationDbContext dbContext;
        private readonly UserManager<ApplicationUser> userManager;
        private readonly IHubContext<ChatHub> hubContext;

        public MessageController(ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager, IHubContext<ChatHub> hubContext)
        {
            this.dbContext = dbContext;
            this.userManager = userManager;
            this.hubContext = hubContext;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendMessage([FromBody] MessageViewModel messageModel)
        {
            if (messageModel == null || string.IsNullOrEmpty(messageModel.Content))
            {
                return BadRequest(new { message = "Съобщението не може да бъде празно!" });
            }

            if (messageModel.ConversationId <= 0)
            {
                return BadRequest(new { message = "Невалиден разговор!" });
            }


            var sender = await userManager.GetUserAsync(User);
            if (sender == null)
            {
                return Unauthorized(new { message = "Не сте влезли в системата!" });
            }

            var conversation = await dbContext.Conversations.FindAsync(messageModel.ConversationId);
            if (conversation == null)
            {
                return NotFound(new { message = "Разговорът не беше намерен!" });
            }

            string profilePictureUrl = sender.ProfilePictureURL;
            if (string.IsNullOrWhiteSpace(profilePictureUrl))
            {
                profilePictureUrl = "/images/default-avatar.jpg";
            }

            var newMessage = new Message
            {
                SenderId = sender.Id,
                ConversationId = messageModel.ConversationId,
                Content = messageModel.Content,
                SentOn = DateTime.UtcNow,
            };

            dbContext.Messages.Add(newMessage);
            await dbContext.SaveChangesAsync();

            // Изпращане на съобщението чрез SignalR
            await hubContext.Clients.Group(messageModel.ConversationId.ToString())
                .SendAsync("ReceiveMessage", sender.UserName, messageModel.Content, profilePictureUrl, newMessage.Id);

            return Ok(new { messageId = newMessage.Id, message = "Съобщението беше изпратено успешно!" });

        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMessage(int id)
        {
            var message = await dbContext.Messages.FindAsync(id);
            if (message == null)
            {
                return NotFound(new { error = "Съобщението не беше намерено!" });
            }

            if (message.SenderId != User.FindFirstValue(ClaimTypes.NameIdentifier))
            {
                return Forbid();
            }

            dbContext.Messages.Remove(message);
            await dbContext.SaveChangesAsync();

            return Ok(new { message = "Съобщението беше изтрито успешно!" });
        }

        [HttpGet]
        public async Task<IActionResult> GetMessages(int conversationId)
        {
            if (conversationId <= 0)
            {
                return BadRequest(new { message = "Невалиден разговор!" });
            }

            var messages = await dbContext.Messages
                .Where(m => m.ConversationId == conversationId)
                .OrderBy(m => m.SentOn)
                .Select(m => new
                {
                    SenderId = m.SenderId,
                    SenderName = m.Sender.UserName,
                    Content = m.Content,
                    MessageSenderProfilePictureURL = string.IsNullOrEmpty(m.Sender.ProfilePictureURL)
                        ? "/images/default-avatar.jpg"
                        : m.Sender.ProfilePictureURL,
                    SentOn = m.SentOn.ToString("g"),
                    MessageId = m.Id,
                })
                .ToListAsync();

            return Json(messages);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateConversation([FromBody] dynamic data)
        {
            var currentUser = await userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Unauthorized(new { message = "Не сте влезли в системата!" });
            }

            string userId = data?.userId;
            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest(new { message = "Невалиден потребител!" });
            }

            var existingConversation = await dbContext.Conversations
                .FirstOrDefaultAsync(c =>
                    (c.User1Id == currentUser.Id && c.User2Id == userId) ||
                    (c.User1Id == userId && c.User2Id == currentUser.Id));

            if (existingConversation != null)
            {
                return Json(new { conversationId = existingConversation.Id });
            }

            var newConversation = new Conversation
            {
                User1Id = currentUser.Id,
                User2Id = userId,
                CreatedOn = DateTime.UtcNow,
            };

            dbContext.Conversations.Add(newConversation);
            await dbContext.SaveChangesAsync();

            return Json(new { conversationId = newConversation.Id });
        }
    }
}
