namespace JobTrack.API.Data;

public sealed class UserProfile
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = "";
    public string Education { get; set; } = "";
    public string ExperienceSummary { get; set; } = "";
    public DateTime CreatedUtc { get; set; }
    public ICollection<UserSkill> Skills { get; set; } = [];
    public ICollection<UserPositionPreference> PositionPreferences { get; set; } = [];
    public ICollection<UserWorkPreference> WorkPreferences { get; set; } = [];
}

public sealed class UserSkill
{
    public Guid Id { get; set; }
    public Guid UserProfileId { get; set; }
    public string Name { get; set; } = "";
    public string Proficiency { get; set; } = "";
    public UserProfile UserProfile { get; set; } = null!;
}

public sealed class UserPositionPreference
{
    public Guid Id { get; set; }
    public Guid UserProfileId { get; set; }
    public string Title { get; set; } = "";
    public string PreferenceType { get; set; } = "";
    public UserProfile UserProfile { get; set; } = null!;
}

public sealed class UserWorkPreference
{
    public Guid Id { get; set; }
    public Guid UserProfileId { get; set; }
    public string Preference { get; set; } = "";
    public UserProfile UserProfile { get; set; } = null!;
}

public sealed class Company
{
    public Guid Id { get; set; }
    public Guid UserProfileId { get; set; }
    public string Name { get; set; } = "";
    public string NormalizedName { get; set; } = "";
    public string Details { get; set; } = "";
    public string WebsiteUrl { get; set; } = "";
    public ICollection<JobApplication> Applications { get; set; } = [];
    public ICollection<JobOffer> Offers { get; set; } = [];
}

public sealed class JobOffer
{
    public Guid Id { get; set; }
    public Guid? CompanyId { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Requirements { get; set; } = "";
    public string Location { get; set; } = "";
    public string WorkMode { get; set; } = "";
    public string Salary { get; set; } = "";
    public string SourceName { get; set; } = "";
    public string SourceExternalId { get; set; } = "";
    public string SourceUrl { get; set; } = "";
    public DateOnly? PublishedDate { get; set; }
    public DateTime CreatedUtc { get; set; }
    public Company? Company { get; set; }
}

public sealed class JobApplication
{
    public Guid Id { get; set; }
    public Guid UserProfileId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid? JobOfferId { get; set; }
    public string Position { get; set; } = "";
    public DateOnly ApplicationDate { get; set; }
    public DateOnly? ResponseDate { get; set; }
    public string Status { get; set; } = "";
    public string WorkMode { get; set; } = "";
    public string Salary { get; set; } = "";
    public string JobUrl { get; set; } = "";
    public string JobDetails { get; set; } = "";
    public string Notes { get; set; } = "";
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public Company Company { get; set; } = null!;
    public JobOffer? JobOffer { get; set; }
    public ICollection<ApplicationDocument> Documents { get; set; } = [];
    public ICollection<ApplicationHistory> History { get; set; } = [];
    public ICollection<Interview> Interviews { get; set; } = [];
    public ICollection<Reminder> Reminders { get; set; } = [];
}

public sealed class ApplicationDocument
{
    public Guid Id { get; set; }
    public Guid ApplicationId { get; set; }
    public string DocumentType { get; set; } = "";
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long SizeBytes { get; set; }
    public byte[] Content { get; set; } = [];
    public DateTime UploadedUtc { get; set; }
    public JobApplication Application { get; set; } = null!;
}

public sealed class ApplicationHistory
{
    public Guid Id { get; set; }
    public Guid ApplicationId { get; set; }
    public string EventType { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTime OccurredUtc { get; set; }
    public JobApplication Application { get; set; } = null!;
}

public sealed class Interview
{
    public Guid Id { get; set; }
    public Guid ApplicationId { get; set; }
    public DateTimeOffset ScheduledUtc { get; set; }
    public string InterviewType { get; set; } = "";
    public string LocationOrLink { get; set; } = "";
    public string ContactName { get; set; } = "";
    public string Notes { get; set; } = "";
    public JobApplication Application { get; set; } = null!;
}

public sealed class Reminder
{
    public Guid Id { get; set; }
    public Guid ApplicationId { get; set; }
    public DateOnly DueDate { get; set; }
    public string Note { get; set; } = "";
    public bool IsCompleted { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
    public JobApplication Application { get; set; } = null!;
}
