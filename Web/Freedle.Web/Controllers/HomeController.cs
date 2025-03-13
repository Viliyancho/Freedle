namespace Freedle.Web.Controllers
{
    using System;
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

        public IActionResult MessagesDemo()
        {
            return this.View();
        }

        public async Task<IActionResult> PostDetails(int id)
        {
            var post = await this.dbContext.Posts
                .Include(p => p.User)
                .Include(p => p.Comments)
                .ThenInclude(c => c.Author)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (post == null)
            {
                return this.NotFound();
            }

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
                Comments = post.Comments.Select(c => new CommentViewModel
                {
                    Id = c.Id,
                    AuthorId = c.Author.Id,
                    AuthorName = $"{c.Author.FirstName} {c.Author.LastName}",
                    AuthorProfilePictureUrl = c.Author.ProfilePictureURL,
                    CommentText = c.CommentText,
                    PostedOn = c.PostedOn.ToString("yyyy-MM-dd HH:mm"),
                }).ToList(),
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

            parentComment.Replies.Add(newReply);

            dbContext.Comments.Add(newReply);
            await dbContext.SaveChangesAsync();

            return RedirectToAction("PostDetails", new { id = postId }); // Оставаме на същия пост
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteComment(int commentId)
        {
            var comment = await this.dbContext.Comments.FindAsync(commentId);
            if (comment == null)
            {
                return this.NotFound();
            }

            this.dbContext.Comments.Remove(comment);
            await dbContext.SaveChangesAsync();

            return Ok();
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

        public IActionResult Search()
        {
            return this.View();
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
