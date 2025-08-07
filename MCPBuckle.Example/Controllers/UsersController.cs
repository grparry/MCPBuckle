using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace MCPBuckle.Example.Controllers
{
    /// <summary>
    /// Advanced controller showcasing MCPBuckle 1.6.0 features including route parameter extraction,
    /// parameter source detection, and complex object schema generation.
    /// </summary>
    [ApiController]
    [Route("api/users")]
    public class UsersController : ControllerBase
    {
        private static readonly List<User> _users = new()
        {
            new User { Id = 1, Username = "alice", Email = "alice@example.com", Profile = new UserProfile { FirstName = "Alice", LastName = "Smith", Bio = "Software Developer" } },
            new User { Id = 2, Username = "bob", Email = "bob@example.com", Profile = new UserProfile { FirstName = "Bob", LastName = "Jones", Bio = "Product Manager" } }
        };

        /// <summary>
        /// Gets user todos with advanced parameter handling showcasing MCPBuckle 1.6.0 capabilities.
        /// MCPBuckle will automatically detect userId as route parameter and status/limit as query parameters.
        /// </summary>
        /// <param name="userId">The user ID to get todos for (route parameter)</param>
        /// <param name="status">Optional status filter (query parameter)</param>
        /// <param name="limit">Maximum number of results to return (query parameter)</param>
        /// <param name="apiKey">API key for authentication (header parameter)</param>
        /// <returns>A list of todo items for the specified user.</returns>
        [HttpGet("{userId}/todos")]
        public ActionResult<IEnumerable<TodoSummary>> GetUserTodos(
            int userId,
            [FromQuery] string? status = null,
            [FromQuery] int limit = 10,
            [FromHeader] string? apiKey = null)
        {
            // MCPBuckle 1.6.0 will generate sophisticated schema:
            // - userId: route parameter with integer type
            // - status: query parameter with string type (optional)
            // - limit: query parameter with integer type and default value
            // - apiKey: header parameter with string type (optional)
            
            var user = _users.FirstOrDefault(u => u.Id == userId);
            if (user == null) return NotFound($"User with ID {userId} not found");

            var todos = new List<TodoSummary>
            {
                new() { Id = 1, Title = $"Task for {user.Username}", IsCompleted = false, Priority = Priority.High },
                new() { Id = 2, Title = $"Another task for {user.Username}", IsCompleted = true, Priority = Priority.Medium }
            };

            if (!string.IsNullOrEmpty(status))
            {
                var isCompleted = status.Equals("completed", StringComparison.OrdinalIgnoreCase);
                todos = todos.Where(t => t.IsCompleted == isCompleted).ToList();
            }

            return Ok(todos.Take(limit));
        }

        /// <summary>
        /// Updates user profile with complex object schema generation.
        /// Demonstrates MCPBuckle 1.6.0's ability to generate detailed schemas for complex request objects.
        /// </summary>
        /// <param name="userId">The user ID to update (route parameter)</param>
        /// <param name="request">The profile update request with nested objects (body parameter)</param>
        /// <returns>The updated user profile.</returns>
        [HttpPut("{userId}/profile")]
        public ActionResult<UserProfile> UpdateUserProfile(
            int userId,
            [FromBody] UpdateProfileRequest request)
        {
            // MCPBuckle 1.6.0 will generate:
            // - userId: route parameter with source annotation
            // - request: complex object schema with recursive property mapping
            //   including nested objects, enums, arrays, and validation requirements
            
            var user = _users.FirstOrDefault(u => u.Id == userId);
            if (user == null) return NotFound();

            // Update logic would go here
            user.Profile.FirstName = request.FirstName;
            user.Profile.LastName = request.LastName;
            user.Profile.Bio = request.Bio;
            
            return Ok(user.Profile);
        }

        /// <summary>
        /// Creates a user notification with multiple parameter sources.
        /// Showcases comprehensive parameter source detection across route, query, header, and body.
        /// </summary>
        /// <param name="userId">Target user ID (route parameter)</param>
        /// <param name="notificationId">Specific notification ID (route parameter)</param>
        /// <param name="urgent">Whether the notification is urgent (query parameter)</param>
        /// <param name="clientId">Client identifier (header parameter)</param>
        /// <param name="notification">The notification content (body parameter)</param>
        /// <returns>The created notification with metadata.</returns>
        [HttpPost("{userId}/notifications/{notificationId}")]
        public ActionResult<NotificationResult> CreateUserNotification(
            int userId,
            Guid notificationId,
            [FromQuery] bool urgent = false,
            [FromHeader] string? clientId = null,
            [FromBody] NotificationRequest notification = null!)
        {
            // MCPBuckle 1.6.0 demonstrates advanced schema generation:
            // - Multiple route parameters (userId: int, notificationId: Guid)  
            // - Query parameters with default values (urgent: bool)
            // - Header parameters (clientId: string, optional)
            // - Complex body parameters with validation attributes
            // - All with appropriate source annotations for MCPInvoke execution
            
            return Ok(new NotificationResult
            {
                Id = notificationId,
                UserId = userId,
                Message = notification.Message,
                IsUrgent = urgent,
                ClientId = clientId,
                CreatedAt = DateTime.UtcNow
            });
        }
    }

    #region Supporting Model Classes - Showcasing Complex Schema Generation

    /// <summary>
    /// Represents a user in the system with nested profile object.
    /// </summary>
    public class User
    {
        /// <summary>
        /// Gets or sets the unique user identifier.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the username.
        /// </summary>
        [Required]
        [StringLength(50, MinimumLength = 3)]
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user's email address.
        /// </summary>
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user's profile information.
        /// </summary>
        public UserProfile Profile { get; set; } = new();
    }

    /// <summary>
    /// Complex nested object demonstrating recursive schema generation.
    /// </summary>
    public class UserProfile
    {
        /// <summary>
        /// Gets or sets the user's first name.
        /// </summary>
        [Required]
        [StringLength(100)]
        public string FirstName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user's last name.
        /// </summary>
        [Required]
        [StringLength(100)]
        public string LastName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user's biography.
        /// </summary>
        [StringLength(500)]
        public string? Bio { get; set; }

        /// <summary>
        /// Gets or sets the user's preferences.
        /// </summary>
        public UserPreferences Preferences { get; set; } = new();

        /// <summary>
        /// Gets or sets the user's contact methods.
        /// </summary>
        public List<ContactMethod> ContactMethods { get; set; } = new();
    }

    /// <summary>
    /// User preferences demonstrating enum handling in schema generation.
    /// </summary>
    public class UserPreferences
    {
        /// <summary>
        /// Gets or sets the preferred theme.
        /// </summary>
        public Theme Theme { get; set; } = Theme.Light;

        /// <summary>
        /// Gets or sets the preferred language.
        /// </summary>
        [StringLength(10)]
        public string Language { get; set; } = "en";

        /// <summary>
        /// Gets or sets notification preferences.
        /// </summary>
        public NotificationSettings Notifications { get; set; } = new();
    }

    /// <summary>
    /// Contact method for demonstrating array of complex objects in schema.
    /// </summary>
    public class ContactMethod
    {
        /// <summary>
        /// Gets or sets the contact type.
        /// </summary>
        public ContactType Type { get; set; }

        /// <summary>
        /// Gets or sets the contact value (phone, email, etc.).
        /// </summary>
        [Required]
        public string Value { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether this is the primary contact method.
        /// </summary>
        public bool IsPrimary { get; set; }
    }

    /// <summary>
    /// Request object for profile updates showcasing complex validation attributes.
    /// </summary>
    public class UpdateProfileRequest
    {
        /// <summary>
        /// Gets or sets the first name.
        /// </summary>
        [Required]
        [StringLength(100, MinimumLength = 1)]
        public string FirstName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the last name.
        /// </summary>
        [Required]
        [StringLength(100, MinimumLength = 1)]
        public string LastName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the biography.
        /// </summary>
        [StringLength(500)]
        public string? Bio { get; set; }

        /// <summary>
        /// Gets or sets updated preferences.
        /// </summary>
        public UserPreferences? Preferences { get; set; }

        /// <summary>
        /// Gets or sets the list of tags associated with the user.
        /// </summary>
        public List<string> Tags { get; set; } = new();
    }

    /// <summary>
    /// Notification request demonstrating complex body parameter schemas.
    /// </summary>
    public class NotificationRequest
    {
        /// <summary>
        /// Gets or sets the notification message.
        /// </summary>
        [Required]
        [StringLength(1000, MinimumLength = 1)]
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the notification type.
        /// </summary>
        public NotificationType Type { get; set; } = NotificationType.Info;

        /// <summary>
        /// Gets or sets additional metadata.
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();

        /// <summary>
        /// Gets or sets the expiration time.
        /// </summary>
        public DateTime? ExpiresAt { get; set; }
    }

    /// <summary>
    /// Notification result demonstrating output schema generation.
    /// </summary>
    public class NotificationResult
    {
        /// <summary>
        /// Gets or sets the notification ID.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the target user ID.
        /// </summary>
        public int UserId { get; set; }

        /// <summary>
        /// Gets or sets the notification message.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether the notification is urgent.
        /// </summary>
        public bool IsUrgent { get; set; }

        /// <summary>
        /// Gets or sets the client ID that created the notification.
        /// </summary>
        public string? ClientId { get; set; }

        /// <summary>
        /// Gets or sets when the notification was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// Todo summary for demonstrating return type schemas.
    /// </summary>
    public class TodoSummary
    {
        /// <summary>
        /// Gets or sets the todo ID.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the todo title.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether the todo is completed.
        /// </summary>
        public bool IsCompleted { get; set; }

        /// <summary>
        /// Gets or sets the todo priority.
        /// </summary>
        public Priority Priority { get; set; }
    }

    /// <summary>
    /// Notification settings for nested object schema generation.
    /// </summary>
    public class NotificationSettings
    {
        /// <summary>
        /// Gets or sets whether email notifications are enabled.
        /// </summary>
        public bool EmailEnabled { get; set; } = true;

        /// <summary>
        /// Gets or sets whether push notifications are enabled.
        /// </summary>
        public bool PushEnabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the notification frequency.
        /// </summary>
        public NotificationFrequency Frequency { get; set; } = NotificationFrequency.Immediate;
    }

    #endregion

    #region Enums for Schema Generation Testing

    /// <summary>
    /// Theme options demonstrating enum schema generation.
    /// </summary>
    public enum Theme
    {
        /// <summary>
        /// Light theme.
        /// </summary>
        Light,

        /// <summary>
        /// Dark theme.
        /// </summary>
        Dark,

        /// <summary>
        /// System theme (follows OS preference).
        /// </summary>
        System
    }

    /// <summary>
    /// Contact types for demonstrating enum arrays in schemas.
    /// </summary>
    public enum ContactType
    {
        /// <summary>
        /// Email contact.
        /// </summary>
        Email,

        /// <summary>
        /// Phone contact.
        /// </summary>
        Phone,

        /// <summary>
        /// SMS contact.
        /// </summary>
        Sms,

        /// <summary>
        /// Slack contact.
        /// </summary>
        Slack
    }

    /// <summary>
    /// Notification types for request object enums.
    /// </summary>
    public enum NotificationType
    {
        /// <summary>
        /// Information notification.
        /// </summary>
        Info,

        /// <summary>
        /// Warning notification.
        /// </summary>
        Warning,

        /// <summary>
        /// Error notification.
        /// </summary>
        Error,

        /// <summary>
        /// Success notification.
        /// </summary>
        Success
    }

    /// <summary>
    /// Notification frequency options.
    /// </summary>
    public enum NotificationFrequency
    {
        /// <summary>
        /// Immediate notifications.
        /// </summary>
        Immediate,

        /// <summary>
        /// Hourly digest.
        /// </summary>
        Hourly,

        /// <summary>
        /// Daily digest.
        /// </summary>
        Daily,

        /// <summary>
        /// Weekly digest.
        /// </summary>
        Weekly
    }

    #endregion
}