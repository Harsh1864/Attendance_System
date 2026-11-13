using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using project.Services;

namespace try_project_casepoint.Services
{
    /// <summary>
    /// Marks active employees with no attendance row as Absent once a day,
    /// at Shift:AbsenteeRunTime (default 18:00). Weekends are skipped when
    /// Shift:SkipWeekends is true.
    /// </summary>
    public class DailyTaskService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IOptionsMonitor<ShiftOptions> _shift;
        private readonly ILogger<DailyTaskService> _logger;

        public DailyTaskService(IServiceProvider serviceProvider, IOptionsMonitor<ShiftOptions> shift, ILogger<DailyTaskService> logger)
        {
            _serviceProvider = serviceProvider;
            _shift = shift;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var options = _shift.CurrentValue;
                var delay = AttendanceRules.DelayUntilNext(DateTime.Now, options.AbsenteeRunTimeOfDay);
                _logger.LogInformation("Absentee job scheduled in {Minutes:F0} minutes", delay.TotalMinutes);

                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }

                if (options.SkipWeekends && AttendanceRules.IsWeekend(DateTime.Today))
                {
                    _logger.LogInformation("Skipping absentee job on weekend {Date:yyyy-MM-dd}", DateTime.Today);
                    continue;
                }

                try
                {
                    await RunDailyTask();
                    _logger.LogInformation("Absentee job finished at {Time}", DateTime.Now);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Absentee job failed");
                }
            }
        }

        private async Task RunDailyTask()
        {
            using var scope = _serviceProvider.CreateScope();
            var adminService = scope.ServiceProvider.GetRequiredService<AdminService>();
            await adminService.MarkAbsenteesAsync();
        }
    }
}
