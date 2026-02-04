using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AppTimeTracker.Models;

/// <summary>
/// Represents a focus/session event with telemetry data for monitoring and analysis.
/// </summary>
[Table("FocusEvents")]
public class FocusEvent
{
    /// <summary>
    /// Primary key for the focus event record.
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>
    /// Name of the process that gained/lost focus (e.g., "chrome", "notepad").
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string ProcessName { get; set; } = string.Empty;

    /// <summary>
    /// Title of the active window during the focus event.
    /// </summary>
    [MaxLength(1024)]
    public string? WindowTitle { get; set; }

    /// <summary>
    /// UTC timestamp when the focus event occurred.
    /// </summary>
    [Required]
    public DateTime StartTimeUtc { get; set; }

    /// <summary>
    /// UTC timestamp when the focus event ended (when focus moved to another app).
    /// </summary>
    public DateTime? EndTimeUtc { get; set; }

    /// <summary>
    /// Type of event (Focus, Blur, Lock, Unlock, SessionStart, SessionEnd).
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string EventType { get; set; } = "Focus";

    /// <summary>
    /// Windows user identifier (SID or username) for multi-user support.
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Date of the event for daily aggregation queries.
    /// </summary>
    [Required]
    public DateOnly EventDate { get; set; }

    /// <summary>
    /// Unique identifier for deduplication purposes (optional).
    /// </summary>
    [MaxLength(256)]
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Timestamp when the event was persisted to database.
    /// </summary>
    public DateTime? PersistedAtUtc { get; set; }
}
