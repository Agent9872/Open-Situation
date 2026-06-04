using Lock.Chat.Services;
using Lock.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Lock.Services
{
    public static class ReportService
    {
        private static readonly string ReportsFolder =
            Path.Combine(FileSystem.AppDataDirectory, "reports");

        static ReportService()
        {
            if (!Directory.Exists(ReportsFolder))
                Directory.CreateDirectory(ReportsFolder);
        }

        public static async Task<bool> SubmitReportAsync(Report report)
        {
            try
            {
                // Snapshot images before insert
                var images = new List<ReportImage>(report.Images ?? new List<ReportImage>());

                // Clear the list so it doesn't get serialized into the report
                report.Images = new List<ReportImage>();
                report.ReportedAt = report.ReportedAt == default ? DateTime.UtcNow : report.ReportedAt;

                Debug.WriteLine($"Inserting report for: {report.ReportedUserName}, category: {report.Category}");

                // Insert report into Supabase
                var insertedReport = await SupabaseService.InsertAndReturnAsync<Report>("Reports", report);

                if (insertedReport == null || insertedReport.Id == 0)
                {
                    Debug.WriteLine("Report insert failed — returned null");
                    return false;
                }

                Debug.WriteLine($"Insert successful, new Id: {insertedReport.Id}");

                // Now save each image with the correct ReportId
                foreach (var image in images)
                {
                    image.ReportId = insertedReport.Id;
                    image.AddedAt = image.AddedAt == default ? DateTime.UtcNow : image.AddedAt;
                    await SupabaseService.InsertAsync("ReportImages", image);
                    Debug.WriteLine($"Inserted image: {image.LocalPath}");
                }

                // Restore images on the object for caller use
                report.Images = images;
                report.Id = insertedReport.Id;

                Debug.WriteLine($"Report submitted successfully. Id: {insertedReport.Id}");
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
                string filter = "";
                if (status.HasValue)
                {
                    filter = $"Status=eq.{(int)status.Value}&order=ReportedAt.desc";
                }
                else
                {
                    filter = "order=ReportedAt.desc";
                }

                var reports = await SupabaseService.GetAsync<Report>("Reports", filter);

                // Load images for each report
                foreach (var report in reports)
                {
                    var images = await SupabaseService.GetAsync<ReportImage>("ReportImages",
                        $"ReportId=eq.{report.Id}");
                    report.Images = images.ToList();
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
                var reports = await SupabaseService.GetAsync<Report>("Reports",
                    $"Id=eq.{reportId}&limit=1");

                var report = reports.FirstOrDefault();

                if (report != null)
                {
                    var images = await SupabaseService.GetAsync<ReportImage>("ReportImages",
                        $"ReportId=eq.{reportId}");
                    report.Images = images.ToList();
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
                var reports = await SupabaseService.GetAsync<Report>("Reports",
                    $"Id=eq.{reportId}&limit=1");

                var report = reports.FirstOrDefault();

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

                bool updated = await SupabaseService.UpdateAsync("Reports", $"Id=eq.{reportId}", report);
                Debug.WriteLine($"UpdateReportStatusAsync: updated {updated} for report {reportId}");
                return updated;
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
                var pendingReports = await SupabaseService.GetAsync<Report>("Reports",
                    $"Status=eq.{(int)ReportStatus.Pending}");

                var underReviewReports = await SupabaseService.GetAsync<Report>("Reports",
                    $"Status=eq.{(int)ReportStatus.UnderReview}");

                return pendingReports.Count + underReviewReports.Count;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetPendingReportsCountAsync error: {ex.Message}");
                return 0;
            }
        }

        // Helper method to delete a report and its images (if needed)
        public static async Task<bool> DeleteReportAsync(int reportId)
        {
            try
            {
                // First delete all images associated with the report
                await SupabaseService.DeleteAsync("ReportImages", $"ReportId=eq.{reportId}");

                // Then delete the report itself
                await SupabaseService.DeleteAsync("Reports", $"Id=eq.{reportId}");

                Debug.WriteLine($"Report {reportId} deleted successfully");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DeleteReportAsync error: {ex.Message}");
                return false;
            }
        }

        // Helper method to get reports by reporter phone
        public static async Task<List<Report>> GetReportsByReporterAsync(string reporterPhone)
        {
            try
            {
                var reports = await SupabaseService.GetAsync<Report>("Reports",
                    $"ReporterPhone=eq.{Uri.EscapeDataString(reporterPhone)}&order=ReportedAt.desc");

                // Load images for each report
                foreach (var report in reports)
                {
                    var images = await SupabaseService.GetAsync<ReportImage>("ReportImages",
                        $"ReportId=eq.{report.Id}");
                    report.Images = images.ToList();
                }

                return reports;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetReportsByReporterAsync error: {ex.Message}");
                return new List<Report>();
            }
        }

        // Helper method to get reports by reported user
        public static async Task<List<Report>> GetReportsByReportedUserAsync(string reportedUserPhone)
        {
            try
            {
                var reports = await SupabaseService.GetAsync<Report>("Reports",
                    $"ReportedUserPhone=eq.{Uri.EscapeDataString(reportedUserPhone)}&order=ReportedAt.desc");

                // Load images for each report
                foreach (var report in reports)
                {
                    var images = await SupabaseService.GetAsync<ReportImage>("ReportImages",
                        $"ReportId=eq.{report.Id}");
                    report.Images = images.ToList();
                }

                return reports;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetReportsByReportedUserAsync error: {ex.Message}");
                return new List<Report>();
            }
        }
    }
}