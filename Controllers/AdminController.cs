using DeanFlow.Data;
using DeanFlow.Data.Entities;
using DeanFlow.Models.ViewModels;
using DeanFlow.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeanFlow.Controllers;

[Authorize(Roles = AppRoles.Administrator)]
public sealed class AdminController(DeanFlowDbContext db, PasswordHasher<AppUser> passwordHasher,
    AuditService audit, NotificationService notifications) : AppController(notifications)
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken token)
    {
        var model = new AdminOverviewViewModel
        {
            Users = await db.Users.CountAsync(token), ActiveUsers = await db.Users.CountAsync(x => x.IsActive, token),
            Departments = await db.Departments.CountAsync(token), ActiveDepartments = await db.Departments.CountAsync(x => x.IsActive, token),
            DocumentTypes = await db.DocumentTypes.CountAsync(token), ActiveTypes = await db.DocumentTypes.CountAsync(x => x.IsActive, token),
            AuditEntries = await db.AuditLogs.CountAsync(token), Roles = await db.Roles.CountAsync(token)
        };
        ViewData["Title"] = "Басқару панелі";
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Users(string? q, int page = 1, CancellationToken token = default)
    {
        page = Math.Max(1, page);
        IQueryable<AppUser> query = db.Users.AsNoTracking().Include(x => x.Role).Include(x => x.Department);
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(x => x.Email.Contains(q) || x.FirstName.Contains(q) || x.LastName.Contains(q));
        var total = await query.CountAsync(token);
        ViewBag.Total = total; ViewBag.Page = page; ViewBag.PageCount = Math.Max(1, (int)Math.Ceiling(total / 25d)); ViewBag.Query = q;
        ViewData["Title"] = "Пайдаланушылар";
        return View(await query.OrderBy(x => x.LastName).ThenBy(x => x.FirstName).Skip((page - 1) * 25).Take(25).ToListAsync(token));
    }

    [HttpGet]
    public async Task<IActionResult> UserCreate(CancellationToken token)
    {
        await LoadUserLookups(token); ViewData["Title"] = "Пайдаланушы қосу"; return View("UserForm", new AdminUserInput());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UserCreate(AdminUserInput input, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(input.NewPassword)) ModelState.AddModelError(nameof(input.NewPassword), "Уақытша құпиясөзді енгізіңіз (кемінде 10 таңба).");
        await ValidateUserInput(input, token);
        if (!ModelState.IsValid) { await LoadUserLookups(token); ViewData["Title"] = "Пайдаланушы қосу"; return View("UserForm", input); }
        var user = new AppUser { Email = input.Email.Trim(), FirstName = input.FirstName.Trim(), LastName = input.LastName.Trim(), MiddleName = input.MiddleName?.Trim(),
            Phone = input.Phone?.Trim(), Position = input.Position?.Trim(), RoleId = input.RoleId, DepartmentId = input.DepartmentId,
            IsActive = input.IsActive, CreatedAt = DateTime.Now };
        user.PasswordHash = passwordHasher.HashPassword(user, input.NewPassword!);
        db.Users.Add(user);
        await db.SaveChangesAsync(token);
        await audit.WriteAsync("CREATE", "Users", user.UserId, $"Пайдаланушы қосылды: {user.Email}; рөлі {input.RoleId}", User.UserId());
        TempData["Success"] = "Пайдаланушы қосылды. Құпиясөз тек хэш түрінде сақталды.";
        return RedirectToAction(nameof(Users));
    }

    [HttpGet]
    public async Task<IActionResult> UserEdit(int id, CancellationToken token)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == id, token);
        if (user is null) return NotFound();
        await LoadUserLookups(token); ViewData["Title"] = "Пайдаланушыны өңдеу";
        return View("UserForm", new AdminUserInput { UserId = id, Email = user.Email, FirstName = user.FirstName, LastName = user.LastName,
            MiddleName = user.MiddleName, Phone = user.Phone, Position = user.Position, RoleId = user.RoleId, DepartmentId = user.DepartmentId, IsActive = user.IsActive });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UserEdit(AdminUserInput input, CancellationToken token)
    {
        if (!input.UserId.HasValue) return BadRequest();
        var user = await db.Users.Include(x => x.Role).FirstOrDefaultAsync(x => x.UserId == input.UserId, token);
        if (user is null) return NotFound();
        if (string.IsNullOrWhiteSpace(input.NewPassword)) ModelState.Remove(nameof(input.NewPassword));
        await ValidateUserInput(input, token);
        if (user.UserId == User.UserId() && (!input.IsActive || input.RoleId != user.RoleId))
            ModelState.AddModelError(string.Empty, "Өз әкімші есептік жазбаңызды өшіруге немесе рөлін ауыстыруға болмайды.");
        var targetRole = await db.Roles.FindAsync([input.RoleId], token);
        if (user.Role?.RoleName == AppRoles.Administrator && (targetRole?.RoleName != AppRoles.Administrator || !input.IsActive))
        {
            var activeAdmins = await db.Users.Include(x => x.Role).CountAsync(x => x.IsActive && x.Role!.RoleName == AppRoles.Administrator, token);
            if (activeAdmins <= 1) ModelState.AddModelError(string.Empty, "Соңғы белсенді әкімші есептік жазбасын өшіруге немесе басқа рөлге ауыстыруға болмайды.");
        }
        if (!ModelState.IsValid) { await LoadUserLookups(token); ViewData["Title"] = "Пайдаланушыны өңдеу"; return View("UserForm", input); }
        var before = $"{user.Email}; {user.Role?.RoleName}; active={user.IsActive}";
        user.Email = input.Email.Trim(); user.FirstName = input.FirstName.Trim(); user.LastName = input.LastName.Trim(); user.MiddleName = input.MiddleName?.Trim();
        user.Phone = input.Phone?.Trim(); user.Position = input.Position?.Trim(); user.RoleId = input.RoleId; user.DepartmentId = input.DepartmentId; user.IsActive = input.IsActive;
        if (!string.IsNullOrWhiteSpace(input.NewPassword)) user.PasswordHash = passwordHasher.HashPassword(user, input.NewPassword);
        await db.SaveChangesAsync(token);
        await audit.WriteAsync("UPDATE", "Users", user.UserId, $"{before} -> {user.Email}; role={input.RoleId}; active={input.IsActive}; passwordReset={!string.IsNullOrWhiteSpace(input.NewPassword)}", User.UserId());
        TempData["Success"] = "Пайдаланушы мәліметтері жаңартылды.";
        return RedirectToAction(nameof(Users));
    }

    [HttpGet]
    public async Task<IActionResult> Departments(CancellationToken token)
    {
        ViewData["Title"] = "Бөлімдер";
        return View(await db.Departments.AsNoTracking().OrderBy(x => x.DepartmentName).ToListAsync(token));
    }

    [HttpGet]
    public async Task<IActionResult> DepartmentEdit(int? id, CancellationToken token)
    {
        var item = id.HasValue ? await db.Departments.FindAsync([id.Value], token) : null;
        if (id.HasValue && item is null) return NotFound();
        ViewData["Title"] = id.HasValue ? "Бөлімді өңдеу" : "Бөлім қосу";
        return View("DepartmentForm", new DepartmentInput { DepartmentId = item?.DepartmentId, DepartmentName = item?.DepartmentName ?? "", ShortName = item?.ShortName, IsActive = item?.IsActive ?? true });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DepartmentEdit(DepartmentInput input, CancellationToken token)
    {
        if (await db.Departments.AnyAsync(x => x.DepartmentName == input.DepartmentName && x.DepartmentId != input.DepartmentId, token)) ModelState.AddModelError(nameof(input.DepartmentName), "Мұндай бөлім бұрыннан бар.");
        if (!ModelState.IsValid) { ViewData["Title"] = input.DepartmentId.HasValue ? "Бөлімді өңдеу" : "Бөлім қосу"; return View("DepartmentForm", input); }
        Department department;
        if (input.DepartmentId.HasValue)
        {
            department = await db.Departments.FirstOrDefaultAsync(x => x.DepartmentId == input.DepartmentId, token) ?? throw new InvalidOperationException("Бөлім табылмады.");
            department.DepartmentName = input.DepartmentName.Trim(); department.ShortName = input.ShortName?.Trim(); department.IsActive = input.IsActive;
        }
        else { department = new Department { DepartmentName = input.DepartmentName.Trim(), ShortName = input.ShortName?.Trim(), IsActive = input.IsActive, CreatedAt = DateTime.Now }; db.Departments.Add(department); }
        await db.SaveChangesAsync(token);
        await audit.WriteAsync(input.DepartmentId.HasValue ? "UPDATE" : "CREATE", "Departments", department.DepartmentId, department.DepartmentName, User.UserId());
        TempData["Success"] = "Бөлім сақталды."; return RedirectToAction(nameof(Departments));
    }

    [HttpGet]
    public async Task<IActionResult> Types(CancellationToken token)
    {
        ViewData["Title"] = "Құжат түрлері";
        return View(await db.DocumentTypes.AsNoTracking().OrderBy(x => x.TypeName).ToListAsync(token));
    }

    [HttpGet]
    public async Task<IActionResult> TypeEdit(int? id, CancellationToken token)
    {
        var item = id.HasValue ? await db.DocumentTypes.FindAsync([id.Value], token) : null;
        if (id.HasValue && item is null) return NotFound();
        ViewData["Title"] = id.HasValue ? "Құжат түрін өңдеу" : "Құжат түрін қосу";
        return View("TypeForm", new DocumentTypeInput { DocumentTypeId = item?.DocumentTypeId, TypeName = item?.TypeName ?? "", Code = item?.Code ?? "", Description = item?.Description, IsActive = item?.IsActive ?? true });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> TypeEdit(DocumentTypeInput input, CancellationToken token)
    {
        if (await db.DocumentTypes.AnyAsync(x => x.Code == input.Code && x.DocumentTypeId != input.DocumentTypeId, token)) ModelState.AddModelError(nameof(input.Code), "Мұндай код бұрыннан бар.");
        if (!ModelState.IsValid) { ViewData["Title"] = input.DocumentTypeId.HasValue ? "Құжат түрін өңдеу" : "Құжат түрін қосу"; return View("TypeForm", input); }
        DocumentType type;
        if (input.DocumentTypeId.HasValue)
        {
            type = await db.DocumentTypes.FirstOrDefaultAsync(x => x.DocumentTypeId == input.DocumentTypeId, token) ?? throw new InvalidOperationException("Құжат түрі табылмады.");
            type.TypeName = input.TypeName.Trim(); type.Code = input.Code.Trim().ToUpperInvariant(); type.Description = input.Description?.Trim(); type.IsActive = input.IsActive;
        }
        else { type = new DocumentType { TypeName = input.TypeName.Trim(), Code = input.Code.Trim().ToUpperInvariant(), Description = input.Description?.Trim(), IsActive = input.IsActive }; db.DocumentTypes.Add(type); }
        await db.SaveChangesAsync(token);
        await audit.WriteAsync(input.DocumentTypeId.HasValue ? "UPDATE" : "CREATE", "DocumentTypes", type.DocumentTypeId, $"{type.Code}: {type.TypeName}", User.UserId());
        TempData["Success"] = "Құжат түрі сақталды."; return RedirectToAction(nameof(Types));
    }

    [HttpGet]
    public async Task<IActionResult> Statuses(CancellationToken token)
    {
        ViewData["Title"] = "Құжат мәртебелері";
        return View(await db.DocumentStatuses.AsNoTracking().OrderBy(x => x.StatusId).ToListAsync(token));
    }

    [HttpGet]
    public async Task<IActionResult> Roles(CancellationToken token)
    {
        ViewData["Title"] = "Жүйе рөлдері";
        return View(await db.Roles.AsNoTracking().OrderBy(x => x.RoleName).ToListAsync(token));
    }

    [HttpGet]
    public async Task<IActionResult> Audit(string? q, string? eventType, DateTime? from, DateTime? to, int page = 1, CancellationToken token = default)
    {
        page = Math.Max(1, page);
        IQueryable<AuditLog> query = db.AuditLogs.AsNoTracking().Include(x => x.User);
        if (!string.IsNullOrWhiteSpace(eventType)) query = query.Where(x => x.Action == eventType);
        if (from.HasValue) query = query.Where(x => x.CreatedAt >= from.Value.Date);
        if (to.HasValue) query = query.Where(x => x.CreatedAt < to.Value.Date.AddDays(1));
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(x => (x.EntityName != null && x.EntityName.Contains(q)) || (x.Details != null && x.Details.Contains(q)) || (x.User != null && (x.User.Email.Contains(q) || x.User.FirstName.Contains(q) || x.User.LastName.Contains(q))));
        var total = await query.CountAsync(token);
        ViewBag.Query = q; ViewBag.EventType = eventType; ViewBag.From = from; ViewBag.To = to; ViewBag.Total = total; ViewBag.Page = page; ViewBag.PageCount = Math.Max(1, (int)Math.Ceiling(total / 30d));
        ViewData["Title"] = "Аудит журналы";
        return View(await query.OrderByDescending(x => x.CreatedAt).Skip((page - 1) * 30).Take(30).ToListAsync(token));
    }

    private async Task ValidateUserInput(AdminUserInput input, CancellationToken token)
    {
        if (await db.Users.AnyAsync(x => x.Email == input.Email && x.UserId != input.UserId, token)) ModelState.AddModelError(nameof(input.Email), "Бұл электрондық пошта тіркелген.");
        if (!await db.Roles.AnyAsync(x => x.RoleId == input.RoleId, token)) ModelState.AddModelError(nameof(input.RoleId), "Жүйедегі рөлді таңдаңыз.");
        if (input.DepartmentId.HasValue && !await db.Departments.AnyAsync(x => x.DepartmentId == input.DepartmentId && x.IsActive, token)) ModelState.AddModelError(nameof(input.DepartmentId), "Белсенді бөлімді таңдаңыз.");
    }

    private async Task LoadUserLookups(CancellationToken token)
    {
        ViewBag.Roles = await db.Roles.AsNoTracking().OrderBy(x => x.RoleName).ToListAsync(token);
        ViewBag.Departments = await db.Departments.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.DepartmentName).ToListAsync(token);
    }
}

