using InstrumentationInterface;
using Microsoft.AspNetCore.Mvc;
using PharmaGo.UsersService.IBusinessLogic;
using PharmaGo.UsersService.Models.Out;

namespace PharmaGo.UsersService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RolesController : ControllerBase
    {
        private readonly IRoleManager _roleManager;
        private readonly IStructuredLogger _structuredLogger;

        public RolesController(IRoleManager manager, IStructuredLogger structuredLogger)
        {
            _roleManager = manager;
            _structuredLogger = structuredLogger;
        }

        [HttpGet]
        public IActionResult GetAll()
        {
            try
            {
                var roles = _roleManager.GetAll();
                List<RoleModelResponse> result = roles.Select(role => new RoleModelResponse(role)).ToList();
                // _structuredLogger.LogInformation(
                //     "Roles retrieved",
                //     new Dictionary<string, object>
                //     {
                //         ["pharma_biz"] = "role_list",
                //         ["component"] = "RolesController",
                //         ["operation"] = "list_roles",
                //         ["db_operation"] = "read_roles",
                //         ["outcome"] = "success",
                //         ["roles_count"] = result.Count
                //     });
                return Ok(result);
            }
            catch (Exception ex)
            {
                _structuredLogger.LogWarning(
                    "Roles retrieval failed",
                    ex,
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "role_list_fail",
                        ["component"] = "RolesController",
                        ["operation"] = "list_roles",
                        ["db_operation"] = "read_roles",
                        ["outcome"] = "failed",
                        ["error_message"] = ex.Message
                    });
                throw;
            }
        }

        [HttpGet("test")]
        public IActionResult Test()
        {
            // _structuredLogger.LogInformation(
            //     "Roles service health test completed",
            //     new Dictionary<string, object>
            //     {
            //         ["pharma_biz"] = "roles_health_test",
            //         ["component"] = "RolesController",
            //         ["operation"] = "health_test",
            //         ["outcome"] = "success"
            //     });
            return Ok(new { status = "ok", message = "Service is healthy" });
        }

        [HttpGet("ping")]
        public IActionResult Ping()
        {
            DateTime timestamp = DateTime.UtcNow;
            // _structuredLogger.LogInformation(
            //     "Roles service ping completed",
            //     new Dictionary<string, object>
            //     {
            //         ["pharma_biz"] = "roles_ping",
            //         ["component"] = "RolesController",
            //         ["operation"] = "ping",
            //         ["outcome"] = "success",
            //         ["timestamp_utc"] = timestamp.ToString("O")
            //     });
            return Ok(new { status = "ok", message = "pong", timestamp });
        }

    }
}

