using System.Security.Claims;

namespace DeanFlow.Services;

public static class AppRoles
{
    public const string Administrator = "Administrator";
    public const string DeanerySpecialist = "DeanerySpecialist";
    public const string Executor = "Executor";
    public const string Management = "Management";
    public const string Student = "Student";
    public const string AdminOrDeanery = Administrator + "," + DeanerySpecialist;
    public const string AdminDeaneryManagement = Administrator + "," + DeanerySpecialist + "," + Management;
}

public static class UserClaims
{
    public static int UserId(this ClaimsPrincipal user)
        => int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    public static int? DepartmentId(this ClaimsPrincipal user)
        => int.TryParse(user.FindFirstValue("DepartmentId"), out var id) ? id : null;
}
