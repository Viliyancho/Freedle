using Freedle.Data.Models;
using Freedle.Data;
using Freedle.Web.Hubs;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Freedle.Web.ViewModels;
using System.IO;
using System.Threading.Tasks;
using System;
using System.Linq;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace Freedle.Web.Controllers
{
    public class PostController : BaseController
    {
        private readonly ApplicationDbContext dbContext;
        private readonly UserManager<ApplicationUser> userManager;
        private readonly IHubContext<ChatHub> hubContext;

        public PostController(ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager, IHubContext<ChatHub> hubContext)
        {
            this.dbContext = dbContext;
            this.userManager = userManager;
            this.hubContext = hubContext;
        }

        public IActionResult CreatePost()
        {
            return this.View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePost(CreatePostViewModel model)
        {
            if (ModelState.IsValid)
            {
                var currentUser = await this.userManager.GetUserAsync(User);
                if (currentUser == null)
                {
                    return this.Redirect("/Identity/Account/Login");
                }

                string imagePath = null;

                if (model.ImageURL != null && model.ImageURL.Length > 0)
                {
                    // Генерираме уникално име за файла
                    string fileName = Guid.NewGuid().ToString() + Path.GetExtension(model.ImageURL.FileName);
                    string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images");

                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    string filePath = Path.Combine(uploadsFolder, fileName);

                    // Запазваме файла в wwwroot/images
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await model.ImageURL.CopyToAsync(fileStream);
                    }

                    imagePath = "/images/" + fileName;
                }

                var newPost = new Post
                {
                    Content = model.Content,
                    ImageURL = imagePath,
                    CreatedOn = DateTime.UtcNow,
                    UserId = currentUser.Id,
                };

                this.dbContext.Posts.Add(newPost);
                await this.dbContext.SaveChangesAsync();

                return RedirectToAction("MyProfile", "Home");
            }

            return this.Redirect("Views/Shared/Error");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeletePost(int postId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null)
            {
                return Unauthorized();
            }

            var post = dbContext.Posts.FirstOrDefault(p => p.Id == postId);
            if (post == null)
            {
                return NotFound();
            }

            bool isAdmin = User.IsInRole("Admin");
            if (post.UserId != userId && !isAdmin)
            {
                return Forbid();
            }

            dbContext.Comments.RemoveRange(post.Comments);
            dbContext.SaveChanges();


            dbContext.Posts.Remove(post);
            dbContext.SaveChanges();

            return Ok();
        }

        public async Task<IActionResult> PostDetails(int id)
        {
            var currentUserId = await this.userManager.GetUserAsync(User);

            ViewData["CurrentUserId"] = currentUserId;

            var post = await this.dbContext.Posts
                .Include(p => p.User)
                .Include(p => p.Comments)
                .ThenInclude(c => c.Author)
                .Include(p => p.Likes)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (post == null)
            {
                return this.NotFound();
            }

            var comments = post.Comments
        .Where(c => c.ParentCommentId == null)
        .Select(c => new CommentViewModel
        {
            Id = c.Id,
            AuthorId = c.Author.Id,
            AuthorName = $"{c.Author.FirstName} {c.Author.LastName}",
            AuthorProfilePictureUrl = c.Author.ProfilePictureURL,
            CommentText = c.CommentText,
            PostAuthorId = c.Post.UserId,
            PostAuthorName = c.Post.User.UserName,
            PostedOn = c.PostedOn.ToString("yyyy-MM-dd HH:mm"),
            Replies = post.Comments
                .Where(r => r.ParentCommentId == c.Id)
                .Select(r => new CommentViewModel
                {
                    Id = r.Id,
                    AuthorId = r.Author.Id,
                    AuthorName = $"{r.Author.FirstName} {r.Author.LastName}",
                    AuthorProfilePictureUrl = r.Author.ProfilePictureURL,
                    CommentText = r.CommentText,
                    PostedOn = r.PostedOn.ToString("yyyy-MM-dd HH:mm"),
                })
                .ToList(),
        })
        .ToList();

            var postViewModel = new PostViewModel
            {
                Id = post.Id,
                Content = post.Content,
                ImageUrl = post.ImageURL,
                CreatedOn = post.CreatedOn.ToString("yyyy-MM-dd HH:mm"),
                AuthorId = post.User.Id,
                AuthorName = $"{post.User.FirstName} {post.User.LastName}",
                AuthorProfilePictureUrl = post.User.ProfilePictureURL,
                LikeCount = post.LikeCount,
                IsLikedByCurrentUser = post.Likes.Any(l => l.UserId == User.FindFirst(ClaimTypes.NameIdentifier)?.Value),
                Comments = comments,
            };


            return this.View(postViewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ToggleLike(int postId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null)
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            var post = this.dbContext.Posts.Include(p => p.Likes).FirstOrDefault(p => p.Id == postId);
            if (post == null)
            {
                return Json(new { success = false, message = "Post not found" });
            }

            var existingLike = this.dbContext.UserLikes.FirstOrDefault(l => l.PostId == postId && l.UserId == userId);
            bool isLiked;

            if (existingLike != null)
            {
                post.Likes.Remove(existingLike);
                this.dbContext.UserLikes.Remove(existingLike);
                post.LikeCount--;
                isLiked = false;
            }
            else
            {
                var like = new UserLike { PostId = postId, UserId = userId };
                post.Likes.Add(like);
                dbContext.UserLikes.Add(like);
                post.LikeCount++;
                isLiked = true;
            }

            dbContext.SaveChanges();

            return Json(new { success = true, likeCount = post.LikeCount, isLiked });
        }

        [Authorize] // Само влезли потребители могат да видят кой е харесал публикацията
        public IActionResult LikedByList(int postId)
        {
            if (postId <= 0)
            {
                return BadRequest("Invalid post ID");
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var likedUsers = dbContext.UserLikes
                .Where(l => l.PostId == postId)
                .Include(l => l.User)
                .Select(l => new UserViewModel
                {
                    Id = l.User.Id,
                    Username = l.User.UserName,
                    ProfilePictureUrl = l.User.ProfilePictureURL,
                })
                .ToList();

            if (likedUsers == null || !likedUsers.Any())
            {
                return NotFound("No likes found for this post.");
            }

            var followingIds = dbContext.UserFollowers
        .Where(f => f.FollowerId == currentUserId)
        .Select(f => f.UserId)
        .ToHashSet();

            foreach (var user in likedUsers)
            {
                user.IsFollowing = followingIds.Contains(user.Id);
            }

            return View("LikedByList", likedUsers);
        }
    }
}
