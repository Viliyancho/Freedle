﻿namespace Freedle.Web.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Security.Claims;
    using System.Threading.Tasks;
    using Freedle.Data;
    using Freedle.Data.Models;
    using Freedle.Web.Hubs;
    using Freedle.Web.ViewModels;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.SignalR;
    using Microsoft.EntityFrameworkCore;
    using SendGrid.Helpers.Mail;

    public class HomeController : BaseController
    {
        private readonly ApplicationDbContext dbContext;
        private readonly UserManager<ApplicationUser> userManager;
        private readonly IHubContext<ChatHub> hubContext;

        public HomeController(ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager, IHubContext<ChatHub> hubContext)
        {
            this.dbContext = dbContext;
            this.userManager = userManager;
            this.hubContext = hubContext;
        }

        public async Task<IActionResult> Index()
        {
            var currentUser = await this.userManager.GetUserAsync(User);
            string currentUserId = currentUser?.Id;

            // Взимаме последните 20 поста
            var posts = this.dbContext.Posts
                .Where(p => !p.IsDeleted)
                .OrderByDescending(p => p.CreatedOn)
                .Take(20)
                .Select(p => new PostViewModel
                {
                    Id = p.Id,
                    Content = p.Content,
                    ImageUrl = p.ImageURL,
                    CreatedOn = p.CreatedOn.ToString("f"),
                    AuthorId = p.User.Id,
                    AuthorName = $"{p.User.FirstName} {p.User.LastName}",
                    AuthorProfilePictureUrl = p.User.ProfilePictureURL,
                    LikeCount = p.LikeCount,
                    IsLikedByCurrentUser = p.Likes.Any(l => l.UserId == currentUserId),
                    Comments = p.Comments.Select(c => new CommentViewModel
                    {
                        Id = c.Id,
                        AuthorId = c.Author.Id,
                        AuthorName = $"{c.Author.FirstName} {c.Author.LastName}",
                        AuthorProfilePictureUrl = c.Author.ProfilePictureURL,
                        CommentText = c.CommentText,
                        PostedOn = c.PostedOn.ToString("f"),
                    }).ToList(),
                })
                .ToList();

            var suggestedUsers = this.dbContext.Users
                .Where(u => u.Id != currentUserId)
                .OrderBy(r => Guid.NewGuid())
                .Take(5)
                .Select(u => new SuggestedUserViewModel
                {
                    Id = u.Id,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    ProfilePictureUrl = u.ProfilePictureURL,
                    IsFollowedByCurrentUser = u.Followers.Any(f => f.FollowerId == currentUserId),
                })
                .ToList();

            var model = new NewsFeedViewModel
            {
                Posts = posts,
                SuggestedUsers = suggestedUsers,
            };

            return this.View(model);
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




        [HttpGet]
        public async Task<IActionResult> UserProfile(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var user = await dbContext.Users
                .Where(u => u.Id == id)
                .Select(u => new UserProfileViewModel
                {
                    ProfileUserId = u.Id,
                    Username = u.UserName,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    Description = u.Description,
                    ProfilePictureUrl = u.ProfilePictureURL ?? "/images/default-avatar.png",
                    Gender = u.Gender,
                    FollowerCount = u.Followers.Count,
                    FollowingCount = u.Following.Count,
                    Posts = u.Posts.Select(p => new PostViewModel
                    {
                        Id = p.Id,
                        ImageUrl = p.ImageURL,
                    }).ToList(),
                    IsFollowing = u.Followers.Any(f => f.FollowerId == currentUserId),
                })
                .FirstOrDefaultAsync();

            if (user == null)
            {
                return NotFound();
            }


            Console.WriteLine("ProfileUserId: " + user.ProfileUserId);
            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleFollow(string id)
        {
            Console.WriteLine("Received ID: " + id);

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (currentUserId == null || id == currentUserId)
            {
                return BadRequest();
            }

            var userToFollow = await dbContext.Users
                .Include(u => u.Followers)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (userToFollow == null)
            {
                return NotFound();
            }

            var existingFollow = await dbContext.UserFollowers
                .FirstOrDefaultAsync(f => f.FollowerId == currentUserId && f.UserId == id);

            if (existingFollow != null)
            {
                dbContext.UserFollowers.Remove(existingFollow); // Ако вече го следва -> Unfollow
            }
            else
            {
                // Ако не го следва - Follow

                dbContext.UserFollowers.Add(new UserFollower
                {
                    FollowerId = currentUserId,
                    UserId = id,
                });
            }

            await dbContext.SaveChangesAsync();
            return Redirect(Request.Headers["Referer"].ToString());
        }

        [HttpGet]
        public IActionResult FollowingList(string id)
        {
            var following = dbContext.UserFollowers
                .Where(f => f.FollowerId == id)
                .Select(f => new UserViewModel
                {
                    Id = f.UserId,
                    Username = f.User.UserName,
                    ProfilePictureUrl = f.User.ProfilePictureURL,
                })
                .ToList();

            return View(following);
        }


        [HttpGet]
        public async Task<IActionResult> FollowersList(string id)
        {
            var user = await dbContext.Users
                .Include(u => u.Followers)
                .ThenInclude(f => f.Follower)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null)
            {
                return NotFound();
            }

            var followers = user.Followers
                .Select(f => new UserViewModel
                {
                    Id = f.Follower.Id,
                    Username = f.Follower.UserName,
                    ProfilePictureUrl = f.Follower.ProfilePictureURL,
                })
                .ToList();

            return View(followers);
        }

        [HttpGet]
        public async Task<IActionResult> UserFollowingList(string id)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var following = await dbContext.UserFollowers
                .Where(f => f.FollowerId == id)
                .Include(f => f.User) // Включваме User, за да имаме данни за него
                .Select(f => new UserViewModel
                {
                    Id = f.User.Id,
                    Username = f.User.UserName,
                    ProfilePictureUrl = f.User.ProfilePictureURL,
                    IsFollowing = dbContext.UserFollowers.Any(uf => uf.FollowerId == currentUserId && uf.UserId == f.User.Id)
                })
                .ToListAsync();

            return View(following);
        }


        [HttpGet]
        public async Task<IActionResult> UserFollowersList(string id)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var followers = await dbContext.UserFollowers
                .Where(f => f.UserId == id)
                .Include(f => f.Follower)
                .Select(f => new UserViewModel
                {
                    Id = f.Follower.Id,
                    Username = f.Follower.UserName,
                    ProfilePictureUrl = f.Follower.ProfilePictureURL,
                    IsFollowing = dbContext.UserFollowers.Any(uf => uf.FollowerId == currentUserId && uf.UserId == f.Follower.Id) // Проверяваме дали логнатият потребител следва този последовател
                })
                .ToListAsync();

            return View(followers);
        }





        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unfollow(string userId)
        {
            var currentUser = await userManager.GetUserAsync(User);
            if (currentUser == null) return Unauthorized();

            if (string.IsNullOrEmpty(userId)) return BadRequest("Invalid user ID");

            var followerEntry = await dbContext.UserFollowers
                .FirstOrDefaultAsync(f => f.UserId == userId && f.FollowerId == currentUser.Id);

            if (followerEntry != null)
            {
                dbContext.UserFollowers.Remove(followerEntry);
                await dbContext.SaveChangesAsync();
            }

            return Redirect(Request.Headers["Referer"].ToString());
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveFollower(string userId)
        {
            var currentUser = await userManager.GetUserAsync(User);
            if (currentUser == null) return Unauthorized();

            // Намираме записа в таблицата UserFollower
            var followerToRemove = await dbContext.UserFollowers
                .FirstOrDefaultAsync(uf => uf.UserId == currentUser.Id && uf.FollowerId == userId);

            if (followerToRemove == null) return NotFound();

            // Премахваме го от базата
            dbContext.UserFollowers.Remove(followerToRemove);
            await dbContext.SaveChangesAsync();

            return RedirectToAction("FollowersList", new { id = currentUser.Id });
        }



        public IActionResult MessagesDemo()
        {
            return this.View();
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
            Replies = post.Comments // Намираме риплеите за този коментар
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


        public async Task<IActionResult> MyProfile()
        {
            var currentUser = await this.userManager.Users
    .Include(u => u.Posts) 
    .FirstOrDefaultAsync(u => u.Id == this.userManager.GetUserId(User));

            if (currentUser == null)
            {
                return this.Redirect("/Identity/Account/Login");
            }

            Console.WriteLine(currentUser.Posts.Count);

            var followerCount = this.dbContext.UserFollowers
        .Where(f => f.UserId == currentUser.Id && f.UnfollowedDate == null)
        .Count();

            var followingCount = this.dbContext.UserFollowers
                .Where(f => f.FollowerId == currentUser.Id && f.UnfollowedDate == null)
                .Count();

            var userProfileViewModel = new UserProfileViewModel
            {
                Username = currentUser.UserName,
                FirstName = currentUser.FirstName,
                LastName = currentUser.LastName,
                ProfilePictureUrl = currentUser.ProfilePictureURL,
                Gender = currentUser.Gender,
                BirthDay = currentUser.BirthDay,
                Description = currentUser.Description,
                City = currentUser.City,
                Country = currentUser.Country,
                Posts = currentUser.Posts.Select(post => new PostViewModel
                {
                    Id = post.Id,
                    Content = post.Content,
                    ImageUrl = post.ImageURL,
                    CreatedOn = post.CreatedOn.ToString("yyyy-MM-dd HH:mm"),
                    LikeCount = post.LikeCount,
                }).ToList(),
                FollowerCount = followerCount,
                FollowingCount = followingCount,
                ProfileUserId = currentUser.Id,
            };

            return View(userProfileViewModel);
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
                AuthorId = user.Id, // Връзка с потребителя
                CommentText = commentText.Trim(), // Премахване на празни символи
                PostedOn = DateTime.UtcNow,
            };

            post.Comments.Add(newComment);
            await this.dbContext.SaveChangesAsync(); // Асинхронно записване

            return RedirectToAction("PostDetails", new { id = postId });
        }

        [HttpGet]
        public async Task<IActionResult> EditProfile()
        {
            var user = await this.userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            var model = new EditMyProfileViewModel
            {
                Username = user.UserName,
                Description = user.Description
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> EditProfile(EditMyProfileViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return RedirectToAction("MyProfile", "Home");
            }

            var user = await this.dbContext.Users.FirstOrDefaultAsync(u => u.Id == this.userManager.GetUserId(User));
            if (user == null)
            {
                return NotFound();
            }

            user.UserName = model.Username;
            user.NormalizedUserName = model.Username.ToUpper();
            user.Description = model.Description;

            // Ако има нова снимка, качваме я и променяме URL
            if (model.ProfilePicture != null)
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/profile_pics");

                // Проверка дали папката съществува, ако не – създаваме я
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(model.ProfilePicture.FileName);
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await model.ProfilePicture.CopyToAsync(fileStream);
                }

                user.ProfilePictureURL = "/images/profile_pics/" + uniqueFileName;
            }

            try
            {
                await this.dbContext.SaveChangesAsync();
                return RedirectToAction("MyProfile", "Home");
            }
            catch (Exception ex)
            {
                // Логване на грешката (ако имаш логер)
                Console.WriteLine($"Error updating profile: {ex.Message}");
                return StatusCode(500, new { message = "Error updating profile", error = ex.Message });
            }
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddReply(int commentId, int postId, string replyText)
        {
            if (string.IsNullOrWhiteSpace(replyText))
            {
                TempData["ErrorMessage"] = "Reply cannot be empty.";
                return RedirectToAction("PostDetails", new { id = postId }); // Оставаме на същия пост
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
                PostId = postId, // Свързваме риплея с публикацията
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
                    .Include(c => c.Replies) // Зареждаме всички отговори на коментара
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

                // Проверяваме дали потребителят е администратор, автор на коментара или автор на поста
                if (!User.IsInRole("Admin") && comment.AuthorId != currentUserId && post.UserId != currentUserId)
                {
                    return Json(new { success = false, message = "Permission denied" });
                }

                // Премахваме всички отговори на коментара
                foreach (var reply in comment.Replies.ToList())
                {
                    this.dbContext.Comments.Remove(reply);
                }

                // Премахваме самия коментар
                this.dbContext.Comments.Remove(comment);

                // Записваме промените в базата
                await dbContext.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DeleteComment Error: {ex.Message}");
                return Json(new { success = false, message = "An error occurred", error = ex.Message });
            }
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

        [HttpGet]
        public IActionResult Search()
        {
            if (!User.Identity.IsAuthenticated)
            {
                return Redirect("/Identity/Account/Login"); // Ако не е логнат, го пращаме към логин
            }

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> SearchUsers(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return Json(new { users = new List<object>() });
            }

            var currentUser = await userManager.GetUserAsync(User);

            var users = await this.dbContext.Users
                .Where(u => u.UserName.Contains(query) && (currentUser == null || u.Id != currentUser.Id)) // Филтрираме текущия потребител
                .Select(u => new
                {
                    id = u.Id,
                    username = u.UserName,
                    profilePicture = u.ProfilePictureURL ?? "/images/default-avatar.png",
                })
                .ToListAsync();


            return Json(new { users });
        }


        public async Task<IActionResult> Messages(int? conversationId)
        {
            var currentUser = await userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Redirect("/Identity/Account/Login");
            }

            string profilePictureUrl = string.IsNullOrEmpty(currentUser.ProfilePictureURL)
        ? "/images/default-avatar.jpg" 
        : currentUser.ProfilePictureURL;

            var mutualFollowers = await dbContext.UserFollowers
                .Where(f => f.FollowerId == currentUser.Id && f.UnfollowedDate == null)
                .Select(f => f.UserId)
                .Intersect(
                    dbContext.UserFollowers
                        .Where(f => f.UserId == currentUser.Id && f.UnfollowedDate == null)
                        .Select(f => f.FollowerId)
                )
                .ToListAsync();

            var conversationUserIds = await dbContext.Conversations
                .Where(c => c.User1Id == currentUser.Id || c.User2Id == currentUser.Id)
                .Select(c => c.User1Id == currentUser.Id ? c.User2Id : c.User1Id)
                .ToListAsync();

            Console.WriteLine($"📌 Проверка: {conversationUserIds.Count} активни разговора, {mutualFollowers.Count} взаимни последователи.");

            var newConversations = new List<Conversation>();

            foreach (var followerId in mutualFollowers)
            {
                if (!conversationUserIds.Contains(followerId)) 
                {
                    Console.WriteLine($"🚀 Създаваме нов разговор с {followerId}...");
                    newConversations.Add(new Conversation
                    {
                        User1Id = currentUser.Id,
                        User2Id = followerId,
                        CreatedOn = DateTime.UtcNow
                    });
                }
            }

            if (newConversations.Any())
            {
                dbContext.Conversations.AddRange(newConversations);
                await dbContext.SaveChangesAsync();
                Console.WriteLine($"✅ {newConversations.Count} нови разговора са създадени.");
            }

            var conversations = await dbContext.Conversations
                .Where(c => (c.User1Id == currentUser.Id && mutualFollowers.Contains(c.User2Id)) ||
                            (c.User2Id == currentUser.Id && mutualFollowers.Contains(c.User1Id)))
                .Select(c => new ConversationViewModel
                {
                    Id = c.Id,
                    OtherUserId = c.User1Id == currentUser.Id ? c.User2Id : c.User1Id,
                    OtherUserName = c.User1Id == currentUser.Id ? c.User2.UserName : c.User1.UserName,
                    OtherUserProfilePicture = c.User1Id == currentUser.Id ? c.User2.ProfilePictureURL : c.User1.ProfilePictureURL,
                })
                .ToListAsync();

            var followedUsers = await dbContext.Users
                .Where(u => mutualFollowers.Contains(u.Id) &&
                            !dbContext.Conversations.Any(c =>
                                (c.User1Id == currentUser.Id && c.User2Id == u.Id) ||
                                (c.User2Id == currentUser.Id && c.User1Id == u.Id)))
                .Select(u => new UserViewModel
                {
                    Id = u.Id,
                    Username = u.UserName,
                    ProfilePictureUrl = u.ProfilePictureURL,
                    IsFollowing = true
                })
                .ToListAsync();

            List<MessageViewModel> messages = new();
            if (conversationId.HasValue && conversations.Any(c => c.Id == conversationId.Value))
            {
                messages = await dbContext.Messages
                    .Where(m => m.ConversationId == conversationId.Value)
                    .OrderBy(m => m.SentOn)
                    .Select(m => new MessageViewModel
                    {
                        ConversationId = m.ConversationId,
                        SenderId = m.SenderId,
                        SenderName = m.Sender.UserName,
                        Content = m.Content,
                        SentOn = m.SentOn,
                    })
                    .ToListAsync();
            }

            var viewModel = new MessagesViewModel
            {
                CurrentUserId = currentUser.Id,
                CurrentUserName = currentUser.UserName,
                CurrentUserProfilePicture = profilePictureUrl,
                Conversations = conversations,
                FollowedUsers = followedUsers,
                SelectedConversationId = conversationId ?? 0,
                Messages = messages,
            };

            return View(viewModel);
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
                .SendAsync("ReceiveMessage", sender.UserName, messageModel.Content, profilePictureUrl);

            return Ok(new { message = "Съобщението беше изпратено успешно!" });
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

        public IActionResult Privacy()
        {
            return this.View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return this.View(
                new ErrorViewModel { RequestId = Activity.Current?.Id ?? this.HttpContext.TraceIdentifier });
        }
    }
}
