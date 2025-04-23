using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace MCPBuckle.Example.Controllers
{
    /// <summary>
    /// Controller for managing todo items.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class TodoController : ControllerBase
    {
        private static readonly List<TodoItem> _todos = new List<TodoItem>
        {
            new TodoItem { Id = 1, Title = "Learn MCPBuckle", IsCompleted = false },
            new TodoItem { Id = 2, Title = "Build an example API", IsCompleted = true },
            new TodoItem { Id = 3, Title = "Test with Claude", IsCompleted = false }
        };

        /// <summary>
        /// Gets all todo items.
        /// </summary>
        /// <returns>A list of all todo items.</returns>
        [HttpGet]
        public ActionResult<IEnumerable<TodoItem>> GetAll()
        {
            return Ok(_todos);
        }

        /// <summary>
        /// Gets a specific todo item by ID.
        /// </summary>
        /// <param name="id">The ID of the todo item to retrieve.</param>
        /// <returns>The requested todo item.</returns>
        [HttpGet("{id}")]
        public ActionResult<TodoItem> GetById(int id)
        {
            var todo = _todos.FirstOrDefault(t => t.Id == id);
            if (todo == null)
            {
                return NotFound();
            }

            return Ok(todo);
        }

        /// <summary>
        /// Creates a new todo item.
        /// </summary>
        /// <param name="todo">The todo item to create.</param>
        /// <returns>The created todo item.</returns>
        [HttpPost]
        public ActionResult<TodoItem> Create(TodoItem todo)
        {
            todo.Id = _todos.Count > 0 ? _todos.Max(t => t.Id) + 1 : 1;
            _todos.Add(todo);
            return CreatedAtAction(nameof(GetById), new { id = todo.Id }, todo);
        }

        /// <summary>
        /// Updates an existing todo item.
        /// </summary>
        /// <param name="id">The ID of the todo item to update.</param>
        /// <param name="todo">The updated todo item.</param>
        /// <returns>No content if successful.</returns>
        [HttpPut("{id}")]
        public IActionResult Update(int id, TodoItem todo)
        {
            var existingTodo = _todos.FirstOrDefault(t => t.Id == id);
            if (existingTodo == null)
            {
                return NotFound();
            }

            existingTodo.Title = todo.Title;
            existingTodo.IsCompleted = todo.IsCompleted;

            return NoContent();
        }

        /// <summary>
        /// Deletes a todo item.
        /// </summary>
        /// <param name="id">The ID of the todo item to delete.</param>
        /// <returns>No content if successful.</returns>
        [HttpDelete("{id}")]
        public IActionResult Delete(int id)
        {
            var todo = _todos.FirstOrDefault(t => t.Id == id);
            if (todo == null)
            {
                return NotFound();
            }

            _todos.Remove(todo);
            return NoContent();
        }
    }

    /// <summary>
    /// Represents a todo item.
    /// </summary>
    public class TodoItem
    {
        /// <summary>
        /// Gets or sets the unique identifier for the todo item.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the title of the todo item.
        /// </summary>
        [Required]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the description of the todo item.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the todo item is completed.
        /// </summary>
        public bool IsCompleted { get; set; }

        /// <summary>
        /// Gets or sets the priority of the todo item.
        /// </summary>
        public Priority Priority { get; set; } = Priority.Medium;

        /// <summary>
        /// Gets or sets the due date of the todo item.
        /// </summary>
        public DateTime? DueDate { get; set; }

        /// <summary>
        /// Gets or sets the tags associated with the todo item.
        /// </summary>
        public List<string> Tags { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the subtasks of the todo item.
        /// </summary>
        public List<SubTask> SubTasks { get; set; } = new List<SubTask>();
    }

    /// <summary>
    /// Represents the priority of a todo item.
    /// </summary>
    public enum Priority
    {
        /// <summary>
        /// Low priority.
        /// </summary>
        Low,

        /// <summary>
        /// Medium priority.
        /// </summary>
        Medium,

        /// <summary>
        /// High priority.
        /// </summary>
        High
    }

    /// <summary>
    /// Represents a subtask of a todo item.
    /// </summary>
    public class SubTask
    {
        /// <summary>
        /// Gets or sets the title of the subtask.
        /// </summary>
        [Required]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether the subtask is completed.
        /// </summary>
        public bool IsCompleted { get; set; }
    }
}
