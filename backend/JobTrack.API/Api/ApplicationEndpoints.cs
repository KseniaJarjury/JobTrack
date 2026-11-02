using System.Globalization;
using JobTrack.API.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace JobTrack.API.Api;

public static class ApplicationEndpoints
{
    private const long MaximumFileSize = 10 * 1024 * 1024;
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".doc", ".docx", ".odt"
    };
    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.Ordinal)
    {
        "Postulada", "En revisión", "Entrevista", "Oferta", "Rechazada", "Retirada"
    };
    private static readonly Expression<Func<JobApplication, ApplicationResponse>> ApplicationProjection =
        application => new ApplicationResponse(
            application.Id,
            application.Company.Name,
            application.Company.Details,
            application.Company.WebsiteUrl,
            application.Position,
            application.ApplicationDate,
            application.ResponseDate,
            application.Reminders.Where(reminder => !reminder.IsCompleted)
                .OrderBy(reminder => reminder.DueDate)
                .Select(reminder => (DateOnly?)reminder.DueDate)
                .FirstOrDefault(),
            application.Status,
            application.WorkMode,
            application.Salary,
            application.JobUrl,
            application.JobDetails,
            application.Notes,
            application.CreatedUtc,
            application.UpdatedUtc,
            application.Documents.Select(document => new DocumentResponse(
                document.Id,
                document.DocumentType,
                document.FileName)).ToList());

    public static RouteGroupBuilder MapApplicationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/applications");

        group.MapGet("/", async (JobTrackDbContext db, CancellationToken cancellationToken) =>
        {
            var applications = await db.JobApplications
                .AsNoTracking()
                .Where(application => application.UserProfileId == JobTrackDbContext.LocalUserId)
                .OrderByDescending(application => application.ApplicationDate)
                .Select(ApplicationProjection)
                .ToListAsync(cancellationToken);
            return Results.Ok(applications);
        });

        group.MapPost("/", CreateApplication)
            .WithMetadata(new RequestSizeLimitAttribute(22 * 1024 * 1024));

        group.MapPut("/{id:guid}", UpdateApplication)
            .WithMetadata(new RequestSizeLimitAttribute(22 * 1024 * 1024));

        group.MapDelete("/{id:guid}", DeleteApplication);
        group.MapGet("/{id:guid}/history", GetHistory);
        group.MapPost("/{id:guid}/history", AddHistoryEntry);
        group.MapPost("/{id:guid}/reminders/complete", CompleteReminder);
        group.MapGet("/{id:guid}/documents/{documentId:guid}", DownloadDocument);

        endpoints.MapGet("/api/interviews", GetInterviews);
        endpoints.MapPost("/api/applications/{id:guid}/interviews", CreateInterview);
        endpoints.MapPut("/api/interviews/{interviewId:guid}", UpdateInterview);
        endpoints.MapDelete("/api/interviews/{interviewId:guid}", DeleteInterview);

        return group;
    }

    private static async Task<IResult> CreateApplication(
        [FromForm] ApplicationForm form,
        JobTrackDbContext db,
        CancellationToken cancellationToken)
    {
        var validationError = Validate(form);
        if (validationError is not null)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["application"] = [validationError] });

        var id = Guid.TryParse(form.Id, out var suppliedId) ? suppliedId : Guid.NewGuid();
        if (await db.JobApplications.AnyAsync(application => application.Id == id, cancellationToken))
            return Results.Conflict(new { message = "La postulación ya existe." });

        var company = await GetOrCreateCompany(
            db,
            form.Company!.Trim(),
            form.CompanyDetails?.Trim() ?? "",
            form.CompanyWebsiteUrl?.Trim() ?? "",
            cancellationToken);
        var now = DateTime.UtcNow;
        var application = new JobApplication
        {
            Id = id,
            UserProfileId = JobTrackDbContext.LocalUserId,
            CompanyId = company.Id,
            Company = company,
            Position = form.Position!.Trim(),
            ApplicationDate = ParseDate(form.ApplicationDate)!.Value,
            ResponseDate = ParseDate(form.ResponseDate),
            Status = form.Status!,
            WorkMode = form.WorkMode?.Trim() ?? "",
            Salary = form.Salary?.Trim() ?? "",
            JobUrl = form.JobUrl?.Trim() ?? "",
            JobDetails = form.JobDetails?.Trim() ?? "",
            Notes = form.Notes?.Trim() ?? "",
            CreatedUtc = now,
            UpdatedUtc = now
        };
        application.History.Add(new ApplicationHistory
        {
            Id = Guid.NewGuid(),
            EventType = "Created",
            Description = $"Postulación registrada con estado «{application.Status}».",
            OccurredUtc = now
        });

        await AddDocumentIfPresent(application, form.Resume, "CV", cancellationToken);
        await AddDocumentIfPresent(application, form.CoverLetter, "CoverLetter", cancellationToken);
        if (application.Status is not ("Rechazada" or "Retirada") &&
            ParseDate(form.NextFollowUpDate) is { } dueDate)
        {
            application.Reminders.Add(new Reminder
            {
                Id = Guid.NewGuid(),
                DueDate = dueDate,
                Note = "Seguimiento de postulación",
                CreatedUtc = now
            });
        }

        db.JobApplications.Add(application);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/applications/{id}", await GetApplicationResponse(db, id, cancellationToken));
    }

    private static async Task<IResult> UpdateApplication(
        Guid id,
        [FromForm] ApplicationForm form,
        JobTrackDbContext db,
        CancellationToken cancellationToken)
    {
        var validationError = Validate(form);
        if (validationError is not null)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["application"] = [validationError] });

        var application = await db.JobApplications
            .Include(item => item.Company)
            .Include(item => item.Reminders.Where(reminder => !reminder.IsCompleted))
            .Include(item => item.Documents)
            .Include(item => item.History)
            .SingleOrDefaultAsync(item => item.Id == id && item.UserProfileId == JobTrackDbContext.LocalUserId, cancellationToken);
        if (application is null) return Results.NotFound();

        var company = await GetOrCreateCompany(
            db,
            form.Company!.Trim(),
            form.CompanyDetails?.Trim() ?? "",
            form.CompanyWebsiteUrl?.Trim() ?? "",
            cancellationToken);
        var oldStatus = application.Status;
        var newStatus = form.Status!;
        application.CompanyId = company.Id;
        application.Company = company;
        application.Position = form.Position!.Trim();
        application.ApplicationDate = ParseDate(form.ApplicationDate)!.Value;
        application.ResponseDate = ParseDate(form.ResponseDate);
        application.Status = newStatus;
        application.WorkMode = form.WorkMode?.Trim() ?? "";
        application.Salary = form.Salary?.Trim() ?? "";
        application.JobUrl = form.JobUrl?.Trim() ?? "";
        application.JobDetails = form.JobDetails?.Trim() ?? "";
        application.Notes = form.Notes?.Trim() ?? "";
        application.UpdatedUtc = DateTime.UtcNow;

        if (!string.Equals(oldStatus, newStatus, StringComparison.Ordinal))
        {
            application.History.Add(new ApplicationHistory
            {
                Id = Guid.NewGuid(),
                EventType = "StatusChanged",
                Description = $"Estado cambiado de «{oldStatus}» a «{newStatus}».",
                OccurredUtc = DateTime.UtcNow
            });
        }

        await UpdateDocument(application, form.Resume, form.RemoveResume, "CV", cancellationToken);
        await UpdateDocument(application, form.CoverLetter, form.RemoveCoverLetter, "CoverLetter", cancellationToken);
        UpdateReminder(application, ParseDate(form.NextFollowUpDate));

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(await GetApplicationResponse(db, id, cancellationToken));
    }

    private static async Task<IResult> DeleteApplication(Guid id, JobTrackDbContext db, CancellationToken cancellationToken)
    {
        var application = await db.JobApplications.SingleOrDefaultAsync(
            item => item.Id == id && item.UserProfileId == JobTrackDbContext.LocalUserId,
            cancellationToken);
        if (application is null) return Results.NotFound();
        db.JobApplications.Remove(application);
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> GetHistory(Guid id, JobTrackDbContext db, CancellationToken cancellationToken)
    {
        var belongsToUser = await db.JobApplications.AnyAsync(
            application => application.Id == id && application.UserProfileId == JobTrackDbContext.LocalUserId,
            cancellationToken);
        if (!belongsToUser) return Results.NotFound();

        var history = await db.ApplicationHistory
            .AsNoTracking()
            .Where(item => item.ApplicationId == id)
            .OrderByDescending(item => item.OccurredUtc)
            .Select(item => new HistoryResponse(item.Id, item.EventType, item.Description, item.OccurredUtc))
            .ToListAsync(cancellationToken);
        return Results.Ok(history);
    }

    private static async Task<IResult> AddHistoryEntry(
        Guid id,
        HistoryEntryForm form,
        JobTrackDbContext db,
        CancellationToken cancellationToken)
    {
        var description = form.Description?.Trim();
        if (string.IsNullOrWhiteSpace(description) || description.Length > 1000)
            return Results.ValidationProblem(new Dictionary<string, string[]> {
                ["description"] = ["La nota es obligatoria y debe tener hasta 1000 caracteres."]
            });

        var application = await db.JobApplications.SingleOrDefaultAsync(
            item => item.Id == id && item.UserProfileId == JobTrackDbContext.LocalUserId,
            cancellationToken);
        if (application is null) return Results.NotFound();

        db.ApplicationHistory.Add(new ApplicationHistory
        {
            Id = Guid.NewGuid(),
            ApplicationId = id,
            EventType = "Note",
            Description = description,
            OccurredUtc = DateTime.UtcNow
        });
        application.UpdatedUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> CompleteReminder(Guid id, JobTrackDbContext db, CancellationToken cancellationToken)
    {
        var application = await db.JobApplications
            .Include(item => item.Reminders.Where(reminder => !reminder.IsCompleted))
            .SingleOrDefaultAsync(item => item.Id == id && item.UserProfileId == JobTrackDbContext.LocalUserId, cancellationToken);
        if (application is null) return Results.NotFound();

        foreach (var reminder in application.Reminders)
        {
            reminder.IsCompleted = true;
            reminder.CompletedUtc = DateTime.UtcNow;
        }
        application.History.Add(new ApplicationHistory
        {
            Id = Guid.NewGuid(),
            EventType = "ReminderCompleted",
            Description = "Seguimiento marcado como realizado.",
            OccurredUtc = DateTime.UtcNow
        });
        application.UpdatedUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> DownloadDocument(
        Guid id,
        Guid documentId,
        JobTrackDbContext db,
        CancellationToken cancellationToken)
    {
        var document = await db.ApplicationDocuments
            .AsNoTracking()
            .Where(item => item.Id == documentId &&
                item.ApplicationId == id &&
                item.Application.UserProfileId == JobTrackDbContext.LocalUserId)
            .Select(item => new { item.Content, item.ContentType, item.FileName })
            .SingleOrDefaultAsync(cancellationToken);
        return document is null
            ? Results.NotFound()
            : Results.File(document.Content, document.ContentType, document.FileName);
    }

    private static async Task<IResult> GetInterviews(JobTrackDbContext db, CancellationToken cancellationToken)
    {
        var interviews = await db.Interviews
            .AsNoTracking()
            .Where(interview => interview.Application.UserProfileId == JobTrackDbContext.LocalUserId)
            .OrderBy(interview => interview.ScheduledUtc)
            .Select(interview => new InterviewResponse(
                interview.Id,
                interview.ApplicationId,
                interview.Application.Company.Name,
                interview.Application.Position,
                interview.ScheduledUtc,
                interview.InterviewType,
                interview.LocationOrLink,
                interview.ContactName,
                interview.Notes))
            .ToListAsync(cancellationToken);
        return Results.Ok(interviews);
    }

    private static async Task<IResult> CreateInterview(
        Guid id,
        InterviewForm form,
        JobTrackDbContext db,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateInterview(form);
        if (validationError is not null)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["interview"] = [validationError] });

        var application = await db.JobApplications.SingleOrDefaultAsync(
            item => item.Id == id && item.UserProfileId == JobTrackDbContext.LocalUserId,
            cancellationToken);
        if (application is null) return Results.NotFound();

        var interview = new Interview
        {
            Id = Guid.NewGuid(),
            ApplicationId = id,
            ScheduledUtc = form.ScheduledAt.ToUniversalTime(),
            InterviewType = form.InterviewType?.Trim() ?? "",
            LocationOrLink = form.LocationOrLink?.Trim() ?? "",
            ContactName = form.ContactName?.Trim() ?? "",
            Notes = form.Notes?.Trim() ?? ""
        };
        db.Interviews.Add(interview);
        db.ApplicationHistory.Add(new ApplicationHistory
        {
            Id = Guid.NewGuid(),
            ApplicationId = id,
            EventType = "InterviewScheduled",
            Description = $"Entrevista agendada para {interview.ScheduledUtc:yyyy-MM-dd HH:mm} UTC.",
            OccurredUtc = DateTime.UtcNow
        });
        application.UpdatedUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/interviews/{interview.Id}", await GetInterviewResponse(db, interview.Id, cancellationToken));
    }

    private static async Task<IResult> UpdateInterview(
        Guid interviewId,
        InterviewForm form,
        JobTrackDbContext db,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateInterview(form);
        if (validationError is not null)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["interview"] = [validationError] });

        var interview = await db.Interviews
            .Include(item => item.Application)
            .SingleOrDefaultAsync(
                item => item.Id == interviewId &&
                    item.Application.UserProfileId == JobTrackDbContext.LocalUserId,
                cancellationToken);
        if (interview is null) return Results.NotFound();

        interview.ScheduledUtc = form.ScheduledAt.ToUniversalTime();
        interview.InterviewType = form.InterviewType?.Trim() ?? "";
        interview.LocationOrLink = form.LocationOrLink?.Trim() ?? "";
        interview.ContactName = form.ContactName?.Trim() ?? "";
        interview.Notes = form.Notes?.Trim() ?? "";
        interview.Application.UpdatedUtc = DateTime.UtcNow;
        db.ApplicationHistory.Add(new ApplicationHistory
        {
            Id = Guid.NewGuid(),
            ApplicationId = interview.ApplicationId,
            EventType = "InterviewUpdated",
            Description = $"Entrevista actualizada para {interview.ScheduledUtc:yyyy-MM-dd HH:mm} UTC.",
            OccurredUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(await GetInterviewResponse(db, interviewId, cancellationToken));
    }

    private static async Task<IResult> DeleteInterview(
        Guid interviewId,
        JobTrackDbContext db,
        CancellationToken cancellationToken)
    {
        var interview = await db.Interviews
            .Include(item => item.Application)
            .SingleOrDefaultAsync(
                item => item.Id == interviewId &&
                    item.Application.UserProfileId == JobTrackDbContext.LocalUserId,
                cancellationToken);
        if (interview is null) return Results.NotFound();

        db.ApplicationHistory.Add(new ApplicationHistory
        {
            Id = Guid.NewGuid(),
            ApplicationId = interview.ApplicationId,
            EventType = "InterviewCancelled",
            Description = $"Entrevista del {interview.ScheduledUtc:yyyy-MM-dd HH:mm} UTC cancelada.",
            OccurredUtc = DateTime.UtcNow
        });
        interview.Application.UpdatedUtc = DateTime.UtcNow;
        db.Interviews.Remove(interview);
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<InterviewResponse?> GetInterviewResponse(
        JobTrackDbContext db,
        Guid interviewId,
        CancellationToken cancellationToken) =>
        await db.Interviews
            .AsNoTracking()
            .Where(interview => interview.Id == interviewId &&
                interview.Application.UserProfileId == JobTrackDbContext.LocalUserId)
            .Select(interview => new InterviewResponse(
                interview.Id,
                interview.ApplicationId,
                interview.Application.Company.Name,
                interview.Application.Position,
                interview.ScheduledUtc,
                interview.InterviewType,
                interview.LocationOrLink,
                interview.ContactName,
                interview.Notes))
            .SingleOrDefaultAsync(cancellationToken);

    private static string? ValidateInterview(InterviewForm form)
    {
        if (form.ScheduledAt == default)
            return "La fecha y hora de la entrevista son obligatorias.";
        if (form.InterviewType?.Length > 100 || form.LocationOrLink?.Length > 1000 ||
            form.ContactName?.Length > 160 || form.Notes?.Length > 2000)
            return "Uno o más datos de la entrevista superan la longitud permitida.";
        return null;
    }

    private static async Task<ApplicationResponse?> GetApplicationResponse(
        JobTrackDbContext db,
        Guid id,
        CancellationToken cancellationToken) =>
        await db.JobApplications
            .AsNoTracking()
            .Where(application => application.Id == id && application.UserProfileId == JobTrackDbContext.LocalUserId)
            .Select(ApplicationProjection)
            .SingleOrDefaultAsync(cancellationToken);

    private static async Task<Company> GetOrCreateCompany(
        JobTrackDbContext db,
        string name,
        string details,
        string websiteUrl,
        CancellationToken cancellationToken)
    {
        var normalizedName = NormalizeCompanyName(name);
        var company = await db.Companies.SingleOrDefaultAsync(
            item => item.UserProfileId == JobTrackDbContext.LocalUserId && item.NormalizedName == normalizedName,
            cancellationToken);
        if (company is not null)
        {
            company.Details = details;
            company.WebsiteUrl = websiteUrl;
            return company;
        }

        company = new Company
        {
            Id = Guid.NewGuid(),
            UserProfileId = JobTrackDbContext.LocalUserId,
            Name = name,
            NormalizedName = normalizedName,
            Details = details,
            WebsiteUrl = websiteUrl
        };
        db.Companies.Add(company);
        return company;
    }

    private static string NormalizeCompanyName(string name) =>
        string.Join(' ', name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();

    private static string? Validate(ApplicationForm form)
    {
        if (string.IsNullOrWhiteSpace(form.Company) || form.Company.Trim().Length > 160)
            return "La empresa es obligatoria y debe tener hasta 160 caracteres.";
        if (string.IsNullOrWhiteSpace(form.Position) || form.Position.Trim().Length > 160)
            return "El puesto es obligatorio y debe tener hasta 160 caracteres.";
        if (form.CompanyDetails?.Length > 2000 || form.CompanyWebsiteUrl?.Length > 500)
            return "La información de empresa supera la longitud permitida.";
        if (!DateOnly.TryParseExact(form.ApplicationDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            return "La fecha de postulación no es válida.";
        if (!string.IsNullOrWhiteSpace(form.ResponseDate) &&
            !DateOnly.TryParseExact(form.ResponseDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            return "La fecha de respuesta no es válida.";
        if (!string.IsNullOrWhiteSpace(form.NextFollowUpDate) &&
            !DateOnly.TryParseExact(form.NextFollowUpDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            return "La fecha del próximo seguimiento no es válida.";
        if (form.Status is null || !AllowedStatuses.Contains(form.Status))
            return "El estado seleccionado no es válido.";
        if (!string.IsNullOrWhiteSpace(form.JobUrl) &&
            (!Uri.TryCreate(form.JobUrl, UriKind.Absolute, out var jobUri) ||
             jobUri.Scheme is not ("http" or "https")))
            return "El enlace de la búsqueda debe usar http o https.";
        if (!string.IsNullOrWhiteSpace(form.CompanyWebsiteUrl) &&
            (!Uri.TryCreate(form.CompanyWebsiteUrl, UriKind.Absolute, out var companyUri) ||
             companyUri.Scheme is not ("http" or "https")))
            return "El sitio de la empresa debe usar http o https.";
        if (form.WorkMode?.Length > 120 || form.Salary?.Length > 120 ||
            form.JobUrl?.Length > 1000 || form.JobDetails?.Length > 8000 || form.Notes?.Length > 4000)
            return "Uno o más campos superan la longitud permitida.";
        if (FileValidationError(form.Resume) is { } resumeError) return resumeError;
        return FileValidationError(form.CoverLetter);
    }

    private static string? FileValidationError(IFormFile? file)
    {
        if (file is null || file.Length == 0) return null;
        if (file.Length > MaximumFileSize) return "Cada documento debe pesar como máximo 10 MB.";
        if (!AllowedExtensions.Contains(Path.GetExtension(file.FileName)))
            return "Los documentos deben tener formato PDF, DOC, DOCX u ODT.";
        return null;
    }

    private static DateOnly? ParseDate(string? value) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;

    private static async Task AddDocumentIfPresent(
        JobApplication application,
        IFormFile? upload,
        string type,
        CancellationToken cancellationToken)
    {
        if (upload is null || upload.Length == 0) return;
        application.Documents.Add(await CreateDocument(upload, type, cancellationToken));
    }

    private static async Task UpdateDocument(
        JobApplication application,
        IFormFile? upload,
        bool remove,
        string type,
        CancellationToken cancellationToken)
    {
        var existing = application.Documents.SingleOrDefault(document => document.DocumentType == type);
        if (remove || (upload is not null && upload.Length > 0))
        {
            if (existing is not null) application.Documents.Remove(existing);
        }
        if (upload is not null && upload.Length > 0)
            application.Documents.Add(await CreateDocument(upload, type, cancellationToken));
    }

    private static void UpdateReminder(JobApplication application, DateOnly? dueDate)
    {
        var activeReminder = application.Reminders.FirstOrDefault();
        if (dueDate is null || application.Status is "Rechazada" or "Retirada")
        {
            if (activeReminder is not null)
            {
                activeReminder.IsCompleted = true;
                activeReminder.CompletedUtc = DateTime.UtcNow;
            }
            return;
        }

        if (activeReminder is not null)
        {
            activeReminder.DueDate = dueDate.Value;
            return;
        }

        application.Reminders.Add(new Reminder
        {
            Id = Guid.NewGuid(),
            DueDate = dueDate.Value,
            Note = "Seguimiento de postulación",
            CreatedUtc = DateTime.UtcNow
        });
    }

    private static async Task<ApplicationDocument> CreateDocument(
        IFormFile upload,
        string type,
        CancellationToken cancellationToken)
    {
        await using var memoryStream = new MemoryStream();
        await upload.CopyToAsync(memoryStream, cancellationToken);
        return new ApplicationDocument
        {
            Id = Guid.NewGuid(),
            DocumentType = type,
            FileName = Path.GetFileName(upload.FileName),
            ContentType = ContentTypeForExtension(Path.GetExtension(upload.FileName)),
            SizeBytes = memoryStream.Length,
            Content = memoryStream.ToArray(),
            UploadedUtc = DateTime.UtcNow
        };
    }

    private static string ContentTypeForExtension(string extension) => extension.ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".doc" => "application/msword",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".odt" => "application/vnd.oasis.opendocument.text",
        _ => "application/octet-stream"
    };
}

public sealed class ApplicationForm
{
    public string? Id { get; set; }
    public string? Company { get; set; }
    public string? CompanyDetails { get; set; }
    public string? CompanyWebsiteUrl { get; set; }
    public string? Position { get; set; }
    public string? ApplicationDate { get; set; }
    public string? ResponseDate { get; set; }
    public string? NextFollowUpDate { get; set; }
    public string? Status { get; set; }
    public string? WorkMode { get; set; }
    public string? Salary { get; set; }
    public string? JobUrl { get; set; }
    public string? JobDetails { get; set; }
    public string? Notes { get; set; }
    public bool RemoveResume { get; set; }
    public bool RemoveCoverLetter { get; set; }
    public IFormFile? Resume { get; set; }
    public IFormFile? CoverLetter { get; set; }
}

public sealed class HistoryEntryForm
{
    public string? Description { get; set; }
}

public sealed class InterviewForm
{
    public DateTimeOffset ScheduledAt { get; set; }
    public string? InterviewType { get; set; }
    public string? LocationOrLink { get; set; }
    public string? ContactName { get; set; }
    public string? Notes { get; set; }
}

public sealed record ApplicationResponse(
    Guid Id,
    string Company,
    string CompanyDetails,
    string CompanyWebsiteUrl,
    string Position,
    DateOnly ApplicationDate,
    DateOnly? ResponseDate,
    DateOnly? NextFollowUpDate,
    string Status,
    string WorkMode,
    string Salary,
    string JobUrl,
    string JobDetails,
    string Notes,
    DateTime CreatedUtc,
    DateTime UpdatedUtc,
    IReadOnlyList<DocumentResponse> Documents);

public sealed record DocumentResponse(Guid Id, string Type, string Name);
public sealed record HistoryResponse(Guid Id, string EventType, string Description, DateTime OccurredUtc);
public sealed record InterviewResponse(
    Guid Id,
    Guid ApplicationId,
    string Company,
    string Position,
    DateTimeOffset ScheduledUtc,
    string InterviewType,
    string LocationOrLink,
    string ContactName,
    string Notes);
