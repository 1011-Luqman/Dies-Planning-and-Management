using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System.Data;

namespace Dies_Planning.Controllers
{
    public class InventoryController : Controller
    {
        private readonly IConfiguration _configuration;

        public InventoryController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetData()
        {
            var connectionString =
                _configuration.GetConnectionString("OracleDb");

            var data = new List<Dictionary<string, object?>>();

            const string sql = @"
                SELECT area, location, lotno, resourcename, qty, diesdiam, vendcode, assignedto
                FROM PROD.DIES_INVENTORY
                WHERE ROWNUM <= 100
            ";

            await using var connection =
                new OracleConnection(connectionString);

            await connection.OpenAsync();

            await using var command =
                new OracleCommand(sql, connection);

            await using var reader =
                await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object?>();

                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var columnName = reader.GetName(i);

                    row[columnName] =
                        reader.IsDBNull(i)
                            ? null
                            : reader.GetValue(i);
                }

                data.Add(row);
            }

            return Json(data);
        }
    }

}
