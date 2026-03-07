// Authorization/Permissions.cs
namespace Web.Authorization
{
    public static class Permissions
    {
        // Each permission is a string constant: "Area.Action"
        // These get stored as claims in AspNetRoleClaims

        public static class Dashboard
        {
            public const string View = "Dashboard.View";
        }

        public static class Questionnaires
        {
            public const string View = "Questionnaires.View";
            public const string Create = "Questionnaires.Create";
            public const string Edit = "Questionnaires.Edit";
            public const string Delete = "Questionnaires.Delete";
            public const string Send = "Questionnaires.Send";
        }

        public static class SurveyAnalysis
        {
            public const string View = "SurveyAnalysis.View";
            public const string Analyze = "SurveyAnalysis.Analyze";
            public const string Reports = "SurveyAnalysis.Reports";
            public const string HighRisk = "SurveyAnalysis.HighRisk";
        }

        public static class Users
        {
            public const string View = "Users.View";
            public const string Create = "Users.Create";
            public const string Edit = "Users.Edit";
            public const string Delete = "Users.Delete";
        }

        public static class Roles
        {
            public const string View = "Roles.View";
            public const string Create = "Roles.Create";
            public const string Edit = "Roles.Edit";
            public const string Delete = "Roles.Delete";
        }

        public static class Responses
        {
            public const string View = "Responses.View";
            public const string Delete = "Responses.Delete";
            public const string Export = "Responses.Export";
        }

        // Claim type used in AspNetRoleClaims
        public const string ClaimType = "Permission";

        // Helper: get ALL permissions grouped by area (used in UI)
        public static Dictionary<string, List<PermissionItem>> GetAllGrouped()
        {
            return new Dictionary<string, List<PermissionItem>>
            {
                ["Dashboard"] = new()
                {
                    new("Dashboard.View", "View Dashboard", "fa-solid fa-gauge-high")
                },
                ["Questionnaires"] = new()
                {
                    new("Questionnaires.View", "View Questionnaires", "fa-solid fa-eye"),
                    new("Questionnaires.Create", "Create Questionnaires", "fa-solid fa-plus"),
                    new("Questionnaires.Edit", "Edit Questionnaires", "fa-solid fa-pen-to-square"),
                    new("Questionnaires.Delete", "Delete Questionnaires", "fa-solid fa-trash-can"),
                    new("Questionnaires.Send", "Send Questionnaires", "fa-solid fa-paper-plane")
                },
                ["Survey Analysis"] = new()
                {
                    new("SurveyAnalysis.View", "View Analysis", "fa-solid fa-eye"),
                    new("SurveyAnalysis.Analyze", "Run Analysis", "fa-solid fa-brain"),
                    new("SurveyAnalysis.Reports", "Generate Reports", "fa-solid fa-file-lines"),
                    new("SurveyAnalysis.HighRisk", "View High Risk Cases", "fa-solid fa-triangle-exclamation")
                },
                ["User Management"] = new()
                {
                    new("Users.View", "View Users", "fa-solid fa-eye"),
                    new("Users.Create", "Create Users", "fa-solid fa-user-plus"),
                    new("Users.Edit", "Edit Users", "fa-solid fa-user-pen"),
                    new("Users.Delete", "Delete Users", "fa-solid fa-user-minus")
                },
                ["Role Management"] = new()
                {
                    new("Roles.View", "View Roles", "fa-solid fa-eye"),
                    new("Roles.Create", "Create Roles", "fa-solid fa-plus"),
                    new("Roles.Edit", "Edit Roles", "fa-solid fa-pen-to-square"),
                    new("Roles.Delete", "Delete Roles", "fa-solid fa-trash-can")
                },
                ["Responses"] = new()
                {
                    new("Responses.View", "View Responses", "fa-solid fa-eye"),
                    new("Responses.Delete", "Delete Responses", "fa-solid fa-trash-can"),
                    new("Responses.Export", "Export Responses", "fa-solid fa-file-export")
                }
            };
        }

        // Helper: get ALL permission values as flat list
        public static List<string> GetAll()
        {
            return GetAllGrouped().Values.SelectMany(g => g.Select(p => p.Value)).ToList();
        }
    }

    public class PermissionItem
    {
        public string Value { get; set; }
        public string DisplayName { get; set; }
        public string Icon { get; set; }

        public PermissionItem(string value, string displayName, string icon)
        {
            Value = value;
            DisplayName = displayName;
            Icon = icon;
        }
    }
}