using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AppTimeTracker.Models;

/// <summary>
/// Represents an app usage session with focus tracking information.
/// </summary>
[Table("AppUsageSessions")]
public class AppUsageSession
{
    /// <summary>
    /// Primary key for the session record.
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>
    /// Name of the process (e.g., "chrome", "notepad").
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string ProcessName { get; set; } = string.Empty;

    /// <summary>
    /// Full path to the executable file.
    /// </summary>
    [MaxLength(1024)]
    public string? ExecutablePath { get; set; }

    /// <summary>
    /// Title of the active window during the session.
    /// </summary>
    [MaxLength(1024)]
    public string? WindowTitle { get; set; }

    /// <summary>
    /// UTC timestamp when the session started (app gained focus).
    /// </summary>
    [Required]
    public DateTime StartTimeUtc { get; set; }

    /// <summary>
    /// UTC timestamp when the session ended (app lost focus).
    /// Null if session is still active.
    /// </summary>
    public DateTime? EndTimeUtc { get; set; }

    /// <summary>
    /// Total duration of the session in seconds.
    /// </summary>
    public long DurationSeconds { get; set; }

    /// <summary>
    /// Date of the session for daily aggregation queries.
    /// </summary>
    [Required]
    public DateOnly SessionDate { get; set; }

    /// <summary>
    /// Windows user identifier (SID or username) for multi-user support.
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Calculates and updates the duration based on start and end times.
    /// </summary>
    public void CalculateDuration()
    {
        if (EndTimeUtc.HasValue)
        {
            DurationSeconds = (long)(EndTimeUtc.Value - StartTimeUtc).TotalSeconds;
        }
    }
}
