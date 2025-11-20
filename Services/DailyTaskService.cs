using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace try_project_casepoint.Services
{
    public class DailyTaskService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;

        public DailyTaskService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var now = DateTime.Now;

                // DateTime.Today.AddHours(17) for 5 PM
                var scheduledTime = DateTime.Now.AddMinutes(100);

                if (now > scheduledTime)
                    scheduledTime = scheduledTime.AddDays(1);

                var delay = scheduledTime - now;
                Console.WriteLine($"Next run in: {delay.TotalMinutes:F2} minutes");

                try
                {
                    await Task.Delay(delay, stoppingToken);
                    await RunDailyTask();

                    Console.WriteLine($"Task done at {DateTime.Now}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Daily tAsk Error {ex.Message}");
                }

                // FOR NEXT DAYYY
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
        }

        private async Task RunDailyTask()
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var adminService = scope.ServiceProvider.GetRequiredService<AdminService>();
                await adminService.MarkAbsenteesAsync();
            }
        }
    }
}
