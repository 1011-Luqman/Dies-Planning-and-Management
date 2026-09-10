using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System.Data;

namespace Dies_Planning.Controllers
{
    public class DashboardController : Controller
    {
        private readonly IConfiguration _configuration;

        public DashboardController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public IActionResult Index()
        {
            return View();
        }


        [HttpGet]
        public async Task<IActionResult> GetDiameterData(
    DateTime? startDate,
    DateTime? endDate)
        {
            var connectionString = _configuration.GetConnectionString("OracleDb");

            if (string.IsNullOrEmpty(connectionString))
            {
                return StatusCode(500, "OracleDb connection string is not configured.");
            }

            // If no date is provided, return all data
            DateTime? endDateExclusive = null;
            if (endDate.HasValue)
            {
                endDateExclusive = endDate.Value.Date.AddDays(1);
            }

            const string sql = @"
SELECT
    B.pm,
    A.mgresult,
    A.issuedate,
    D.aomgfrom
FROM prod.dies_diesissuedetail A
JOIN prod.ta20tbl01_workid B ON A.workid = B.workid
JOIN prod.pdpm B1 ON B.pm = B1.pm
JOIN prod.fdbasresc C ON B1.pm = C.resourceuk
JOIN prod.aocspecaddon D ON D.parentobjectid = C.objectid
WHERE
    A.rejectstatus = 0
    AND A.issuedate IS NOT NULL
    AND A.mgresult <> 0
    AND D.aomgfrom IS NOT NULL
    AND (:startDate IS NULL OR A.issuedate >= :startDate)
    AND (:endDate IS NULL OR A.issuedate < :endDate)
ORDER BY A.issuedate";

            var dailyData = new Dictionary<DateTime, decimal>();

            try
            {
                using var connection = new OracleConnection(connectionString);
                await connection.OpenAsync();

                using var command = new OracleCommand(sql, connection);
                command.BindByName = true;

                // Start date
                var startParameter = new OracleParameter("startDate", OracleDbType.Date);
                startParameter.Value = startDate.HasValue ? startDate.Value.Date : DBNull.Value;
                command.Parameters.Add(startParameter);

                // End date
                var endParameter = new OracleParameter("endDate", OracleDbType.Date);
                endParameter.Value = endDateExclusive.HasValue ? endDateExclusive.Value : DBNull.Value;
                command.Parameters.Add(endParameter);

                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    string pm = reader["pm"]?.ToString() ?? "";
                    decimal mgResult = reader["mgresult"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["mgresult"]);
                    decimal aomgfrom = reader["aomgfrom"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["aomgfrom"]);
                    DateTime issuedate = Convert.ToDateTime(reader["issuedate"]);

                    // Determine constant based on PM
                    decimal constant;
                    if (pm.Contains("GPH", StringComparison.OrdinalIgnoreCase))
                    {
                        constant = 0.0030331m;
                    }
                    else if (pm.Contains("GPK", StringComparison.OrdinalIgnoreCase))
                    {
                        constant = 0.0030321m;
                    }
                    else if (pm.Contains("LC", StringComparison.OrdinalIgnoreCase))
                    {
                        constant = 0.002656216036m;
                    }
                    else if (pm.Contains("GP", StringComparison.OrdinalIgnoreCase))
                    {
                        constant = 0.00301592832m;
                    }
                    else
                    {
                        constant = 0.00303478m;
                    }

                    // Prevent invalid square root
                    if (aomgfrom <= 0 || mgResult < 0)
                        continue;

                    // Calculate Dia Min
                    double diaMin = Math.Sqrt((double)(aomgfrom / constant));

                    // Calculate Dia Incoming
                    double diaIncm = Math.Sqrt((double)(mgResult / constant));

                    if (diaMin == 0)
                        continue;

                    decimal deltaIncoming = (decimal)diaIncm - (decimal)diaMin;

                    // Group by date
                    DateTime date = issuedate.Date;
                    if (dailyData.ContainsKey(date))
                    {
                        dailyData[date] += deltaIncoming;
                    }
                    else
                    {
                        dailyData[date] = deltaIncoming;
                    }
                }

                var resultData = dailyData
                    .OrderBy(x => x.Key)
                    .Select(x => new
                    {
                        date = x.Key.ToString("yyyy-MM-dd"),
                        deltaIncoming = Math.Round(x.Value, 3)
                    });

                if (!startDate.HasValue && !endDate.HasValue)
                {
                    // Take the 10 most recent chronological dates
                    var result = resultData.TakeLast(10).ToList();
                    return Json(result);
                }
                else
                {
                    var result = resultData.ToList();
                    return Json(result);
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving diameter data.", error = ex.Message });
            }
        }


    }
}