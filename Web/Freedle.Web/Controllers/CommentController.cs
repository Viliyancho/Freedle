using Freedle.Data.Models;
using Freedle.Data;
using Freedle.Web.Hubs;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System;
using System.Linq;

namespace Freedle.Web.Controllers
{
    public class CommentController : BaseController
    {
        private readonly ApplicationDbContext dbContext;
        private readonly UserManager<ApplicationUser> userManager;
        private readonly IHubContext<ChatHub> hubContext;

        public CommentController(ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager, IHubContext<ChatHub> hubContext)
        {
            this.dbContext = dbContext;
            this.userManager = userManager;
            this.hubContext = hubContext;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddComment(int postId, string commentText)
        {
            if (string.IsNullOrWhiteSpace(commentText))
            {
                TempData["ErrorMessage"] = "Comment must not be empty.";
                return RedirectToAction("MyProfile", "Home");
            }

            var user = await this.userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }

            var post = dbContext.Posts.Include(p => p.Comments).FirstOrDefault(p => p.Id == postId);

            if (post == null)
            {
                return NotFound();
            }

            var newComment = new Comment
            {
                PostId = postId,
                AuthorId = user.Id,
                CommentText = commentText.Trim(), // Премахване на празни символи
                PostedOn = DateTime.UtcNow,
            };

            post.Comments.Add(newComment);
            await this.dbContext.SaveChangesAsync();

            return RedirectToAction("PostDetails", "Post", new { id = postId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteComment(int commentId)
        {
            try
            {
                if (commentId <= 0)
                {
                    return Json(new { success = false, message = "Invalid comment ID" });
                }

                var comment = await dbContext.Comments
                    .Include(c => c.Replies) // Зареждаме всички replies на коментара
                    .FirstOrDefaultAsync(c => c.Id == commentId);
                if (comment == null)
                {
                    return Json(new { success = false, message = "Comment not found" });
                }

                var post = await dbContext.Posts
                    .FirstOrDefaultAsync(p => p.Comments.Any(c => c.Id == commentId));

                if (post == null)
                {
                    return Json(new { success = false, message = "Post not found" });
                }

                var currentUserId = this.userManager.GetUserId(User);
                if (string.IsNullOrEmpty(currentUserId))
                {
                    return Json(new { success = false, message = "User not authenticated" });
                }

                if (!User.IsInRole("Admin") && comment.AuthorId != currentUserId && post.UserId != currentUserId)
                {
                    return Json(new { success = false, message = "Permission denied" });
                }

                // Първо премахваме всички отговори на коментара
                foreach (var reply in comment.Replies.ToList())
                {
                    this.dbContext.Comments.Remove(reply);
                }

                // След това премахваме самия коментар
                this.dbContext.Comments.Remove(comment);

                await dbContext.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DeleteComment Error: {ex.Message}");
                return Json(new { success = false, message = "An error occurred", error = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddReply(int commentId, int postId, string replyText)
        {
            if (string.IsNullOrWhiteSpace(replyText))
            {
                TempData["ErrorMessage"] = "Reply cannot be empty.";
                return RedirectToAction("PostDetails", "Post",new { id = postId }); // Оставаме на същия пост
            }

            var user = await this.userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }

            var parentComment = await dbContext.Comments
                .FirstOrDefaultAsync(c => c.Id == commentId);

            if (parentComment == null)
            {
                return NotFound();
            }

            var newReply = new Comment
            {
                PostId = postId,
                AuthorId = user.Id,
                CommentText = replyText.Trim(),
                ParentCommentId = commentId,
                PostedOn = DateTime.UtcNow,
            };


            dbContext.Comments.Add(newReply);
            await dbContext.SaveChangesAsync();

            return Json(new { success = true, message = "Reply added successfully!", replyId = newReply.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteReply(int replyId)
        {
            try
            {
                if (replyId <= 0)
                {
                    return Json(new { success = false, message = "Invalid reply ID" });
                }

                var reply = await dbContext.Comments.FindAsync(replyId);
                if (reply == null)
                {
                    return Json(new { success = false, message = "Reply not found" });
                }

                var post = await dbContext.Posts
                    .Include(p => p.Comments)
                    .FirstOrDefaultAsync(p => p.Comments.Any(c => c.Id == replyId));

                if (post == null)
                {
                    return Json(new { success = false, message = "Post not found" });
                }

                var currentUserId = this.userManager.GetUserId(User);

                if (string.IsNullOrEmpty(currentUserId))
                {
                    return Json(new { success = false, message = "User not authenticated" });
                }

                if (!User.IsInRole("Admin") && reply.AuthorId != currentUserId && post.UserId != currentUserId)
                {
                    return Json(new { success = false, message = "Permission denied" });
                }

                dbContext.Comments.Remove(reply);
                await dbContext.SaveChangesAsync();



                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DeleteReply Error: {ex.Message}");
                return Json(new { success = false, message = "An error occurred", error = ex.Message });
            }
        }
    }
}
