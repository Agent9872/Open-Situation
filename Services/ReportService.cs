using Lock.Chat.Services;
using Lock.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Lock.Services
{
    public static class ReportService
    {
        private static readonly string ReportsFolder =
            Path.Combine(FileSystem.AppDataDirectory, "reports");

        private static bool _tablesEnsured = false;

        static ReportService()
        {
            if (!Directory.Exists(ReportsFolder))
                Directory.CreateDirectory(ReportsFolder);
        }

        // ── Ensure both tables exist (safe to call multiple times) ──
        private static async Task EnsureTablesAsync()
        {
            if (_tablesEnsured) return;

            await DatabaseService.InitializeAsync();
            var db = DatabaseService.GetConnection();

            // CreateTableAsync is idempotent — creates only if not exists
            await db.CreateTableAsync<Report>();
            await db.CreateTableAsync<ReportImage>();

            _tablesEnsured = true;
            Debug.WriteLine("ReportService: tables ensured");
        }

        public static async Task<bool> SubmitReportAsync(Report report)
        {
            try
            {
                await EnsureTablesAsync();
                var db = DatabaseService.GetConnection();

                // Snapshot images before insert (InsertAsync sets report.Id)
                var images = new List<ReportImage>(report.Images ?? new List<ReportImage>());

                // Clear the [Ignore] list so SQLite doesn't get confused
                report.Images = new List<ReportImage>();
                report.ReportedAt = report.ReportedAt == default ? DateTime.UtcNow : report.ReportedAt;

                Debug.WriteLine($"Inserting report for: {report.ReportedUserName}, category: {report.Category}");

                int rows = await db.InsertAsync(report);

                Debug.WriteLine($"InsertAsync result: {rows}, new Id: {report.Id}");

                if (rows <= 0)
                {
                    Debug.WriteLine("Report insert returned 0 rows — failed silently");
                    return false;
                }

                // Now save each image with the correct ReportId
                foreach (var image in images)
                {
                    image.ReportId = report.Id;
                    image.AddedAt = image.AddedAt == default ? DateTime.UtcNow : image.AddedAt;
                    await db.InsertAsync(image);
                    Debug.WriteLine($"Inserted image: {image.LocalPath}");
                }

                // Restore images on the object for caller use
                report.Images = images;

                Debug.WriteLine($"Report submitted successfully. Id: {report.Id}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SubmitReportAsync EXCEPTION: {ex.GetType().Name}");
                Debug.WriteLine($"Message: {ex.Message}");
                Debug.WriteLine($"StackTrace: {ex.StackTrace}");
                if (ex.InnerException != null)
                    Debug.WriteLine($"Inner: {ex.InnerException.Message}");
                return false;
            }
        }

        public static async Task<List<Report>> GetAllReportsAsync(ReportStatus? status = null)
        {
            try
            {
                await EnsureTablesAsync();
                var db = DatabaseService.GetConnection();

                List<Report> reports;

                if (status.HasValue)
                    reports = await db.Table<Report>()
                        .Where(r => r.Status == status.Value)
                        .OrderByDescending(r => r.ReportedAt)
                        .ToListAsync();
                else
                    reports = await db.Table<Report>()
                        .OrderByDescending(r => r.ReportedAt)
                        .ToListAsync();

                // Load images for each report
                foreach (var report in reports)
                {
                    report.Images = await db.Table<ReportImage>()
                        .Where(i => i.ReportId == report.Id)
                        .ToListAsync();
                }

                return reports;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetAllReportsAsync error: {ex.Message}");
                return new List<Report>();
            }
        }

        public static async Task<Report> GetReportByIdAsync(int reportId)
        {
            try
            {
                await EnsureTablesAsync();
                var db = DatabaseService.GetConnection();

                var report = await db.Table<Report>()
                    .Where(r => r.Id == reportId)
                    .FirstOrDefaultAsync();

                if (report != null)
                {
                    report.Images = await db.Table<ReportImage>()
                        .Where(i => i.ReportId == reportId)
                        .ToListAsync();
                }

                return report;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetReportByIdAsync error: {ex.Message}");
                return null;
            }
        }

        public static async Task<bool> UpdateReportStatusAsync(
            int reportId,
            ReportStatus status,
            string adminNotes = null,
            AdminAction action = AdminAction.None)
        {
            try
            {
                await EnsureTablesAsync();
                var db = DatabaseService.GetConnection();

                var report = await db.Table<Report>()
                    .Where(r => r.Id == reportId)
                    .FirstOrDefaultAsync();

                if (report == null)
                {
                    Debug.WriteLine($"UpdateReportStatusAsync: report {reportId} not found");
                    return false;
                }

                report.Status = status;

                if (!string.IsNullOrEmpty(adminNotes))
                    report.AdminNotes = adminNotes;

                if (action != AdminAction.None)
                    report.ActionTaken = action;

                if (status == ReportStatus.Resolved || status == ReportStatus.ActionTaken)
                    report.ResolvedAt = DateTime.UtcNow;

                int updated = await db.UpdateAsync(report);
                Debug.WriteLine($"UpdateReportStatusAsync: updated {updated} rows for report {reportId}");
                return updated > 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpdateReportStatusAsync error: {ex.Message}");
                return false;
            }
        }

        public static string SaveReportImage(byte[] imageData, string extension = ".jpg")
        {
            try
            {
                if (imageData == null || imageData.Length == 0)
                {
                    Debug.WriteLine("SaveReportImage: imageData is null or empty");
                    return null;
                }

                if (!extension.StartsWith("."))
                    extension = "." + extension;

                string fileName = $"report_{Guid.NewGuid():N}{extension}";
                string filePath = Path.Combine(ReportsFolder, fileName);
                File.WriteAllBytes(filePath, imageData);

                Debug.WriteLine($"SaveReportImage: saved {imageData.Length} bytes to {filePath}");
                return filePath;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SaveReportImage error: {ex.Message}");
                return null;
            }
        }

        public static async Task<int> GetPendingReportsCountAsync()
        {
            try
            {
                await EnsureTablesAsync();
                var db = DatabaseService.GetConnection();
                return await db.Table<Report>()
                    .Where(r => r.Status == ReportStatus.Pending || r.Status == ReportStatus.UnderReview)
                    .CountAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetPendingReportsCountAsync error: {ex.Message}");
                return 0;
            }
        }
    }
}