using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace TailoredApps.Shared.WebApi.HealthChecks
{
    /// <summary>
    /// Writes health reports as JSON. By default only the aggregate status is emitted; a per-check
    /// breakdown is included when detailed responses are explicitly enabled.
    /// </summary>
    internal static class HealthCheckResponseWriter
    {
        private const string JsonContentType = "application/json; charset=utf-8";

        public static Func<HttpContext, HealthReport, Task> Create(bool includeDetails)
        {
            return (context, report) =>
            {
                context.Response.ContentType = JsonContentType;
                return context.Response.WriteAsync(Serialize(report, includeDetails));
            };
        }

        private static string Serialize(HealthReport report, bool includeDetails)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
                {
                    writer.WriteStartObject();
                    writer.WriteString("status", report.Status.ToString());
                    writer.WriteString("totalDuration", report.TotalDuration.ToString());

                    if (includeDetails)
                    {
                        writer.WriteStartObject("entries");
                        foreach (var entry in report.Entries)
                        {
                            writer.WriteStartObject(entry.Key);
                            writer.WriteString("status", entry.Value.Status.ToString());
                            writer.WriteString("duration", entry.Value.Duration.ToString());
                            if (!string.IsNullOrEmpty(entry.Value.Description))
                            {
                                writer.WriteString("description", entry.Value.Description);
                            }

                            writer.WriteEndObject();
                        }

                        writer.WriteEndObject();
                    }

                    writer.WriteEndObject();
                }

                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }
    }
}
