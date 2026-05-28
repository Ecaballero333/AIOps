using InstrumentationInterface;
using Microsoft.AspNetCore.Mvc;
using PharmaGo.PharmacyService.IBusinessLogic;
using PharmaGo.PharmacyService.Enums;
using PharmaGo.PharmacyService.Filters;
using PharmaGo.PharmacyService.Models.In.Exports;

namespace PharmaGo.PharmacyService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [TypeFilter(typeof(ExceptionFilter))]
    [AuthorizationFilter(new string[] { nameof(RoleType.Employee) })]
    public class ExportController : Controller
    {
        private readonly IExportManager _exportManager;
        private readonly IStructuredLogger _structuredLogger;

        public ExportController(IExportManager transactionManager, IStructuredLogger structuredLogger)
        {
            _exportManager = transactionManager;
            _structuredLogger = structuredLogger;
        }

        [HttpGet("exporters")]
        public IActionResult GetAllExporters()
        {
            return Ok(_exportManager.GetAllExporters());
        }

        [HttpGet("parameters")]
        public IActionResult GetParameters([FromQuery] string exporterName)
        {
            return Ok(_exportManager.GetParameters(exporterName));
        }

        [HttpPost]
        public IActionResult ExportDrugs([FromBody] DrugsExportationModel drugExportationModel)
        {
            try
            {
                string token = HttpContext.Request.Headers["Authorization"];
                _exportManager.ExportDrugs(drugExportationModel.FormatName, drugExportationModel.Parameters, token);
                _structuredLogger.LogInformation(
                    "Drug export completed",
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "drug_export",
                        ["component"] = "ExportController",
                        ["operation"] = "export_drugs",
                        ["db_operation"] = "read_drugs_for_export",
                        ["outcome"] = "success",
                        ["export_format"] = drugExportationModel.FormatName,
                        ["parameters_count"] = drugExportationModel.Parameters?.Count() ?? 0
                    });
                return Ok(true);
            }
            catch (Exception ex)
            {
                _structuredLogger.LogWarning(
                    "Drug export failed",
                    ex,
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "drug_export_fail",
                        ["component"] = "ExportController",
                        ["operation"] = "export_drugs",
                        ["db_operation"] = "read_drugs_for_export",
                        ["outcome"] = "failed",
                        ["export_format"] = drugExportationModel?.FormatName ?? "",
                        ["parameters_count"] = drugExportationModel?.Parameters?.Count() ?? 0,
                        ["error_message"] = ex.Message
                    });
                throw;
            }
        }
    }
}
