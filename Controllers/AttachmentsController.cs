using System.IO.Compression;
using DeanFlow.Data;
using DeanFlow.Data.Entities;
using DeanFlow.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeanFlow.Controllers;

[Authorize]
public sealed class AttachmentsController : AppController
{
    private readonly DeanFlowDbContext db;
    private readonly DocumentAccessService access;
    private readonly AuditService audit;
    private readonly IWebHostEnvironment environment;
    private readonly NotificationService notifications;

    public AttachmentsController(DeanFlowDbContext db, DocumentAccessService access, AuditService audit,
        IWebHostEnvironment environment, NotificationService notifications) : base(notifications)
    {
        this.db = db; this.access = access; this.audit = audit; this.environment = environment; this.notifications = notifications;
    }

    private const long MaxFileSize = 20 * 1024 * 1024;
    private static readonly Dictionary<string, string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg"
    };

    [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(MaxFileSize + 1024 * 1024)]
    public async Task<IActionResult> Upload(int documentId, IFormFile? file, CancellationToken token)
    {
        var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        if (!await access.CanParticipateAsync(documentId, User.UserId(), role, token)) return NotFound();
        if (file is null || file.Length == 0 || file.Length > MaxFileSize)
        {
            TempData["Error"] = "Файл бос болмауы және көлемі 20 MB-тан аспауы керек.";
            return RedirectToAction("Details", "Documents", new { id = documentId });
        }
        var extension = Path.GetExtension(file.FileName);
        if (!AllowedTypes.TryGetValue(extension, out var expectedType) ||
            (!string.Equals(file.ContentType, expectedType, StringComparison.OrdinalIgnoreCase) && file.ContentType != "application/octet-stream"))
        {
            TempData["Error"] = "Бұл файл пішіміне рұқсат жоқ.";
            return RedirectToAction("Details", "Documents", new { id = documentId });
        }
        var content = new byte[checked((int)file.Length)];
        await using (var input = file.OpenReadStream())
        {
            var offset = 0;
            while (offset < content.Length)
            {
                var count = await input.ReadAsync(content.AsMemory(offset), token);
                if (count == 0) break;
                offset += count;
            }
            if (offset != content.Length || !HasValidSignature(extension, content))
            {
                TempData["Error"] = "Файлдың ішкі пішімі оның кеңейтіміне сәйкес келмейді.";
                return RedirectToAction("Details", "Documents", new { id = documentId });
            }
        }

        var document = await db.Documents.FirstOrDefaultAsync(x => x.DocumentId == documentId, token);
        if (document is null) return NotFound();
        var storedName = Guid.NewGuid().ToString("N") + extension.ToLowerInvariant();
        var storageRoot = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "App_Data", "attachments"));
        Directory.CreateDirectory(storageRoot);
        var fullPath = Path.GetFullPath(Path.Combine(storageRoot, storedName));
        if (!fullPath.StartsWith(storageRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return BadRequest();
        var safeName = Path.GetFileName(file.FileName);
        safeName = new string(safeName.Where(ch => !char.IsControl(ch)).Take(250).ToArray());
        var metadataSaved = false;
        try
        {
            await System.IO.File.WriteAllBytesAsync(fullPath, content, token);
            var attachment = new Attachment
            {
                DocumentId = documentId, UploadedByUserId = User.UserId(), FileName = safeName,
                StoredFileName = storedName, FilePath = Path.Combine("App_Data", "attachments", storedName).Replace('\\', '/'),
                FileType = expectedType, FileSize = file.Length, UploadedAt = DateTime.Now
            };
            db.Attachments.Add(attachment);
            db.DocumentHistory.Add(new DocumentHistoryEntry { DocumentId = documentId, UserId = User.UserId(), ActionType = "UPLOAD", OldStatusId = document.StatusId, NewStatusId = document.StatusId, Description = $"Файл жүктелді: {safeName}", ActionDate = DateTime.Now });
            await db.SaveChangesAsync(token);
            metadataSaved = true;
            await audit.WriteAsync("UPLOAD", "Attachments", attachment.AttachmentId, safeName, User.UserId());
            await notifications.NotifyDocumentParticipantsAsync(document, User.UserId(), "Құжатқа файл қосылды", $"{document.RegistrationNumber}: {safeName}", "System", token);
            TempData["Success"] = "Файл қауіпсіз түрде жүктелді.";
        }
        catch
        {
            if (!metadataSaved && System.IO.File.Exists(fullPath)) System.IO.File.Delete(fullPath);
            throw;
        }
        return RedirectToAction("Details", "Documents", new { id = documentId });
    }

    [HttpGet]
    public async Task<IActionResult> Download(int id, CancellationToken token)
    {
        var attachment = await db.Attachments.AsNoTracking().FirstOrDefaultAsync(x => x.AttachmentId == id, token);
        if (attachment is null) return NotFound();
        var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        if (!await access.CanParticipateAsync(attachment.DocumentId, User.UserId(), role, token)) return NotFound();
        if (string.IsNullOrWhiteSpace(attachment.StoredFileName)) return NotFound();
        var storageRoot = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "App_Data", "attachments"));
        var fullPath = Path.GetFullPath(Path.Combine(storageRoot, attachment.StoredFileName));
        if (!fullPath.StartsWith(storageRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !System.IO.File.Exists(fullPath))
        {
            TempData["Error"] = "Файл табылмады. Ол серверден бұрын жойылған болуы мүмкін.";
            return RedirectToAction("Details", "Documents", new { id = attachment.DocumentId });
        }
        return PhysicalFile(fullPath, attachment.FileType ?? "application/octet-stream", attachment.FileName, enableRangeProcessing: true);
    }

    private static bool HasValidSignature(string extension, byte[] bytes)
    {
        if (extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase)) return bytes.Length >= 5 && bytes.AsSpan(0, 5).SequenceEqual("%PDF-"u8);
        if (extension.Equals(".png", StringComparison.OrdinalIgnoreCase)) return bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        if (extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)) return bytes.Length >= 3 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff;
        if (bytes.Length < 4 || bytes[0] != 0x50 || bytes[1] != 0x4b || bytes[2] != 0x03 || bytes[3] != 0x04) return false;
        try
        {
            using var stream = new MemoryStream(bytes);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
            var entries = archive.Entries.Select(x => x.FullName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var hasTypes = entries.Contains("[Content_Types].xml");
            return hasTypes && (extension.Equals(".docx", StringComparison.OrdinalIgnoreCase) ? entries.Contains("word/document.xml") : entries.Contains("xl/workbook.xml"));
        }
        catch (InvalidDataException) { return false; }
    }
}
