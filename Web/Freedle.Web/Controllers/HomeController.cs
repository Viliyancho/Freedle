namespace Freedle.Web.Controllers
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
    using Freedle.Web.ViewModels;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using SendGrid.Helpers.Mail;

    public class HomeController : BaseController
    {
        private readonly ApplicationDbContext dbContext;
        private readonly UserManager<ApplicationUser> userManager;

        public HomeController(ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager)
        {
            this.dbContext = dbContext;
            this.userManager = userManager;
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

            // Препоръчани потребители (случайни 5)
            var suggestedUsers = this.dbContext.Users
                .Where(u => u.Id != currentUserId)
                .OrderBy(r => Guid.NewGuid()) // Random
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
                return Unauthorized(); // Ако не е логнат, връщаме грешка
            }

            var post = dbContext.Posts.FirstOrDefault(p => p.Id == postId);
            if (post == null)
            {
                return NotFound(); // Постът не съществува
            }

            // Проверка дали текущият потребител е автор на поста или администратор
            bool isAdmin = User.IsInRole("Admin"); // Ако имаш роля за админ
            if (post.UserId != userId && !isAdmin)
            {
                return Forbid(); // Забраняваме изтриването, ако не е негов
            }

            dbContext.Comments.RemoveRange(post.Comments);
            dbContext.SaveChanges(); // Запази промените преди да трием поста


            dbContext.Posts.Remove(post);
            dbContext.SaveChanges();

            return Ok(); // Връщаме 200 статус за успешна заявка
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ToggleLike(int postId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null)
            {
                return this.Redirect("/Identity/Account/Login"); // Ако не е логнат, пращаме към логин
            }

            var post = this.dbContext.Posts.Include(p => p.Likes).FirstOrDefault(p => p.Id == postId);
            if (post == null)
            {
                return this.NotFound();
            }

            var existingLike = this.dbContext.UserLikes.FirstOrDefault(l => l.PostId == postId && l.UserId == userId);

            if (existingLike != null)
            {
                // Премахва лайка, ако вече е натиснат
                post.Likes.Remove(existingLike);
                this.dbContext.UserLikes.Remove(existingLike);
                post.LikeCount--;
            }
            else
            {
                // Добавя нов лайк
                var like = new UserLike { PostId = postId, UserId = userId };
                post.Likes.Add(like);
                dbContext.UserLikes.Add(like);
                post.LikeCount++;
            }

            dbContext.SaveChanges();

            return RedirectToAction("Index", "Home"); // Презарежда страницата
        }

        [HttpGet]
        public async Task<IActionResult> UserProfile(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier); // Взимаме логнатия потребител

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
            Console.WriteLine("Received ID: " + id); // Логване на входния параметър

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
                .ToList(); // Връща List<UserViewModel>

            return View(following); // Подаваме правилния тип
        }


        [HttpGet]
        public async Task<IActionResult> FollowersList(string id)
        {
            var user = await dbContext.Users
                .Include(u => u.Followers)
                .ThenInclude(f => f.Follower) // Зареждаме детайлите на последователите
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
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier); // ID на логнатия потребител

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
                .ToListAsync(); // Изпълняваме заявката асинхронно

            return View(following);
        }


        [HttpGet]
        public async Task<IActionResult> UserFollowersList(string id)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier); // ID на логнатия потребител

            var followers = await dbContext.UserFollowers
                .Where(f => f.UserId == id) // Взимаме всички, които следват този user
                .Include(f => f.Follower) // Включваме детайлите за последователя
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
        [ValidateAntiForgeryToken] // CSRF защита
        public async Task<IActionResult> Unfollow(string userId)
        {
            var currentUser = await userManager.GetUserAsync(User);
            if (currentUser == null) return Unauthorized();

            // Проверяваме дали userId е валиден
            if (string.IsNullOrEmpty(userId)) return BadRequest("Invalid user ID");

            // Търсим връзката за премахване
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
        [ValidateAntiForgeryToken] // CSRF защита
        public async Task<IActionResult> RemoveFollower(string userId)
        {
            var currentUser = await userManager.GetUserAsync(User);
            if (currentUser == null) return Unauthorized();

            // Намираме записа в таблицата UserFollower
            var followerToRemove = await dbContext.UserFollowers
                .FirstOrDefaultAsync(uf => uf.UserId == currentUser.Id && uf.FollowerId == userId);

            if (followerToRemove == null) return NotFound();

            // Премахваме го от контекста
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
    .Include(u => u.Posts) // Зареждаме постовете заедно с потребителя
    .FirstOrDefaultAsync(u => u.Id == this.userManager.GetUserId(User));

            // Ако потребителят не е логнат, пренасочваме към страницата за вход
            if (currentUser == null)
            {
                return this.Redirect("/Identity/Account/Login");
            }

            Console.WriteLine(currentUser.Posts.Count);

            var followerCount = this.dbContext.UserFollowers
        .Where(f => f.UserId == currentUser.Id && f.UnfollowedDate == null)
        .Count();

            // Броят на хората, които потребителят следва: броим колко потребители текущият потребител следва
            var followingCount = this.dbContext.UserFollowers
                .Where(f => f.FollowerId == currentUser.Id && f.UnfollowedDate == null)
                .Count();

            // Вземаме допълнителна информация от потребителския профил (например, име, дата на раждане, пол и т.н.)
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

            // Връщаме данните към изгледа (може да го използвате с изглед с име "MyProfile")
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





        // GET метод, който показва формата за създаване на пост
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

                // Проверяваме дали потребителят е качил файл
                if (model.ImageURL != null && model.ImageURL.Length > 0)
                {
                    // Генерираме уникално име за файла
                    string fileName = Guid.NewGuid().ToString() + Path.GetExtension(model.ImageURL.FileName);
                    string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images");

                    // Създаваме директорията ако не съществува
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

            var currentUser = await userManager.GetUserAsync(User); // Взимаме логнатия потребител

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


        public IActionResult Messages()
        {
            return this.View();
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
